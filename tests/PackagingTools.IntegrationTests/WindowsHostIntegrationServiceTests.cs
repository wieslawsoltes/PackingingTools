using System.Collections.Generic;
using System.Linq;
using PackagingTools.Core.Models;
using PackagingTools.Core.Windows.Configuration;
using Xunit;

namespace PackagingTools.IntegrationTests;

public class WindowsHostIntegrationServiceTests
{
    private static PlatformConfiguration CreateConfig(IDictionary<string, string> properties)
        => new PlatformConfiguration(new[] { "msi" }, new Dictionary<string, string>(properties));

    [Fact]
    public void Load_ReturnsEnabledSettings()
    {
        var properties = new Dictionary<string, string>
        {
            ["windows.msi.shortcutName"] = "Sample",
            ["windows.msi.protocolName"] = "sample",
            ["windows.msi.shellExtensionProgId"] = "Sample.File"
        };

        var service = new WindowsHostIntegrationService();
        var settings = service.Load(CreateConfig(properties));

        Assert.True(settings.ShortcutEnabled);
        Assert.True(settings.ProtocolEnabled);
        Assert.True(settings.FileAssociationEnabled);
        Assert.Equal("Sample", settings.ShortcutName);
    }

    [Fact]
    public void CalculateDiff_AddsEntriesWhenEnablingFeatures()
    {
        var service = new WindowsHostIntegrationService();
        var existing = CreateConfig(new Dictionary<string, string>());
        var desired = new WindowsHostIntegrationSettings(
            ShortcutEnabled: true,
            ShortcutName: "Sample",
            ShortcutTarget: "Sample.exe",
            ShortcutDescription: null,
            ShortcutIcon: null,
            ProtocolEnabled: true,
            ProtocolName: "sample",
            ProtocolDisplayName: "Sample Protocol",
            ProtocolCommand: "Sample.exe \"%1\"",
            FileAssociationEnabled: true,
            FileAssociationExtension: ".sample",
            FileAssociationProgId: "Sample.File",
            FileAssociationDescription: "Sample File",
            FileAssociationCommand: "Sample.exe \"%1\"");

        var diff = service.CalculateDiff(existing, desired);

        Assert.Contains(diff, d => d.Key == "windows.msi.shortcutName");
        Assert.Contains(diff, d => d.Key == "windows.msi.protocolName");
        Assert.Contains(diff, d => d.Key == "windows.msi.shellExtensionProgId");
    }

    [Fact]
    public void Apply_RemovesKeysWhenDisabled()
    {
        var properties = new Dictionary<string, string>
        {
            ["windows.msi.shortcutName"] = "Sample",
            ["windows.msi.shortcutTarget"] = "Sample.exe"
        };

        var service = new WindowsHostIntegrationService();
        var updated = service.Apply(
            CreateConfig(properties),
            new WindowsHostIntegrationSettings(
                ShortcutEnabled: false,
                ShortcutName: null,
                ShortcutTarget: null,
                ShortcutDescription: null,
                ShortcutIcon: null,
                ProtocolEnabled: false,
                ProtocolName: null,
                ProtocolDisplayName: null,
                ProtocolCommand: null,
                FileAssociationEnabled: false,
                FileAssociationExtension: null,
                FileAssociationProgId: null,
                FileAssociationDescription: null,
                FileAssociationCommand: null));

        Assert.DoesNotContain("windows.msi.shortcutName", updated.Properties.Keys);
        Assert.DoesNotContain("windows.msi.shortcutTarget", updated.Properties.Keys);
    }

    [Fact]
    public void Validate_FlagsErrorsForMissingFields()
    {
        var service = new WindowsHostIntegrationService();
        var settings = new WindowsHostIntegrationSettings(
            ShortcutEnabled: true,
            ShortcutName: null,
            ShortcutTarget: null,
            ShortcutDescription: null,
            ShortcutIcon: null,
            ProtocolEnabled: true,
            ProtocolName: null,
            ProtocolDisplayName: null,
            ProtocolCommand: null,
            FileAssociationEnabled: true,
            FileAssociationExtension: null,
            FileAssociationProgId: null,
            FileAssociationDescription: null,
            FileAssociationCommand: null);

        var issues = service.Validate(settings);

        Assert.Contains(issues, i => i.Code == "windows.host.shortcut_name_missing" && i.Severity == HostIntegrationIssueSeverity.Error);
        Assert.Contains(issues, i => i.Code == "windows.host.protocol_command_missing" && i.Severity == HostIntegrationIssueSeverity.Error);
        Assert.Contains(issues, i => i.Code == "windows.host.command_missing" && i.Severity == HostIntegrationIssueSeverity.Error);
        Assert.Contains(issues, i => i.Code == "windows.host.shortcut_icon_missing" && i.Severity == HostIntegrationIssueSeverity.Warning);
    }
}
