using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Plugins;

public sealed class PluginManifest
{
    public string AssemblyPath { get; init; } = string.Empty;
    public string? PluginType { get; init; }
    public bool Disabled { get; init; }
}

public sealed class PluginDescriptor
{
    public PluginDescriptor(IPlugin instance, Assembly assembly, string assemblyPath)
    {
        Instance = instance;
        Assembly = assembly;
        AssemblyPath = assemblyPath;
    }

    public IPlugin Instance { get; }
    public Assembly Assembly { get; }
    public string AssemblyPath { get; }
}

public interface IPluginLoader
{
    Task<IReadOnlyList<PluginDescriptor>> LoadAsync(string directory, CancellationToken cancellationToken = default);
}

public sealed class PluginLoader : IPluginLoader
{
    public Task<IReadOnlyList<PluginDescriptor>> LoadAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            return Task.FromResult<IReadOnlyList<PluginDescriptor>>(Array.Empty<PluginDescriptor>());
        }

        var descriptors = new List<PluginDescriptor>();
        foreach (var manifest in DiscoverManifests(directory))
        {
            if (manifest.Disabled)
            {
                continue;
            }

            try
            {
                var assemblyPath = ResolveAssemblyPath(directory, manifest.AssemblyPath);
                if (!File.Exists(assemblyPath))
                {
                    continue;
                }

                var assembly = Assembly.LoadFrom(assemblyPath);
                var pluginType = ResolvePluginType(assembly, manifest.PluginType);
                if (pluginType is null)
                {
                    continue;
                }

                if (Activator.CreateInstance(pluginType) is not IPlugin plugin)
                {
                    continue;
                }

                descriptors.Add(new PluginDescriptor(plugin, assembly, assemblyPath));
            }
            catch
            {
                // Ignore plugin failures so core scenario continues.
            }
        }

        return Task.FromResult<IReadOnlyList<PluginDescriptor>>(descriptors);
    }

    private static string ResolveAssemblyPath(string root, string assemblyPath)
        => Path.IsPathRooted(assemblyPath) ? assemblyPath : Path.Combine(root, assemblyPath);

    private static IEnumerable<PluginManifest> DiscoverManifests(string directory)
    {
        foreach (var file in Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly))
        {
            PluginManifest? manifest = null;
            try
            {
                manifest = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(file));
            }
            catch
            {
                // ignored
            }

            if (manifest is not null)
            {
                yield return manifest;
            }
        }
    }

    private static Type? ResolvePluginType(Assembly assembly, string? typeName)
    {
        if (!string.IsNullOrWhiteSpace(typeName))
        {
            var type = assembly.GetType(typeName, throwOnError: false, ignoreCase: true);
            if (type is not null && typeof(IPlugin).IsAssignableFrom(type) && type.GetConstructor(Type.EmptyTypes) is not null)
            {
                return type;
            }
        }

        return assembly
            .GetTypes()
            .FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) is not null);
    }
}
