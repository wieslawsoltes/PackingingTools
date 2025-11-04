using System.IO;

namespace PackagingTools.Core.Utilities;

/// <summary>
/// Helper utilities for manipulating directories during packaging.
/// </summary>
public static class DirectoryUtilities
{
    public static void CopyRecursive(string sourceDir, string destinationDir, bool overwrite = true)
    {
        if (!Directory.Exists(sourceDir))
        {
            throw new DirectoryNotFoundException($"Source directory '{sourceDir}' was not found.");
        }

        Directory.CreateDirectory(destinationDir);

        foreach (var file in Directory.EnumerateFiles(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var targetPath = Path.Combine(destinationDir, Path.GetFileName(file)!);
            File.Copy(file, targetPath, overwrite);
        }

        foreach (var directory in Directory.EnumerateDirectories(sourceDir, "*", SearchOption.TopDirectoryOnly))
        {
            var targetPath = Path.Combine(destinationDir, Path.GetFileName(directory)!);
            CopyRecursive(directory, targetPath, overwrite);
        }
    }
}
