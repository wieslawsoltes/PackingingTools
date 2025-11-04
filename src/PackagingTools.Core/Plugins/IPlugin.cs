using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace PackagingTools.Core.Plugins;

/// <summary>
/// Represents a PackagingTools plugin that can register additional services.
/// </summary>
public interface IPlugin
{
    /// <summary>
    /// Plugin identifier (typically a reverse-DNS name).
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin semantic version.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Allows the plugin to register services in the hosting container.
    /// </summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>
    /// Called after service registration to perform any asynchronous initialisation.
    /// </summary>
    Task InitialiseAsync(IServiceProvider provider, CancellationToken cancellationToken);
}
