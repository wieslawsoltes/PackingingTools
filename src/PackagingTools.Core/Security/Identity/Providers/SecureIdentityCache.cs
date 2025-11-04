using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Security;

namespace PackagingTools.Core.Security.Identity.Providers;

internal sealed class SecureIdentityCache
{
    private readonly ISecureStore _secureStore;

    public SecureIdentityCache(ISecureStore secureStore)
    {
        _secureStore = secureStore ?? throw new ArgumentNullException(nameof(secureStore));
    }

    public async Task<IdentityResult?> TryGetAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var secret = await _secureStore.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (secret is null)
        {
            return null;
        }

        try
        {
            var document = JsonSerializer.Deserialize<IdentityCacheDocument>(secret.Payload.Span);
            if (document is null)
            {
                return null;
            }

            var principal = new IdentityPrincipal(
                document.Principal.Id,
                document.Principal.DisplayName,
                document.Principal.Email,
                document.Principal.Roles ?? Array.Empty<string>(),
                document.Principal.Claims ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

            var accessToken = document.AccessToken is null
                ? null
                : new IdentityToken(
                    document.AccessToken.Value,
                    document.AccessToken.ExpiresAtUtc,
                    document.AccessToken.Scopes ?? Array.Empty<string>());

            var refreshToken = document.RefreshToken is null
                ? null
                : new IdentityToken(
                    document.RefreshToken.Value,
                    document.RefreshToken.ExpiresAtUtc,
                    document.RefreshToken.Scopes ?? Array.Empty<string>());

            return new IdentityResult(principal, accessToken, refreshToken);
        }
        catch
        {
            return null;
        }
    }

    public async Task SetAsync(string cacheKey, IdentityResult identity, CancellationToken cancellationToken)
    {
        var document = IdentityCacheDocument.From(identity);
        var payload = JsonSerializer.SerializeToUtf8Bytes(document);
        await _secureStore.PutAsync(
            cacheKey,
            payload,
            new SecureStoreEntryOptions(
                ExpiresAt: identity.AccessToken?.ExpiresAtUtc),
            cancellationToken).ConfigureAwait(false);
    }

    private sealed record IdentityCacheDocument(
        IdentityPrincipalDocument Principal,
        IdentityTokenDocument? AccessToken,
        IdentityTokenDocument? RefreshToken)
    {
        public static IdentityCacheDocument From(IdentityResult identity)
        {
            var principal = new IdentityPrincipalDocument(
                identity.Principal.Id,
                identity.Principal.DisplayName,
                identity.Principal.Email,
                identity.Principal.Roles,
                identity.Principal.Claims);

            return new IdentityCacheDocument(
                principal,
                identity.AccessToken is null
                    ? null
                    : new IdentityTokenDocument(
                        identity.AccessToken.Value,
                        identity.AccessToken.ExpiresAtUtc,
                        identity.AccessToken.Scopes),
                identity.RefreshToken is null
                    ? null
                    : new IdentityTokenDocument(
                        identity.RefreshToken.Value,
                        identity.RefreshToken.ExpiresAtUtc,
                        identity.RefreshToken.Scopes));
        }
    }

    private sealed record IdentityPrincipalDocument(
        string Id,
        string DisplayName,
        string? Email,
        IReadOnlyCollection<string>? Roles,
        IReadOnlyDictionary<string, string>? Claims);

    private sealed record IdentityTokenDocument(
        string Value,
        DateTimeOffset ExpiresAtUtc,
        IReadOnlyCollection<string>? Scopes);
}
