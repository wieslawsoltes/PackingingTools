using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PackagingTools.Core.AppServices;
using PackagingTools.Core.Models;
using PackagingTools.Core.Telemetry.Dashboards;
using PackagingTools.Core.Windows.Configuration;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace PackagingTools.App.ViewModels;

public enum MainViewMode
{
    Wizard,
    Workspace,
    Dashboard
}

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ProjectWorkspace _workspace = new();
    private readonly DashboardTelemetryAggregator _dashboardAggregator;
    private readonly ServiceProvider _identityServiceProvider;
    private readonly IIdentityService _identityService;
    private readonly IIdentityContextAccessor _identityContextAccessor;
    private readonly WindowsHostIntegrationService _windowsHostIntegrationService = new();
    private static readonly string[] HostKeys =
    {
        "windows.msi.shortcutName",
        "windows.msi.shortcutTarget",
        "windows.msi.shortcutDescription",
        "windows.msi.shortcutIcon",
        "windows.msi.protocolName",
        "windows.msi.protocolDisplayName",
        "windows.msi.protocolCommand",
        "windows.msi.shellExtensionExtension",
        "windows.msi.shellExtensionProgId",
        "windows.msi.shellExtensionDescription",
        "windows.msi.shellExtensionCommand"
    };

    [ObservableProperty]
    private string? projectPath;

    [ObservableProperty]
    private MainViewMode currentView = MainViewMode.Wizard;

    [ObservableProperty]
    private PlatformConfigurationViewModel? selectedPlatform;

    [ObservableProperty]
    private string statusMessage = "Use the setup wizard or load a project.";

    [ObservableProperty]
    private WindowsHostIntegrationViewModel? windowsHostIntegration;

    [ObservableProperty]
    private string identityProviderName = "azuread";

    [ObservableProperty]
    private string identityScopes = "packaging.run packaging.approve";

    [ObservableProperty]
    private string identityTenant = "common";

    [ObservableProperty]
    private string identityUsername = Environment.UserName;

    [ObservableProperty]
    private string identityRoles = "ReleaseEngineer";

    [ObservableProperty]
    private bool identityRequireMfa;

    [ObservableProperty]
    private string identityStatus = "Not authenticated.";

    [ObservableProperty]
    private string pluginDirectories = string.Empty;

    public SetupWizardViewModel Wizard { get; }
    public DashboardViewModel Dashboard { get; }
    public ConfigurationAuditViewModel Audit { get; }
    public ObservableCollection<PlatformConfigurationViewModel> Platforms { get; } = new();
    public ObservableCollection<PropertyDeltaViewModel> HostIntegrationPreview { get; } = new();
    public ObservableCollection<HostIntegrationIssueViewModel> HostIntegrationValidation { get; } = new();

    public MainWindowViewModel()
    {
        _dashboardAggregator = DashboardTelemetryStore.CreateSharedAggregator();
        DashboardTelemetryStore.TryReload(_dashboardAggregator);
        _identityServiceProvider = new ServiceCollection()
            .AddPackagingIdentity()
            .BuildServiceProvider();
        _identityService = _identityServiceProvider.GetRequiredService<IIdentityService>();
        _identityContextAccessor = _identityServiceProvider.GetRequiredService<IIdentityContextAccessor>();
        Wizard = new SetupWizardViewModel();
        Wizard.Completed += OnWizardCompleted;
        Dashboard = new DashboardViewModel(_dashboardAggregator);
        Audit = new ConfigurationAuditViewModel();
        Audit.RollbackRequested += OnAuditRollbackRequested;
        _ = Dashboard.RefreshAsync();
    }

    public bool IsWizardVisible => CurrentView == MainViewMode.Wizard;
    public bool IsWorkspaceVisible => CurrentView == MainViewMode.Workspace;
    public bool IsDashboardVisible => CurrentView == MainViewMode.Dashboard;

    [RelayCommand]
    private async Task LoadProjectAsync()
    {
        if (string.IsNullOrWhiteSpace(ProjectPath))
        {
            StatusMessage = "Project path is required.";
            return;
        }

        try
        {
            await _workspace.LoadAsync(ProjectPath);
            Audit.Reset();
            PopulateFromWorkspace();
            StatusMessage = $"Loaded project '{_workspace.CurrentProject?.Name}'.";
            CurrentView = MainViewMode.Workspace;
            CaptureAuditSnapshot("Loaded project");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load project: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        if (_workspace.CurrentProject is null)
        {
            StatusMessage = "No project loaded.";
            return;
        }

        try
        {
            UpdateWindowsHostIntegrationValidation();
            if (HostIntegrationValidation.Any(i => i.IsError))
            {
                StatusMessage = "Resolve host integration errors before saving.";
                return;
            }

            SyncViewToWorkspace();
            await _workspace.SaveAsync();
            RefreshHostIntegrationBaseline();
            CaptureAuditSnapshot("Saved project");
            StatusMessage = $"Saved project to '{_workspace.ProjectPath}'.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to save project: {ex.Message}";
        }
    }

    [RelayCommand]
    private void ShowWizard()
    {
        CurrentView = MainViewMode.Wizard;
        StatusMessage = "Setup wizard ready.";
    }

    [RelayCommand]
    private void ShowWorkspace()
    {
        if (_workspace.CurrentProject is null)
        {
            StatusMessage = "Create or load a project before opening the workspace.";
            return;
        }

        CurrentView = MainViewMode.Workspace;
        StatusMessage = "Workspace ready.";
    }

    [RelayCommand]
    private async Task ShowDashboardAsync()
    {
        CurrentView = MainViewMode.Dashboard;
        await Dashboard.RefreshAsync();
        StatusMessage = "Dashboard refreshed.";
    }

    [RelayCommand]
    private async Task SignInIdentityAsync()
    {
        try
        {
            var scopes = IdentityScopes.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (scopes.Length == 0)
            {
                scopes = new[] { "packaging.run" };
            }

            var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["tenantId"] = IdentityTenant,
                ["username"] = IdentityUsername,
                ["roles"] = IdentityRoles
            };

            var request = new IdentityRequest(
                IdentityProviderName,
                scopes,
                IdentityRequireMfa,
                parameters);

            var result = await _identityService.AcquireAsync(request);
            _identityContextAccessor.SetIdentity(result);
            IdentityStatus = $"Signed in as {result.Principal.DisplayName} ({string.Join(", ", result.Principal.Roles)})";
        }
        catch (Exception ex)
        {
            IdentityStatus = $"Identity login failed: {ex.Message}";
        }
    }

    [RelayCommand]
    private void AddWorkspaceProperty()
    {
        if (SelectedPlatform is null)
        {
            StatusMessage = "Select a platform before adding properties.";
            return;
        }

        SelectedPlatform.Properties.Add(new PropertyItemViewModel("property.key", string.Empty));
    }

    [RelayCommand]
    private void RemoveWorkspaceProperty(PropertyItemViewModel? property)
    {
        if (SelectedPlatform is null || property is null)
        {
            return;
        }

        SelectedPlatform.Properties.Remove(property);
    }

    [RelayCommand]
    private void AddWorkspaceFormat()
    {
        if (SelectedPlatform is null)
        {
            StatusMessage = "Select a platform before modifying formats.";
            return;
        }

        var next = $"format-{SelectedPlatform.Formats.Count + 1}";
        if (!SelectedPlatform.Formats.Contains(next))
        {
            SelectedPlatform.Formats.Add(next);
        }
    }

    [RelayCommand]
    private void RemoveWorkspaceFormat(string? format)
    {
        if (SelectedPlatform is null || string.IsNullOrWhiteSpace(format))
        {
            return;
        }

        SelectedPlatform.Formats.Remove(format);
    }

    private void PopulateFromWorkspace()
    {
        Platforms.Clear();
        PluginDirectories = string.Empty;

        var project = _workspace.CurrentProject;
        if (project is null)
        {
            return;
        }

        foreach (var (platform, config) in project.Platforms)
        {
            var vm = new PlatformConfigurationViewModel(platform.ToString(), config);
            Platforms.Add(vm);
        }

        if (project.Metadata.TryGetValue(PluginConfiguration.MetadataKey, out var value))
        {
            var paths = PluginConfiguration.ParsePathList(value);
            PluginDirectories = paths.Count > 0 ? string.Join(Environment.NewLine, paths) : string.Empty;
        }

        SelectedPlatform = Platforms.FirstOrDefault();
    }

    private void OnWizardCompleted(object? sender, SetupWizardViewModel.WizardCompletedEventArgs e)
    {
        _workspace.Initialize(e.Project, e.ProjectPath);
        ProjectPath = e.ProjectPath;
        PopulateFromWorkspace();
        RefreshHostIntegrationBaseline();
        Audit.Reset();
        CaptureAuditSnapshot("Wizard completed");
        CurrentView = MainViewMode.Workspace;
        StatusMessage = $"Created project '{e.Project.Name}'.";
    }

    private void SyncViewToWorkspace()
    {
        if (_workspace.CurrentProject is null)
        {
            return;
        }

        ApplyWindowsHostIntegrationToPlatformViewModel();

        foreach (var vm in Platforms)
        {
            if (Enum.TryParse<PackagingPlatform>(vm.Name, out var platform))
            {
                var configuration = new PlatformConfiguration(vm.Formats.ToList(), vm.Properties.ToDictionary(k => k.Key, v => v.Value));
                _workspace.UpdatePlatformConfiguration(platform, configuration);
            }
        }

        if (_workspace.CurrentProject is null)
        {
            return;
        }

        var metadata = new Dictionary<string, string>(_workspace.CurrentProject.Metadata, StringComparer.OrdinalIgnoreCase);
        var entries = PluginConfiguration.ParsePathList(PluginDirectories);
        var serialized = PluginConfiguration.FormatPathList(entries);
        if (string.IsNullOrWhiteSpace(serialized))
        {
            metadata.Remove(PluginConfiguration.MetadataKey);
        }
        else
        {
            metadata[PluginConfiguration.MetadataKey] = serialized;
        }

        var updatedProject = _workspace.CurrentProject with { Metadata = metadata };
        _workspace.Initialize(updatedProject, _workspace.ProjectPath);
    }

    partial void OnSelectedPlatformChanged(PlatformConfigurationViewModel? value)
    {
        if (_workspace.CurrentProject is null)
        {
            WindowsHostIntegration = null;
            HostIntegrationPreview.Clear();
            return;
        }

        if (value is null || !Enum.TryParse<PackagingPlatform>(value.Name, out var platform) || platform != PackagingPlatform.Windows)
        {
            WindowsHostIntegration = null;
            HostIntegrationPreview.Clear();
            return;
        }

        LoadWindowsHostIntegration();
    }

    private void LoadWindowsHostIntegration()
    {
        if (_workspace.CurrentProject is null)
        {
            return;
        }

        var config = _workspace.CurrentProject.GetPlatformConfiguration(PackagingPlatform.Windows);
        var settings = _windowsHostIntegrationService.Load(config);
        var viewModel = WindowsHostIntegration ?? new WindowsHostIntegrationViewModel();
        viewModel.Changed -= OnWindowsHostIntegrationChanged;
        viewModel.ProjectName = _workspace.CurrentProject.Name;
        viewModel.LoadFromSettings(settings);
        viewModel.Changed += OnWindowsHostIntegrationChanged;
        WindowsHostIntegration = viewModel;
        UpdateWindowsHostIntegrationPreview();
        UpdateWindowsHostIntegrationValidation();
    }

    private void OnWindowsHostIntegrationChanged()
    {
        ApplyWindowsHostIntegrationToPlatformViewModel();
        UpdateWindowsHostIntegrationPreview();
        UpdateWindowsHostIntegrationValidation();
    }

    private void UpdateWindowsHostIntegrationPreview()
    {
        HostIntegrationPreview.Clear();
        if (_workspace.CurrentProject is null || WindowsHostIntegration is null)
        {
            return;
        }

        var existing = _workspace.CurrentProject.GetPlatformConfiguration(PackagingPlatform.Windows);
        var desired = WindowsHostIntegration.ToSettings();
        foreach (var delta in _windowsHostIntegrationService.CalculateDiff(existing, desired))
        {
            HostIntegrationPreview.Add(new PropertyDeltaViewModel(delta));
        }
    }

    private void UpdateWindowsHostIntegrationValidation()
    {
        HostIntegrationValidation.Clear();
        if (_workspace.CurrentProject is null || WindowsHostIntegration is null)
        {
            return;
        }

        var issues = _windowsHostIntegrationService.Validate(WindowsHostIntegration.ToSettings());
        foreach (var issue in issues)
        {
            HostIntegrationValidation.Add(new HostIntegrationIssueViewModel(issue));
        }
    }

    private void ApplyWindowsHostIntegrationToPlatformViewModel()
    {
        if (WindowsHostIntegration is null || SelectedPlatform is null)
        {
            return;
        }

        var snapshot = new PlatformConfiguration(
            SelectedPlatform.Formats.ToList(),
            SelectedPlatform.Properties.ToDictionary(p => p.Key, p => p.Value, StringComparer.OrdinalIgnoreCase));

        var applied = _windowsHostIntegrationService.Apply(snapshot, WindowsHostIntegration.ToSettings());
        foreach (var key in HostKeys)
        {
            applied.Properties.TryGetValue(key, out var value);
            SelectedPlatform.SetOrRemoveProperty(key, value);
        }
    }

    private void RefreshHostIntegrationBaseline()
    {
        if (WindowsHostIntegration is null || _workspace.CurrentProject is null)
        {
            return;
        }

        // Reload from workspace to reset preview against persisted state.
        LoadWindowsHostIntegration();
    }

    private void CaptureAuditSnapshot(string? comment)
    {
        if (_workspace.CurrentProject is null)
        {
            return;
        }

        var author = Environment.UserName;
        Audit.CaptureSnapshot(_workspace.CurrentProject, author, comment, skipIfNoChanges: true);
        TrackConfigurationJob(_workspace.CurrentProject, comment ?? "snapshot");
    }

    private void OnAuditRollbackRequested(object? sender, Guid snapshotId)
    {
        if (!Audit.TryGetSnapshotClone(snapshotId, out var project))
        {
            StatusMessage = "Rollback failed: snapshot not found.";
            return;
        }

        try
        {
            _workspace.Initialize(project, _workspace.ProjectPath);
            PopulateFromWorkspace();
            RefreshHostIntegrationBaseline();

            var targetSnapshot = Audit.Snapshots.FirstOrDefault(s => s.Id == snapshotId);
            if (targetSnapshot is not null)
            {
                Audit.SelectedSnapshot = targetSnapshot;
                var previous = Audit.Snapshots
                    .Where(s => s.CapturedAt < targetSnapshot.CapturedAt)
                    .OrderByDescending(s => s.CapturedAt)
                    .FirstOrDefault();
                Audit.ComparisonSnapshot = previous;
            }

            Audit.RefreshDiffCommand.Execute(null);
            TrackConfigurationJob(project, "rollback");
            StatusMessage = $"Rolled back to snapshot captured {project.Version} ({snapshotId}).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Rollback failed: {ex.Message}";
        }
    }

    private void TrackConfigurationJob(PackagingProject project, string label)
    {
        var platform = project.Platforms.Keys.FirstOrDefault();
        var resolvedPlatform = Enum.IsDefined(typeof(PackagingPlatform), platform) ? platform : PackagingPlatform.Windows;
        var channel = string.IsNullOrWhiteSpace(project.Version) ? "config" : project.Version;
        var summary = new JobRunSummary(
            $"config:{Guid.NewGuid()}",
            $"{project.Name} {label}",
            resolvedPlatform,
            channel,
            DashboardJobStatus.Succeeded,
            TimeSpan.Zero,
            DateTimeOffset.UtcNow,
            0);

        _dashboardAggregator.RecordJob(summary);
        DashboardTelemetryStore.SaveSnapshot(_dashboardAggregator);
    }

    partial void OnCurrentViewChanged(MainViewMode value)
    {
        OnPropertyChanged(nameof(IsWizardVisible));
        OnPropertyChanged(nameof(IsWorkspaceVisible));
        OnPropertyChanged(nameof(IsDashboardVisible));
    }
}
