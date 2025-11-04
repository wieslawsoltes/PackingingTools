using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security.Identity.Providers;

/// <summary>
/// Provides identity acquisition for a specific provider (azuread, okta, etc.).
/// </summary>
internal interface IIdentityProvider
{
    bool CanHandle(string provider);

    Task<IdentityResult> AcquireAsync(IdentityRequest request, CancellationToken cancellationToken);
}
