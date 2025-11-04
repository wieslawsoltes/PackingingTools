using System;
using CommunityToolkit.Mvvm.ComponentModel;
using PackagingTools.Core.Windows.Configuration;

namespace PackagingTools.App.ViewModels;

public partial class WindowsHostIntegrationViewModel : ObservableObject
{
    private bool _suspendNotifications;

    [ObservableProperty]
    private string? projectName;

    [ObservableProperty]
    private bool shortcutEnabled;

    [ObservableProperty]
    private string? shortcutName;

    [ObservableProperty]
    private string? shortcutTarget;

    [ObservableProperty]
    private string? shortcutDescription;

    [ObservableProperty]
    private string? shortcutIcon;

    [ObservableProperty]
    private bool protocolEnabled;

    [ObservableProperty]
    private string? protocolName;

    [ObservableProperty]
    private string? protocolDisplayName;

    [ObservableProperty]
    private string? protocolCommand;

    [ObservableProperty]
    private bool fileAssociationEnabled;

    [ObservableProperty]
    private string? fileAssociationExtension;

    [ObservableProperty]
    private string? fileAssociationProgId;

    [ObservableProperty]
    private string? fileAssociationDescription;

    [ObservableProperty]
    private string? fileAssociationCommand;

    public event Action? Changed;

    public void LoadFromSettings(WindowsHostIntegrationSettings settings)
    {
        _suspendNotifications = true;
        ShortcutEnabled = settings.ShortcutEnabled;
        ShortcutName = settings.ShortcutName;
        ShortcutTarget = settings.ShortcutTarget;
        ShortcutDescription = settings.ShortcutDescription;
        ShortcutIcon = settings.ShortcutIcon;
        ProtocolEnabled = settings.ProtocolEnabled;
        ProtocolName = settings.ProtocolName;
        ProtocolDisplayName = settings.ProtocolDisplayName;
        ProtocolCommand = settings.ProtocolCommand;
        FileAssociationEnabled = settings.FileAssociationEnabled;
        FileAssociationExtension = settings.FileAssociationExtension;
        FileAssociationProgId = settings.FileAssociationProgId;
        FileAssociationDescription = settings.FileAssociationDescription;
        FileAssociationCommand = settings.FileAssociationCommand;
        _suspendNotifications = false;
    }

    public WindowsHostIntegrationSettings ToSettings()
        => new(
            ShortcutEnabled,
            ShortcutName,
            ShortcutTarget,
            ShortcutDescription,
            ShortcutIcon,
            ProtocolEnabled,
            ProtocolName,
            ProtocolDisplayName,
            ProtocolCommand,
            FileAssociationEnabled,
            FileAssociationExtension,
            FileAssociationProgId,
            FileAssociationDescription,
            FileAssociationCommand);

    partial void OnShortcutEnabledChanged(bool value)
    {
        if (value)
        {
            EnsureShortcutDefaults();
        }
        NotifyChanged();
    }

    partial void OnShortcutNameChanged(string? value) => NotifyChanged();
    partial void OnShortcutTargetChanged(string? value) => NotifyChanged();
    partial void OnShortcutDescriptionChanged(string? value) => NotifyChanged();
    partial void OnShortcutIconChanged(string? value) => NotifyChanged();

    partial void OnProtocolEnabledChanged(bool value)
    {
        if (value)
        {
            EnsureProtocolDefaults();
        }
        NotifyChanged();
    }

    partial void OnProtocolNameChanged(string? value) => NotifyChanged();
    partial void OnProtocolDisplayNameChanged(string? value) => NotifyChanged();
    partial void OnProtocolCommandChanged(string? value) => NotifyChanged();

    partial void OnFileAssociationEnabledChanged(bool value)
    {
        if (value)
        {
            EnsureFileAssociationDefaults();
        }
        NotifyChanged();
    }

    partial void OnFileAssociationExtensionChanged(string? value)
    {
        if (!_suspendNotifications && !string.IsNullOrWhiteSpace(value) && !value.StartsWith(".", StringComparison.Ordinal))
        {
            SetWithoutNotify(() => FileAssociationExtension = "." + value.Trim());
        }
        NotifyChanged();
    }
    partial void OnFileAssociationProgIdChanged(string? value) => NotifyChanged();
    partial void OnFileAssociationDescriptionChanged(string? value) => NotifyChanged();
    partial void OnFileAssociationCommandChanged(string? value) => NotifyChanged();

    private void EnsureShortcutDefaults()
    {
        if (string.IsNullOrWhiteSpace(ShortcutName) && !string.IsNullOrWhiteSpace(ProjectName))
        {
            SetWithoutNotify(() => ShortcutName = ProjectName);
        }

        if (string.IsNullOrWhiteSpace(ShortcutTarget) && !string.IsNullOrWhiteSpace(ProjectName))
        {
            SetWithoutNotify(() => ShortcutTarget = $"{ProjectName}.exe");
        }
    }

    private void EnsureProtocolDefaults()
    {
        if (string.IsNullOrWhiteSpace(ProtocolName) && !string.IsNullOrWhiteSpace(ProjectName))
        {
            var scheme = ProjectName!.Replace(" ", string.Empty).ToLowerInvariant();
            SetWithoutNotify(() => ProtocolName = scheme);
        }

        if (string.IsNullOrWhiteSpace(ProtocolDisplayName) && !string.IsNullOrWhiteSpace(ProjectName))
        {
            SetWithoutNotify(() => ProtocolDisplayName = $"{ProjectName} Protocol");
        }

        if (string.IsNullOrWhiteSpace(ProtocolCommand) && !string.IsNullOrWhiteSpace(ShortcutTarget))
        {
            SetWithoutNotify(() => ProtocolCommand = $"{ShortcutTarget} \"%1\"");
        }
    }

    private void EnsureFileAssociationDefaults()
    {
        if (string.IsNullOrWhiteSpace(FileAssociationProgId) && !string.IsNullOrWhiteSpace(ProjectName))
        {
            SetWithoutNotify(() => FileAssociationProgId = $"{ProjectName}.File");
        }

        if (string.IsNullOrWhiteSpace(FileAssociationExtension))
        {
            SetWithoutNotify(() => FileAssociationExtension = ".sample");
        }

        if (string.IsNullOrWhiteSpace(FileAssociationDescription) && !string.IsNullOrWhiteSpace(ProjectName))
        {
            SetWithoutNotify(() => FileAssociationDescription = $"{ProjectName} Document");
        }

        if (string.IsNullOrWhiteSpace(FileAssociationCommand) && !string.IsNullOrWhiteSpace(ShortcutTarget))
        {
            SetWithoutNotify(() => FileAssociationCommand = $"{ShortcutTarget} \"%1\"");
        }
    }

    private void NotifyChanged()
    {
        if (_suspendNotifications)
        {
            return;
        }

        Changed?.Invoke();
    }

    private void SetWithoutNotify(Action action)
    {
        _suspendNotifications = true;
        action();
        _suspendNotifications = false;
    }
}
