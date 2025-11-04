using System;
using System.Collections.Generic;

namespace PackagingTools.Core.Policies;

/// <summary>
/// Represents governance policy settings resolved from project metadata.
/// </summary>
public sealed record PolicyConfiguration(
    bool RequireSigning,
    bool RequireTimestamp,
    bool RequireApproval,
    string ApprovalProperty,
    int? MaxRetentionDays,
    string RetentionMetadataKey,
    bool RequireIdentity,
    IReadOnlyCollection<string> RequiredRoles)
{
    public static PolicyConfiguration FromMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        metadata ??= new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        return new PolicyConfiguration(
            RequireSigning: ResolveBool(metadata, "policy.signing.required"),
            RequireTimestamp: ResolveBool(metadata, "policy.signing.timestampRequired"),
            RequireApproval: ResolveBool(metadata, "policy.approval.required"),
            ApprovalProperty: ResolveString(metadata, "policy.approval.tokenProperty", "policy.approvalToken"),
            MaxRetentionDays: ResolveInt(metadata, "policy.retention.maxDays"),
            RetentionMetadataKey: ResolveString(metadata, "policy.retention.metadataKey", "retention.days"),
            RequireIdentity: ResolveBool(metadata, "policy.identity.required"),
            RequiredRoles: ResolveStringList(metadata, "policy.identity.requiredRoles"));
    }

    private static bool ResolveBool(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value) &&
            bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        return false;
    }

    private static string ResolveString(IReadOnlyDictionary<string, string> metadata, string key, string fallback)
        => metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

    private static int? ResolveInt(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value) &&
            int.TryParse(value, out var parsed) &&
            parsed > 0)
        {
            return parsed;
        }

        return null;
    }

    private static IReadOnlyCollection<string> ResolveStringList(IReadOnlyDictionary<string, string> metadata, string key)
    {
        if (metadata.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            var items = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (items.Length > 0)
            {
                return items;
            }
        }

        return Array.Empty<string>();
    }
}
