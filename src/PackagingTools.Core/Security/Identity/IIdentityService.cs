using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security.Identity;

/// <summary>
/// Performs authentication/authorization with configured identity providers.
/// </summary>
public interface IIdentityService
{
    Task<IdentityResult> AcquireAsync(IdentityRequest request, CancellationToken cancellationToken = default);
}

/// <summary>
/// Describes an identity acquisition request.
/// </summary>
/// <param name="Provider">Identity provider identifier (e.g., azuread, okta).</param>
/// <param name="Scopes">Requested scopes/permissions.</param>
/// <param name="RequireMfa">Whether multi-factor authentication must be satisfied.</param>
/// <param name="Parameters">Additional provider-specific parameters (tenant ids, resource URLs, etc.).</param>
public sealed record IdentityRequest(
    string Provider,
    IReadOnlyCollection<string> Scopes,
    bool RequireMfa,
    IReadOnlyDictionary<string, string> Parameters);

/// <summary>
/// Represents the outcome of an identity acquisition operation.
/// </summary>
/// <param name="Principal">Resolved identity principal.</param>
/// <param name="AccessToken">Optional access token for downstream APIs.</param>
/// <param name="RefreshToken">Optional refresh token for renewal.</param>
public sealed record IdentityResult(
    IdentityPrincipal Principal,
    IdentityToken? AccessToken,
    IdentityToken? RefreshToken);

/// <summary>
/// Represents a security token with expiration metadata.
/// </summary>
/// <param name="Value">Opaque token value.</param>
/// <param name="ExpiresAtUtc">UTC expiration instant.</param>
/// <param name="Scopes">Scopes included in the token.</param>
public sealed record IdentityToken(
    string Value,
    DateTimeOffset ExpiresAtUtc,
    IReadOnlyCollection<string> Scopes);
