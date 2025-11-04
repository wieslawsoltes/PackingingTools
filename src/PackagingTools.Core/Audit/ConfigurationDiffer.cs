using System;
using System.Collections.Generic;
using System.Linq;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Audit;

internal static class ConfigurationDiffer
{
    public static ConfigurationDiff CreateDiff(PackagingProject baseline, PackagingProject target)
    {
        var metadataChanges = DiffDictionary(
            baseline.Metadata,
            target.Metadata,
            StringComparer.OrdinalIgnoreCase);

        var platformDiffs = new List<PlatformConfigurationDiff>();
        var platforms = new HashSet<PackagingPlatform>(baseline.Platforms.Keys);
        platforms.UnionWith(target.Platforms.Keys);

        foreach (var platform in platforms)
        {
            var baselineConfig = baseline.GetPlatformConfiguration(platform);
            var targetConfig = target.GetPlatformConfiguration(platform);

            if (baselineConfig is null && targetConfig is null)
            {
                continue;
            }

            var addedFormats = DiffFormats(baselineConfig?.Formats, targetConfig?.Formats, added: true);
            var removedFormats = DiffFormats(baselineConfig?.Formats, targetConfig?.Formats, added: false);
            var propertyChanges = DiffDictionary(
                baselineConfig?.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                targetConfig?.Properties ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);

            if (addedFormats.Count > 0 || removedFormats.Count > 0 || propertyChanges.Count > 0)
            {
                platformDiffs.Add(new PlatformConfigurationDiff(platform, addedFormats, removedFormats, propertyChanges));
            }
        }

        return new ConfigurationDiff(metadataChanges, platformDiffs);
    }

    private static List<string> DiffFormats(
        IReadOnlyCollection<string>? baseline,
        IReadOnlyCollection<string>? target,
        bool added)
    {
        var baselineSet = new HashSet<string>(baseline ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);
        var targetSet = new HashSet<string>(target ?? Array.Empty<string>(), StringComparer.OrdinalIgnoreCase);

        if (added)
        {
            targetSet.ExceptWith(baselineSet);
            return targetSet
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        baselineSet.ExceptWith(targetSet);
        return baselineSet
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<ConfigurationValueChange> DiffDictionary(
        IReadOnlyDictionary<string, string> baseline,
        IReadOnlyDictionary<string, string> target,
        StringComparer comparer)
    {
        var changes = new List<ConfigurationValueChange>();

        foreach (var key in baseline.Keys.Except(target.Keys, comparer))
        {
            changes.Add(new ConfigurationValueChange(key, ConfigurationChangeType.Removed, baseline[key], null));
        }

        foreach (var key in target.Keys.Except(baseline.Keys, comparer))
        {
            changes.Add(new ConfigurationValueChange(key, ConfigurationChangeType.Added, null, target[key]));
        }

        foreach (var key in baseline.Keys.Intersect(target.Keys, comparer))
        {
            var before = baseline[key];
            var after = target[key];
            if (!string.Equals(before, after, StringComparison.Ordinal))
            {
                changes.Add(new ConfigurationValueChange(key, ConfigurationChangeType.Updated, before, after));
            }
        }

        return changes
            .OrderBy(c => c.Key, comparer)
            .ToList();
    }
}
