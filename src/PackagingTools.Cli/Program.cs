using System.Linq;
using System.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.AppServices;
using PackagingTools.Core.Configuration;
using PackagingTools.Core.Models;
using PackagingTools.Core.Windows;
using PackagingTools.Core.Mac;
using PackagingTools.Core.Linux;
using PackagingTools.Core.Windows.Configuration;
using PackagingTools.Core.Telemetry.Dashboards;
using PackagingTools.Core.Policies;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Plugins;

namespace PackagingTools.Cli;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return 0;
        }

        var command = args[0];
        var commandArgs = args.Skip(1).ToArray();
        if (string.Equals(command, "pack", StringComparison.OrdinalIgnoreCase))
        {
            return await RunPackCommandAsync(commandArgs);
        }

        if (string.Equals(command, "host", StringComparison.OrdinalIgnoreCase))
        {
            return await RunHostCommandAsync(commandArgs);
        }

        if (string.Equals(command, "identity", StringComparison.OrdinalIgnoreCase))
        {
            return await RunIdentityCommandAsync(commandArgs);
        }

        Console.Error.WriteLine($"Unknown command '{command}'.");
        PrintUsage();
        return 1;
    }

    private static async Task<int> RunPackCommandAsync(string[] args)
    {
        PackOptions options;
        try
        {
            options = PackOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }

        if (!options.IsValid(out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        var workspace = new ProjectWorkspace();
        await workspace.LoadAsync(options.ProjectPath!, CancellationToken.None);
        var project = workspace.CurrentProject ?? throw new InvalidOperationException("Failed to load project.");
        var platform = options.Platform!.Value;
        var configuration = options.Configuration ?? "Release";
        var platformConfig = project.GetPlatformConfiguration(platform);
        var formats = options.Formats.Count > 0
            ? options.Formats
            : platformConfig?.Formats.ToList() ?? new List<string>();
        if (formats.Count == 0)
        {
            Console.Error.WriteLine("No formats specified. Use --format or configure formats in the project file.");
            return 1;
        }

        var outputDirectory = Path.GetFullPath(options.Output ?? Path.Combine(Environment.CurrentDirectory, "artifacts"));
        Directory.CreateDirectory(outputDirectory);

        var properties = MergeProperties(platformConfig?.Properties, options.Properties);

        await using var provider = await BuildServiceProviderAsync(
            platform,
            project,
            workspace.ProjectPath,
            CancellationToken.None).ConfigureAwait(false);
        var pipeline = provider.GetRequiredService<IEnumerable<IPackagingPipeline>>().First(p => p.Platform == platform);

        var request = new PackagingRequest(
            project.Id,
            platform,
            formats,
            configuration,
            outputDirectory,
            properties);

        Console.WriteLine($"Starting packaging for {project.Name} ({platform})...");
        await InitializeIdentityAsync(provider, project, request, cancellationToken: CancellationToken.None);
        PackagingResult result;
        try
        {
            result = await pipeline.ExecuteAsync(request);
        }
        finally
        {
            PersistDashboardTelemetry(provider);
        }

        if (!result.Success)
        {
            Console.Error.WriteLine("Packaging failed:");
            foreach (var issue in result.Issues.Where(i => i.Severity == PackagingIssueSeverity.Error))
            {
                Console.Error.WriteLine($"  [ERROR] {issue.Code}: {issue.Message}");
            }
            return 1;
        }

        foreach (var artifact in result.Artifacts)
        {
            Console.WriteLine($"[OK] {artifact.Format} -> {artifact.Path}");
        }

        foreach (var warning in result.Issues.Where(i => i.Severity == PackagingIssueSeverity.Warning))
        {
            Console.WriteLine($"[WARN] {warning.Code}: {warning.Message}");
        }

        // Persist updated project (allows CLI overrides to be saved when desired in future).
        if (!string.IsNullOrEmpty(options.SaveProjectPath))
        {
            workspace.UpdatePlatformConfiguration(platform, new PlatformConfiguration(formats.ToArray(), properties));
            await workspace.SaveAsync(options.SaveProjectPath, CancellationToken.None);
            Console.WriteLine($"Project saved to {options.SaveProjectPath}.");
        }

        return 0;
    }

    private static async Task<int> RunHostCommandAsync(string[] args)
    {
        HostOptions options;
        try
        {
            options = HostOptions.Parse(args);
        }
        catch (ArgumentException ex)
        {
            Console.Error.WriteLine(ex.Message);
            PrintUsage();
            return 1;
        }

        if (!options.IsValid(out var error))
        {
            Console.Error.WriteLine(error);
            PrintUsage();
            return 1;
        }

        var workspace = new ProjectWorkspace();
        await workspace.LoadAsync(options.ProjectPath!, CancellationToken.None);
        var project = workspace.CurrentProject ?? throw new InvalidOperationException("Failed to load project.");
        await using var provider = await BuildServiceProviderAsync(
            PackagingPlatform.Windows,
            project,
            workspace.ProjectPath,
            CancellationToken.None).ConfigureAwait(false);
        var identityRequest = new PackagingRequest(
            project.Id,
            PackagingPlatform.Windows,
            Array.Empty<string>(),
            "HostIntegration",
            Environment.CurrentDirectory);
        await InitializeIdentityAsync(provider, project, identityRequest, CancellationToken.None);

        var existingConfig = project.GetPlatformConfiguration(PackagingPlatform.Windows);
        var hostService = new WindowsHostIntegrationService();
        var current = hostService.Load(existingConfig);

        var desired = ApplyHostOptions(options, current, project);
        var diff = hostService.CalculateDiff(existingConfig, desired);
        var issues = hostService.Validate(desired);

        Console.WriteLine("Windows Host Integration Preview");
        Console.WriteLine("--------------------------------");
        if (diff.Count == 0)
        {
            Console.WriteLine("No changes detected.");
        }
        else
        {
            foreach (var delta in diff)
            {
                var indicator = delta.ChangeType switch
                {
                    PropertyChangeType.Added => "+",
                    PropertyChangeType.Updated => "~",
                    PropertyChangeType.Removed => "-",
                    _ => " "
                };
                Console.WriteLine($"{indicator} {delta.Key} :: {delta.OldValue ?? "<none>"} => {delta.NewValue ?? "<none>"}");
            }
        }

        if (issues.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("Validation Messages:");
            foreach (var issue in issues)
            {
                var prefix = issue.Severity == HostIntegrationIssueSeverity.Error ? "[ERROR]" : "[WARN]";
                Console.WriteLine($"  {prefix} {issue.Code}: {issue.Message}");
            }
        }

        if (!options.ApplyChanges)
        {
            Console.WriteLine();
            Console.WriteLine("Run again with --apply to persist these changes.");
            PersistDashboardTelemetry(provider);
            return 0;
        }

        if (issues.Any(i => i.Severity == HostIntegrationIssueSeverity.Error))
        {
            Console.Error.WriteLine();
            Console.Error.WriteLine("Host integration configuration contains errors. Fix them before applying changes.");
            PersistDashboardTelemetry(provider);
            return 1;
        }

        var updatedConfig = hostService.Apply(existingConfig, desired);
        workspace.UpdatePlatformConfiguration(PackagingPlatform.Windows, updatedConfig);
        await workspace.SaveAsync(options.SaveProjectPath ?? workspace.ProjectPath, CancellationToken.None);
        Console.WriteLine();
        Console.WriteLine($"Host integration metadata saved to '{workspace.ProjectPath}'.");

        PersistDashboardTelemetry(provider);
        return 0;
    }

    private static async Task<int> RunIdentityCommandAsync(string[] args)
    {
        if (args.Length == 0 || !string.Equals(args[0], "login", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine("Unknown identity sub-command.");
            PrintUsage();
            return 1;
        }

        var options = IdentityLoginOptions.Parse(args.Skip(1).ToArray());
        using var services = new ServiceCollection()
            .AddPackagingIdentity()
            .BuildServiceProvider();

        var identityService = services.GetRequiredService<IIdentityService>();
        var contextAccessor = services.GetRequiredService<IIdentityContextAccessor>();

        try
        {
            var request = options.ToIdentityRequest();
            var result = await identityService.AcquireAsync(request);
            contextAccessor.SetIdentity(result);

            Console.WriteLine($"Signed in as {result.Principal.DisplayName} ({result.Principal.Id}).");
            if (result.AccessToken is not null)
            {
                Console.WriteLine($"Access token expires {result.AccessToken.ExpiresAtUtc:g}.");
            }
            Console.WriteLine($"Roles: {string.Join(", ", result.Principal.Roles)}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Identity login failed: {ex.Message}");
            return 1;
        }
    }

    private static WindowsHostIntegrationSettings ApplyHostOptions(
        HostOptions options,
        WindowsHostIntegrationSettings current,
        PackagingProject project)
    {
        var updated = current;

        if (options.ShortcutEnabled.HasValue)
        {
            updated = updated with { ShortcutEnabled = options.ShortcutEnabled.Value };
        }

        if (options.ProtocolEnabled.HasValue)
        {
            updated = updated with { ProtocolEnabled = options.ProtocolEnabled.Value };
        }

        if (options.FileAssociationEnabled.HasValue)
        {
            updated = updated with { FileAssociationEnabled = options.FileAssociationEnabled.Value };
        }

        updated = updated with
        {
            ShortcutName = options.ShortcutName ?? updated.ShortcutName ?? project.Name,
            ShortcutTarget = NormalizePath(options.ShortcutTarget ?? updated.ShortcutTarget ?? $"{project.Name}.exe"),
            ShortcutDescription = options.ShortcutDescription ?? updated.ShortcutDescription,
            ShortcutIcon = options.ShortcutIcon ?? updated.ShortcutIcon,
            ProtocolName = NormalizeScheme(options.ProtocolName ?? updated.ProtocolName),
            ProtocolDisplayName = options.ProtocolDisplayName ?? updated.ProtocolDisplayName,
            ProtocolCommand = options.ProtocolCommand ?? updated.ProtocolCommand,
            FileAssociationExtension = NormalizeExtension(options.FileAssociationExtension ?? updated.FileAssociationExtension),
            FileAssociationProgId = options.FileAssociationProgId ?? updated.FileAssociationProgId,
            FileAssociationDescription = options.FileAssociationDescription ?? updated.FileAssociationDescription,
            FileAssociationCommand = options.FileAssociationCommand ?? updated.FileAssociationCommand
        };

        return updated;
    }

    private static string? NormalizeExtension(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        var trimmed = value.Trim();
        return trimmed.StartsWith(".", StringComparison.Ordinal) ? trimmed : "." + trimmed;
    }

    private static string? NormalizeScheme(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Trim().Replace(" ", string.Empty).ToLowerInvariant();
    }

    private static string? NormalizePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Trim();
    }

    private static async Task<ServiceProvider> BuildServiceProviderAsync(
        PackagingPlatform platform,
        PackagingProject project,
        string? projectPath,
        CancellationToken cancellationToken)
    {
        var services = new ServiceCollection();
        var dashboardTelemetry = DashboardTelemetryStore.CreateSharedAggregator();
        services.AddSingleton(dashboardTelemetry);
        services.AddSingleton<IDashboardTelemetryProvider>(dashboardTelemetry);
        services.AddSingleton<ITelemetryChannel>(dashboardTelemetry);
        services.AddPackagingIdentity();
        services.AddSingleton<IPolicyEvaluator, PolicyEngineEvaluator>();
        services.AddSingleton<IBuildAgentBroker, LocalAgentBroker>();
        services.AddSingleton<IPackagingProjectStore>(new InMemoryProjectStore(project));

        switch (platform)
        {
            case PackagingPlatform.Windows:
                services.AddWindowsPackaging();
                break;
            case PackagingPlatform.MacOS:
                services.AddMacPackaging();
                break;
            case PackagingPlatform.Linux:
                services.AddLinuxPackaging();
                break;
        }

        var pluginManager = new PluginManager(services);
        var pluginDirectories = PluginConfiguration.ResolveProbeDirectories(project, projectPath);

        foreach (var directory in pluginDirectories)
        {
            await pluginManager.LoadFromAsync(directory, cancellationToken).ConfigureAwait(false);
        }

        var provider = services.BuildServiceProvider();
        await pluginManager.InitialiseAsync(provider, cancellationToken).ConfigureAwait(false);
        return provider;
    }

    private static void PersistDashboardTelemetry(ServiceProvider provider)
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

        var identityRequest = IdentityRequestBuilder.Create(project, request);

        try
        {
            var identity = await identityService.AcquireAsync(identityRequest, cancellationToken).ConfigureAwait(false);
            contextAccessor.SetIdentity(identity);
        }
        catch (Exception ex)
        {
            contextAccessor.Clear();
            Console.Error.WriteLine($"[identity] Failed to acquire identity: {ex.Message}");
        }
    }

    private static Dictionary<string, string> MergeProperties(IReadOnlyDictionary<string, string>? baseProperties, IReadOnlyDictionary<string, string> overrides)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
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

    private static void PrintUsage()
    {
        Console.WriteLine("PackagingTools CLI");
        Console.WriteLine();
        Console.WriteLine("Commands:");
        Console.WriteLine("  pack      Build packaging artifacts.");
        Console.WriteLine("  host      Configure Windows host integration metadata.");
        Console.WriteLine("  identity  Manage identity tokens (login).");
        Console.WriteLine("  packagingtools identity login [identity options]");
        Console.WriteLine();
        Console.WriteLine("Usage:");
        Console.WriteLine("  packagingtools pack --project <file> --platform <windows|mac|linux> [options]");
        Console.WriteLine("  packagingtools host --project <file> [host options]");
        Console.WriteLine();
        Console.WriteLine("Pack Options:");
        Console.WriteLine("  --project <path>          Path to project definition JSON file");
        Console.WriteLine("  --platform <name>         Target platform");
        Console.WriteLine("  --format <value>          Requested format (repeatable)");
        Console.WriteLine("  --configuration <value>   Build configuration (default Release)");
        Console.WriteLine("  --output <path>           Output directory (default ./artifacts)");
        Console.WriteLine("  --property key=value      Provider property override");
        Console.WriteLine("  --save-project <path>     Persist updated project after run");
        Console.WriteLine("    Remote signing properties: windows.signing.azureKeyVaultCertificate, windows.signing.azureKeyVaultUrl");
        Console.WriteLine("    Local signing properties: windows.signing.certificatePath, windows.signing.password");
        Console.WriteLine();
        Console.WriteLine("Host Options:");
        Console.WriteLine("  --project <path>                    Path to project definition JSON file");
        Console.WriteLine("  --enable-shortcut|--disable-shortcut");
        Console.WriteLine("  --shortcut-name <value>");
        Console.WriteLine("  --shortcut-target <value>");
        Console.WriteLine("  --shortcut-description <value>");
        Console.WriteLine("  --shortcut-icon <value>");
        Console.WriteLine("  --enable-protocol|--disable-protocol");
        Console.WriteLine("  --protocol-name <value>");
        Console.WriteLine("  --protocol-display-name <value>");
        Console.WriteLine("  --protocol-command <value>");
        Console.WriteLine("  --enable-file-association|--disable-file-association");
        Console.WriteLine("  --file-extension <value>");
        Console.WriteLine("  --file-progid <value>");
        Console.WriteLine("  --file-description <value>");
        Console.WriteLine("  --file-command <value>");
        Console.WriteLine("  --apply                             Persist changes (default previews only)");
        Console.WriteLine("  --save-project <path>               Optional override path when applying");
        Console.WriteLine();
        Console.WriteLine("Identity Login Options:");
        Console.WriteLine("  --provider <name>        Identity provider (azuread|okta|local, default azuread)");
        Console.WriteLine("  --scope <value>          Requested scope (repeatable)");
        Console.WriteLine("  --require-mfa            Require MFA for the login session");
        Console.WriteLine("  --tenant <value>         Azure AD tenant id");
        Console.WriteLine("  --client-id <value>      Azure AD client id");
        Console.WriteLine("  --domain <value>         Okta domain");
        Console.WriteLine("  --username <value>       Username or UPN");
        Console.WriteLine("  --display-name <value>   Friendly display name override");
        Console.WriteLine("  --email <value>          Email override");
        Console.WriteLine("  --roles <value>          Comma-separated role list");
        Console.WriteLine("  --organization <value>   Okta organization name");
        Console.WriteLine("  --mfa-code <value>       MFA verification code");
    }

    private sealed record PackOptions
    {
        public string? ProjectPath { get; init; }
        public PackagingPlatform? Platform { get; init; }
        public List<string> Formats { get; } = new();
        public string? Configuration { get; init; }
        public string? Output { get; init; }
        public string? SaveProjectPath { get; init; }
        public Dictionary<string, string> Properties { get; } = new(StringComparer.OrdinalIgnoreCase);

        public static PackOptions Parse(string[] args)
        {
            var options = new PackOptions();
            var i = 0;
            while (i < args.Length)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--project":
                        options = options with { ProjectPath = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--platform":
                        var value = GetValue(args, ++i, arg);
                        options = options with
                        {
                            Platform = value?.ToLowerInvariant() switch
                            {
                                "windows" => PackagingPlatform.Windows,
                                "mac" or "macos" => PackagingPlatform.MacOS,
                                "linux" => PackagingPlatform.Linux,
                                _ => null
                            }
                        };
                        i++;
                        break;
                    case "--format":
                        options.Formats.Add(GetValue(args, ++i, arg)!);
                        i++;
                        break;
                    case "--configuration":
                        options = options with { Configuration = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--output":
                        options = options with { Output = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--property":
                        var kv = GetValue(args, ++i, arg);
                        if (!string.IsNullOrEmpty(kv))
                        {
                            var parts = kv.Split('=', 2);
                            if (parts.Length == 2)
                            {
                                options.Properties[parts[0]] = parts[1];
                            }
                        }
                        i++;
                        break;
                    case "--save-project":
                        options = options with { SaveProjectPath = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{arg}'.");
                }
            }

            return options;
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(ProjectPath))
            {
                error = "--project is required.";
                return false;
            }

            if (Platform is null)
            {
                error = "--platform is required.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string? GetValue(string[] args, int index, string option)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"Missing value for {option}.");
            }
            return args[index];
        }
    }

    private sealed record HostOptions
    {
        public string? ProjectPath { get; init; }
        public bool? ShortcutEnabled { get; init; }
        public bool? ProtocolEnabled { get; init; }
        public bool? FileAssociationEnabled { get; init; }
        public string? ShortcutName { get; init; }
        public string? ShortcutTarget { get; init; }
        public string? ShortcutDescription { get; init; }
        public string? ShortcutIcon { get; init; }
        public string? ProtocolName { get; init; }
        public string? ProtocolDisplayName { get; init; }
        public string? ProtocolCommand { get; init; }
        public string? FileAssociationExtension { get; init; }
        public string? FileAssociationProgId { get; init; }
        public string? FileAssociationDescription { get; init; }
        public string? FileAssociationCommand { get; init; }
        public bool ApplyChanges { get; init; }
        public string? SaveProjectPath { get; init; }

        public static HostOptions Parse(string[] args)
        {
            var options = new HostOptions();
            var i = 0;
            while (i < args.Length)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--project":
                        options = options with { ProjectPath = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--enable-shortcut":
                        options = options with { ShortcutEnabled = true };
                        i++;
                        break;
                    case "--disable-shortcut":
                        options = options with { ShortcutEnabled = false };
                        i++;
                        break;
                    case "--shortcut-name":
                        options = options with { ShortcutName = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--shortcut-target":
                        options = options with { ShortcutTarget = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--shortcut-description":
                        options = options with { ShortcutDescription = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--shortcut-icon":
                        options = options with { ShortcutIcon = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--enable-protocol":
                        options = options with { ProtocolEnabled = true };
                        i++;
                        break;
                    case "--disable-protocol":
                        options = options with { ProtocolEnabled = false };
                        i++;
                        break;
                    case "--protocol-name":
                        options = options with { ProtocolName = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--protocol-display-name":
                        options = options with { ProtocolDisplayName = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--protocol-command":
                        options = options with { ProtocolCommand = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--enable-file-association":
                        options = options with { FileAssociationEnabled = true };
                        i++;
                        break;
                    case "--disable-file-association":
                        options = options with { FileAssociationEnabled = false };
                        i++;
                        break;
                    case "--file-extension":
                        options = options with { FileAssociationExtension = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--file-progid":
                        options = options with { FileAssociationProgId = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--file-description":
                        options = options with { FileAssociationDescription = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--file-command":
                        options = options with { FileAssociationCommand = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    case "--apply":
                        options = options with { ApplyChanges = true };
                        i++;
                        break;
                    case "--save-project":
                        options = options with { SaveProjectPath = GetValue(args, ++i, arg) };
                        i++;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{arg}'.");
                }
            }

            return options;
        }

        public bool IsValid(out string error)
        {
            if (string.IsNullOrWhiteSpace(ProjectPath))
            {
                error = "--project is required.";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static string? GetValue(string[] args, int index, string option)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"Missing value for {option}.");
            }

            return args[index];
        }
    }

    private sealed record IdentityLoginOptions
    {
        public string Provider { get; init; } = "azuread";
        public List<string> Scopes { get; init; } = new();
        public bool RequireMfa { get; init; }
        public Dictionary<string, string> Parameters { get; init; } = new(StringComparer.OrdinalIgnoreCase);

        public static IdentityLoginOptions Parse(string[] args)
        {
            var options = new IdentityLoginOptions();
            var index = 0;
            while (index < args.Length)
            {
                var arg = args[index];
                switch (arg)
                {
                    case "--provider":
                        options = options with { Provider = GetValue(args, ++index, arg) ?? "azuread" };
                        index++;
                        break;
                    case "--scope":
                        options.Scopes.Add(GetValue(args, ++index, arg) ?? "packaging.run");
                        index++;
                        break;
                    case "--require-mfa":
                        options = options with { RequireMfa = true };
                        index++;
                        break;
                    case "--tenant":
                        options.Parameters["tenantId"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--client-id":
                        options.Parameters["clientId"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--domain":
                        options.Parameters["domain"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--username":
                        options.Parameters["username"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--display-name":
                        options.Parameters["displayName"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--email":
                        options.Parameters["email"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--roles":
                        options.Parameters["roles"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--organization":
                        options.Parameters["organization"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    case "--mfa-code":
                        options.Parameters["mfaCode"] = GetValue(args, ++index, arg) ?? string.Empty;
                        index++;
                        break;
                    default:
                        throw new ArgumentException($"Unknown option '{arg}'.");
                }
            }

            return options;
        }

        public IdentityRequest ToIdentityRequest()
        {
            var scopes = Scopes.Count > 0 ? Scopes : new List<string> { "packaging.run" };
            var parameters = Parameters
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);

            return new IdentityRequest(Provider, scopes, RequireMfa, parameters);
        }

        private static string? GetValue(string[] args, int index, string option)
        {
            if (index >= args.Length)
            {
                throw new ArgumentException($"Missing value for {option}.");
            }

            return args[index];
        }
    }

    private sealed class LocalAgentBroker : IBuildAgentBroker
    {
        private sealed class Handle : IBuildAgentHandle
        {
            public string Name => "local";
            public IReadOnlyDictionary<string, string> Capabilities => new Dictionary<string, string>();
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }

        private static readonly Handle SharedHandle = new();

        public Task<IBuildAgentHandle> AcquireAsync(PackagingPlatform platform, CancellationToken cancellationToken = default)
            => Task.FromResult<IBuildAgentHandle>(SharedHandle);
    }

    private sealed class InMemoryProjectStore : IPackagingProjectStore
    {
        private readonly PackagingProject _project;

        public InMemoryProjectStore(PackagingProject project) => _project = project;

        public Task<PackagingProject?> TryLoadAsync(string projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(_project.Id == projectId ? _project : null);
    }
}
