using System;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security.Identity.Providers;

internal sealed class LocalIdentityProvider : IIdentityProvider
{
    public bool CanHandle(string provider)
        => string.Equals(provider, "local", StringComparison.OrdinalIgnoreCase);

    public Task<IdentityResult> AcquireAsync(IdentityRequest request, CancellationToken cancellationToken)
    {
        var principal = IdentityPrincipal.ServiceAccount with
        {
            Claims = new Dictionary<string, string>(IdentityPrincipal.ServiceAccount.Claims)
            {
                ["provider"] = "local"
            }
        };

        return Task.FromResult(new IdentityResult(principal, null, null));
    }
}
