using System;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Audit;

/// <summary>
/// Describes the type of configuration change.
/// </summary>
public enum ConfigurationChangeType
{
    Added,
    Removed,
    Updated
}

/// <summary>
/// Represents a key/value change within metadata or platform properties.
/// </summary>
/// <param name="Key">Configuration key.</param>
/// <param name="ChangeType">Nature of the change.</param>
/// <param name="Before">Value before the change.</param>
/// <param name="After">Value after the change.</param>
public sealed record ConfigurationValueChange(
    string Key,
    ConfigurationChangeType ChangeType,
    string? Before,
    string? After);

/// <summary>
/// Captures format/property deltas for a specific platform.
/// </summary>
/// <param name="Platform">Target platform.</param>
/// <param name="AddedFormats">Formats newly added.</param>
/// <param name="RemovedFormats">Formats removed.</param>
/// <param name="PropertyChanges">Property-level changes.</param>
public sealed record PlatformConfigurationDiff(
    PackagingPlatform Platform,
    IReadOnlyList<string> AddedFormats,
    IReadOnlyList<string> RemovedFormats,
    IReadOnlyList<ConfigurationValueChange> PropertyChanges);

/// <summary>
/// Aggregated diff between two project snapshots.
/// </summary>
/// <param name="MetadataChanges">Metadata differences.</param>
/// <param name="PlatformDiffs">Per-platform differences.</param>
public sealed record ConfigurationDiff(
    IReadOnlyList<ConfigurationValueChange> MetadataChanges,
    IReadOnlyList<PlatformConfigurationDiff> PlatformDiffs);

/// <summary>
/// Captured snapshot of a project configuration suitable for audit history.
/// </summary>
/// <param name="Id">Snapshot identifier.</param>
/// <param name="CapturedAt">Capture timestamp (UTC).</param>
/// <param name="Author">Actor initiating the capture (optional).</param>
/// <param name="Comment">Optional comment describing the change.</param>
/// <param name="Project">Immutable project payload.</param>
public sealed record ConfigurationSnapshot(
    Guid Id,
    DateTimeOffset CapturedAt,
    string? Author,
    string? Comment,
    PackagingProject Project);
