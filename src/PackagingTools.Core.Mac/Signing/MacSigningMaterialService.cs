using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Security;

namespace PackagingTools.Core.Mac.Signing;

/// <summary>
/// Manages macOS signing materials (entitlements and provisioning profiles) using the secure store.
/// </summary>
public sealed class MacSigningMaterialService
{
    public const string EntitlementsMetadataKey = "mac.signing.entitlementsEntryId";
    public const string ProvisioningMetadataKey = "mac.signing.provisioningProfileEntryId";

    private const string KindKey = "kind";
    private const string EntitlementsKind = "mac.entitlements";
    private const string ProvisioningKind = "mac.provisioningProfile";
    private static readonly TimeSpan RotationWindow = TimeSpan.FromDays(21);

    private readonly ISecureStore _secureStore;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<MacSigningMaterialService>? _logger;

    public MacSigningMaterialService(ISecureStore secureStore, ITelemetryChannel telemetry, ILogger<MacSigningMaterialService>? logger = null)
    {
        _secureStore = secureStore;
        _telemetry = telemetry;
        _logger = logger;
    }

    public Task<SecureStoreEntry> StoreEntitlementsAsync(
        string id,
        ReadOnlyMemory<byte> entitlements,
        DateTimeOffset? expiresAt = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
        => StoreAsync(id, entitlements, EntitlementsKind, expiresAt, metadata, cancellationToken);

    public Task<SecureStoreEntry> StoreProvisioningProfileAsync(
        string id,
        ReadOnlyMemory<byte> profile,
        DateTimeOffset? expiresAt = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        CancellationToken cancellationToken = default)
        => StoreAsync(id, profile, ProvisioningKind, expiresAt, metadata, cancellationToken);

    public async Task<MacSigningMaterialResult> PrepareAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        string? entitlementsPath = null;
        string? provisioningProfilePath = null;

        if (context.Project.Metadata.TryGetValue(EntitlementsMetadataKey, out var entitlementsId) && !string.IsNullOrWhiteSpace(entitlementsId))
        {
            entitlementsPath = await MaterializeAsync(
                    entitlementsId,
                    EntitlementsKind,
                    ".plist",
                    context.WorkingDirectory,
                    "mac.entitlements",
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (context.Project.Metadata.TryGetValue("mac.signing.entitlements", out var entitlementsPathSetting))
        {
            entitlementsPath = await TryCopyExistingAsync(
                    entitlementsPathSetting,
                    context.WorkingDirectory,
                    "entitlements.plist",
                    "mac.entitlements.missing",
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        if (context.Project.Metadata.TryGetValue(ProvisioningMetadataKey, out var provisioningId) && !string.IsNullOrWhiteSpace(provisioningId))
        {
            provisioningProfilePath = await MaterializeAsync(
                    provisioningId,
                    ProvisioningKind,
                    ".mobileprovision",
                    context.WorkingDirectory,
                    "mac.provisioning",
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        else if (context.Project.Metadata.TryGetValue("mac.signing.provisioningProfile", out var provisioningPathSetting))
        {
            provisioningProfilePath = await TryCopyExistingAsync(
                    provisioningPathSetting,
                    context.WorkingDirectory,
                    "embedded.provisionprofile",
                    "mac.provisioning.missing",
                    issues,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var success = issues.TrueForAll(i => i.Severity != PackagingIssueSeverity.Error);
        return new MacSigningMaterialResult(success, entitlementsPath, provisioningProfilePath, issues);
    }

    private async Task<string?> MaterializeAsync(
        string entryId,
        string expectedKind,
        string extension,
        string workingDirectory,
        string issuePrefix,
        List<PackagingIssue> issues,
        CancellationToken cancellationToken)
    {
        var secret = await _secureStore.TryGetAsync(entryId, cancellationToken).ConfigureAwait(false);
        if (secret is null)
        {
            issues.Add(new PackagingIssue(
                $"{issuePrefix}.not_found",
                $"Signing material entry '{entryId}' was not found in secure storage.",
                PackagingIssueSeverity.Error));
            return null;
        }

        if (!secret.Entry.Metadata.TryGetValue(KindKey, out var kind) ||
            !string.Equals(kind, expectedKind, StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new PackagingIssue(
                $"{issuePrefix}.wrong_type",
                $"Signing material entry '{entryId}' is of kind '{kind ?? "unknown"}' but '{expectedKind}' was required.",
                PackagingIssueSeverity.Error));
            return null;
        }

        EvaluateExpiration(issuePrefix, secret.Entry, issues);

        var signingDir = Path.Combine(workingDirectory, "signing");
        Directory.CreateDirectory(signingDir);
        var targetPath = Path.Combine(signingDir, $"{entryId}{extension}");
        await File.WriteAllBytesAsync(targetPath, secret.Payload.ToArray(), cancellationToken).ConfigureAwait(false);

        _telemetry.TrackEvent(
            "mac.signing.material.materialized",
            new Dictionary<string, object?>
            {
                ["entryId"] = entryId,
                ["kind"] = expectedKind,
                ["path"] = targetPath
            });

        _logger?.LogDebug("Materialized signing material {EntryId} ({Kind}) to {Path}", entryId, expectedKind, targetPath);
        return targetPath;
    }

    private static async Task<string?> TryCopyExistingAsync(
        string configuredPath,
        string workingDirectory,
        string fileName,
        string issueCode,
        List<PackagingIssue> issues,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(configuredPath))
        {
            issues.Add(new PackagingIssue(
                issueCode,
                $"Configured signing material '{configuredPath}' could not be located.",
                PackagingIssueSeverity.Error));
            return null;
        }

        var signingDir = Path.Combine(workingDirectory, "signing");
        Directory.CreateDirectory(signingDir);
        var destination = Path.Combine(signingDir, fileName);

        await using var source = File.OpenRead(configuredPath);
        await using var destinationStream = File.Create(destination);
        await source.CopyToAsync(destinationStream, cancellationToken).ConfigureAwait(false);

        return destination;
    }

    private static void EvaluateExpiration(string issuePrefix, SecureStoreEntry entry, ICollection<PackagingIssue> issues)
    {
        if (entry.ExpiresAt is null)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var remaining = entry.ExpiresAt.Value - now;
        if (remaining <= TimeSpan.Zero)
        {
            issues.Add(new PackagingIssue(
                $"{issuePrefix}.expired",
                $"Signing material entry '{entry.Id}' expired on {entry.ExpiresAt:O}. Update it to continue shipping builds.",
                PackagingIssueSeverity.Error));
            return;
        }

        if (remaining <= RotationWindow)
        {
            issues.Add(new PackagingIssue(
                $"{issuePrefix}.rotation_due",
                $"Signing material entry '{entry.Id}' expires in {remaining.Days} days ({entry.ExpiresAt:yyyy-MM-dd}). Refresh it to avoid build interruptions.",
                PackagingIssueSeverity.Warning));
        }
    }

    private Task<SecureStoreEntry> StoreAsync(
        string id,
        ReadOnlyMemory<byte> payload,
        string kind,
        DateTimeOffset? expiresAt,
        IReadOnlyDictionary<string, string>? metadata,
        CancellationToken cancellationToken)
    {
        var mergedMetadata = new Dictionary<string, string>
        {
            [KindKey] = kind
        };

        if (metadata is not null)
        {
            foreach (var kvp in metadata)
            {
                mergedMetadata[kvp.Key] = kvp.Value;
            }
        }

        var options = new SecureStoreEntryOptions(expiresAt, mergedMetadata);
        return _secureStore.PutAsync(id, payload, options, cancellationToken);
    }
}

public sealed record MacSigningMaterialResult(
    bool Success,
    string? EntitlementsPath,
    string? ProvisioningProfilePath,
    IReadOnlyCollection<PackagingIssue> Issues);
