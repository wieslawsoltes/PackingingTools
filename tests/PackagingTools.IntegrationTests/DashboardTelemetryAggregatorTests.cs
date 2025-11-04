using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Telemetry.Dashboards;

namespace PackagingTools.IntegrationTests;

public class DashboardTelemetryAggregatorTests
{
    [Fact]
    public async Task Aggregator_FiltersJobs_ByChannelAndFailure()
    {
        var aggregator = new DashboardTelemetryAggregator();

        aggregator.RecordJob(new JobRunSummary("job-success", "Linux Stable", PackagingPlatform.Linux, "stable", DashboardJobStatus.Succeeded, TimeSpan.FromMinutes(12), DateTimeOffset.UtcNow.AddMinutes(-5), 0));
        aggregator.RecordJob(new JobRunSummary("job-failure", "Windows Stable", PackagingPlatform.Windows, "stable", DashboardJobStatus.Failed, TimeSpan.FromMinutes(18), DateTimeOffset.UtcNow.AddMinutes(-1), 2));
        aggregator.RecordJob(new JobRunSummary("job-beta", "Windows Beta", PackagingPlatform.Windows, "beta", DashboardJobStatus.Succeeded, TimeSpan.FromMinutes(10), DateTimeOffset.UtcNow.AddMinutes(-2), 0));

        var query = new DashboardQuery("stable", true, 10);
        var snapshot = await aggregator.GetSnapshotAsync(query);

        Assert.Single(snapshot.RecentJobs);
        Assert.Equal("job-failure", snapshot.RecentJobs[0].Id);
    }

    [Fact]
    public async Task Aggregator_Export_ReturnsJobAndChannelSets()
    {
        var aggregator = new DashboardTelemetryAggregator();
        var now = DateTimeOffset.UtcNow;
        aggregator.RecordJob(new JobRunSummary("job-1", "Linux Stable", PackagingPlatform.Linux, "stable", DashboardJobStatus.Succeeded, TimeSpan.FromMinutes(12), now, 0));
        aggregator.UpsertReleaseChannel(new ReleaseChannelSnapshot("stable", "1.0.0", now, 5, false));

        var export = await aggregator.ExportAsync();

        Assert.Single(export.Jobs);
        Assert.Single(export.ReleaseChannels);
    }

    private sealed class AggregatingTelemetryChannel : ITelemetryChannel
    {
        private readonly DashboardTelemetryAggregator _aggregator;

        public AggregatingTelemetryChannel(DashboardTelemetryAggregator aggregator)
        {
            _aggregator = aggregator;
        }

        public void TrackDependency(string dependencyName, TimeSpan duration, bool success, IReadOnlyDictionary<string, object?>? properties = null)
            => _aggregator.TrackDependency(dependencyName, duration, success, properties);

        public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
            => _aggregator.TrackEvent(eventName, properties);
    }
}
