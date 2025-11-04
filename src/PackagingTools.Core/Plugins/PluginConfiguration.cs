using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Plugins;

/// <summary>
/// Resolves plugin probing directories from user preferences, environment configuration, and defaults.
/// </summary>
public static class PluginConfiguration
{
    public const string MetadataKey = "plugins.directories";
    public const string EnvironmentVariable = "PACKAGINGTOOLS_PLUGIN_PATHS";

    private static readonly char[] Separators = { Path.PathSeparator, ';', ',', '\n', '\r' };

    public static IReadOnlyList<string> ParsePathList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Array.Empty<string>();
        }

        return value
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(path => path.Trim())
            .Where(path => path.Length > 0)
            .ToArray();
    }

    public static string FormatPathList(IEnumerable<string> paths)
    {
        if (paths is null)
        {
            return string.Empty;
        }

        return string.Join(
            Path.PathSeparator,
            paths
                .Select(path => path.Trim())
                .Where(path => path.Length > 0));
    }

    public static IReadOnlyList<string> ResolveProbeDirectories(
        PackagingProject? project,
        string? projectPath = null,
        IEnumerable<string>? primaryOverrides = null,
        IEnumerable<string>? secondaryOverrides = null)
    {
        var resolved = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var projectDirectory = GetProjectDirectory(projectPath);
        var applicationDirectory = AppContext.BaseDirectory;

        void AddPath(string? candidate, string? baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                return;
            }

            var trimmed = candidate.Trim();
            string fullPath;
            if (Path.IsPathRooted(trimmed))
            {
                fullPath = Path.GetFullPath(trimmed);
            }
            else if (!string.IsNullOrWhiteSpace(baseDirectory))
            {
                fullPath = Path.GetFullPath(Path.Combine(baseDirectory, trimmed));
            }
            else
            {
                fullPath = Path.GetFullPath(trimmed);
            }

            if (seen.Add(fullPath))
            {
                resolved.Add(fullPath);
            }
        }

        if (primaryOverrides is not null)
        {
            foreach (var path in primaryOverrides)
            {
                AddPath(path, projectDirectory);
            }
        }

        if (project?.Metadata is { Count: > 0 } metadata &&
            metadata.TryGetValue(MetadataKey, out var metadataValue))
        {
            foreach (var path in ParsePathList(metadataValue))
            {
                AddPath(path, projectDirectory);
            }
        }

        if (secondaryOverrides is not null)
        {
            foreach (var path in secondaryOverrides)
            {
                AddPath(path, applicationDirectory);
            }
        }

        var environmentValue = Environment.GetEnvironmentVariable(EnvironmentVariable);
        foreach (var path in ParsePathList(environmentValue))
        {
            AddPath(path, null);
        }

        AddPath(Path.Combine(applicationDirectory, "plugins"), null);

        var userRoot = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrWhiteSpace(userRoot))
        {
            AddPath(Path.Combine(userRoot, "PackagingTools", "plugins"), null);
        }

        return resolved;
    }

    private static string? GetProjectDirectory(string? projectPath)
    {
        if (string.IsNullOrWhiteSpace(projectPath))
        {
            return null;
        }

        try
        {
            if (Directory.Exists(projectPath))
            {
                return Path.GetFullPath(projectPath);
            }

            var directory = Path.GetDirectoryName(projectPath);
            return string.IsNullOrWhiteSpace(directory) ? null : Path.GetFullPath(directory);
        }
        catch
        {
            return null;
        }
    }
}
