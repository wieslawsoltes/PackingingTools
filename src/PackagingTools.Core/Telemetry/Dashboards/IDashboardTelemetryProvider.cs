namespace PackagingTools.Core.Telemetry.Dashboards;

/// <summary>
/// Supplies dashboard-ready telemetry snapshots for the UX layer.
/// </summary>
public interface IDashboardTelemetryProvider
{
    /// <summary>
    /// Retrieves the most recent snapshot using the supplied query (optional).
    /// </summary>
    Task<DashboardSnapshot> GetSnapshotAsync(DashboardQuery? query = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports dashboard data for reporting or archival purposes.
    /// </summary>
    Task<DashboardExport> ExportAsync(DashboardQuery? query = null, CancellationToken cancellationToken = default);
}
