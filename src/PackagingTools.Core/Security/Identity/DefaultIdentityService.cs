using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security.Identity;

/// <summary>
/// Default identity service used when no external provider is configured.
/// Produces a service account principal for offline scenarios.
/// </summary>
public sealed class DefaultIdentityService : IIdentityService
{
    public Task<IdentityResult> AcquireAsync(IdentityRequest request, CancellationToken cancellationToken = default)
    {
        var principal = IdentityPrincipal.ServiceAccount;
        var claims = new Dictionary<string, string>(principal.Claims, StringComparer.OrdinalIgnoreCase)
        {
            ["provider"] = request.Provider,
            ["scopes"] = string.Join(' ', request.Scopes)
        };

        var enrichedPrincipal = new IdentityPrincipal(
            principal.Id,
            principal.DisplayName,
            principal.Email,
            principal.Roles,
            claims);

        return Task.FromResult(new IdentityResult(enrichedPrincipal, null, null));
    }
}
