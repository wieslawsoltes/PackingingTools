using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Telemetry.Dashboards;

/// <summary>
/// Aggregates telemetry signals and exposes dashboard-ready snapshots.
/// </summary>
public sealed class DashboardTelemetryAggregator : IDashboardTelemetryProvider, ITelemetryChannel
{
    private readonly object _sync = new();
    private readonly Dictionary<string, JobRunSummary> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ReleaseChannelSnapshot> _channels = new(StringComparer.OrdinalIgnoreCase);
    private SigningDashboardSummary _signing = new(0, 0, 0, 0);
    private DependencyDashboardSummary _dependency = new(0, 0, 0, 0, null);

    public Task<DashboardSnapshot> GetSnapshotAsync(DashboardQuery? query = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var snapshot = BuildSnapshotCore();
            return Task.FromResult(ApplyQuery(snapshot, query));
        }
    }

    public Task<DashboardExport> ExportAsync(DashboardQuery? query = null, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            var snapshot = ApplyQuery(BuildSnapshotCore(), query);
            return Task.FromResult(new DashboardExport(snapshot.GeneratedAt, snapshot.RecentJobs, snapshot.ReleaseChannels));
        }
    }

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
    {
        if (string.IsNullOrWhiteSpace(eventName) || properties is null)
        {
            return;
        }

        switch (eventName.ToLowerInvariant())
        {
            case "pipeline.completed":
                RecordJob(ParseJobSummary(properties));
                break;
            case "signing.summary":
                UpdateSigning(ParseSigningSummary(properties));
                break;
            case "dependency.summary":
                UpdateDependency(ParseDependencySummary(properties));
                break;
            case "release.channel.updated":
                UpsertReleaseChannel(ParseReleaseChannel(properties));
                break;
        }
    }

    public void TrackDependency(string dependencyName, TimeSpan duration, bool success, IReadOnlyDictionary<string, object?>? properties = null)
    {
        // Use dependency telemetry to update dependency summary when explicit events are absent.
        if (!success)
        {
            lock (_sync)
            {
                _dependency = _dependency with { HighSeverityFindings = _dependency.HighSeverityFindings + 1 };
            }
        }
    }

    public void RecordJob(JobRunSummary summary)
    {
        if (summary is null)
        {
            return;
        }

        lock (_sync)
        {
            _jobs[summary.Id] = summary;
        }
    }

    public void UpdateSigning(SigningDashboardSummary summary)
    {
        lock (_sync)
        {
            _signing = summary;
        }
    }

    public void UpdateDependency(DependencyDashboardSummary summary)
    {
        lock (_sync)
        {
            _dependency = summary;
        }
    }

    public void UpsertReleaseChannel(ReleaseChannelSnapshot snapshot)
    {
        lock (_sync)
        {
            _channels[snapshot.Channel] = snapshot;
        }
    }

    public DashboardSnapshot GetCurrentSnapshot()
    {
        lock (_sync)
        {
            return BuildSnapshotCore();
        }
    }

    public void LoadSnapshot(DashboardSnapshot snapshot)
    {
        if (snapshot is null)
        {
            return;
        }

        lock (_sync)
        {
            _jobs.Clear();
            foreach (var job in snapshot.RecentJobs)
            {
                _jobs[job.Id] = job;
            }

            _channels.Clear();
            foreach (var channel in snapshot.ReleaseChannels)
            {
                _channels[channel.Channel] = channel;
            }

            _signing = snapshot.Signing;
            _dependency = snapshot.Dependency;
        }
    }

    private DashboardSnapshot BuildSnapshotCore()
    {
        var jobs = _jobs.Values
            .OrderByDescending(j => j.CompletedAt)
            .ToList();
        var channels = _channels.Values
            .OrderBy(c => c.Channel, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new DashboardSnapshot(DateTimeOffset.UtcNow, jobs, _signing, _dependency, channels);
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

        var maxJobs = query.MaxJobs <= 0 ? 50 : query.MaxJobs;
        jobs = jobs.OrderByDescending(j => j.CompletedAt).Take(maxJobs);

        return snapshot with { RecentJobs = jobs.ToList() };
    }

    private static JobRunSummary ParseJobSummary(IReadOnlyDictionary<string, object?> properties)
    {
        var id = GetString(properties, "jobId") ?? Guid.NewGuid().ToString();
        var displayName = GetString(properties, "displayName") ?? id;
        var channel = GetString(properties, "channel") ?? "default";
        var platform = ParsePlatform(GetString(properties, "platform"));
        var status = ParseStatus(GetString(properties, "status"));
        var duration = TimeSpan.FromSeconds(GetDouble(properties, "durationSeconds") ?? 0);
        var completedAt = ParseDateTime(GetString(properties, "completedAt")) ?? DateTimeOffset.UtcNow;
        var blocking = GetInt(properties, "blockingIssues") ?? 0;

        return new JobRunSummary(id, displayName, platform, channel, status, duration, completedAt, blocking);
    }

    private static SigningDashboardSummary ParseSigningSummary(IReadOnlyDictionary<string, object?> properties)
        => new(
            ActiveCertificates: GetInt(properties, "activeCertificates") ?? 0,
            CertificatesExpiringSoon: GetInt(properties, "expiringSoon") ?? 0,
            FailedSignaturesLast7Days: GetInt(properties, "failedLast7Days") ?? 0,
            PendingApprovals: GetInt(properties, "pendingApprovals") ?? 0);

    private static DependencyDashboardSummary ParseDependencySummary(IReadOnlyDictionary<string, object?> properties)
        => new(
            TrackedComponents: GetInt(properties, "trackedComponents") ?? 0,
            HighSeverityFindings: GetInt(properties, "highSeverity") ?? 0,
            MediumSeverityFindings: GetInt(properties, "mediumSeverity") ?? 0,
            LowSeverityFindings: GetInt(properties, "lowSeverity") ?? 0,
            LastSbomGeneratedAt: ParseDateTime(GetString(properties, "lastSbomGeneratedAt")));

    private static ReleaseChannelSnapshot ParseReleaseChannel(IReadOnlyDictionary<string, object?> properties)
    {
        var channel = GetString(properties, "channel") ?? "unknown";
        var version = GetString(properties, "latestVersion") ?? "0.0.0";
        var publishedAt = ParseDateTime(GetString(properties, "publishedAt")) ?? DateTimeOffset.UtcNow;
        var deployments = GetInt(properties, "deploymentsLast30Days") ?? 0;
        var isPaused = GetBool(properties, "isPaused");
        return new ReleaseChannelSnapshot(channel, version, publishedAt, deployments, isPaused);
    }

    private static PackagingPlatform ParsePlatform(string? value)
        => Enum.TryParse<PackagingPlatform>(value, true, out var platform) ? platform : PackagingPlatform.Linux;

    private static DashboardJobStatus ParseStatus(string? value)
        => value?.ToUpperInvariant() switch
        {
            "SUCCEEDED" or "SUCCESS" => DashboardJobStatus.Succeeded,
            "FAILED" or "FAILURE" => DashboardJobStatus.Failed,
            "RUNNING" => DashboardJobStatus.Running,
            "QUEUED" => DashboardJobStatus.Queued,
            "CANCELLED" or "CANCELED" => DashboardJobStatus.Cancelled,
            _ => DashboardJobStatus.Unknown
        };

    private static string? GetString(IReadOnlyDictionary<string, object?> properties, string key)
        => properties.TryGetValue(key, out var value) ? value?.ToString() : null;

    private static int? GetInt(IReadOnlyDictionary<string, object?> properties, string key)
        => properties.TryGetValue(key, out var value) && int.TryParse(value?.ToString(), out var result) ? result : null;

    private static double? GetDouble(IReadOnlyDictionary<string, object?> properties, string key)
        => properties.TryGetValue(key, out var value) && double.TryParse(value?.ToString(), out var result) ? result : null;

    private static DateTimeOffset? ParseDateTime(string? value)
        => DateTimeOffset.TryParse(value, out var dto) ? dto : null;

    private static bool GetBool(IReadOnlyDictionary<string, object?> properties, string key)
        => properties.TryGetValue(key, out var value) && bool.TryParse(value?.ToString(), out var result) && result;
}
