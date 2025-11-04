using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Linux.Sandbox;
using PackagingTools.Core.Models;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class LinuxSandboxProfileServiceTests : System.IDisposable
{
    private readonly string _tempRoot;

    public LinuxSandboxProfileServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "LinuxSandboxTests", System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task WritesProfileJsonWhenConfigurationProvided()
    {
        var service = new LinuxSandboxProfileService(NullLogger<LinuxSandboxProfileService>.Instance);

        var project = new PackagingProject(
            "linux.sandbox",
            "SandboxApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>());

        var outputDir = Path.Combine(_tempRoot, "output");
        Directory.CreateDirectory(outputDir);
        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.Linux,
            new[] { "appimage" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["linux.sandbox.enabled"] = "true",
                ["linux.sandbox.apparmorProfile"] = "usr.bin.app",
                ["linux.sandbox.flatpakPermissions"] = "filesystem=home"
            });
        var context = new PackageFormatContext(project, request, outputDir);

        var artifact = new PackagingArtifact("appimage", Path.Combine(_tempRoot, "artifact.appimage"), new Dictionary<string, string>());
        var result = new PackagingResult(true, new[] { artifact }, new PackagingIssue[0]);

        var issues = await service.ApplyAsync(context, result);

        Assert.Empty(issues);
        var profilePath = Directory.GetFiles(Path.Combine(outputDir, "_Audit"), "profile.json", SearchOption.AllDirectories);
        Assert.Single(profilePath);
        var json = await File.ReadAllTextAsync(profilePath[0]);
        using var document = JsonDocument.Parse(json);
        Assert.Equal("usr.bin.app", document.RootElement.GetProperty("AppArmorProfile").GetString());
    }

    [Fact]
    public async Task EmitsWarningWhenEnabledButMissingConfiguration()
    {
        var service = new LinuxSandboxProfileService(NullLogger<LinuxSandboxProfileService>.Instance);

        var project = new PackagingProject(
            "linux.sandbox.warning",
            "SandboxApp",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>());

        var outputDir = Path.Combine(_tempRoot, "output-warning");
        Directory.CreateDirectory(outputDir);
        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.Linux,
            new[] { "appimage" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["linux.sandbox.enabled"] = "true"
            });
        var context = new PackageFormatContext(project, request, outputDir);
        var artifact = new PackagingArtifact("appimage", Path.Combine(_tempRoot, "artifact.appimage"), new Dictionary<string, string>());
        var result = new PackagingResult(true, new[] { artifact }, new PackagingIssue[0]);

        var issues = await service.ApplyAsync(context, result);

        Assert.Contains(issues, i => i.Code == "linux.sandbox.missing_configuration" && i.Severity == PackagingIssueSeverity.Warning);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, true);
            }
        }
        catch
        {
        }
    }
}
