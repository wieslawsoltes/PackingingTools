using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Mac.Audit;

/// <summary>
/// Persists notarization logs, signing receipts, and verification artifacts for compliance exports.
/// </summary>
public sealed class MacAuditService : IMacAuditService
{
    private readonly ILogger<MacAuditService>? _logger;

    public MacAuditService(ILogger<MacAuditService>? logger = null)
    {
        _logger = logger;
    }

    public Task<IReadOnlyCollection<PackagingIssue>> CaptureAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();

        foreach (var artifact in result.Artifacts)
        {
            try
            {
                if (artifact.Metadata.TryGetValue("notarizationLog", out var logPath) && File.Exists(logPath))
                {
                    CopyArtifact(context, artifact, logPath, "notarization");
                }

                if (artifact.Metadata.TryGetValue("stapled", out var stapled) && stapled.Equals("True", StringComparison.OrdinalIgnoreCase))
                {
                    var receiptDir = Path.Combine(Path.GetDirectoryName(artifact.Path) ?? context.Request.OutputDirectory, "_Audit", Path.GetFileNameWithoutExtension(artifact.Path) ?? "artifact");
                    Directory.CreateDirectory(receiptDir);
                    var receiptPath = Path.Combine(receiptDir, "receipt.json");
                    File.WriteAllText(receiptPath, BuildReceiptJson(artifact), Encoding.UTF8);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to capture audit artifact for {Artifact}", artifact.Path);
                issues.Add(new PackagingIssue(
                    "mac.audit.capture_failed",
                    $"Unable to capture audit artifact for '{artifact.Path}': {ex.Message}",
                    PackagingIssueSeverity.Warning));
            }
        }

        return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(issues);
    }

    private static void CopyArtifact(PackageFormatContext context, PackagingArtifact artifact, string sourcePath, string category)
    {
        var auditDir = Path.Combine(context.Request.OutputDirectory, "_Audit", Path.GetFileNameWithoutExtension(artifact.Path) ?? "artifact", category);
        Directory.CreateDirectory(auditDir);
        var destination = Path.Combine(auditDir, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, destination, overwrite: true);
    }

    private static string BuildReceiptJson(PackagingArtifact artifact)
    {
        var builder = new StringBuilder();
        builder.Append("{");
        builder.Append("\"format\":\"");
        builder.Append(artifact.Format);
        builder.Append("\",\"path\":\"");
        builder.Append(artifact.Path.Replace("\\", "\\\\"));
        builder.Append("\",");
        var first = true;
        builder.Append("\"metadata\":{");
        foreach (var kvp in artifact.Metadata)
        {
            if (!first)
            {
                builder.Append(',');
            }
            first = false;
            builder.Append('\"');
            builder.Append(kvp.Key);
            builder.Append("\":\"");
            builder.Append(kvp.Value.Replace("\\", "\\\\"));
            builder.Append("\"");
        }
        builder.Append("}}");
        return builder.ToString();
    }
}
