using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace PackagingTools.IntegrationTests;

internal static class WindowsTestUtilities
{
    private static readonly string[] DefaultWiXTools = { "heat.exe", "candle.exe", "light.exe" };
    private static readonly Version MinimumWiXVersion = new(3, 14, 0);

    public static List<string> FindMissingWiXTools()
        => FindMissingTools(DefaultWiXTools, requireCompatibleWiX: true);

    public static List<string> FindMissingTools(params string[] toolNames)
        => FindMissingTools(toolNames, requireCompatibleWiX: false);

    private static List<string> FindMissingTools(IEnumerable<string> toolNames, bool requireCompatibleWiX)
    {
        var missing = new List<string>();
        foreach (var tool in toolNames)
        {
            var resolved = FindOnPath(tool);
            if (resolved is null)
            {
                missing.Add(tool);
                continue;
            }

            if (requireCompatibleWiX && !IsCompatibleWiXVersion(resolved, out var versionText))
            {
                missing.Add($"{tool} (requires WiX 3.14+, found {versionText})");
            }
        }

        return missing;
    }

    public static string? FindOnPath(string tool)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(pathEnv))
        {
            return null;
        }

        foreach (var segment in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = Path.Combine(segment.Trim(), tool);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static bool IsCompatibleWiXVersion(string toolPath, out string versionText)
    {
        versionText = "unknown";

        try
        {
            var info = FileVersionInfo.GetVersionInfo(toolPath);

            if (TryCreateVersion(info.ProductMajorPart, info.ProductMinorPart, info.ProductBuildPart, info.ProductPrivatePart, out var productVersion))
            {
                versionText = productVersion.ToString();
                return productVersion.Major == 3 && productVersion >= MinimumWiXVersion;
            }

            if (TryCreateVersion(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart, out var fileVersion))
            {
                versionText = fileVersion.ToString();
                return fileVersion.Major == 3 && fileVersion >= MinimumWiXVersion;
            }
        }
        catch
        {
            // Treat unreadable version metadata as incompatible so the smoke tests do not fail spuriously.
        }

        return false;
    }

    private static bool TryCreateVersion(int major, int minor, int build, int revision, out Version version)
    {
        version = new Version();
        if (major <= 0)
        {
            return false;
        }

        version = new Version(
            major,
            Math.Max(minor, 0),
            Math.Max(build, 0),
            Math.Max(revision, 0));

        return true;
    }
}
