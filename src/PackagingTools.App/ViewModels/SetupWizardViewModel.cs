using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackagingTools.Core.AppServices;
using PackagingTools.Core.Models;

namespace PackagingTools.App.ViewModels;

public enum WizardStage
{
    ProjectDetails = 0,
    EnvironmentValidation = 1,
    PlatformConfiguration = 2
}

public partial class SetupWizardViewModel : ObservableObject
{
    private readonly EnvironmentValidationService _environmentValidationService;
    private readonly List<WizardStage> _stages = new() { WizardStage.ProjectDetails, WizardStage.EnvironmentValidation, WizardStage.PlatformConfiguration };
    private readonly Dictionary<PackagingPlatform, PlatformConfigurationViewModel> _platformCache = new();

    [ObservableProperty]
    private string projectName = "New Packaging Project";

    [ObservableProperty]
    private string projectIdentifier = "com.contoso.app";

    [ObservableProperty]
    private string projectVersion = "1.0.0";

    [ObservableProperty]
    private string? projectFilePath;

    [ObservableProperty]
    private bool includeWindows = true;

    [ObservableProperty]
    private bool includeMac;

    [ObservableProperty]
    private bool includeLinux;

    [ObservableProperty]
    private PlatformConfigurationViewModel? selectedPlatformEditor;

    [ObservableProperty]
    private int currentStepIndex;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusMessage = "Provide project details to begin.";

    public ObservableCollection<EnvironmentCheckViewModel> EnvironmentChecks { get; } = new();
    public ObservableCollection<PlatformConfigurationViewModel> PlatformEditors { get; } = new();

    public SetupWizardViewModel()
        : this(new EnvironmentValidationService())
    {
    }

    public SetupWizardViewModel(EnvironmentValidationService environmentValidationService)
    {
        _environmentValidationService = environmentValidationService;
        CurrentStepIndex = 0;
        UpdatePlatformEditors();
    }

    public IReadOnlyList<WizardStage> Stages => _stages;

    public WizardStage CurrentStage => _stages[Math.Clamp(CurrentStepIndex, 0, _stages.Count - 1)];

    public string CurrentTitle => CurrentStage switch
    {
        WizardStage.ProjectDetails => "Project Details",
        WizardStage.EnvironmentValidation => "Environment Validation",
        WizardStage.PlatformConfiguration => "Platform Configuration",
        _ => string.Empty
    };

    public string CurrentDescription => CurrentStage switch
    {
        WizardStage.ProjectDetails => "Set the identifiers and platforms for your packaging workspace.",
        WizardStage.EnvironmentValidation => "Validate tooling availability for the selected platforms.",
        WizardStage.PlatformConfiguration => "Review formats and key properties before generating the project.",
        _ => string.Empty
    };

    public bool IsFirstStep => CurrentStepIndex <= 0;

    public bool IsLastStep => CurrentStepIndex >= _stages.Count - 1;

    public event EventHandler<WizardCompletedEventArgs>? Completed;

    [RelayCommand(CanExecute = nameof(CanMovePrevious))]
    private void Previous()
    {
        if (CurrentStepIndex > 0)
        {
            CurrentStepIndex--;
            StatusMessage = string.Empty;
        }
    }

    private bool CanMovePrevious() => CurrentStepIndex > 0 && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanMoveNext))]
    private void Next()
    {
        if (CurrentStepIndex < _stages.Count - 1)
        {
            CurrentStepIndex++;
            StatusMessage = string.Empty;
        }
    }

    private bool CanMoveNext() => !IsLastStep && !IsBusy && ValidateCurrentStep();

    [RelayCommand(CanExecute = nameof(CanFinish))]
    private void Finish()
    {
        if (!CanFinish())
        {
            return;
        }

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["project.displayName"] = ProjectName
        };

        var platforms = new Dictionary<PackagingPlatform, PlatformConfiguration>();
        foreach (var editor in PlatformEditors)
        {
            if (Enum.TryParse<PackagingPlatform>(editor.Name, out var platform))
            {
                platforms[platform] = editor.ToConfiguration();
            }
        }

        var project = new PackagingProject(
            ProjectIdentifier,
            ProjectName,
            NormalizeVersion(ProjectVersion),
            metadata,
            platforms);

        Completed?.Invoke(this, new WizardCompletedEventArgs(project, ProjectFilePath));
    }

    private bool CanFinish() => IsLastStep && !IsBusy && ValidateCurrentStep();

    [RelayCommand]
    private async Task RunEnvironmentValidationAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        StatusMessage = "Validating environment...";
        EnvironmentChecks.Clear();

        try
        {
            var request = new EnvironmentValidationRequest(IncludeWindows, IncludeMac, IncludeLinux);
            var results = await _environmentValidationService.ValidateAsync(request);
            foreach (var result in results)
            {
                EnvironmentChecks.Add(new EnvironmentCheckViewModel
                {
                    Id = result.Id,
                    Title = result.Title,
                    Passed = result.Passed,
                    Details = result.Details,
                    Remediation = result.Remediation
                });
            }

            StatusMessage = EnvironmentChecks.All(c => c.Passed)
                ? "Environment checks passed."
                : "Review the items marked as needing attention.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Validation failed: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
            NextCommand.NotifyCanExecuteChanged();
            FinishCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void AddFormat(PlatformConfigurationViewModel? platform)
    {
        platform ??= SelectedPlatformEditor;
        if (platform is null)
        {
            return;
        }

        var hasMsix = platform.Formats.Any(f => string.Equals(f, "msix", StringComparison.OrdinalIgnoreCase));
        var suggested = hasMsix
            ? $"custom-{platform.Formats.Count + 1}"
            : platform.Name switch
            {
                nameof(PackagingPlatform.Windows) => "msix",
                nameof(PackagingPlatform.MacOS) => "app",
                nameof(PackagingPlatform.Linux) => "deb",
                _ => $"format-{platform.Formats.Count + 1}"
            };

        if (!platform.Formats.Any(f => string.Equals(f, suggested, StringComparison.OrdinalIgnoreCase)))
        {
            platform.Formats.Add(suggested);
        }
    }

    [RelayCommand]
    private void RemoveFormat(string? format)
    {
        if (SelectedPlatformEditor is null || string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        SelectedPlatformEditor.Formats.Remove(format);
    }

    [RelayCommand]
    private void AddProperty(PlatformConfigurationViewModel? platform)
    {
        platform ??= SelectedPlatformEditor;
        if (platform is null)
        {
            return;
        }

        platform.Properties.Add(new PropertyItemViewModel("property.key", string.Empty));
    }

    [RelayCommand]
    private void RemoveProperty(PropertyItemViewModel? property)
    {
        if (SelectedPlatformEditor is null || property is null)
        {
            return;
        }

        SelectedPlatformEditor.Properties.Remove(property);
    }

    partial void OnCurrentStepIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentStage));
        OnPropertyChanged(nameof(CurrentTitle));
        OnPropertyChanged(nameof(CurrentDescription));
        OnPropertyChanged(nameof(IsFirstStep));
        OnPropertyChanged(nameof(IsLastStep));

        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();

        if (CurrentStage == WizardStage.EnvironmentValidation && EnvironmentChecks.Count == 0)
        {
            _ = RunEnvironmentValidationAsync();
        }
    }

    partial void OnIncludeWindowsChanged(bool value)
    {
        UpdatePlatformEditors();
        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    partial void OnIncludeMacChanged(bool value)
    {
        UpdatePlatformEditors();
        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    partial void OnIncludeLinuxChanged(bool value)
    {
        UpdatePlatformEditors();
        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsBusyChanged(bool value)
    {
        PreviousCommand.NotifyCanExecuteChanged();
        NextCommand.NotifyCanExecuteChanged();
        FinishCommand.NotifyCanExecuteChanged();
    }

    private bool ValidateCurrentStep()
    {
        switch (CurrentStage)
        {
            case WizardStage.ProjectDetails:
                if (string.IsNullOrWhiteSpace(ProjectName))
                {
                    StatusMessage = "Project name is required.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(ProjectIdentifier))
                {
                    StatusMessage = "Project identifier is required.";
                    return false;
                }

                if (!Version.TryParse(NormalizeVersion(ProjectVersion), out _))
                {
                    StatusMessage = "Provide a valid semantic version (e.g. 1.0.0).";
                    return false;
                }

                if (!IncludeWindows && !IncludeMac && !IncludeLinux)
                {
                    StatusMessage = "Select at least one platform.";
                    return false;
                }

                StatusMessage = string.Empty;
                return true;

            case WizardStage.EnvironmentValidation:
                if (EnvironmentChecks.Count == 0)
                {
                    StatusMessage = "Run environment validation before continuing.";
                    return false;
                }

                StatusMessage = EnvironmentChecks.All(c => c.Passed)
                    ? string.Empty
                    : "Some environment checks require attention.";
                return true;

            case WizardStage.PlatformConfiguration:
                if (PlatformEditors.Any(p => p.Formats.Count == 0))
                {
                    StatusMessage = "Each platform must include at least one format.";
                    return false;
                }

                StatusMessage = string.Empty;
                return true;
        }

        return true;
    }

    private static string NormalizeVersion(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "1.0.0";
        }

        var trimmed = value.Trim();
        return Version.TryParse(trimmed, out _) ? trimmed : "1.0.0";
    }

    private void UpdatePlatformEditors()
    {
        var requested = new List<PackagingPlatform>();
        if (IncludeWindows)
        {
            requested.Add(PackagingPlatform.Windows);
        }
        if (IncludeMac)
        {
            requested.Add(PackagingPlatform.MacOS);
        }
        if (IncludeLinux)
        {
            requested.Add(PackagingPlatform.Linux);
        }

        foreach (var platform in requested)
        {
            if (!_platformCache.ContainsKey(platform))
            {
                _platformCache[platform] = CreateDefaultPlatform(platform);
            }
        }

        var previous = SelectedPlatformEditor;
        PlatformEditors.Clear();
        foreach (var platform in requested)
        {
            PlatformEditors.Add(_platformCache[platform]);
        }

        if (previous is not null && PlatformEditors.Contains(previous))
        {
            SelectedPlatformEditor = previous;
        }
        else
        {
            SelectedPlatformEditor = PlatformEditors.FirstOrDefault();
        }
    }

    private static PlatformConfigurationViewModel CreateDefaultPlatform(PackagingPlatform platform)
    {
        var vm = new PlatformConfigurationViewModel
        {
            Name = platform.ToString()
        };

        switch (platform)
        {
            case PackagingPlatform.Windows:
                vm.Formats = new ObservableCollection<string>(new[] { "msix", "msi" });
                vm.Properties = new ObservableCollection<PropertyItemViewModel>(new[]
                {
                    new PropertyItemViewModel("windows.msi.sourceDirectory", "payload"),
                    new PropertyItemViewModel("windows.msix.payloadDirectory", "payload"),
                    new PropertyItemViewModel("windows.signing.certificatePath", string.Empty),
                    new PropertyItemViewModel("windows.signing.azureKeyVaultCertificate", string.Empty),
                    new PropertyItemViewModel("windows.signing.azureKeyVaultUrl", "https://contoso.vault.azure.net")
                });
                break;
            case PackagingPlatform.MacOS:
                vm.Formats = new ObservableCollection<string>(new[] { "app", "pkg", "dmg" });
                vm.Properties = new ObservableCollection<PropertyItemViewModel>(new[]
                {
                    new PropertyItemViewModel("mac.bundleId", "com.contoso.app"),
                    new PropertyItemViewModel("mac.signing.identity", "Developer ID Application: Contoso")
                });
                break;
            case PackagingPlatform.Linux:
                vm.Formats = new ObservableCollection<string>(new[] { "deb", "rpm", "appimage" });
                vm.Properties = new ObservableCollection<PropertyItemViewModel>(new[]
                {
                    new PropertyItemViewModel("linux.app.id", "com.contoso.app"),
                    new PropertyItemViewModel("linux.repo.channel", "stable")
                });
                break;
        }

        return vm;
    }

    public sealed record WizardCompletedEventArgs(PackagingProject Project, string? ProjectPath);
}
