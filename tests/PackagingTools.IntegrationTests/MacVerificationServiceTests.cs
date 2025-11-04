using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Mac.Tooling;
using PackagingTools.Core.Mac.Verification;
using PackagingTools.Core.Models;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class MacVerificationServiceTests : IDisposable
{
    private readonly string _tempRoot;

    public MacVerificationServiceTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "MacVerifyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Theory]
    [InlineData("app", new[] { "spctl" })]
    [InlineData("pkg", new[] { "spctl", "pkgutil" })]
    [InlineData("dmg", new[] { "hdiutil" })]
    public async Task InvokesExpectedToolsPerFormat(string format, string[] expectedTools)
    {
        var runner = new RecordingMacProcessRunner();
        var telemetry = new StubTelemetry();
        var service = new MacVerificationService(runner, telemetry, NullLogger<MacVerificationService>.Instance);

        var artifactPath = Path.Combine(_tempRoot, $"artifact.{format}");
        File.WriteAllText(artifactPath, string.Empty);

        var context = CreateContext();
        var artifact = new PackagingArtifact(format, artifactPath, new Dictionary<string, string>());

        var result = await service.VerifyAsync(context, artifact);

        Assert.DoesNotContain(result.Issues, i => i.Severity == PackagingIssueSeverity.Error);
        Assert.Equal(expectedTools, runner.Invocations.Select(r => r.FileName));
    }

    [Fact]
    public async Task RecordsIssueWhenToolFails()
    {
        var runner = new RecordingMacProcessRunner(failTool: "spctl");
        var service = new MacVerificationService(runner, new StubTelemetry(), NullLogger<MacVerificationService>.Instance);

        var artifactPath = Path.Combine(_tempRoot, "artifact.app");
        File.WriteAllText(artifactPath, string.Empty);

        var context = CreateContext();
        var artifact = new PackagingArtifact("app", artifactPath, new Dictionary<string, string>());

        var result = await service.VerifyAsync(context, artifact);

        Assert.Contains(result.Issues, i => i.Code.StartsWith("mac.verify.spctl_failed", StringComparison.Ordinal));
        Assert.False(result.Success);
    }

    private PackageFormatContext CreateContext()
    {
        var project = new PackagingProject(
            "mac.verify",
            "MacVerify",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>());

        var outputDir = Path.Combine(_tempRoot, "output");
        var workingDir = Path.Combine(_tempRoot, "working");
        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(workingDir);

        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.MacOS,
            new[] { "app" },
            "Release",
            outputDir,
            new Dictionary<string, string>());

        return new PackageFormatContext(project, request, workingDir);
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
            // ignore cleanup issues
        }
    }

    private sealed class RecordingMacProcessRunner : IMacProcessRunner
    {
        private readonly string? _failTool;

        public RecordingMacProcessRunner(string? failTool = null)
        {
            _failTool = failTool;
        }

        public List<MacProcessRequest> Invocations { get; } = new();

        public Task<MacProcessResult> ExecuteAsync(MacProcessRequest request, CancellationToken cancellationToken = default)
        {
            Invocations.Add(request);

            if (_failTool is not null && string.Equals(request.FileName, _failTool, StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new MacProcessResult(1, string.Empty, "failed"));
            }

            return Task.FromResult(new MacProcessResult(0, "ok", string.Empty));
        }
    }

    private sealed class StubTelemetry : ITelemetryChannel
    {
        public List<(string Event, IReadOnlyDictionary<string, object?>? Properties)> Events { get; } = new();

        public void TrackDependency(string dependencyName, TimeSpan duration, bool success, IReadOnlyDictionary<string, object?>? properties = null)
            => Events.Add(($"dep:{dependencyName}", properties));

        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
            => Events.Add((eventName, properties));
    }
}
