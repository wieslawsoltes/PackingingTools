using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PackagingTools.Core.Telemetry.Dashboards;

/// <summary>
/// Provides lightweight persistence for dashboard telemetry so multiple processes can share snapshots.
/// </summary>
public static class DashboardTelemetryStore
{
    private const string DashboardFileName = "dashboard.json";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static DashboardTelemetryAggregator CreateSharedAggregator()
    {
        var aggregator = new DashboardTelemetryAggregator();
        TryReload(aggregator);
        return aggregator;
    }

    public static void TryReload(DashboardTelemetryAggregator aggregator)
    {
        if (aggregator is null)
        {
            return;
        }

        var path = GetStorePath();
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            using var stream = File.OpenRead(path);
            var document = JsonSerializer.Deserialize<DashboardSnapshotDocument>(stream, SerializerOptions);
            if (document is null)
            {
                return;
            }

            var snapshot = document.ToSnapshot();
            aggregator.LoadSnapshot(snapshot);
        }
        catch
        {
            // Ignore corrupt snapshot payloads to avoid blocking telemetry updates.
        }
    }

    public static void SaveSnapshot(DashboardTelemetryAggregator aggregator)
    {
        if (aggregator is null)
        {
            return;
        }

        try
        {
            var path = GetStorePath();
            var directory = Path.GetDirectoryName(path) ?? AppContext.BaseDirectory;
            Directory.CreateDirectory(directory);

            var snapshot = aggregator.GetCurrentSnapshot();
            var document = DashboardSnapshotDocument.FromSnapshot(snapshot);

            var tempFile = Path.Combine(directory, $"{Guid.NewGuid():N}.tmp");
            using (var stream = File.Create(tempFile))
            {
                JsonSerializer.Serialize(stream, document, SerializerOptions);
            }

            File.Copy(tempFile, path, overwrite: true);
            File.Delete(tempFile);
        }
        catch
        {
            // Ignore persistence failures to avoid impacting packaging operations.
        }
    }

    private static string GetStorePath()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        var telemetryRoot = Path.Combine(root, "PackagingTools", "telemetry");
        return Path.Combine(telemetryRoot, DashboardFileName);
    }

    private sealed record DashboardSnapshotDocument(
        List<JobRunSummary>? Jobs,
        SigningDashboardSummary? Signing,
        DependencyDashboardSummary? Dependency,
        List<ReleaseChannelSnapshot>? Channels)
    {
        public DashboardSnapshot ToSnapshot()
        {
            var jobs = Jobs ?? new List<JobRunSummary>();
            var signing = Signing ?? new SigningDashboardSummary(0, 0, 0, 0);
            var dependency = Dependency ?? new DependencyDashboardSummary(0, 0, 0, 0, null);
            var channels = Channels ?? new List<ReleaseChannelSnapshot>();

            return new DashboardSnapshot(DateTimeOffset.UtcNow, jobs, signing, dependency, channels);
        }

        public static DashboardSnapshotDocument FromSnapshot(DashboardSnapshot snapshot)
        {
            var jobs = snapshot.RecentJobs?.ToList() ?? new List<JobRunSummary>();
            var channels = snapshot.ReleaseChannels?.ToList() ?? new List<ReleaseChannelSnapshot>();

            return new DashboardSnapshotDocument(jobs, snapshot.Signing, snapshot.Dependency, channels);
        }
    }
}
