using System;
using System.Collections.Generic;
using PackagingTools.Core.Audit;
using PackagingTools.Core.Models;

namespace PackagingTools.IntegrationTests;

public class ConfigurationAuditServiceTests
{
    [Fact]
    public void ConfigurationAuditService_ComputesDiffBetweenSnapshots()
    {
        var service = new ConfigurationAuditService();
        var baseline = CreateProject("1.0.0", new Dictionary<string, string> { ["owner"] = "team-a" },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new[] { "deb" }, new Dictionary<string, string> { ["linux.packageRoot"] = "./root" })
            });

        var baselineSnapshot = service.CaptureSnapshot(baseline, "tester", "Initial load");
        Assert.NotEqual(Guid.Empty, baselineSnapshot.Id);

        var updated = CreateProject("1.1.0", new Dictionary<string, string> { ["owner"] = "team-b", ["release"] = "2024-09" },
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [PackagingPlatform.Linux] = new(new[] { "deb", "rpm" }, new Dictionary<string, string>
                {
                    ["linux.packageRoot"] = "./root",
                    ["linux.repo.enabled"] = "true"
                })
            });

        var preview = service.PreviewDiff(updated);
        Assert.NotNull(preview);
        Assert.Equal(2, preview!.MetadataChanges.Count); // owner updated, release added

        var updatedSnapshot = service.CaptureSnapshot(updated, "tester", "Enabled RPM + repo");
        Assert.NotEqual(baselineSnapshot.Id, updatedSnapshot.Id);

        var diff = service.ComputeDiff(baselineSnapshot.Id, updatedSnapshot.Id);
        Assert.Equal(2, diff.MetadataChanges.Count);
        Assert.Single(diff.PlatformDiffs);
        var platformDiff = diff.PlatformDiffs[0];
        Assert.Equal(PackagingPlatform.Linux, platformDiff.Platform);
        Assert.Contains("rpm", platformDiff.AddedFormats, StringComparer.OrdinalIgnoreCase);
        Assert.Empty(platformDiff.RemovedFormats);
        Assert.Single(platformDiff.PropertyChanges);
        var propertyChange = platformDiff.PropertyChanges[0];
        Assert.Equal("linux.repo.enabled", propertyChange.Key);
        Assert.Equal(ConfigurationChangeType.Added, propertyChange.ChangeType);
        Assert.Null(propertyChange.Before);
        Assert.Equal("true", propertyChange.After);
    }

    private static PackagingProject CreateProject(
        string version,
        IReadOnlyDictionary<string, string> metadata,
        IReadOnlyDictionary<PackagingPlatform, PlatformConfiguration> platforms)
        => new("sample.app", "Sample", version, metadata, platforms);
}
