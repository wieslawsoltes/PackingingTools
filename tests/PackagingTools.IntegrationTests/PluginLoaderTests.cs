using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Plugins;
using PackagingTools.IntegrationTests.Plugins;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class PluginLoaderTests
{
    [Fact]
    public async Task LoadsManagedPluginAssembly()
    {
        SamplePackageFormatPlugin.Reset();
        using var tempDir = new TempDir();
        var pluginAssembly = typeof(SamplePackageFormatPlugin).Assembly.Location;
        var manifestPath = Path.Combine(tempDir.Path, "sample-plugin.json");
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(new PluginManifest
        {
            AssemblyPath = pluginAssembly,
            PluginType = typeof(SamplePackageFormatPlugin).FullName
        }));

        var services = new ServiceCollection();
        var manager = new PluginManager(services, new PluginLoader());
        await manager.LoadFromAsync(tempDir.Path);

        Assert.Contains(services, d => d.ServiceType == typeof(IPackageFormatProvider) && d.ImplementationInstance is SamplePackageFormatPlugin);
    }

}

internal sealed class TempDir : System.IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public void Dispose()
    {
        if (Directory.Exists(Path))
        {
            Directory.Delete(Path, recursive: true);
        }
    }
}
