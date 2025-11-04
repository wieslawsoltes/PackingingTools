using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Security.Identity.Providers;

internal sealed class AzureAdIdentityProvider : IIdentityProvider
{
    private readonly SecureIdentityCache _cache;

    public AzureAdIdentityProvider(SecureIdentityCache cache)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public bool CanHandle(string provider)
        => string.Equals(provider, "azuread", StringComparison.OrdinalIgnoreCase);

    public async Task<IdentityResult> AcquireAsync(IdentityRequest request, CancellationToken cancellationToken)
    {
        var tenantId = ResolveParameter(request, "tenantId", "common");
        var username = ResolveParameter(request, "username", Environment.UserName);
        var cacheKey = $"identity.azuread.{tenantId}.{username}".ToLowerInvariant();

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
            throw new InvalidOperationException("Multi-factor authentication is required but no MFA code was supplied.");
        }

        var scopes = request.Scopes.ToArray();
        var accessToken = new IdentityToken($"aad-access-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddHours(1), scopes);
        var refreshToken = new IdentityToken($"aad-refresh-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(30), Array.Empty<string>());

        var roles = ResolveList(request, "roles", new[] { "ReleaseEngineer" });
        var claims = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["provider"] = "azuread",
            ["tenantId"] = tenantId,
            ["username"] = username,
            ["mfa"] = request.RequireMfa ? "true" : "false"
        };

        if (request.Parameters.TryGetValue("clientId", out var clientId))
        {
            claims["clientId"] = clientId;
        }

        if (request.RequireMfa && !string.IsNullOrWhiteSpace(mfaCode))
        {
            claims["mfaCode"] = mfaCode;
        }

        var displayName = ResolveParameter(request, "displayName", username);
        var email = ResolveParameter(request, "email", $"{username}@{tenantId}");

        var principal = new IdentityPrincipal(
            Id: $"aad:{tenantId}:{username}",
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
