using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Linux.Repos;

/// <summary>
/// Resolves credentials from packaging request properties.
/// </summary>
public sealed class PropertyLinuxRepositoryCredentialProvider : ILinuxRepositoryCredentialProvider
{
    public Task<RepositoryCredential?> GetCredentialAsync(PackageFormatContext context, string credentialId, CancellationToken cancellationToken = default)
    {
        if (context.Request.Properties is null || string.IsNullOrWhiteSpace(credentialId))
        {
            return Task.FromResult<RepositoryCredential?>(null);
        }

        var prefix = $"linux.repo.credential.{credentialId}.";
        var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var type = "generic";
        var found = false;

        foreach (var kv in context.Request.Properties)
        {
            if (!kv.Key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var suffix = kv.Key.Substring(prefix.Length);
            if (suffix.Equals("type", StringComparison.OrdinalIgnoreCase))
            {
                type = kv.Value;
                found = true;
                continue;
            }

            properties[suffix] = kv.Value;
            found = true;
        }

        if (!found)
        {
            return Task.FromResult<RepositoryCredential?>(null);
        }

        return Task.FromResult<RepositoryCredential?>(new RepositoryCredential(credentialId, type, properties));
    }
}
