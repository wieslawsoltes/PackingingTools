using PackagingTools.Core.Models;

namespace PackagingTools.Core.Telemetry.Dashboards;

/// <summary>
/// Immutable snapshot rendered by the UI dashboard.
/// </summary>
/// <param name="GeneratedAt">UTC timestamp when the snapshot was produced.</param>
/// <param name="RecentJobs">Latest job runs shown in the activity table.</param>
/// <param name="Signing">Signing estate summary.</param>
/// <param name="Dependency">Dependency and vulnerability summary.</param>
/// <param name="ReleaseChannels">Status for key release channels.</param>
public sealed record DashboardSnapshot(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<JobRunSummary> RecentJobs,
    SigningDashboardSummary Signing,
    DependencyDashboardSummary Dependency,
    IReadOnlyList<ReleaseChannelSnapshot> ReleaseChannels);

/// <summary>
/// Summary of a packaging job surfaced on the dashboard.
/// </summary>
/// <param name="Id">Unique run identifier.</param>
/// <param name="DisplayName">Friendly label (e.g., project/channel).</param>
/// <param name="Platform">Target packaging platform.</param>
/// <param name="Channel">Release channel or environment.</param>
/// <param name="Status">Execution status for the run.</param>
/// <param name="Duration">Wall-clock duration.</param>
/// <param name="CompletedAt">Completion timestamp (UTC).</param>
/// <param name="BlockingIssueCount">Number of blocking issues encountered.</param>
public sealed record JobRunSummary(
    string Id,
    string DisplayName,
    PackagingPlatform Platform,
    string Channel,
    DashboardJobStatus Status,
    TimeSpan Duration,
    DateTimeOffset CompletedAt,
    int BlockingIssueCount);

/// <summary>
/// High-level signing insights.
/// </summary>
/// <param name="ActiveCertificates">Total active certificates.</param>
/// <param name="CertificatesExpiringSoon">Certificates expiring within 30 days.</param>
/// <param name="FailedSignaturesLast7Days">Count of failed signing attempts in the last week.</param>
/// <param name="PendingApprovals">Outstanding approval/workflow items.</param>
public sealed record SigningDashboardSummary(
    int ActiveCertificates,
    int CertificatesExpiringSoon,
    int FailedSignaturesLast7Days,
    int PendingApprovals);

/// <summary>
/// Dependency and vulnerability posture summary.
/// </summary>
/// <param name="TrackedComponents">Number of components monitored.</param>
/// <param name="HighSeverityFindings">High severity findings outstanding.</param>
/// <param name="MediumSeverityFindings">Medium severity findings outstanding.</param>
/// <param name="LowSeverityFindings">Low severity findings outstanding.</param>
/// <param name="LastSbomGeneratedAt">Timestamp of most recent SBOM generation.</param>
public sealed record DependencyDashboardSummary(
    int TrackedComponents,
    int HighSeverityFindings,
    int MediumSeverityFindings,
    int LowSeverityFindings,
    DateTimeOffset? LastSbomGeneratedAt);

/// <summary>
/// Current state of a release channel.
/// </summary>
/// <param name="Channel">Channel identifier (e.g., stable, beta).</param>
/// <param name="LatestVersion">Latest version published.</param>
/// <param name="PublishedAt">When the latest version went live.</param>
/// <param name="DeploymentsLast30Days">Number of deployments executed in the last 30 days.</param>
/// <param name="IsPaused">Whether the channel is currently paused.</param>
public sealed record ReleaseChannelSnapshot(
    string Channel,
    string LatestVersion,
    DateTimeOffset PublishedAt,
    int DeploymentsLast30Days,
    bool IsPaused);

/// <summary>
/// Optional query parameters applied when retrieving dashboard data.
/// </summary>
/// <param name="Channel">Filter jobs by release channel.</param>
/// <param name="FailuresOnly">Restrict jobs to failure/blocked outcomes.</param>
/// <param name="MaxJobs">Maximum number of job entries to return.</param>
public sealed record DashboardQuery(
    string? Channel,
    bool FailuresOnly,
    int MaxJobs = 50);

/// <summary>
/// Represents an exported snapshot payload for downstream reporting.
/// </summary>
/// <param name="GeneratedAt">UTC timestamp for the export.</param>
/// <param name="Jobs">Job runs included in the export.</param>
/// <param name="ReleaseChannels">Release channel status included in the export.</param>
public sealed record DashboardExport(
    DateTimeOffset GeneratedAt,
    IReadOnlyList<JobRunSummary> Jobs,
    IReadOnlyList<ReleaseChannelSnapshot> ReleaseChannels);
