using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PackagingTools.Core.Abstractions;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Mac.Audit;
using PackagingTools.Core.Mac.Formats;
using PackagingTools.Core.Mac.Pipelines;
using PackagingTools.Core.Mac.Signing;
using PackagingTools.Core.Mac.Tooling;
using PackagingTools.Core.Mac.Verification;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Policies;
using PackagingTools.Core.Security;

namespace PackagingTools.Core.Mac;

public static class MacPackagingServiceCollectionExtensions
{
    public static IServiceCollection AddMacPackaging(this IServiceCollection services)
    {
        services.TryAddSingleton<MacProcessRunner>();
        services.AddPackagingIdentity();
        services.TryAddSingleton<ISigningService, MacSigningService>();
        services.TryAddSingleton<MacSigningMaterialService>();
        services.TryAddSingleton<IPolicyEvaluator, PolicyEngineEvaluator>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IMacAuditService, MacAuditService>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IRemoteMacCommandClient, SshRemoteMacCommandClient>());
        services.AddSingleton<IMacProcessRunner>(sp =>
        {
            var localRunner = sp.GetRequiredService<MacProcessRunner>();
            var remoteClients = sp.GetServices<IRemoteMacCommandClient>();
            var telemetry = sp.GetRequiredService<ITelemetryChannel>();
            var logger = sp.GetService<ILogger<AgentAwareMacProcessRunner>>();
            return new AgentAwareMacProcessRunner(localRunner, remoteClients, telemetry, logger);
        });
        services.TryAddSingleton<IMacVerificationService, MacVerificationService>();
        services.TryAddSingleton<AuditIntegrationService>();
        services.AddSingleton<IPackagingPipeline, MacPackagingPipeline>();
        services.AddSingleton<IPackageFormatProvider, AppBundleFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, PkgFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, DmgFormatProvider>();
        services.AddSingleton<IPackageFormatProvider, NotarizationFormatProvider>();
        return services;
    }
}
