using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Policies;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Windows.Formats;
using PackagingTools.Core.Windows.Pipelines;
using PackagingTools.Core.Windows.Signing;
using PackagingTools.Core.Windows.Signing.Azure;
using PackagingTools.Core.Windows.Tooling;

namespace PackagingTools.Core.Windows;

/// <summary>
/// Service collection helpers for wiring Windows packaging services.
/// </summary>
public static class WindowsPackagingServiceCollectionExtensions
{
    public static IServiceCollection AddWindowsPackaging(this IServiceCollection services)
    {
        services.TryAddSingleton<IProcessRunner, ProcessRunner>();
        services.AddPackagingIdentity();
        services.TryAddSingleton<IAzureKeyVaultClient, DefaultAzureKeyVaultClient>();
        services.TryAddSingleton<IAzureKeyVaultSigner, AzureKeyVaultSigner>();
        services.TryAddSingleton<ISigningService, WindowsSigningService>();
        services.TryAddSingleton<IPolicyEvaluator, PolicyEngineEvaluator>();
        services.AddSingleton<IPackagingPipeline, WindowsPackagingPipeline>();
        services.AddSingleton<IPackageFormatProvider, MsixPackageFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, MsiPackageFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, AppInstallerFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, WinGetManifestProvider>();
        return services;
    }
}
