namespace PackagingTools.Core.Telemetry.Dashboards;

/// <summary>
/// Represents the execution state of a recent packaging job for dashboard purposes.
/// </summary>
public enum DashboardJobStatus
{
    Unknown = 0,
    Succeeded = 1,
    Failed = 2,
    Running = 3,
    Queued = 4,
    Cancelled = 5
}
