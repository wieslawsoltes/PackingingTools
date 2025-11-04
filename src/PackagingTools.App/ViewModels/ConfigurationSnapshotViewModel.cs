using CommunityToolkit.Mvvm.ComponentModel;
using System;
using PackagingTools.Core.Audit;

namespace PackagingTools.App.ViewModels;

public partial class ConfigurationSnapshotViewModel : ObservableObject
{
    public Guid Id { get; private set; }
    public DateTimeOffset CapturedAt { get; private set; }
    public string? Author { get; private set; }
    public string? Comment { get; private set; }

    private bool _isRollbackCandidate;

    public bool IsRollbackCandidate
    {
        get => _isRollbackCandidate;
        private set => SetProperty(ref _isRollbackCandidate, value);
    }

    public string DisplayLabel => $"{CapturedAt:yyyy-MM-dd HH:mm:ss} — {Author ?? "system"}";
    public string Description => string.IsNullOrWhiteSpace(Comment) ? "(no comment)" : Comment!;

    public ConfigurationSnapshotViewModel(ConfigurationSnapshot snapshot)
    {
        Update(snapshot);
    }

    public void Update(ConfigurationSnapshot snapshot)
    {
        Id = snapshot.Id;
        CapturedAt = snapshot.CapturedAt;
        Author = snapshot.Author;
        Comment = snapshot.Comment;
        OnPropertyChanged(nameof(DisplayLabel));
    }

    public void SetRollbackCandidate(bool value) => IsRollbackCandidate = value;
}
