using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Linux.Container;
using PackagingTools.Core.Linux.Formats;
using PackagingTools.Core.Linux.Pipelines;
using PackagingTools.Core.Linux.Repos;
using PackagingTools.Core.Linux.Signing;
using PackagingTools.Core.Linux.Tooling;
using PackagingTools.Core.Linux.Sandbox;
using PackagingTools.Core.Policies;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Security.Sbom;
using PackagingTools.Core.Security.Vulnerability;
using PackagingTools.Core.Security.Vulnerability.Scanners;

namespace PackagingTools.Core.Linux;

public static class LinuxPackagingServiceCollectionExtensions
{
    public static IServiceCollection AddLinuxPackaging(this IServiceCollection services)
    {
        services.TryAddSingleton<ILinuxProcessRunner, LinuxProcessRunner>();
        services.AddPackagingIdentity();
        services.TryAddSingleton<ISigningService, LinuxSigningService>();
        services.TryAddSingleton<ILinuxSandboxProfileService, LinuxSandboxProfileService>();
        services.TryAddSingleton<ILinuxRepositoryCredentialProvider, PropertyLinuxRepositoryCredentialProvider>();
        services.TryAddSingleton<ILinuxRepositoryPublisher, LinuxRepositoryPublisher>();
        services.TryAddSingleton<ISbomGenerator, CycloneDxSbomGenerator>();
        services.TryAddSingleton<IVulnerabilityScanner, TrivyVulnerabilityScanner>();
        services.TryAddSingleton<ILinuxContainerBuildService, DockerLinuxContainerBuildService>();
        services.TryAddSingleton<IPolicyEvaluator, PolicyEngineEvaluator>();
        services.AddSingleton<IPackagingPipeline, LinuxPackagingPipeline>();
        services.AddSingleton<IPackageFormatProvider, DebFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, RpmFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, AppImageFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, FlatpakFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, SnapFormatProvider>();
        return services;
    }
}
