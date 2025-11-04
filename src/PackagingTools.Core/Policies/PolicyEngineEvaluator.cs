using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Policies;

/// <summary>
/// Default policy engine enforcing signing, approval, and retention rules.
/// </summary>
public sealed class PolicyEngineEvaluator : IPolicyEvaluator
{
    public Task<PolicyEvaluationResult> EvaluateAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default)
    {
        var configuration = PolicyConfiguration.FromMetadata(context.Project.Metadata);
        var issues = new List<PackagingIssue>();

        EvaluateSigning(configuration, context, issues);
        EvaluateApprovals(configuration, context, issues);
        EvaluateRetention(configuration, context, issues);
        EvaluateIdentity(configuration, context, issues);

        if (issues.Count == 0)
        {
            return Task.FromResult(PolicyEvaluationResult.Allowed());
        }

        return Task.FromResult(PolicyEvaluationResult.Blocked(issues.ToArray()));
    }

    private static void EvaluateSigning(PolicyConfiguration config, PolicyEvaluationContext context, List<PackagingIssue> issues)
    {
        if (!config.RequireSigning)
        {
            return;
        }

        if (!HasSigningConfiguration(context.Project, context.Request))
        {
            issues.Add(new PackagingIssue(
                "policy.signing.required",
                "Signing is required by policy but no signing material was configured for this run.",
                PackagingIssueSeverity.Error));
            return;
        }

        if (config.RequireTimestamp && !HasTimestampConfiguration(context.Project, context.Request))
        {
            issues.Add(new PackagingIssue(
                "policy.signing.timestamp_missing",
                "Timestamping is required by policy but no timestamp configuration was provided.",
                PackagingIssueSeverity.Error));
        }
    }

    private static void EvaluateApprovals(PolicyConfiguration config, PolicyEvaluationContext context, List<PackagingIssue> issues)
    {
        if (!config.RequireApproval)
        {
            return;
        }

        if (!TryGetSetting(context.Project, context.Request, config.ApprovalProperty, out var token) ||
            string.IsNullOrWhiteSpace(token))
        {
            issues.Add(new PackagingIssue(
                "policy.approval.missing_token",
                $"Packaging requires an approval token ('{config.ApprovalProperty}') but none was supplied.",
                PackagingIssueSeverity.Error));
        }
    }

    private static void EvaluateRetention(PolicyConfiguration config, PolicyEvaluationContext context, List<PackagingIssue> issues)
    {
        if (config.MaxRetentionDays is null)
        {
            return;
        }

        if (!TryGetSetting(context.Project, context.Request, config.RetentionMetadataKey, out var retentionValue))
        {
            return;
        }

        if (int.TryParse(retentionValue, out var requestedRetention) &&
            requestedRetention > config.MaxRetentionDays)
        {
            issues.Add(new PackagingIssue(
                "policy.retention.exceeds_limit",
                $"Retention of {requestedRetention} days exceeds the policy maximum of {config.MaxRetentionDays} days.",
                PackagingIssueSeverity.Error));
        }
    }

    private static void EvaluateIdentity(PolicyConfiguration config, PolicyEvaluationContext context, List<PackagingIssue> issues)
    {
        var identity = context.Identity;

        if (config.RequireIdentity && identity?.Principal is null)
        {
            issues.Add(new PackagingIssue(
                "policy.identity.required",
                "Authenticated identity is required by policy but none was provided.",
                PackagingIssueSeverity.Error));
            return;
        }

        if (config.RequiredRoles.Count == 0 || identity?.Principal is null)
        {
            return;
        }

        var principalRoles = new HashSet<string>(identity.Principal.Roles, StringComparer.OrdinalIgnoreCase);
        var missingRoles = config.RequiredRoles
            .Where(role => !principalRoles.Contains(role))
            .ToArray();

        if (missingRoles.Length > 0)
        {
            issues.Add(new PackagingIssue(
                "policy.identity.missing_roles",
                $"Identity is missing required roles: {string.Join(", ", missingRoles)}.",
                PackagingIssueSeverity.Error));
        }
    }

    private static bool HasSigningConfiguration(PackagingProject project, PackagingRequest request)
    {
        return request.Platform switch
        {
            PackagingPlatform.Windows => HasAnySetting(project, request, new[]
            {
                "windows.signing.certificatePath",
                "windows.signing.certificateThumbprint",
                "windows.signing.azureKeyVaultCertificate"
            }),
            PackagingPlatform.MacOS => HasAnySetting(project, request, new[]
            {
                "mac.signing.identity"
            }),
            PackagingPlatform.Linux => HasAnySetting(project, request, new[]
            {
                "linux.signing.keyId",
                "linux.signing.gpgKeyPath"
            }),
            _ => false
        };
    }

    private static bool HasTimestampConfiguration(PackagingProject project, PackagingRequest request)
    {
        return request.Platform switch
        {
            PackagingPlatform.Windows => HasAnySetting(project, request, new[]
            {
                "windows.signing.timestampUrl"
            }),
            PackagingPlatform.MacOS => HasAnySetting(project, request, new[]
            {
                "mac.notarization.required",
                "mac.notarization.profile",
                "mac.notarytool.profile"
            }),
            PackagingPlatform.Linux => HasAnySetting(project, request, new[]
            {
                "linux.signing.timestampService"
            }),
            _ => false
        };
    }

    private static bool HasAnySetting(PackagingProject project, PackagingRequest request, IEnumerable<string> keys)
        => keys.Any(key => TryGetSetting(project, request, key, out var value) && !string.IsNullOrWhiteSpace(value));

    private static bool TryGetSetting(PackagingProject project, PackagingRequest request, string key, out string? value)
    {
        if (TryGetValue(request.Properties, key, out value))
        {
            return true;
        }

        if (TryGetValue(project.Metadata, key, out value))
        {
            return true;
        }

        var platformConfig = project.GetPlatformConfiguration(request.Platform);
        if (platformConfig is not null && TryGetValue(platformConfig.Properties, key, out value))
        {
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryGetValue(IReadOnlyDictionary<string, string>? source, string key, out string? value)
    {
        value = null;
        if (source is null)
        {
            return false;
        }

        if (source.TryGetValue(key, out var found))
        {
            value = found;
            return true;
        }

        foreach (var pair in source)
        {
            if (string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                value = pair.Value;
                return true;
            }
        }

        return false;
    }
}
