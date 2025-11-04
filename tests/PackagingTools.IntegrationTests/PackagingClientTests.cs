using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Configuration;
using PackagingTools.Core.Models;
using PackagingTools.Core.Plugins;
using PackagingTools.Core.Utilities;
using PackagingTools.IntegrationTests.Plugins;
using PackagingTools.Sdk;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class PackagingClientTests
{
    [Fact]
    public async Task PackAsync_WithProjectAndRequest_InvokesPipeline()
    {
        var project = new PackagingProject(
            "sdk.one",
            "SDK Sample",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "format" }, new Dictionary<string, string>())
            });

        var outputDir = Path.Combine(Path.GetTempPath(), "PackagingToolsSdk", Guid.NewGuid().ToString("N"));
        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.Windows,
            new[] { "format" },
            "Release",
            outputDir,
            new Dictionary<string, string>());

        var stubPipeline = new StubPipeline(PackagingPlatform.Windows);
        var client = PackagingClient.CreateDefault(options =>
        {
            options.IncludeWindows = false;
            options.IncludeMac = false;
            options.IncludeLinux = false;
            options.ConfigureServices = services => services.AddSingleton<IPackagingPipeline>(stubPipeline);
        });

        var result = await client.PackAsync(project, request);

        Assert.True(result.Success);
        Assert.NotNull(stubPipeline.LastRequest);
        Assert.Equal(project.Id, stubPipeline.LastRequest!.ProjectId);
    }

    [Fact]
    public async Task PackAsync_FromRunOptionsLoadsProject()
    {
        await using var temp = TemporaryDirectoryScope.Create("sdk-runoptions");
        var project = new PackagingProject(
            "sdk.two",
            "SDK Sample",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "format" }, new Dictionary<string, string>())
            });

        var projectPath = Path.Combine(temp.DirectoryPath, "project.json");
        await PackagingProjectSerializer.SaveAsync(project, projectPath);

        var outputDir = Path.Combine(temp.DirectoryPath, "out");
        var runOptions = new PackagingRunOptions(projectPath, PackagingPlatform.Windows)
        {
            OutputDirectory = outputDir,
            Configuration = "Release"
        };
        runOptions.Formats.Add("format");
        runOptions.Properties["custom"] = "value";

        var stubPipeline = new StubPipeline(PackagingPlatform.Windows);
        var client = PackagingClient.CreateDefault(options =>
        {
            options.IncludeWindows = false;
            options.IncludeMac = false;
            options.IncludeLinux = false;
            options.ConfigureServices = services => services.AddSingleton<IPackagingPipeline>(stubPipeline);
        });

        var result = await client.PackAsync(runOptions);

        Assert.True(result.Success);
        Assert.NotNull(stubPipeline.LastRequest);
        Assert.Equal(Path.GetFullPath(outputDir), stubPipeline.LastRequest!.OutputDirectory);
        Assert.Equal("value", stubPipeline.LastRequest.Properties?["custom"]);
    }

    [Fact]
    public async Task PackAsync_RegistersPluginsFromClientOptions()
    {
        SamplePackageFormatPlugin.Reset();
        PluginAwarePipeline.Reset();

        await using var tempDir = TemporaryDirectoryScope.Create("sdk-plugin-options");
        WritePluginManifest(tempDir.DirectoryPath);

        var project = new PackagingProject(
            "sdk.plugins.options",
            "SDK Plugins Options",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "sample" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.Windows,
            new[] { "sample" },
            "Release",
            tempDir.DirectoryPath,
            new Dictionary<string, string>());

        var client = PackagingClient.CreateDefault(options =>
        {
            options.IncludeWindows = false;
            options.IncludeMac = false;
            options.IncludeLinux = false;
            options.PluginDirectories.Add(tempDir.DirectoryPath);
            options.ConfigureServices = services =>
            {
                services.AddSingleton<IPackagingPipeline, PluginAwarePipeline>();
            };
        });

        var result = await client.PackAsync(project, request);

        Assert.True(result.Success);
        Assert.True(SamplePackageFormatPlugin.ServicesConfigured);
        Assert.True(SamplePackageFormatPlugin.Initialised);
        Assert.NotNull(PluginAwarePipeline.LastInstance);
        Assert.True(PluginAwarePipeline.LastInstance!.SawSamplePlugin);
        Assert.True(PluginAwarePipeline.LastInstance.Executed);
    }

    [Fact]
    public async Task PackAsync_RegistersPluginsFromRunOptions()
    {
        SamplePackageFormatPlugin.Reset();
        PluginAwarePipeline.Reset();

        await using var tempDir = TemporaryDirectoryScope.Create("sdk-plugin-runoptions");
        var project = new PackagingProject(
            "sdk.plugins.runoptions",
            "SDK Plugins RunOptions",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "sample" }, new Dictionary<string, string>())
            });

        var projectPath = Path.Combine(tempDir.DirectoryPath, "project.json");
        await PackagingProjectSerializer.SaveAsync(project, projectPath);

        var pluginDirectory = Path.Combine(tempDir.DirectoryPath, "plugins");
        Directory.CreateDirectory(pluginDirectory);
        WritePluginManifest(pluginDirectory);

        var runOptions = new PackagingRunOptions(projectPath, PackagingPlatform.Windows)
        {
            OutputDirectory = Path.Combine(tempDir.DirectoryPath, "out"),
            Configuration = "Release"
        };
        runOptions.Formats.Add("sample");
        runOptions.PluginDirectories.Add("plugins");

        var client = PackagingClient.CreateDefault(options =>
        {
            options.IncludeWindows = false;
            options.IncludeMac = false;
            options.IncludeLinux = false;
            options.ConfigureServices = services =>
            {
                services.AddSingleton<IPackagingPipeline, PluginAwarePipeline>();
            };
        });

        var result = await client.PackAsync(runOptions);

        Assert.True(result.Success);
        Assert.True(SamplePackageFormatPlugin.ServicesConfigured);
        Assert.True(SamplePackageFormatPlugin.Initialised);
        Assert.NotNull(PluginAwarePipeline.LastInstance);
        Assert.True(PluginAwarePipeline.LastInstance!.SawSamplePlugin);
        Assert.True(PluginAwarePipeline.LastInstance.Executed);
    }

    [Fact]
    public async Task PackAsync_RegistersPluginsFromProjectMetadata()
    {
        SamplePackageFormatPlugin.Reset();
        PluginAwarePipeline.Reset();

        await using var tempDir = TemporaryDirectoryScope.Create("sdk-plugin-metadata");

        var pluginDirectory = Path.Combine(tempDir.DirectoryPath, "plugins");
        Directory.CreateDirectory(pluginDirectory);
        WritePluginManifest(pluginDirectory);

        var project = new PackagingProject(
            "sdk.plugins.metadata",
            "SDK Plugins Metadata",
            "1.0.0",
            new Dictionary<string, string>
            {
                [PluginConfiguration.MetadataKey] = PluginConfiguration.FormatPathList(new[] { "plugins" })
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "sample" }, new Dictionary<string, string>())
            });

        var projectPath = Path.Combine(tempDir.DirectoryPath, "project.json");
        await PackagingProjectSerializer.SaveAsync(project, projectPath);

        var runOptions = new PackagingRunOptions(projectPath, PackagingPlatform.Windows)
        {
            OutputDirectory = Path.Combine(tempDir.DirectoryPath, "out"),
            Configuration = "Release"
        };
        runOptions.Formats.Add("sample");

        var client = PackagingClient.CreateDefault(options =>
        {
            options.IncludeWindows = false;
            options.IncludeMac = false;
            options.IncludeLinux = false;
            options.ConfigureServices = services =>
            {
                services.AddSingleton<IPackagingPipeline, PluginAwarePipeline>();
            };
        });

        var result = await client.PackAsync(runOptions);

        Assert.True(result.Success);
        Assert.True(SamplePackageFormatPlugin.ServicesConfigured);
        Assert.True(SamplePackageFormatPlugin.Initialised);
        Assert.NotNull(PluginAwarePipeline.LastInstance);
        Assert.True(PluginAwarePipeline.LastInstance!.SawSamplePlugin);
        Assert.True(PluginAwarePipeline.LastInstance.Executed);
    }

    private sealed class StubPipeline : IPackagingPipeline
    {
        public StubPipeline(PackagingPlatform platform)
        {
            Platform = platform;
        }

        public PackagingPlatform Platform { get; }

        public PackagingRequest? LastRequest { get; private set; }

        public Task<PackagingResult> ExecuteAsync(PackagingRequest request, CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            var artifactPath = Path.Combine(request.OutputDirectory, $"{request.ProjectId}.{request.Platform}.pkg");
            var artifact = new PackagingArtifact("format", artifactPath, new Dictionary<string, string>());
            return Task.FromResult(PackagingResult.Succeeded(new[] { artifact }));
        }
    }

    private sealed class PluginAwarePipeline : IPackagingPipeline
    {
        private readonly IReadOnlyList<IPackageFormatProvider> _providers;

        public PluginAwarePipeline(IEnumerable<IPackageFormatProvider> providers)
        {
            _providers = (providers ?? Array.Empty<IPackageFormatProvider>()).ToList();
            LastInstance = this;
            SawSamplePlugin = _providers.OfType<SamplePackageFormatPlugin>().Any();
        }

        public static PluginAwarePipeline? LastInstance { get; private set; }

        public static void Reset()
        {
            LastInstance = null;
        }

        public PackagingPlatform Platform => PackagingPlatform.Windows;

        public bool SawSamplePlugin { get; }

        public bool Executed { get; private set; }

        public Task<PackagingResult> ExecuteAsync(PackagingRequest request, CancellationToken cancellationToken = default)
        {
            Executed = true;
            var artifactPath = Path.Combine(request.OutputDirectory, $"{request.ProjectId}.{request.Platform}.pkg");
            var artifact = new PackagingArtifact("sample", artifactPath, new Dictionary<string, string>());
            return Task.FromResult(PackagingResult.Succeeded(new[] { artifact }));
        }
    }

    private static void WritePluginManifest(string directory)
    {
        var manifestPath = Path.Combine(directory, "sample-plugin.json");
        var manifest = new PluginManifest
        {
            AssemblyPath = typeof(SamplePackageFormatPlugin).Assembly.Location,
            PluginType = typeof(SamplePackageFormatPlugin).FullName
        };
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest));
    }
}
