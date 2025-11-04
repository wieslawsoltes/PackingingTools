using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Mac.Formats;
using PackagingTools.Core.Mac.Tooling;
using PackagingTools.Core.Models;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class NotarizationFormatProviderTests : IDisposable
{
    private readonly string _tempRoot;

    public NotarizationFormatProviderTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PackagingToolsNotaryTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task SubmitAccepted_StaplesAndReturnsMetadata()
    {
        var artifact = CreateArtifact();
        var runner = new ScriptedMacProcessRunner(request =>
        {
            if (request.FileName == "notarytool")
            {
                var verb = request.Arguments[0];
                return verb switch
                {
                    "submit" => new MacProcessResult(0, "{\"id\":\"req-123\",\"status\":\"Accepted\",\"statusSummary\":\"Accepted\"}", string.Empty),
                    "log" => new MacProcessResult(0, "{\"log\":\"accepted\"}", string.Empty),
                    _ => throw new InvalidOperationException($"Unexpected verb '{verb}'.")
                };
            }

            if (request.FileName == "xcrun")
            {
                return new MacProcessResult(0, "stapled", string.Empty);
            }

            throw new InvalidOperationException($"Unexpected tool '{request.FileName}'.");
        });

        var telemetry = new RecordingTelemetry();
        var provider = new NotarizationFormatProvider(runner, telemetry, NullLogger<NotarizationFormatProvider>.Instance);

        var context = CreateContext(
            artifact,
            new Dictionary<string, string>
            {
                ["mac.appleId"] = "user@example.com",
                ["mac.teamId"] = "TEAMID",
                ["mac.notarytool.profile"] = "profile"
            },
            new Dictionary<string, string>
            {
                ["mac.notarization.artifact"] = artifact
            });

        var result = await provider.PackageAsync(context);

        Assert.DoesNotContain(result.Issues, i => i.Severity == PackagingIssueSeverity.Error);
        var notarized = Assert.Single(result.Artifacts);
        Assert.True(notarized.Metadata.TryGetValue("notarizationStatus", out var status));
        Assert.Equal("Accepted", status);
        Assert.True(notarized.Metadata.TryGetValue("stapled", out var stapledValue));
        Assert.Equal("True", stapledValue);
        Assert.True(notarized.Metadata.TryGetValue("notarizationLog", out var logPath));
        Assert.True(File.Exists(logPath));
        Assert.Contains(runner.Requests, r => r.FileName == "xcrun");
    }

    [Fact]
    public async Task SubmitInProgress_PollsUntilAccepted()
    {
        var artifact = CreateArtifact();
        var pollCount = 0;
        var runner = new ScriptedMacProcessRunner(request =>
        {
            if (request.FileName == "notarytool")
            {
                var verb = request.Arguments[0];
                return verb switch
                {
                    "submit" => new MacProcessResult(0, "{\"id\":\"req-456\",\"status\":\"InProgress\"}", string.Empty),
                    "status" => ++pollCount == 1
                        ? new MacProcessResult(0, "{\"id\":\"req-456\",\"status\":\"In Progress\"}", string.Empty)
                        : new MacProcessResult(0, "{\"id\":\"req-456\",\"status\":\"Accepted\",\"statusSummary\":\"Accepted\"}", string.Empty),
                    "log" => new MacProcessResult(0, "{\"log\":\"accepted\"}", string.Empty),
                    _ => throw new InvalidOperationException($"Unexpected verb '{verb}'.")
                };
            }

            if (request.FileName == "xcrun")
            {
                return new MacProcessResult(0, "stapled", string.Empty);
            }

            throw new InvalidOperationException($"Unexpected tool '{request.FileName}'.");
        });

        var provider = new NotarizationFormatProvider(runner, new RecordingTelemetry(), NullLogger<NotarizationFormatProvider>.Instance);
        var context = CreateContext(
            artifact,
            new Dictionary<string, string>
            {
                ["mac.appleId"] = "user@example.com",
                ["mac.teamId"] = "TEAMID"
            },
            new Dictionary<string, string>
            {
                ["mac.notarization.artifact"] = artifact,
                ["mac.notarization.maxPollAttempts"] = "3",
                ["mac.notarization.pollIntervalSeconds"] = "0"
            });

        var result = await provider.PackageAsync(context);

        Assert.DoesNotContain(result.Issues, i => i.Severity == PackagingIssueSeverity.Error);
        Assert.Contains(runner.Requests, r => r.FileName == "notarytool" && r.Arguments[0] == "status");
        var artifactResult = Assert.Single(result.Artifacts);
        Assert.True(artifactResult.Metadata.TryGetValue("notarizationStatus", out var status));
        Assert.Equal("Accepted", status);
    }

    [Fact]
    public async Task RejectedRequest_ReturnsRichIssue()
    {
        var artifact = CreateArtifact();
        var runner = new ScriptedMacProcessRunner(request =>
        {
            if (request.FileName == "notarytool")
            {
                var verb = request.Arguments[0];
                return verb switch
                {
                    "submit" => new MacProcessResult(0, "{\"id\":\"req-789\",\"status\":\"InProgress\"}", string.Empty),
                    "status" => new MacProcessResult(0, "{\"id\":\"req-789\",\"status\":\"Invalid\",\"statusSummary\":\"The binary is unsigned\"}", string.Empty),
                    "log" => new MacProcessResult(0, "{\"issues\":[{\"code\":123,\"message\":\"Code signature invalid\"}]}", string.Empty),
                    _ => throw new InvalidOperationException($"Unexpected verb '{verb}'.")
                };
            }

            throw new InvalidOperationException($"Unexpected tool '{request.FileName}'.");
        });

        var provider = new NotarizationFormatProvider(runner, new RecordingTelemetry(), NullLogger<NotarizationFormatProvider>.Instance);
        var context = CreateContext(
            artifact,
            new Dictionary<string, string>
            {
                ["mac.appleId"] = "user@example.com",
                ["mac.teamId"] = "TEAMID"
            },
            new Dictionary<string, string>
            {
                ["mac.notarization.artifact"] = artifact,
                ["mac.notarization.maxPollAttempts"] = "1",
                ["mac.notarization.pollIntervalSeconds"] = "0"
            });

        var result = await provider.PackageAsync(context);

        Assert.Contains(result.Issues, i => i.Severity == PackagingIssueSeverity.Error);
        var issue = Assert.Single(result.Issues, i => i.Code == "mac.notarization.rejected");
        Assert.Contains("The binary is unsigned", issue.Message);
        Assert.Contains("req-789", issue.Message);
        Assert.Contains(result.Issues, i => i.Code == "mac.notarization.log_saved");
    }

    [Fact]
    public async Task StaplerFailureFailsPackaging()
    {
        var artifact = CreateArtifact();
        var runner = new ScriptedMacProcessRunner(request =>
        {
            if (request.FileName == "notarytool")
            {
                var verb = request.Arguments[0];
                return verb switch
                {
                    "submit" => new MacProcessResult(0, "{\"id\":\"req\",\"status\":\"Accepted\"}", string.Empty),
                    "log" => new MacProcessResult(0, "{\"log\":\"accepted\"}", string.Empty),
                    _ => throw new InvalidOperationException($"Unexpected verb '{verb}'.")
                };
            }

            if (request.FileName == "xcrun")
            {
                return new MacProcessResult(1, string.Empty, "stapler error");
            }

            throw new InvalidOperationException($"Unexpected tool '{request.FileName}'.");
        });

        var provider = new NotarizationFormatProvider(runner, new RecordingTelemetry(), NullLogger<NotarizationFormatProvider>.Instance);
        var context = CreateContext(
            artifact,
            new Dictionary<string, string>
            {
                ["mac.appleId"] = "user@example.com",
                ["mac.teamId"] = "TEAMID"
            },
            new Dictionary<string, string>
            {
                ["mac.notarization.artifact"] = artifact
            });

        var result = await provider.PackageAsync(context);

        Assert.Contains(result.Issues, i => i.Severity == PackagingIssueSeverity.Error);
        Assert.Contains(result.Issues, i => i.Code == "mac.notarization.staple_failed");
    }

    private string CreateArtifact()
    {
        var artifact = Path.Combine(_tempRoot, "artifact.zip");
        File.WriteAllText(artifact, "dummy");
        return artifact;
    }

    private static PackageFormatContext CreateContext(
        string artifactPath,
        IReadOnlyDictionary<string, string> projectMetadata,
        IReadOnlyDictionary<string, string> requestProperties)
    {
        var project = new PackagingProject(
            "notary",
            "NotaryApp",
            "1.0.0",
            projectMetadata,
            new Dictionary<PackagingPlatform, PlatformConfiguration>());

        var outputDir = Path.Combine(Path.GetDirectoryName(artifactPath)!, "output");
        Directory.CreateDirectory(outputDir);
        var workingDir = Path.Combine(Path.GetDirectoryName(artifactPath)!, "working");
        Directory.CreateDirectory(workingDir);

        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.MacOS,
            new[] { "notarize" },
            "Release",
            outputDir,
            requestProperties);

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
            // ignore cleanup errors
        }
    }

    private sealed class ScriptedMacProcessRunner : IMacProcessRunner
    {
        private readonly Func<MacProcessRequest, MacProcessResult> _handler;

        public ScriptedMacProcessRunner(Func<MacProcessRequest, MacProcessResult> handler)
        {
            _handler = handler;
        }

        public List<MacProcessRequest> Requests { get; } = new();

        public Task<MacProcessResult> ExecuteAsync(MacProcessRequest request, CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return Task.FromResult(_handler(request));
        }
    }
}
