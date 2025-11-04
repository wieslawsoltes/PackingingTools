namespace PackagingTools.Core.Models;

/// <summary>
/// Represents a persisted packaging project definition shared by GUI/CLI.
/// </summary>
/// <param name="Id">Project identifier.</param>
/// <param name="Name">Friendly display name.</param>
/// <param name="Version">Default application version.</param>
/// <param name="Metadata">Arbitrary metadata.</param>
/// <param name="Platforms">Per-platform configuration blocks.</param>
public sealed record PackagingProject(
    string Id,
    string Name,
    string Version,
    IReadOnlyDictionary<string, string> Metadata,
    IReadOnlyDictionary<PackagingPlatform, PlatformConfiguration> Platforms)
{
    public PlatformConfiguration? GetPlatformConfiguration(PackagingPlatform platform)
        => Platforms.TryGetValue(platform, out var config) ? config : null;
}

/// <summary>
/// Generic configuration blob for a specific platform.
/// </summary>
/// <param name="Formats">Enabled formats (e.g. msix, msi).</param>
/// <param name="Properties">Platform-specific properties.</param>
public sealed record PlatformConfiguration(
    IReadOnlyCollection<string> Formats,
    IReadOnlyDictionary<string, string> Properties);
