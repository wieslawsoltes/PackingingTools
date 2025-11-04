using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Plugins.Providers;

public abstract class PackageFormatProviderPlugin : IPlugin, IPackageFormatProvider
{
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract string Format { get; }
    public abstract Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default);

    public virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IPackageFormatProvider>(this);
    }

    public virtual Task InitialiseAsync(IServiceProvider provider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public abstract class SigningProviderPlugin : IPlugin, ISigningService
{
    public abstract string Name { get; }
    public abstract string Version { get; }
    public abstract Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default);

    public virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ISigningService>(this);
    }

    public virtual Task InitialiseAsync(IServiceProvider provider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

public abstract class TelemetryPlugin : IPlugin, ITelemetryChannel
{
    public abstract string Name { get; }
    public abstract string Version { get; }

    public abstract void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? properties = null);

    public abstract void TrackDependency(string dependencyName, TimeSpan duration, bool success, IReadOnlyDictionary<string, object?>? properties = null);

    public virtual void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<ITelemetryChannel>(this);
    }

    public virtual Task InitialiseAsync(IServiceProvider provider, CancellationToken cancellationToken)
        => Task.CompletedTask;
}
