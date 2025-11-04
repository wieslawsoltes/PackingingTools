using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Windows.Signing.Azure;

/// <summary>
/// Lightweight Azure Key Vault signing client that simulates remote signing.
/// </summary>
public sealed class DefaultAzureKeyVaultClient : IAzureKeyVaultClient
{
    public Task<AzureKeyVaultSignResult> SignAsync(
        string vaultUrl,
        string certificateName,
        string artifactPath,
        IReadOnlyDictionary<string, string> properties,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(vaultUrl))
        {
            return Task.FromResult(new AzureKeyVaultSignResult(false, "Vault URL was not specified.", null));
        }

        if (!File.Exists(artifactPath))
        {
            return Task.FromResult(new AzureKeyVaultSignResult(false, $"Artifact '{artifactPath}' was not found.", null));
        }

        var signaturePath = Path.ChangeExtension(artifactPath, ".remote.sig");
        File.WriteAllText(signaturePath, $"Signed by {certificateName} via {vaultUrl} at {DateTimeOffset.UtcNow:O}");

        return Task.FromResult(new AzureKeyVaultSignResult(true, null, signaturePath));
    }
}

