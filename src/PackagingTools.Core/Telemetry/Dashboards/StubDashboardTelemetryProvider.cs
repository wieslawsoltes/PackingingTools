using System;
using System.Linq;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Telemetry.Dashboards;

/// <summary>
/// Provides deterministic sample data so UI surfaces can be developed without backing services.
/// </summary>
public sealed class StubDashboardTelemetryProvider : IDashboardTelemetryProvider
{
    private static readonly DashboardSnapshot SampleSnapshot = BuildSampleSnapshot();

    public Task<DashboardSnapshot> GetSnapshotAsync(DashboardQuery? query = null, CancellationToken cancellationToken = default)
        => Task.FromResult(ApplyQuery(SampleSnapshot with { GeneratedAt = DateTimeOffset.UtcNow }, query));

    public Task<DashboardExport> ExportAsync(DashboardQuery? query = null, CancellationToken cancellationToken = default)
    {
        var snapshot = ApplyQuery(SampleSnapshot with { GeneratedAt = DateTimeOffset.UtcNow }, query);
        return Task.FromResult(new DashboardExport(snapshot.GeneratedAt, snapshot.RecentJobs, snapshot.ReleaseChannels));
    }

    private static DashboardSnapshot BuildSampleSnapshot()
    {
        var now = DateTimeOffset.UtcNow;
        var recentJobs = new List<JobRunSummary>
        {
            new("linux:stable:2024-09-18", "Linux Stable", PackagingPlatform.Linux, "stable", DashboardJobStatus.Succeeded, TimeSpan.FromMinutes(12), now.AddMinutes(-15), 0),
            new("macos:beta:2024-09-18", "macOS Beta", PackagingPlatform.MacOS, "beta", DashboardJobStatus.Failed, TimeSpan.FromMinutes(18), now.AddHours(-2), 2),
            new("windows:release:2024-09-17", "Windows Release", PackagingPlatform.Windows, "release", DashboardJobStatus.Running, TimeSpan.FromMinutes(5), now.AddMinutes(-3), 0),
            new("linux:nightly:2024-09-17", "Linux Nightly", PackagingPlatform.Linux, "nightly", DashboardJobStatus.Queued, TimeSpan.Zero, now.AddMinutes(-1), 0),
            new("macos:stable:2024-09-17", "macOS Stable", PackagingPlatform.MacOS, "stable", DashboardJobStatus.Cancelled, TimeSpan.FromMinutes(7), now.AddHours(-6), 1)
        };

        var signing = new SigningDashboardSummary(
            ActiveCertificates: 12,
            CertificatesExpiringSoon: 2,
            FailedSignaturesLast7Days: 1,
            PendingApprovals: 3);

        var dependency = new DependencyDashboardSummary(
            TrackedComponents: 148,
            HighSeverityFindings: 1,
            MediumSeverityFindings: 4,
            LowSeverityFindings: 9,
            LastSbomGeneratedAt: now.AddHours(-5));

        var releaseChannels = new List<ReleaseChannelSnapshot>
        {
            new("stable", "1.8.2", now.AddDays(-2), 42, false),
            new("beta", "1.9.0-beta.3", now.AddHours(-20), 11, false),
            new("nightly", "1.9.0-nightly.20240918", now.AddHours(-1), 28, false),
            new("lts", "1.6.5", now.AddDays(-14), 9, true)
        };

        return new DashboardSnapshot(now, recentJobs, signing, dependency, releaseChannels);
    }

    private static DashboardSnapshot ApplyQuery(DashboardSnapshot snapshot, DashboardQuery? query)
    {
        if (query is null)
        {
            return snapshot;
        }

        var jobs = snapshot.RecentJobs.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(query.Channel))
        {
            jobs = jobs.Where(j => string.Equals(j.Channel, query.Channel, StringComparison.OrdinalIgnoreCase));
        }

        if (query.FailuresOnly)
        {
            jobs = jobs.Where(j => j.Status is DashboardJobStatus.Failed or DashboardJobStatus.Cancelled or DashboardJobStatus.Unknown);
        }

        if (query.MaxJobs > 0)
        {
            jobs = jobs.OrderByDescending(j => j.CompletedAt).Take(query.MaxJobs);
        }

        return snapshot with { RecentJobs = jobs.ToList() };
    }
}
