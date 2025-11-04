using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Collections.Generic;
using System.Linq;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Audit;

/// <summary>
/// Records immutable snapshots of packaging projects and produces diffs for audit/history features.
/// </summary>
public sealed class ConfigurationAuditService
{
    private readonly ConcurrentDictionary<Guid, ConfigurationSnapshot> _snapshots = new();
    private readonly List<Guid> _order = new();
    private readonly object _gate = new();

    /// <summary>
    /// Captures the provided project and stores a snapshot for future diffing.
    /// </summary>
    /// <param name="project">Project to capture.</param>
    /// <param name="author">Optional actor identifier.</param>
    /// <param name="comment">Optional change annotation.</param>
    public ConfigurationSnapshot CaptureSnapshot(PackagingProject project, string? author = null, string? comment = null)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var clone = CloneProject(project);
        var snapshot = new ConfigurationSnapshot(Guid.NewGuid(), DateTimeOffset.UtcNow, author, comment, clone);

        if (!_snapshots.TryAdd(snapshot.Id, snapshot))
        {
            throw new InvalidOperationException("Failed to store configuration snapshot.");
        }

        lock (_gate)
        {
            _order.Add(snapshot.Id);
        }

        return snapshot;
    }

    /// <summary>
    /// Returns the snapshots ordered by capture time.
    /// </summary>
    public IReadOnlyList<ConfigurationSnapshot> GetSnapshots()
    {
        lock (_gate)
        {
            return _order
                .Select(id => _snapshots[id])
                .OrderBy(s => s.CapturedAt)
                .ToList();
        }
    }

    /// <summary>
    /// Clears all captured snapshots.
    /// </summary>
    public void Clear()
    {
        lock (_gate)
        {
            _order.Clear();
            _snapshots.Clear();
        }
    }

    /// <summary>
    /// Returns the most recent snapshot or null when history is empty.
    /// </summary>
    public ConfigurationSnapshot? GetLatestSnapshot()
    {
        lock (_gate)
        {
            if (_order.Count == 0)
            {
                return null;
            }

            var lastId = _order[^1];
            return _snapshots[lastId];
        }
    }

    /// <summary>
    /// Computes a diff between two stored snapshots.
    /// </summary>
    public ConfigurationDiff ComputeDiff(Guid baselineSnapshotId, Guid targetSnapshotId)
    {
        if (!_snapshots.TryGetValue(baselineSnapshotId, out var baseline))
        {
            throw new ArgumentException("Baseline snapshot not found.", nameof(baselineSnapshotId));
        }

        if (!_snapshots.TryGetValue(targetSnapshotId, out var target))
        {
            throw new ArgumentException("Target snapshot not found.", nameof(targetSnapshotId));
        }

        return ConfigurationDiffer.CreateDiff(baseline.Project, target.Project);
    }

    /// <summary>
    /// Computes a diff between the most recent stored snapshot and the provided project.
    /// Returns null when no baseline snapshot exists.
    /// </summary>
    public ConfigurationDiff? PreviewDiff(PackagingProject project)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        var latest = GetLatestSnapshot();
        if (latest is null)
        {
            return null;
        }

        var clone = CloneProject(project);
        return ConfigurationDiffer.CreateDiff(latest.Project, clone);
    }

    /// <summary>
    /// Attempts to clone the project captured in a snapshot for restoration purposes.
    /// </summary>
    public bool TryGetSnapshotClone(Guid snapshotId, [MaybeNullWhen(false)] out PackagingProject project)
    {
        if (_snapshots.TryGetValue(snapshotId, out var snapshot))
        {
            project = CloneProject(snapshot.Project);
            return true;
        }

        project = null!;
        return false;
    }

    private static PackagingProject CloneProject(PackagingProject project)
    {
        var metadata = project.Metadata.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        var platforms = project.Platforms.ToDictionary(
            kvp => kvp.Key,
            kvp => new PlatformConfiguration(
                kvp.Value.Formats.ToArray(),
                kvp.Value.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase)),
            EqualityComparer<PackagingPlatform>.Default);

        return new PackagingProject(
            project.Id,
            project.Name,
            project.Version,
            metadata,
            platforms);
    }
}
