using System;
using System.Collections.Generic;
using System.Linq;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Security.Identity;

/// <summary>
/// Builds identity requests from project and packaging metadata.
/// </summary>
public static class IdentityRequestBuilder
{
    public static IdentityRequest Create(PackagingProject project, PackagingRequest request)
    {
        if (project is null)
        {
            throw new ArgumentNullException(nameof(project));
        }

        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var provider = ResolveSetting(project, request, "identity.provider") ?? "local";
        var scopesValue = ResolveSetting(project, request, "identity.scopes");
        var scopes = string.IsNullOrWhiteSpace(scopesValue)
            ? new[] { "packaging.run" }
            : scopesValue.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var requireMfa = bool.TryParse(ResolveSetting(project, request, "identity.requireMfa"), out var mfa) && mfa;
        var parameters = CollectParameters(project, request);

        return new IdentityRequest(provider, scopes, requireMfa, parameters);
    }

    private static string? ResolveSetting(PackagingProject project, PackagingRequest request, string key)
    {
        if (request.Properties is not null && request.Properties.TryGetValue(key, out var value))
        {
            return value;
        }

        if (project.Metadata.TryGetValue(key, out value))
        {
            return value;
        }

        var platformConfig = project.GetPlatformConfiguration(request.Platform);
        if (platformConfig?.Properties is not null && platformConfig.Properties.TryGetValue(key, out value))
        {
            return value;
        }

        return null;
    }

    private static Dictionary<string, string> CollectParameters(PackagingProject project, PackagingRequest request)
    {
        var parameters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void Harvest(IReadOnlyDictionary<string, string>? source)
        {
            if (source is null)
            {
                return;
            }

            foreach (var kv in source)
            {
                if (kv.Key.StartsWith("identity.", StringComparison.OrdinalIgnoreCase))
                {
                    var name = kv.Key["identity.".Length..];
                    if (!string.Equals(name, "provider", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "scopes", StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(name, "requireMfa", StringComparison.OrdinalIgnoreCase))
                    {
                        parameters[name] = kv.Value;
                    }
                }
            }
        }

        Harvest(project.Metadata);
        Harvest(project.GetPlatformConfiguration(request.Platform)?.Properties);
        Harvest(request.Properties);

        return parameters;
    }
}
