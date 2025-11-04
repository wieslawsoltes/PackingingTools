using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Mac.Tooling;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Mac.Verification;

/// <summary>
/// Executes macOS artifact verification checks (spctl, pkgutil, hdiutil).
/// </summary>
public sealed class MacVerificationService : IMacVerificationService
{
    private readonly IMacProcessRunner _processRunner;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<MacVerificationService>? _logger;

    public MacVerificationService(IMacProcessRunner processRunner, ITelemetryChannel telemetry, ILogger<MacVerificationService>? logger = null)
    {
        _processRunner = processRunner;
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<MacVerificationResult> VerifyAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        var success = true;

        switch (artifact.Format.ToLowerInvariant())
        {
            case "app":
                success &= await RunSpctlAsync(context, artifact.Path, "execute", issues, cancellationToken).ConfigureAwait(false);
                break;

            case "pkg":
                success &= await RunSpctlAsync(context, artifact.Path, "install", issues, cancellationToken).ConfigureAwait(false);
                success &= await RunPkgutilAsync(context, artifact.Path, issues, cancellationToken).ConfigureAwait(false);
                break;

            case "dmg":
                success &= await RunHdiutilVerifyAsync(context, artifact.Path, issues, cancellationToken).ConfigureAwait(false);
                break;

            default:
                _logger?.LogTrace("No verification routine registered for format '{Format}'", artifact.Format);
                break;
        }

        return new MacVerificationResult(success, issues);
    }

    private async Task<bool> RunSpctlAsync(PackageFormatContext context, string targetPath, string assessmentType, ICollection<PackagingIssue> issues, CancellationToken cancellationToken)
    {
        var args = new List<string>
        {
            "--assess",
            "--type",
            assessmentType,
            "--ignore-cache",
            "--no-cache",
            targetPath
        };

        var result = await ExecuteAsync(context, "spctl", args, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return true;
        }

        issues.Add(new PackagingIssue(
            $"mac.verify.spctl_failed.{assessmentType}",
            BuildFailureMessage("Gatekeeper assessment failed.", args, result, context),
            PackagingIssueSeverity.Error));
        return false;
    }

    private async Task<bool> RunPkgutilAsync(PackageFormatContext context, string targetPath, ICollection<PackagingIssue> issues, CancellationToken cancellationToken)
    {
        var args = new List<string>
        {
            "--check-signature",
            targetPath
        };

        var result = await ExecuteAsync(context, "pkgutil", args, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return true;
        }

        issues.Add(new PackagingIssue(
            "mac.verify.pkg_signature_failed",
            BuildFailureMessage("pkgutil signature check failed.", args, result, context),
            PackagingIssueSeverity.Error));
        return false;
    }

    private async Task<bool> RunHdiutilVerifyAsync(PackageFormatContext context, string targetPath, ICollection<PackagingIssue> issues, CancellationToken cancellationToken)
    {
        var args = new List<string>
        {
            "verify",
            targetPath
        };

        var result = await ExecuteAsync(context, "hdiutil", args, cancellationToken).ConfigureAwait(false);
        if (result.IsSuccess)
        {
            return true;
        }

        issues.Add(new PackagingIssue(
            "mac.verify.hdiutil_failed",
            BuildFailureMessage("Disk image verification failed.", args, result, context),
            PackagingIssueSeverity.Error));
        return false;
    }

    private async Task<MacProcessResult> ExecuteAsync(
        PackageFormatContext context,
        string tool,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var request = new MacProcessRequest(tool, args, context.WorkingDirectory);
        var start = DateTimeOffset.UtcNow;
        var result = await _processRunner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        _telemetry.TrackDependency(
            $"mac.verify.{tool}",
            DateTimeOffset.UtcNow - start,
            result.IsSuccess,
            new Dictionary<string, object?>
            {
                ["artifact"] = context.Project.Name,
                ["arguments"] = string.Join(' ', args),
                ["exitCode"] = result.ExitCode
            });

        return result;
    }

    private string BuildFailureMessage(string summary, IReadOnlyList<string> args, MacProcessResult result, PackageFormatContext context)
    {
        var diagnostics = TryWriteDiagnostics(context, summary.Replace(' ', '-').ToLowerInvariant(), args, result);
        var builder = new StringBuilder(summary);
        if (!string.IsNullOrWhiteSpace(result.StandardError))
        {
            builder.Append(' ');
            builder.Append(result.StandardError.Trim());
        }

        if (!string.IsNullOrWhiteSpace(diagnostics))
        {
            builder.Append($" See '{diagnostics}' for more details.");
        }

        return builder.ToString();
    }

    private static string? TryWriteDiagnostics(
        PackageFormatContext context,
        string prefix,
        IReadOnlyList<string> args,
        MacProcessResult result)
    {
        try
        {
            Directory.CreateDirectory(context.Request.OutputDirectory);
            var diagnosticsDir = Path.Combine(context.Request.OutputDirectory, "_diagnostics");
            Directory.CreateDirectory(diagnosticsDir);
            var logPath = Path.Combine(diagnosticsDir, $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.log");
            var builder = new StringBuilder();
            builder.AppendLine($"# Tool: {prefix}");
            builder.AppendLine($"# Arguments: {string.Join(' ', args)}");
            builder.AppendLine($"# ExitCode: {result.ExitCode}");
            builder.AppendLine("## Standard Output");
            builder.AppendLine(result.StandardOutput);
            builder.AppendLine();
            builder.AppendLine("## Standard Error");
            builder.AppendLine(result.StandardError);
            File.WriteAllText(logPath, builder.ToString(), Encoding.UTF8);
            return logPath;
        }
        catch
        {
            return null;
        }
    }
}
