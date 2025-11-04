using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Windows.Formats;
using PackagingTools.Core.Windows.Pipelines;
using PackagingTools.Core.Windows.Tooling;
using PackagingTools.Core.Security.Identity;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class WindowsRegressionTests : IDisposable
{
    private readonly string _tempRoot;

    public WindowsRegressionTests()
    {
        _tempRoot = Path.Combine(Path.GetTempPath(), "PackagingToolsWinRegression", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempRoot);
    }

    [Fact]
    public async Task WindowsPipeline_MsiInstallsAndUninstallsWithMsiexec()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var missingTools = WindowsTestUtilities.FindMissingWiXTools();
        if (missingTools.Count > 0)
        {
            return;
        }

        var msiexecPath = Path.Combine(Environment.SystemDirectory, "msiexec.exe");
        if (!File.Exists(msiexecPath))
        {
            return;
        }

        var productCode = Guid.NewGuid();
        var payloadDir = CreateSamplePayload();
        var outputDir = Path.Combine(_tempRoot, "msi-output");
        var installDir = Path.Combine(_tempRoot, "install");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            "regression.windows",
            "RegressionSample",
            "2.0.0",
            new Dictionary<string, string>
            {
                ["windows.msi.productCode"] = productCode.ToString("B"),
                ["windows.msi.upgradeCode"] = Guid.NewGuid().ToString("B")
            },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "msi" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.Windows,
            new[] { "msi" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["windows.msi.sourceDirectory"] = payloadDir
            });

        var processRunner = new ProcessRunner();
        var signingService = new StubSigningService();
        var telemetry = new RecordingTelemetry();

        var pipeline = new WindowsPackagingPipeline(
            new InMemoryProjectStore(project),
            new IPackageFormatProvider[]
            {
                new MsiPackageFormatProvider(processRunner, signingService, telemetry, NullLogger<MsiPackageFormatProvider>.Instance)
            },
            AllowAllPolicy.Instance,
            NoopAgentBroker.Instance,
            telemetry,
            new TestIdentityContextAccessor(),
            NullLogger<WindowsPackagingPipeline>.Instance);

        var packaging = await pipeline.ExecuteAsync(request);
        Assert.True(packaging.Success, $"Packaging issues:{Environment.NewLine}{FormatIssues(packaging.Issues)}");

        var artifact = Assert.Single(packaging.Artifacts, a => a.Format == "msi");
        Assert.True(File.Exists(artifact.Path), "MSI artifact was not generated.");

        TryDeleteDirectory(installDir);

        try
        {
            var installResult = await RunProcessAsync(
                msiexecPath,
                $"/i \"{artifact.Path}\" /qn /norestart INSTALLFOLDER=\"{installDir}\" MSIINSTALLPERUSER=1");
            Assert.True(installResult.ExitCode == 0, FormatProcessFailure("msiexec install", installResult));

            var installedPayload = Path.Combine(installDir, "Sample.exe");
            Assert.True(File.Exists(installedPayload), $"Installed payload missing at '{installedPayload}'.");

            var uninstallResult = await RunProcessAsync(
                msiexecPath,
                $"/x {productCode:B} /qn /norestart");
            Assert.True(uninstallResult.ExitCode == 0, FormatProcessFailure("msiexec uninstall", uninstallResult));

            await WaitForDirectoryRemovalAsync(installDir);
            Assert.False(Directory.Exists(installDir), "Install directory was not removed by uninstall.");
        }
        finally
        {
            await RunProcessAsync(
                msiexecPath,
                $"/x {productCode:B} /qn /norestart",
                suppressErrors: true);

            TryDeleteDirectory(installDir);
        }
    }

    [Fact]
    public async Task WindowsPipeline_RespectsBlockingPolicies()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var payloadDir = CreateSamplePayload();
        var outputDir = Path.Combine(_tempRoot, "policy-output");
        Directory.CreateDirectory(outputDir);

        var project = new PackagingProject(
            "regression.windows.policy",
            "PolicySample",
            "1.0.0",
            new Dictionary<string, string>(),
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Windows] = new(new[] { "msi" }, new Dictionary<string, string>())
            });

        var request = new PackagingRequest(
            project.Id,
            PackagingPlatform.Windows,
            new[] { "msi" },
            "Release",
            outputDir,
            new Dictionary<string, string>
            {
                ["windows.msi.sourceDirectory"] = payloadDir
            });

        var telemetry = new RecordingTelemetry();
        var provider = new TrackingPackageFormatProvider();

        var pipeline = new WindowsPackagingPipeline(
            new InMemoryProjectStore(project),
            new IPackageFormatProvider[] { provider },
            new BlockingPolicy(),
            NoopAgentBroker.Instance,
            telemetry,
            new TestIdentityContextAccessor(),
            NullLogger<WindowsPackagingPipeline>.Instance);

        var result = await pipeline.ExecuteAsync(request);

        Assert.False(result.Success);
        Assert.Contains(result.Issues, i => i.Code == "test.policy.blocked");
        Assert.Equal(0, provider.InvocationCount);
    }

    private string CreateSamplePayload()
    {
        var dir = Path.Combine(_tempRoot, "payload", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "Sample.exe"), "echo sample");
        return dir;
    }

    private static string FormatIssues(IReadOnlyCollection<PackagingIssue> issues)
        => string.Join(Environment.NewLine, issues.Select(i => $"{i.Severity}:{i.Code}:{i.Message}"));

    private static async Task WaitForDirectoryRemovalAsync(string directory, int retries = 10, int delayMilliseconds = 300)
    {
        for (var attempt = 0; attempt < retries; attempt++)
        {
            if (!Directory.Exists(directory))
            {
                return;
            }

            await Task.Delay(delayMilliseconds);
        }
    }

    private static async Task<ProcessResult> RunProcessAsync(string fileName, string arguments, bool suppressErrors = false, CancellationToken cancellationToken = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException($"Failed to start process '{fileName}'.");
        var stdOutTask = process.StandardOutput.ReadToEndAsync();
        var stdErrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync(cancellationToken);
        var result = new ProcessResult(process.ExitCode, await stdOutTask, await stdErrTask);

        if (!suppressErrors && result.ExitCode != 0)
        {
            throw new InvalidOperationException(FormatProcessFailure($"Process '{fileName}'", result));
        }

        return result;
    }

    private static string FormatProcessFailure(string operation, ProcessResult result)
    {
        return $"{operation} failed with exit code {result.ExitCode}.{Environment.NewLine}STDOUT:{Environment.NewLine}{result.StandardOutput}{Environment.NewLine}STDERR:{Environment.NewLine}{result.StandardError}";
    }

    private static void TryDeleteDirectory(string directory)
    {
        try
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }

    public void Dispose()
    {
        TryDeleteDirectory(_tempRoot);
    }

    private sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError);

    private sealed class TrackingPackageFormatProvider : IPackageFormatProvider
    {
        public int InvocationCount { get; private set; }

        public string Format => "msi";

        public Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(PackageFormatResult.Empty());
        }
    }

    private sealed class BlockingPolicy : IPolicyEvaluator
    {
        public Task<PolicyEvaluationResult> EvaluateAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default)
        {
            var issue = new PackagingIssue(
                "test.policy.blocked",
                "Policy enforcement prevented this packaging run.",
                PackagingIssueSeverity.Error);
            return Task.FromResult(PolicyEvaluationResult.Blocked(issue));
        }
    }
}
