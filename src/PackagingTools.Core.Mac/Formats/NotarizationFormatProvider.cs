using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Mac.Tooling;

namespace PackagingTools.Core.Mac.Formats;

/// <summary>
/// Handles notarization using Apple's notarytool and stapler utilities.
/// </summary>
public sealed class NotarizationFormatProvider : IPackageFormatProvider
{
    private readonly IMacProcessRunner _processRunner;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<NotarizationFormatProvider>? _logger;

    public NotarizationFormatProvider(
        IMacProcessRunner processRunner,
        ITelemetryChannel telemetry,
        ILogger<NotarizationFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "notarize";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();

        if (!TryGetArtifact(context, out var artifactPath, issues))
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var submitArgs = BuildSubmitArguments(context, artifactPath);
        var submitResult = await ExecuteToolAsync(
            context,
            "notarytool",
            submitArgs,
            cancellationToken).ConfigureAwait(false);

        if (!submitResult.IsSuccess)
        {
            var diagnostics = TryWriteDiagnostics(context, "notarytool-submit", submitArgs, submitResult);
            var message = BuildFailureMessage("notarytool submit failed.", submitResult.StandardError, diagnostics);
            issues.Add(new PackagingIssue("mac.notarization.submit_failed", message, PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        if (!TryParseResponse(submitResult.StandardOutput, out var submissionInfo, out var parseError))
        {
            issues.Add(new PackagingIssue(
                "mac.notarization.submit_unexpected_response",
                $"Unable to parse notarytool response: {parseError}",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var requestId = submissionInfo.RequestId;
        var status = submissionInfo.Status;
        var summary = submissionInfo.Summary;
        var finalResponse = submitResult.StandardOutput;

        if (ShouldPoll(status))
        {
            var pollResult = await PollForCompletionAsync(context, requestId, submissionInfo, cancellationToken).ConfigureAwait(false);
            issues.AddRange(pollResult.Issues);

            if (!pollResult.Success)
            {
                return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
            }

            status = pollResult.Status;
            summary = pollResult.Summary ?? summary;
            finalResponse = pollResult.RawResponse ?? finalResponse;
        }

        var logResult = await FetchNotarizationLogAsync(context, requestId, cancellationToken).ConfigureAwait(false);
        string? logPath = null;
        if (!logResult.Success)
        {
            issues.Add(logResult.Issue);
        }
        else
        {
            logPath = logResult.LogPath;
            issues.Add(logResult.Issue);
        }

        if (!string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
        {
            var remediation = BuildRemediationMessage(summary, finalResponse, logPath);
            issues.Add(new PackagingIssue("mac.notarization.rejected", remediation, PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var stapled = false;
        if (ShouldStaple(context))
        {
            var staplerArgs = new List<string> { "stapler", "staple", artifactPath };
            var staplerResult = await ExecuteToolAsync(context, "xcrun", staplerArgs, cancellationToken).ConfigureAwait(false);
            if (!staplerResult.IsSuccess)
            {
                var diagnostics = TryWriteDiagnostics(context, "stapler", staplerArgs, staplerResult);
                var message = BuildFailureMessage("Stapling failed.", staplerResult.StandardError, diagnostics);
                issues.Add(new PackagingIssue("mac.notarization.staple_failed", message, PackagingIssueSeverity.Error));
                return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
            }

            stapled = true;
        }

        var metadata = new Dictionary<string, string>
        {
            ["notarizationRequestId"] = requestId,
            ["notarizationStatus"] = status,
            ["stapled"] = stapled.ToString()
        };

        if (!string.IsNullOrEmpty(summary))
        {
            metadata["notarizationSummary"] = summary!;
        }

        if (!string.IsNullOrEmpty(logPath))
        {
            metadata["notarizationLog"] = logPath!;
        }

        var artifact = new PackagingArtifact(Format, artifactPath, metadata);
        return new PackageFormatResult(new[] { artifact }, issues);
    }

    private static bool TryGetArtifact(PackageFormatContext context, out string artifactPath, ICollection<PackagingIssue> issues)
    {
        artifactPath = string.Empty;
        if (context.Request.Properties is null ||
            !context.Request.Properties.TryGetValue("mac.notarization.artifact", out var path) ||
            string.IsNullOrWhiteSpace(path))
        {
            issues.Add(new PackagingIssue(
                "mac.notarization.artifact_missing",
                "No artifact was specified for notarization. Provide 'mac.notarization.artifact' property.",
                PackagingIssueSeverity.Error));
            return false;
        }

        artifactPath = path;
        return true;
    }

    private static List<string> BuildSubmitArguments(PackageFormatContext context, string artifactPath)
    {
        var args = new List<string>
        {
            "submit",
            artifactPath,
            "--apple-id",
            context.Project.Metadata.TryGetValue("mac.appleId", out var appleId) ? appleId : "apple-id@example.com",
            "--team-id",
            context.Project.Metadata.TryGetValue("mac.teamId", out var teamId) ? teamId : "TEAMID",
            "--output-format",
            "json"
        };

        if (context.Project.Metadata.TryGetValue("mac.notarytool.profile", out var profile))
        {
            args.Add("--keychain-profile");
            args.Add(profile);
        }

        return args;
    }

    private async Task<(bool Success, string Status, string? Summary, string? RawResponse, IReadOnlyCollection<PackagingIssue> Issues)> PollForCompletionAsync(
        PackageFormatContext context,
        string requestId,
        NotarizationResponse submissionInfo,
        CancellationToken cancellationToken)
    {
        var issues = new List<PackagingIssue>();
        var maxAttempts = ResolveIntOption(context, "mac.notarization.maxPollAttempts", 60);
        var pollInterval = TimeSpan.FromSeconds(ResolveIntOption(context, "mac.notarization.pollIntervalSeconds", 10));

        string status = submissionInfo.Status;
        string? summary = submissionInfo.Summary;
        string? rawResponse = submissionInfo.RawResponse;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (attempt > 0 || string.IsNullOrEmpty(submissionInfo.Status))
            {
                await Task.Delay(pollInterval, cancellationToken).ConfigureAwait(false);
            }

            var statusArgs = new List<string>
            {
                "status",
                requestId,
                "--output-format",
                "json"
            };

            var statusResult = await ExecuteToolAsync(context, "notarytool", statusArgs, cancellationToken).ConfigureAwait(false);
            if (!statusResult.IsSuccess)
            {
                var diagnostics = TryWriteDiagnostics(context, "notarytool-status", statusArgs, statusResult);
                var message = BuildFailureMessage("Failed to query notarization status.", statusResult.StandardError, diagnostics);
                issues.Add(new PackagingIssue("mac.notarization.status_failed", message, PackagingIssueSeverity.Error));
                return (false, status, summary, rawResponse, issues);
            }

            rawResponse = statusResult.StandardOutput;

            if (!TryParseResponse(statusResult.StandardOutput, out var statusInfo, out var parseError))
            {
                issues.Add(new PackagingIssue(
                    "mac.notarization.status_unexpected_response",
                    $"Unable to parse notarization status response: {parseError}",
                    PackagingIssueSeverity.Error));
                return (false, status, summary, rawResponse, issues);
            }

            status = statusInfo.Status;
            summary = statusInfo.Summary ?? summary;

            if (!ShouldPoll(status))
            {
                return (true, status, summary, rawResponse, issues);
            }
        }

        issues.Add(new PackagingIssue(
            "mac.notarization.status_timeout",
            $"Notarization request '{requestId}' did not complete within the allotted polling window.",
            PackagingIssueSeverity.Error));
        return (false, status, summary, rawResponse, issues);
    }

    private async Task<(bool Success, string? LogPath, PackagingIssue Issue)> FetchNotarizationLogAsync(
        PackageFormatContext context,
        string requestId,
        CancellationToken cancellationToken)
    {
        var logArgs = new List<string>
        {
            "log",
            requestId,
            "--output-format",
            "json"
        };

        var logResult = await ExecuteToolAsync(context, "notarytool", logArgs, cancellationToken).ConfigureAwait(false);
        if (!logResult.IsSuccess)
        {
            var diagnostics = TryWriteDiagnostics(context, "notarytool-log", logArgs, logResult);
            var message = BuildFailureMessage("Fetching notarization log failed.", logResult.StandardError, diagnostics);
            var issue = new PackagingIssue("mac.notarization.log_failed", message, PackagingIssueSeverity.Warning);
            return (false, null, issue);
        }

        try
        {
            var logsDirectory = Path.Combine(context.Request.OutputDirectory, "notarization");
            Directory.CreateDirectory(logsDirectory);
            var logFile = Path.Combine(logsDirectory, $"notarytool-log-{requestId}.json");
            await File.WriteAllTextAsync(logFile, logResult.StandardOutput, Encoding.UTF8, cancellationToken).ConfigureAwait(false);
            var issue = new PackagingIssue(
                "mac.notarization.log_saved",
                $"Notarization log saved to '{logFile}'.",
                PackagingIssueSeverity.Info);
            return (true, logFile, issue);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to persist notarization log for request {RequestId}", requestId);
            var issue = new PackagingIssue(
                "mac.notarization.log_persist_failed",
                $"Unable to persist notarization log: {ex.Message}",
                PackagingIssueSeverity.Warning);
            return (false, null, issue);
        }
    }

    private async Task<MacProcessResult> ExecuteToolAsync(
        PackageFormatContext context,
        string fileName,
        IReadOnlyList<string> args,
        CancellationToken cancellationToken)
    {
        var request = new MacProcessRequest(fileName, args, context.WorkingDirectory);
        var start = DateTimeOffset.UtcNow;
        var result = await _processRunner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);

        _telemetry.TrackDependency(
            fileName,
            DateTimeOffset.UtcNow - start,
            result.IsSuccess,
            new Dictionary<string, object?>
            {
                ["arguments"] = string.Join(' ', args),
                ["exitCode"] = result.ExitCode
            });

        return result;
    }

    private static int ResolveIntOption(PackageFormatContext context, string key, int defaultValue)
    {
        if (context.Request.Properties?.TryGetValue(key, out var rawValue) == true &&
            int.TryParse(rawValue, out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return defaultValue;
    }

    private static bool ShouldPoll(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "In Progress", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "In_Progress", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldStaple(PackageFormatContext context)
    {
        if (context.Request.Properties is null)
        {
            return true;
        }

        if (!context.Request.Properties.TryGetValue("mac.notarization.staple", out var value))
        {
            return true;
        }

        return !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFailureMessage(string summary, string details, string? diagnosticsPath)
    {
        var builder = new StringBuilder(summary);
        if (!string.IsNullOrWhiteSpace(details))
        {
            builder.Append(' ');
            builder.Append(details.Trim());
        }

        if (!string.IsNullOrEmpty(diagnosticsPath))
        {
            builder.Append($" See '{diagnosticsPath}' for details.");
        }

        return builder.ToString();
    }

    private static string BuildRemediationMessage(string? summary, string? rawResponse, string? logPath)
    {
        var builder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(summary))
        {
            builder.Append(summary.Trim());
        }
        else
        {
            builder.Append("Notarization was rejected.");
        }

        if (!string.IsNullOrWhiteSpace(rawResponse))
        {
            builder.Append(' ');
            builder.Append("Response: ");
            builder.Append(rawResponse.Trim());
        }

        if (!string.IsNullOrEmpty(logPath))
        {
            builder.Append($" Refer to '{logPath}' for detailed remediation guidance.");
        }

        return builder.ToString();
    }

    private static string? TryWriteDiagnostics(
        PackageFormatContext context,
        string toolName,
        IReadOnlyList<string> args,
        MacProcessResult result)
    {
        try
        {
            Directory.CreateDirectory(context.Request.OutputDirectory);
            var diagnosticsDir = Path.Combine(context.Request.OutputDirectory, "_diagnostics");
            Directory.CreateDirectory(diagnosticsDir);

            var logFile = Path.Combine(diagnosticsDir, $"{toolName}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}.log");
            var builder = new StringBuilder();
            builder.AppendLine($"# Tool: {toolName}");
            builder.AppendLine($"# Arguments: {string.Join(' ', args)}");
            builder.AppendLine($"# ExitCode: {result.ExitCode}");
            builder.AppendLine("## Standard Output");
            builder.AppendLine(result.StandardOutput);
            builder.AppendLine();
            builder.AppendLine("## Standard Error");
            builder.AppendLine(result.StandardError);

            File.WriteAllText(logFile, builder.ToString(), Encoding.UTF8);
            return logFile;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryParseResponse(string json, out NotarizationResponse response, out string? error)
    {
        response = default!;
        error = null;

        if (string.IsNullOrWhiteSpace(json))
        {
            error = "Response was empty.";
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() ?? string.Empty : string.Empty;
            var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() ?? string.Empty : string.Empty;
            if (string.IsNullOrWhiteSpace(status) && root.TryGetProperty("statusSummary", out var statusSummaryElement))
            {
                status = statusSummaryElement.GetString() ?? string.Empty;
            }

            var summary = root.TryGetProperty("statusSummary", out var summaryElement)
                ? summaryElement.GetString()
                : root.TryGetProperty("message", out var messageElement)
                    ? messageElement.GetString()
                    : null;

            response = new NotarizationResponse(id, status, summary, json);
            return true;
        }
        catch (JsonException ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private sealed record NotarizationResponse(string RequestId, string Status, string? Summary, string RawResponse);
}
