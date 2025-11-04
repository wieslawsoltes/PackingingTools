using System;
using System.Collections.Generic;
using PackagingTools.Core.Models;

namespace PackagingTools.Sdk;

/// <summary>
/// Parameters controlling a packaging run executed via the SDK.
/// </summary>
public sealed class PackagingRunOptions
{
    public PackagingRunOptions(string projectPath, PackagingPlatform platform)
    {
        ProjectPath = projectPath ?? throw new ArgumentNullException(nameof(projectPath));
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            throw new ArgumentException("Project path cannot be empty.", nameof(projectPath));
        }

        Platform = platform;
    }

    /// <summary>
    /// Path to the packaging project definition JSON file.
    /// </summary>
    public string ProjectPath { get; }

    /// <summary>
    /// Target platform for the packaging run.
    /// </summary>
    public PackagingPlatform Platform { get; }

    /// <summary>
    /// Requested package formats. When empty, formats configured in the project file are used.
    /// </summary>
    public IList<string> Formats { get; } = new List<string>();

    /// <summary>
    /// Build configuration name (defaults to Release).
    /// </summary>
    public string Configuration { get; set; } = "Release";

    /// <summary>
    /// Output directory for generated artifacts. Defaults to <c>./artifacts/{platform}</c>.
    /// </summary>
    public string? OutputDirectory { get; set; }

    /// <summary>
    /// Per-run property overrides passed directly to packaging providers.
    /// </summary>
    public IDictionary<string, string> Properties { get; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Additional plugin directories to probe for this run. Relative paths are resolved against <see cref="ProjectPath"/>.
    /// </summary>
    public IList<string> PluginDirectories { get; } = new List<string>();
}
