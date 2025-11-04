using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackagingTools.Core.Telemetry.Dashboards;

namespace PackagingTools.App.ViewModels;

public partial class DashboardViewModel : ViewModelBase
{
    private const string AllChannelsLabel = "All channels";
    private readonly IDashboardTelemetryProvider _telemetryProvider;

    public ObservableCollection<JobRunSummary> RecentJobs { get; } = new();
    public ObservableCollection<ReleaseChannelSnapshot> ReleaseChannels { get; } = new();
    public ObservableCollection<string> AvailableChannels { get; } = new();

    [ObservableProperty]
    private SigningDashboardSummary signing = new(0, 0, 0, 0);

    [ObservableProperty]
    private DependencyDashboardSummary dependency = new(0, 0, 0, 0, null);

    [ObservableProperty]
    private DateTimeOffset generatedAt = DateTimeOffset.UtcNow;

    [ObservableProperty]
    private bool isRefreshing;

    [ObservableProperty]
    private string statusMessage = "Dashboard not yet loaded.";

    [ObservableProperty]
    private string selectedChannelFilter = AllChannelsLabel;

    [ObservableProperty]
    private bool showFailuresOnly;

    [ObservableProperty]
    private int maxJobs = 50;

    [ObservableProperty]
    private string exportSummary = "No export generated.";

    public DashboardViewModel()
        : this(null)
    {
    }

    public DashboardViewModel(IDashboardTelemetryProvider? telemetryProvider)
    {
        _telemetryProvider = telemetryProvider ?? new StubDashboardTelemetryProvider();
        AvailableChannels.Add(AllChannelsLabel);
    }

    [RelayCommand]
    private async Task RefreshDashboardAsync()
    {
        if (IsRefreshing)
        {
            return;
        }

        try
        {
            IsRefreshing = true;
            StatusMessage = "Refreshing dashboard…";
            if (_telemetryProvider is DashboardTelemetryAggregator aggregator)
            {
                DashboardTelemetryStore.TryReload(aggregator);
            }
            var snapshot = await _telemetryProvider.GetSnapshotAsync(CreateQuery());
            ApplySnapshot(snapshot);
            StatusMessage = $"Updated {snapshot.GeneratedAt.LocalDateTime:t}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to refresh dashboard: {ex.Message}";
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public Task RefreshAsync()
        => RefreshDashboardCommand.ExecuteAsync(null);

    [RelayCommand]
    private async Task ExportDashboardAsync()
    {
        try
        {
            var export = await _telemetryProvider.ExportAsync(CreateQuery());
            ExportSummary = $"Exported {export.Jobs.Count} job(s) and {export.ReleaseChannels.Count} channel(s) at {export.GeneratedAt:O}.";
        }
        catch (Exception ex)
        {
            ExportSummary = $"Export failed: {ex.Message}";
        }
    }

    private void ApplySnapshot(DashboardSnapshot snapshot)
    {
        GeneratedAt = snapshot.GeneratedAt;
        Signing = snapshot.Signing;
        Dependency = snapshot.Dependency;

        RecentJobs.Clear();
        foreach (var job in snapshot.RecentJobs.OrderByDescending(j => j.CompletedAt))
        {
            RecentJobs.Add(job);
        }

        ReleaseChannels.Clear();
        foreach (var channel in snapshot.ReleaseChannels.OrderBy(c => c.Channel, StringComparer.OrdinalIgnoreCase))
        {
            ReleaseChannels.Add(channel);
        }

        AvailableChannels.Clear();
        AvailableChannels.Add(AllChannelsLabel);
        foreach (var channel in ReleaseChannels.Select(c => c.Channel).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AvailableChannels.Add(channel);
        }

        if (!AvailableChannels.Contains(SelectedChannelFilter))
        {
            SelectedChannelFilter = AllChannelsLabel;
        }
    }

    private DashboardQuery CreateQuery()
    {
        var channel = SelectedChannelFilter == AllChannelsLabel ? null : SelectedChannelFilter;
        var safeMax = MaxJobs <= 0 ? 50 : MaxJobs;
        return new DashboardQuery(channel, ShowFailuresOnly, safeMax);
    }

    partial void OnSelectedChannelFilterChanged(string value)
        => _ = RefreshAsync();

    partial void OnShowFailuresOnlyChanged(bool value)
        => _ = RefreshAsync();

    partial void OnMaxJobsChanged(int value)
    {
        if (value <= 0)
        {
            MaxJobs = 50;
        }
        _ = RefreshAsync();
    }
}
