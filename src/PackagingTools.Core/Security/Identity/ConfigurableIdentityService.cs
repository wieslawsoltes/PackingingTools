using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Security.Identity.Providers;

namespace PackagingTools.Core.Security.Identity;

/// <summary>
/// Routes identity acquisition to provider implementations based on the request provider key.
/// </summary>
internal sealed class ConfigurableIdentityService : IIdentityService
{
    private readonly IReadOnlyList<IIdentityProvider> _providers;

    public ConfigurableIdentityService(IEnumerable<IIdentityProvider> providers)
    {
        if (providers is null)
        {
            throw new ArgumentNullException(nameof(providers));
        }

        _providers = providers.ToList();
        if (_providers.Count == 0)
        {
            throw new ArgumentException("At least one identity provider must be registered.", nameof(providers));
        }
    }

    public Task<IdentityResult> AcquireAsync(IdentityRequest request, CancellationToken cancellationToken = default)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var provider = _providers.FirstOrDefault(p => p.CanHandle(request.Provider));
        if (provider is null)
        {
            provider = _providers.First(p => p.CanHandle("local"));
        }

        return provider.AcquireAsync(request, cancellationToken);
    }
}
