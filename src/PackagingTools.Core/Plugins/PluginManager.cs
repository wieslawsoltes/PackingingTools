using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace PackagingTools.Core.Plugins;

public sealed class PluginManager
{
    private readonly IPluginLoader _loader;
    private readonly IServiceCollection _services;
    private readonly List<PluginDescriptor> _descriptors = new();

    public PluginManager(IServiceCollection services, IPluginLoader? loader = null)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _loader = loader ?? new PluginLoader();
    }

    public async Task LoadFromAsync(string directory, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        var descriptors = await _loader.LoadAsync(directory, cancellationToken).ConfigureAwait(false);
        foreach (var descriptor in descriptors)
        {
            descriptor.Instance.ConfigureServices(_services);
            _descriptors.Add(descriptor);
        }
    }

    public async Task InitialiseAsync(IServiceProvider provider, CancellationToken cancellationToken = default)
    {
        foreach (var descriptor in _descriptors)
        {
            await descriptor.Instance.InitialiseAsync(provider, cancellationToken).ConfigureAwait(false);
        }
    }
}
