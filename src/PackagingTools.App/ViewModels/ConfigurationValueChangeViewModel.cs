using PackagingTools.Core.Audit;

namespace PackagingTools.App.ViewModels;

public sealed class ConfigurationValueChangeViewModel
{
    public string Key { get; }
    public ConfigurationChangeType ChangeType { get; }
    public string? Before { get; }
    public string? After { get; }

    public string DisplayChange => ChangeType switch
    {
        ConfigurationChangeType.Added => $"Added → {After}",
        ConfigurationChangeType.Removed => $"Removed (was {Before})",
        ConfigurationChangeType.Updated => $"Updated: {Before} → {After}",
        _ => "Unknown change"
    };

    public ConfigurationValueChangeViewModel(ConfigurationValueChange change)
    {
        Key = change.Key;
        ChangeType = change.ChangeType;
        Before = change.Before;
        After = change.After;
    }
}
