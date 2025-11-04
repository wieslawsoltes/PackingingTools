using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security.Identity.Providers;

internal sealed class OktaIdentityProvider : IIdentityProvider
{
    private readonly SecureIdentityCache _cache;

    public OktaIdentityProvider(SecureIdentityCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public bool CanHandle(string provider)
        => string.Equals(provider, "okta", StringComparison.OrdinalIgnoreCase);

    public async Task<IdentityResult> AcquireAsync(IdentityRequest request, CancellationToken cancellationToken)
    {
        var domain = ResolveParameter(request, "domain", "example.okta.com");
        var username = ResolveParameter(request, "username", Environment.UserName);
        var cacheKey = $"identity.okta.{domain}.{username}".ToLowerInvariant();

        var cached = await _cache.TryGetAsync(cacheKey, cancellationToken).ConfigureAwait(false);
        if (cached is not null && cached.AccessToken is IdentityToken token &&
            token.ExpiresAtUtc > DateTimeOffset.UtcNow.AddMinutes(1) &&
            token.Scopes.Intersect(request.Scopes, StringComparer.OrdinalIgnoreCase).Count() == request.Scopes.Count)
        {
            return cached;
        }

        string? mfaCode = null;
        if (request.RequireMfa && !request.Parameters.TryGetValue("mfaCode", out mfaCode))
        {
            throw new InvalidOperationException("Okta login requires an MFA code when MFA is requested.");
        }

        var scopes = request.Scopes.ToArray();
        var accessToken = new IdentityToken($"okta-access-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddMinutes(50), scopes);
        var refreshToken = new IdentityToken($"okta-refresh-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(15), Array.Empty<string>());

        var roles = ResolveList(request, "roles", new[] { "Developer" });
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["provider"] = "okta",
            ["domain"] = domain,
            ["username"] = username,
            ["mfa"] = request.RequireMfa ? "true" : "false"
        };

        if (request.Parameters.TryGetValue("organization", out var organization))
        {
            claims["organization"] = organization;
        }

        if (request.RequireMfa && !string.IsNullOrWhiteSpace(mfaCode))
        {
            claims["mfaCode"] = mfaCode;
        }

        var displayName = ResolveParameter(request, "displayName", username);
        var email = ResolveParameter(request, "email", $"{username}@{domain}");

        var principal = new IdentityPrincipal(
            Id: $"okta:{domain}:{username}",
            DisplayName: displayName,
            Email: email,
            Roles: roles,
            Claims: claims);

        var result = new IdentityResult(principal, accessToken, refreshToken);
        await _cache.SetAsync(cacheKey, result, cancellationToken).ConfigureAwait(false);
        return result;
    }

    private static string ResolveParameter(IdentityRequest request, string key, string fallback)
        => request.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static IReadOnlyCollection<string> ResolveList(IdentityRequest request, string key, IReadOnlyCollection<string> fallback)
    {
        if (request.Parameters.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
        {
            var items = value.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (items.Length > 0)
            {
                return items;
            }
        }

        return fallback;
    }
}
