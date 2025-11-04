using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.AppServices;
using PackagingTools.Core.Models;
using PackagingTools.Core.Policies;
using PackagingTools.Core.Windows;
using PackagingTools.Core.Mac;
using PackagingTools.Core.Linux;
using PackagingTools.Core.Telemetry.Dashboards;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Plugins;

namespace PackagingTools.Sdk;

/// <summary>
/// High-level facade for embedding PackagingTools orchestration in custom build pipelines.
/// </summary>
public sealed class PackagingClient
{
    private readonly PackagingClientOptions _options;

    private PackagingClient(PackagingClientOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>
    /// Creates a client using the default configuration.
    /// </summary>
    public static PackagingClient CreateDefault(Action<PackagingClientOptions>? configure = null)
    {
        var options = new PackagingClientOptions();
        configure?.Invoke(options);
        return new PackagingClient(options);
    }

    /// <summary>
    /// Executes a packaging run based on the contents of a project file.
    /// </summary>
    public async Task<PackagingResult> PackAsync(PackagingRunOptions runOptions, CancellationToken cancellationToken = default)
    {
        if (runOptions is null)
        {
            throw new ArgumentNullException(nameof(runOptions));
        }

        var workspace = new ProjectWorkspace();
        await workspace.LoadAsync(runOptions.ProjectPath, cancellationToken).ConfigureAwait(false);
        var project = workspace.CurrentProject ?? throw new InvalidOperationException($"Failed to load project '{runOptions.ProjectPath}'.");

        var platformConfig = project.GetPlatformConfiguration(runOptions.Platform);
        var formats = runOptions.Formats.Count > 0
            ? runOptions.Formats.ToList()
            : platformConfig?.Formats.ToList() ?? new List<string>();

        if (formats.Count == 0)
        {
            throw new InvalidOperationException("No formats specified. Add formats to the project or run options.");
        }

        var outputDirectory = ResolveOutputDirectory(runOptions);
        Directory.CreateDirectory(outputDirectory);

        var properties = MergeProperties(platformConfig?.Properties, runOptions.Properties);

        var request = new PackagingRequest(
            project.Id,
            runOptions.Platform,
            formats,
            runOptions.Configuration,
            outputDirectory,
            properties);

        return await ExecuteAsync(
            project,
            request,
            runOptions.ProjectPath,
            runOptions.PluginDirectories,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Executes a packaging run using a pre-loaded <see cref="PackagingProject"/> and <see cref="PackagingRequest"/>.
    /// </summary>
    public Task<PackagingResult> PackAsync(PackagingProject project, PackagingRequest request, CancellationToken cancellationToken = default)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (!string.Equals(project.Id, request.ProjectId, StringComparison.Ordinal))
        {
            throw new ArgumentException("Request project id does not match the supplied project.", nameof(request));
        }

        return ExecuteAsync(project, request, projectPath: null, runtimePluginDirectories: null, cancellationToken);
    }

    private async Task<PackagingResult> ExecuteAsync(
        PackagingProject project,
        PackagingRequest request,
        string? projectPath,
        IEnumerable<string>? runtimePluginDirectories,
        CancellationToken cancellationToken)
    {
        await using var provider = await BuildServiceProviderAsync(
            project,
            projectPath,
            runtimePluginDirectories,
            cancellationToken).ConfigureAwait(false);
        var pipelines = provider.GetRequiredService<IEnumerable<IPackagingPipeline>>();
        var pipeline = pipelines.FirstOrDefault(p => p.Platform == request.Platform)
            ?? throw new InvalidOperationException($"No packaging pipeline registered for platform '{request.Platform}'.");

        try
        {
            await InitializeIdentityAsync(provider, project, request, cancellationToken).ConfigureAwait(false);
            return await pipeline.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            PersistDashboardTelemetry(provider);
        }
    }

    private async Task<ServiceProvider> BuildServiceProviderAsync(
        PackagingProject project,
        string? projectPath,
        IEnumerable<string>? runtimePluginDirectories,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();

        var telemetry = _options.TelemetryChannel;
        DashboardTelemetryAggregator? dashboardAggregator = null;
        if (telemetry is null)
        {
            dashboardAggregator = DashboardTelemetryStore.CreateSharedAggregator();
            telemetry = dashboardAggregator;
        }
        else
        {
            dashboardAggregator = telemetry as DashboardTelemetryAggregator;
        }

        services.AddSingleton<ITelemetryChannel>(telemetry);
        if (dashboardAggregator is not null)
        {
            services.AddSingleton(dashboardAggregator);
            services.AddSingleton<IDashboardTelemetryProvider>(dashboardAggregator);
        }
        services.AddPackagingIdentity();
        services.AddSingleton<IPolicyEvaluator>(_options.PolicyEvaluator ?? new PolicyEngineEvaluator());
        services.AddSingleton<IBuildAgentBroker>(_options.AgentBroker ?? new LocalAgentBroker());
        services.AddSingleton<IPackagingProjectStore>(new InMemoryProjectStore(project));

        if (_options.IncludeWindows)
        {
            services.AddWindowsPackaging();
        }

        if (_options.IncludeMac)
        {
            services.AddMacPackaging();
        }

        if (_options.IncludeLinux)
        {
            services.AddLinuxPackaging();
        }

        _options.ConfigureServices?.Invoke(services);

        var pluginManager = new PluginManager(services);
        var pluginDirectories = PluginConfiguration.ResolveProbeDirectories(
            project,
            projectPath,
            runtimePluginDirectories,
            _options.PluginDirectories);

        foreach (var directory in pluginDirectories)
        {
            await pluginManager.LoadFromAsync(directory, cancellationToken).ConfigureAwait(false);
        }

        var provider = services.BuildServiceProvider();
        await pluginManager.InitialiseAsync(provider, cancellationToken).ConfigureAwait(false);
        return provider;
    }

    private static void PersistDashboardTelemetry(IServiceProvider provider)
    {
        var aggregator = provider.GetService<DashboardTelemetryAggregator>();
        if (aggregator is null)
        {
            return;
        }

        DashboardTelemetryStore.SaveSnapshot(aggregator);
    }

    private static async Task InitializeIdentityAsync(
        ServiceProvider provider,
        PackagingProject project,
        PackagingRequest request,
        CancellationToken cancellationToken)
    {
        var identityService = provider.GetService<IIdentityService>();
        var contextAccessor = provider.GetService<IIdentityContextAccessor>();
        if (identityService is null || contextAccessor is null)
        {
            return;
        }

        try
        {
            var identityRequest = IdentityRequestBuilder.Create(project, request);
            var identity = await identityService.AcquireAsync(identityRequest, cancellationToken).ConfigureAwait(false);
            contextAccessor.SetIdentity(identity);
        }
        catch
        {
            contextAccessor.Clear();
        }
    }

    private static string ResolveOutputDirectory(PackagingRunOptions runOptions)
    {
        if (!string.IsNullOrWhiteSpace(runOptions.OutputDirectory))
        {
            return Path.GetFullPath(runOptions.OutputDirectory);
        }

        var platformSegment = runOptions.Platform.ToString().ToLowerInvariant();
        return Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "artifacts", platformSegment));
    }

    private static IReadOnlyDictionary<string, string> MergeProperties(IReadOnlyDictionary<string, string>? baseProperties, IDictionary<string, string> overrides)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if ((baseProperties is null || baseProperties.Count == 0) && overrides.Count == 0)
        {
            return result;
        }
        if (baseProperties is not null)
        {
            foreach (var kv in baseProperties)
            {
                result[kv.Key] = kv.Value;
            }
        }

        foreach (var kv in overrides)
        {
            result[kv.Key] = kv.Value;
        }

        return result;
    }

    private sealed class LocalAgentBroker : IBuildAgentBroker
    {
        private sealed class Handle : IBuildAgentHandle
        {
            public string Name => "local";

            public IReadOnlyDictionary<string, string> Capabilities { get; } = new Dictionary<string, string>();

            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private static readonly Handle SharedHandle = new();

        public Task<IBuildAgentHandle> AcquireAsync(PackagingPlatform platform, CancellationToken cancellationToken = default)
            => Task.FromResult<IBuildAgentHandle>(SharedHandle);
    }

    private sealed class InMemoryProjectStore : IPackagingProjectStore
    {
        private readonly PackagingProject _project;

        public InMemoryProjectStore(PackagingProject project)
        {
            _project = project;
        }

        public Task<PackagingProject?> TryLoadAsync(string projectId, CancellationToken cancellationToken = default)
        {
            var match = string.Equals(_project.Id, projectId, StringComparison.Ordinal) ? _project : null;
            return Task.FromResult(match);
        }
    }

    private sealed class NoopTelemetryChannel : ITelemetryChannel
    {
        public static NoopTelemetryChannel Instance { get; } = new();

        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }

        public void TrackDependency(string dependencyName, TimeSpan duration, bool success, IReadOnlyDictionary<string, object?>? properties = null)
        {
        }
    }
}
