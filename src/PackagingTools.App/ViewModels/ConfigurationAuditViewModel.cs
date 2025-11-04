using System;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackagingTools.Core.Audit;
using PackagingTools.Core.Models;

namespace PackagingTools.App.ViewModels;

public partial class ConfigurationAuditViewModel : ViewModelBase
{
    private readonly ConfigurationAuditService _auditService;

    public event EventHandler<Guid>? RollbackRequested;

    public ObservableCollection<ConfigurationSnapshotViewModel> Snapshots { get; } = new();
    public ObservableCollection<ConfigurationValueChangeViewModel> MetadataChanges { get; } = new();
    public ObservableCollection<PlatformDiffViewModel> PlatformChanges { get; } = new();

    [ObservableProperty]
    private ConfigurationSnapshotViewModel? selectedSnapshot;

    [ObservableProperty]
    private ConfigurationSnapshotViewModel? comparisonSnapshot;

    [ObservableProperty]
    private string diffSummary = "No differences available.";

    public ConfigurationAuditViewModel(ConfigurationAuditService? auditService = null)
    {
        _auditService = auditService ?? new ConfigurationAuditService();
    }

    internal bool TryGetSnapshotClone(Guid snapshotId, [MaybeNullWhen(false)] out PackagingProject project)
        => _auditService.TryGetSnapshotClone(snapshotId, out project);

    public void Reset()
    {
        _auditService.Clear();
        Snapshots.Clear();
        MetadataChanges.Clear();
        PlatformChanges.Clear();
        SelectedSnapshot = null;
        ComparisonSnapshot = null;
        DiffSummary = "No differences available.";
        RequestRollbackCommand.NotifyCanExecuteChanged();
    }

    public void CaptureSnapshot(PackagingProject project, string? author, string? comment, bool skipIfNoChanges = true)
    {
        var existingLatest = _auditService.GetLatestSnapshot();
        if (skipIfNoChanges && existingLatest is not null)
        {
            var preview = _auditService.PreviewDiff(project);
            if (preview is not null && !HasChanges(preview))
            {
                return;
            }
        }

        var snapshot = _auditService.CaptureSnapshot(project, author, comment);
        UpsertSnapshot(snapshot);
        AutoSelectSnapshots();
        RefreshDiff();
    }

    [RelayCommand]
    private void RefreshDiff()
        => ComputeDiff();

    [RelayCommand(CanExecute = nameof(CanRollback))]
    private void RequestRollback()
    {
        if (ComparisonSnapshot is null)
        {
            return;
        }

        RollbackRequested?.Invoke(this, ComparisonSnapshot.Id);
    }

    partial void OnSelectedSnapshotChanged(ConfigurationSnapshotViewModel? value)
    {
        ComputeDiff();
        RequestRollbackCommand.NotifyCanExecuteChanged();
    }

    partial void OnComparisonSnapshotChanged(ConfigurationSnapshotViewModel? value)
    {
        ComputeDiff();
        RequestRollbackCommand.NotifyCanExecuteChanged();
    }

    private void UpsertSnapshot(ConfigurationSnapshot snapshot)
    {
        var existing = Snapshots.FirstOrDefault(s => s.Id == snapshot.Id);
        if (existing is not null)
        {
            existing.Update(snapshot);
            return;
        }

        var vm = new ConfigurationSnapshotViewModel(snapshot);
        vm.SetRollbackCandidate(false);
        Snapshots.Add(vm);
        var ordered = Snapshots.OrderBy(s => s.CapturedAt).ToList();
        if (!ordered.SequenceEqual(Snapshots))
        {
            Snapshots.Clear();
            foreach (var item in ordered)
            {
                Snapshots.Add(item);
            }
        }
    }

    private void AutoSelectSnapshots()
    {
        if (Snapshots.Count == 0)
        {
            SelectedSnapshot = null;
            ComparisonSnapshot = null;
            return;
        }

        var latest = Snapshots[^1];
        if (SelectedSnapshot?.Id != latest.Id)
        {
            SelectedSnapshot = latest;
        }

        var previous = Snapshots
            .Where(s => s.CapturedAt < SelectedSnapshot!.CapturedAt)
            .OrderByDescending(s => s.CapturedAt)
            .FirstOrDefault();

        ComparisonSnapshot = previous;
    }

    private void ComputeDiff()
    {
        MetadataChanges.Clear();
        PlatformChanges.Clear();

        if (SelectedSnapshot is null || ComparisonSnapshot is null)
        {
            DiffSummary = "Select two snapshots to compare.";
            return;
        }

        var diff = _auditService.ComputeDiff(ComparisonSnapshot.Id, SelectedSnapshot.Id);

        foreach (var change in diff.MetadataChanges)
        {
            MetadataChanges.Add(new ConfigurationValueChangeViewModel(change));
        }

        foreach (var platformDiff in diff.PlatformDiffs)
        {
            PlatformChanges.Add(new PlatformDiffViewModel(platformDiff));
        }

        UpdateRollbackFlags();
        DiffSummary = BuildSummary(diff, ComparisonSnapshot!, SelectedSnapshot!);
    }

    private static bool HasChanges(ConfigurationDiff diff)
        => diff.MetadataChanges.Count > 0 ||
           diff.PlatformDiffs.Any(p => p.AddedFormats.Count > 0 || p.RemovedFormats.Count > 0 || p.PropertyChanges.Count > 0);

    private void UpdateRollbackFlags()
    {
        foreach (var snapshot in Snapshots)
        {
            snapshot.SetRollbackCandidate(false);
        }

        ComparisonSnapshot?.SetRollbackCandidate(true);
    }

    private static string BuildSummary(ConfigurationDiff diff, ConfigurationSnapshotViewModel baseline, ConfigurationSnapshotViewModel target)
    {
        if (diff.MetadataChanges.Count == 0 && diff.PlatformDiffs.Count == 0)
        {
            return "No differences detected.";
        }

        var platformDeltaCount = diff.PlatformDiffs.Sum(p => p.PropertyChanges.Count + p.AddedFormats.Count + p.RemovedFormats.Count);
        return $"Comparing '{baseline.DisplayLabel}' → '{target.DisplayLabel}': {diff.MetadataChanges.Count} metadata change(s), {platformDeltaCount} platform change item(s).";
    }

    private bool CanRollback()
        => ComparisonSnapshot is not null;
}
