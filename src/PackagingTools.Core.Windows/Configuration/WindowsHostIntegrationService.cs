using System;
using System.Collections.Generic;
using System.Linq;
using PackagingTools.Core.Configuration;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Windows.Configuration;

/// <summary>
/// Provides helpers for reading, diffing, and applying Windows host integration metadata.
/// </summary>
public sealed class WindowsHostIntegrationService
{
    private static readonly string[] ShortcutKeys =
    {
        "windows.msi.shortcutName",
        "windows.msi.shortcutTarget",
        "windows.msi.shortcutDescription",
        "windows.msi.shortcutIcon"
    };

    private static readonly string[] ProtocolKeys =
    {
        "windows.msi.protocolName",
        "windows.msi.protocolDisplayName",
        "windows.msi.protocolCommand"
    };

    private static readonly string[] FileAssociationKeys =
    {
        "windows.msi.shellExtensionExtension",
        "windows.msi.shellExtensionProgId",
        "windows.msi.shellExtensionDescription",
        "windows.msi.shellExtensionCommand"
    };

    /// <summary>
    /// Materializes settings from an existing platform configuration.
    /// </summary>
    public WindowsHostIntegrationSettings Load(PlatformConfiguration? configuration)
    {
        var properties = configuration?.Properties ?? new Dictionary<string, string>();

        return new WindowsHostIntegrationSettings(
            ShortcutEnabled: properties.TryGetValue("windows.msi.shortcutName", out _) ||
                             properties.TryGetValue("windows.msi.shortcutTarget", out _),
            ShortcutName: TryGet(properties, "windows.msi.shortcutName"),
            ShortcutTarget: TryGet(properties, "windows.msi.shortcutTarget"),
            ShortcutDescription: TryGet(properties, "windows.msi.shortcutDescription"),
            ShortcutIcon: TryGet(properties, "windows.msi.shortcutIcon"),
            ProtocolEnabled: properties.TryGetValue("windows.msi.protocolName", out _),
            ProtocolName: TryGet(properties, "windows.msi.protocolName"),
            ProtocolDisplayName: TryGet(properties, "windows.msi.protocolDisplayName"),
            ProtocolCommand: TryGet(properties, "windows.msi.protocolCommand"),
            FileAssociationEnabled: properties.TryGetValue("windows.msi.shellExtensionProgId", out _),
            FileAssociationExtension: TryGet(properties, "windows.msi.shellExtensionExtension"),
            FileAssociationProgId: TryGet(properties, "windows.msi.shellExtensionProgId"),
            FileAssociationDescription: TryGet(properties, "windows.msi.shellExtensionDescription"),
            FileAssociationCommand: TryGet(properties, "windows.msi.shellExtensionCommand"));
    }

    /// <summary>
    /// Calculates the property-level differences between the current configuration and the requested settings.
    /// </summary>
    public IReadOnlyList<PropertyDelta> CalculateDiff(PlatformConfiguration? existing, WindowsHostIntegrationSettings desired)
    {
        var properties = existing?.Properties ?? new Dictionary<string, string>();
        var deltas = new List<PropertyDelta>();

        AppendDiff(deltas, properties, desired.ShortcutEnabled, ShortcutKeys, key => desired switch
        {
            _ when key == "windows.msi.shortcutName" => desired.ShortcutName,
            _ when key == "windows.msi.shortcutTarget" => desired.ShortcutTarget,
            _ when key == "windows.msi.shortcutDescription" => desired.ShortcutDescription,
            _ when key == "windows.msi.shortcutIcon" => desired.ShortcutIcon,
            _ => null
        });

        AppendDiff(deltas, properties, desired.ProtocolEnabled, ProtocolKeys, key => desired switch
        {
            _ when key == "windows.msi.protocolName" => desired.ProtocolName,
            _ when key == "windows.msi.protocolDisplayName" => desired.ProtocolDisplayName,
            _ when key == "windows.msi.protocolCommand" => desired.ProtocolCommand,
            _ => null
        });

        AppendDiff(deltas, properties, desired.FileAssociationEnabled, FileAssociationKeys, key => desired switch
        {
            _ when key == "windows.msi.shellExtensionExtension" => desired.FileAssociationExtension,
            _ when key == "windows.msi.shellExtensionProgId" => desired.FileAssociationProgId,
            _ when key == "windows.msi.shellExtensionDescription" => desired.FileAssociationDescription,
            _ when key == "windows.msi.shellExtensionCommand" => desired.FileAssociationCommand,
            _ => null
        });

        return deltas;
    }

    /// <summary>
    /// Applies the requested settings and returns an updated platform configuration.
    /// </summary>
    public PlatformConfiguration Apply(PlatformConfiguration? existing, WindowsHostIntegrationSettings desired)
    {
        var formats = existing?.Formats ?? Array.Empty<string>();
        var properties = new Dictionary<string, string>(existing?.Properties ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);

        Apply(properties, desired.ShortcutEnabled, ShortcutKeys, key => desired switch
        {
            _ when key == "windows.msi.shortcutName" => desired.ShortcutName,
            _ when key == "windows.msi.shortcutTarget" => desired.ShortcutTarget,
            _ when key == "windows.msi.shortcutDescription" => desired.ShortcutDescription,
            _ when key == "windows.msi.shortcutIcon" => desired.ShortcutIcon,
            _ => null
        });

        Apply(properties, desired.ProtocolEnabled, ProtocolKeys, key => desired switch
        {
            _ when key == "windows.msi.protocolName" => desired.ProtocolName,
            _ when key == "windows.msi.protocolDisplayName" => desired.ProtocolDisplayName,
            _ when key == "windows.msi.protocolCommand" => desired.ProtocolCommand,
            _ => null
        });

        Apply(properties, desired.FileAssociationEnabled, FileAssociationKeys, key => desired switch
        {
            _ when key == "windows.msi.shellExtensionExtension" => desired.FileAssociationExtension,
            _ when key == "windows.msi.shellExtensionProgId" => desired.FileAssociationProgId,
            _ when key == "windows.msi.shellExtensionDescription" => desired.FileAssociationDescription,
            _ when key == "windows.msi.shellExtensionCommand" => desired.FileAssociationCommand,
            _ => null
        });

        return new PlatformConfiguration(formats, properties);
    }

    /// <summary>
    /// Produces validation issues for the provided settings.
    /// </summary>
    public IReadOnlyList<HostIntegrationIssue> Validate(WindowsHostIntegrationSettings settings)
    {
        var issues = new List<HostIntegrationIssue>();

        if (settings.ShortcutEnabled)
        {
            if (string.IsNullOrWhiteSpace(settings.ShortcutName))
            {
                issues.Add(new HostIntegrationIssue("windows.host.shortcut_name_missing", "Shortcut name is required when shortcuts are enabled.", HostIntegrationIssueSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(settings.ShortcutTarget))
            {
                issues.Add(new HostIntegrationIssue("windows.host.shortcut_target_missing", "Shortcut target executable is required when shortcuts are enabled.", HostIntegrationIssueSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(settings.ShortcutIcon))
            {
                issues.Add(new HostIntegrationIssue("windows.host.shortcut_icon_missing", "Shortcut icon is not specified; Windows will use the application executable icon.", HostIntegrationIssueSeverity.Warning));
            }
        }

        if (settings.ProtocolEnabled)
        {
            if (string.IsNullOrWhiteSpace(settings.ProtocolName))
            {
                issues.Add(new HostIntegrationIssue("windows.host.protocol_name_missing", "Protocol scheme name is required when the protocol handler is enabled.", HostIntegrationIssueSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(settings.ProtocolCommand))
            {
                issues.Add(new HostIntegrationIssue("windows.host.protocol_command_missing", "Protocol command is required when the protocol handler is enabled.", HostIntegrationIssueSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(settings.ProtocolDisplayName))
            {
                issues.Add(new HostIntegrationIssue("windows.host.protocol_display_missing", "Protocol display name is not set; Windows will show the raw scheme to end users.", HostIntegrationIssueSeverity.Warning));
            }
        }

        if (settings.FileAssociationEnabled)
        {
            if (string.IsNullOrWhiteSpace(settings.FileAssociationExtension))
            {
                issues.Add(new HostIntegrationIssue("windows.host.extension_missing", "File extension is required when file associations are enabled.", HostIntegrationIssueSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(settings.FileAssociationProgId))
            {
                issues.Add(new HostIntegrationIssue("windows.host.progid_missing", "ProgId is required when file associations are enabled.", HostIntegrationIssueSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(settings.FileAssociationCommand))
            {
                issues.Add(new HostIntegrationIssue("windows.host.command_missing", "Open command is required when file associations are enabled.", HostIntegrationIssueSeverity.Error));
            }

            if (string.IsNullOrWhiteSpace(settings.FileAssociationDescription))
            {
                issues.Add(new HostIntegrationIssue("windows.host.description_missing", "File association description is not set; users will see the ProgId in Explorer.", HostIntegrationIssueSeverity.Warning));
            }
        }

        return issues;
    }

    private static void AppendDiff(
        ICollection<PropertyDelta> deltas,
        IReadOnlyDictionary<string, string> existing,
        bool enabled,
        IEnumerable<string> keys,
        Func<string, string?> valueAccessor)
    {
        foreach (var key in keys)
        {
            var hadValue = existing.TryGetValue(key, out var oldValue);
            var newValue = enabled ? valueAccessor(key) : null;

            if (!enabled || string.IsNullOrWhiteSpace(newValue))
            {
                if (hadValue)
                {
                    deltas.Add(PropertyDelta.Removed(key, oldValue));
                }
                continue;
            }

            if (!hadValue)
            {
                deltas.Add(PropertyDelta.Added(key, newValue));
            }
            else if (!string.Equals(oldValue, newValue, StringComparison.Ordinal))
            {
                deltas.Add(PropertyDelta.Updated(key, oldValue, newValue));
            }
        }
    }

    private static void Apply(
        IDictionary<string, string> properties,
        bool enabled,
        IEnumerable<string> keys,
        Func<string, string?> valueAccessor)
    {
        foreach (var key in keys)
        {
            if (!enabled)
            {
                properties.Remove(key);
                continue;
            }

            var value = valueAccessor(key);
            if (string.IsNullOrWhiteSpace(value))
            {
                properties.Remove(key);
            }
            else
            {
                properties[key] = value!;
            }
        }
    }

    private static string? TryGet(IReadOnlyDictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out var value) ? value : null;
}

/// <summary>
/// Strongly-typed representation of Windows host integration metadata.
/// </summary>
/// <param name="ShortcutEnabled">Whether a start menu shortcut should be created.</param>
/// <param name="ShortcutName">Shortcut display name.</param>
/// <param name="ShortcutTarget">Shortcut executable target.</param>
/// <param name="ShortcutDescription">Shortcut description text.</param>
/// <param name="ShortcutIcon">Relative icon path.</param>
/// <param name="ProtocolEnabled">Whether a custom URI protocol should be registered.</param>
/// <param name="ProtocolName">Protocol scheme name.</param>
/// <param name="ProtocolDisplayName">Protocol friendly name presented to users.</param>
/// <param name="ProtocolCommand">Command executed for the protocol.</param>
/// <param name="FileAssociationEnabled">Whether a custom file association should be created.</param>
/// <param name="FileAssociationExtension">File extension (e.g. .sample).</param>
/// <param name="FileAssociationProgId">ProgId used for association.</param>
/// <param name="FileAssociationDescription">Description of the associated file type.</param>
/// <param name="FileAssociationCommand">Command to run when files are opened.</param>
public sealed record WindowsHostIntegrationSettings(
    bool ShortcutEnabled,
    string? ShortcutName,
    string? ShortcutTarget,
    string? ShortcutDescription,
    string? ShortcutIcon,
    bool ProtocolEnabled,
    string? ProtocolName,
    string? ProtocolDisplayName,
    string? ProtocolCommand,
    bool FileAssociationEnabled,
    string? FileAssociationExtension,
    string? FileAssociationProgId,
    string? FileAssociationDescription,
    string? FileAssociationCommand)
{
    public WindowsHostIntegrationSettings WithShortcut(bool enabled)
        => this with { ShortcutEnabled = enabled };

    public WindowsHostIntegrationSettings WithProtocol(bool enabled)
        => this with { ProtocolEnabled = enabled };

    public WindowsHostIntegrationSettings WithFileAssociation(bool enabled)
        => this with { FileAssociationEnabled = enabled };
}
