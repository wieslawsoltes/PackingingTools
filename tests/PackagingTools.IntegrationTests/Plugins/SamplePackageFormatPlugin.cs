using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Plugins.Providers;

namespace PackagingTools.IntegrationTests.Plugins;

internal sealed class SamplePackageFormatPlugin : PackageFormatProviderPlugin
{
    public static bool ServicesConfigured { get; private set; }
    public static bool Initialised { get; private set; }

    public static void Reset()
    {
        ServicesConfigured = false;
        Initialised = false;
    }

    public override string Name => "Sample.Plugin";

    public override string Version => "1.0.0";

    public override string Format => "sample";

    public override void ConfigureServices(IServiceCollection services)
    {
        ServicesConfigured = true;
        base.ConfigureServices(services);
    }

    public override Task InitialiseAsync(IServiceProvider provider, CancellationToken cancellationToken)
    {
        Initialised = true;
        return base.InitialiseAsync(provider, cancellationToken);
    }

    public override Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(new PackageFormatResult(Array.Empty<PackagingArtifact>(), Array.Empty<PackagingIssue>()));
}
