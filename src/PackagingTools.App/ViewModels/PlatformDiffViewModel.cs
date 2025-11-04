using System.Collections.Generic;
using System.Collections.ObjectModel;
using PackagingTools.Core.Audit;
using PackagingTools.Core.Models;

namespace PackagingTools.App.ViewModels;

public sealed class PlatformDiffViewModel
{
    public PackagingPlatform Platform { get; }
    public IReadOnlyList<string> AddedFormats { get; }
    public IReadOnlyList<string> RemovedFormats { get; }
    public ObservableCollection<ConfigurationValueChangeViewModel> PropertyChanges { get; } = new();

    public bool HasFormatChanges => AddedFormats.Count > 0 || RemovedFormats.Count > 0;
    public bool HasPropertyChanges => PropertyChanges.Count > 0;

    public PlatformDiffViewModel(PlatformConfigurationDiff diff)
    {
        Platform = diff.Platform;
        AddedFormats = diff.AddedFormats;
        RemovedFormats = diff.RemovedFormats;

        foreach (var change in diff.PropertyChanges)
        {
            PropertyChanges.Add(new ConfigurationValueChangeViewModel(change));
        }
    }
}
