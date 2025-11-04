using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PackagingTools.Core.Utilities;

/// <summary>
/// Utility for resolving tooling executables from the current PATH (cross-platform).
/// </summary>
public sealed class ToolLocator
{
    private readonly IReadOnlyCollection<string> _pathExtensions;

    public ToolLocator()
    {
        if (OperatingSystem.IsWindows())
        {
            var pathext = Environment.GetEnvironmentVariable("PATHEXT")
                ?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();

            _pathExtensions = pathext.Length > 0
                ? pathext.Select(e => e.StartsWith(".") ? e : "." + e).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                : new[] { ".exe", ".bat", ".cmd" };
        }
        else
        {
            _pathExtensions = Array.Empty<string>();
        }
    }

    public bool TryLocate(string toolName, out string? fullPath)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            fullPath = null;
            return false;
        }

        var searchPaths = Environment.GetEnvironmentVariable("PATH")
            ?.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? Array.Empty<string>();

        foreach (var candidate in ExpandCandidates(toolName))
        {
            foreach (var root in searchPaths)
            {
                var resolved = Path.Combine(root, candidate);
                if (File.Exists(resolved))
                {
                    fullPath = Path.GetFullPath(resolved);
                    return true;
                }
            }
        }

        fullPath = null;
        return false;
    }

    private IEnumerable<string> ExpandCandidates(string toolName)
    {
        if (!OperatingSystem.IsWindows() || Path.HasExtension(toolName))
        {
            yield return toolName;
            yield break;
        }

        yield return toolName;
        foreach (var ext in _pathExtensions)
        {
            yield return toolName + ext;
        }
    }
}
