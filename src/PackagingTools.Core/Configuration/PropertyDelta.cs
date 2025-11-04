using System;

namespace PackagingTools.Core.Configuration;

/// <summary>
/// Represents a change to a platform property when previewing configuration updates.
/// </summary>
/// <param name="Key">Property key.</param>
/// <param name="OldValue">Existing value, if any.</param>
/// <param name="NewValue">Proposed value, if any.</param>
/// <param name="ChangeType">Nature of the change.</param>
public sealed record PropertyDelta(
    string Key,
    string? OldValue,
    string? NewValue,
    PropertyChangeType ChangeType)
{
    public static PropertyDelta Added(string key, string? newValue)
        => new(key ?? throw new ArgumentNullException(nameof(key)), null, newValue, PropertyChangeType.Added);

    public static PropertyDelta Updated(string key, string? oldValue, string? newValue)
        => new(key ?? throw new ArgumentNullException(nameof(key)), oldValue, newValue, PropertyChangeType.Updated);

    public static PropertyDelta Removed(string key, string? oldValue)
        => new(key ?? throw new ArgumentNullException(nameof(key)), oldValue, null, PropertyChangeType.Removed);
}

public enum PropertyChangeType
{
    None = 0,
    Added,
    Updated,
    Removed
}
