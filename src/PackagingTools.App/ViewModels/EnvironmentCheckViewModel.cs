using CommunityToolkit.Mvvm.ComponentModel;

namespace PackagingTools.App.ViewModels;

public partial class EnvironmentCheckViewModel : ObservableObject
{
    [ObservableProperty]
    private string id = string.Empty;

    [ObservableProperty]
    private string title = string.Empty;

    [ObservableProperty]
    private bool passed;

    [ObservableProperty]
    private string? details;

    [ObservableProperty]
    private string? remediation;

    public string Status => Passed ? "Ready" : "Needs attention";

    partial void OnPassedChanged(bool value) => OnPropertyChanged(nameof(Status));
}
