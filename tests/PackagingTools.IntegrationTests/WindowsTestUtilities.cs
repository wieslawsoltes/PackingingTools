using System;
using System.Collections.Generic;
using System.IO;

namespace PackagingTools.IntegrationTests;

internal static class WindowsTestUtilities
{
    private static readonly string[] DefaultWiXTools = { "heat.exe", "candle.exe", "light.exe" };

    public static List<string> FindMissingWiXTools()
        => FindMissingTools(DefaultWiXTools);

    public static List<string> FindMissingTools(params string[] toolNames)
    {
        var missing = new List<string>();
        foreach (var tool in toolNames)
        {
            if (FindOnPath(tool) is null)
            {
                missing.Add(tool);
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
}
