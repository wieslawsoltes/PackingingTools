using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PackagingTools.Core.Security.Identity.Providers;
using PackagingTools.Core.Security;

namespace PackagingTools.Core.Security.Identity;

public static class IdentityServiceFactory
{
    public static IIdentityService CreateDefault(ISecureStore secureStore)
    {
        if (secureStore is null)
        {
            throw new ArgumentNullException(nameof(secureStore));
        }

        var cache = new SecureIdentityCache(secureStore);
        var providers = new List<IIdentityProvider>
        {
            new AzureAdIdentityProvider(cache),
            new OktaIdentityProvider(cache),
            new LocalIdentityProvider()
        };

        return new ConfigurableIdentityService(providers);
    }

    public static IServiceCollection AddPackagingIdentity(this IServiceCollection services)
    {
        if (services is null)
        {
            throw new ArgumentNullException(nameof(services));
        }

        services.TryAddSingleton<ISecureStore>(_ => new FileSecureStore());
        services.TryAddSingleton<IIdentityContextAccessor, IdentityContextAccessor>();
        services.TryAddSingleton<IIdentityService>(sp =>
        {
            var secureStore = sp.GetRequiredService<ISecureStore>();
            return CreateDefault(secureStore);
        });

        return services;
    }
}
