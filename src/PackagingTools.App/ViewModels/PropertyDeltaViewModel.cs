using PackagingTools.Core.Configuration;

namespace PackagingTools.App.ViewModels;

public sealed class PropertyDeltaViewModel
{
    public PropertyDeltaViewModel(PropertyDelta delta)
    {
        Delta = delta;
    }

    public PropertyDelta Delta { get; }

    public string Key => Delta.Key;

    public string ChangeType => Delta.ChangeType.ToString();

    public string OldValueDisplay => string.IsNullOrWhiteSpace(Delta.OldValue) ? "<none>" : Delta.OldValue!;

    public string NewValueDisplay => string.IsNullOrWhiteSpace(Delta.NewValue) ? "<none>" : Delta.NewValue!;

    public string Description => $"{OldValueDisplay} -> {NewValueDisplay}";
}
