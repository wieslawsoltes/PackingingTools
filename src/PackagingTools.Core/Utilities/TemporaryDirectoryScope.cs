using System;
using System.IO;
using System.Threading.Tasks;

namespace PackagingTools.Core.Utilities;

/// <summary>
/// Creates and cleans up a temporary working directory for packaging steps across platforms.
/// </summary>
public sealed class TemporaryDirectoryScope : IAsyncDisposable, IDisposable
{
    public string DirectoryPath { get; }

    private TemporaryDirectoryScope(string path)
    {
        Directory.CreateDirectory(path);
        DirectoryPath = path;
    }

    public static TemporaryDirectoryScope Create(string? hint = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), "PackagingTools", hint ?? Guid.NewGuid().ToString("N"));
        if (Directory.Exists(folder))
        {
            folder = Path.Combine(Path.GetTempPath(), "PackagingTools", Guid.NewGuid().ToString("N"));
        }
        return new TemporaryDirectoryScope(folder);
    }

    public ValueTask DisposeAsync()
    {
        Cleanup();
        return ValueTask.CompletedTask;
    }

    public void Dispose() => Cleanup();

    private void Cleanup()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, true);
            }
        }
        catch
        {
            // best effort cleanup
        }
    }
}
