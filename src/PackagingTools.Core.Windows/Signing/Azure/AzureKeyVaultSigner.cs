using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Windows.Signing.Azure;

public sealed class AzureKeyVaultSigner : IAzureKeyVaultSigner
{
    private readonly IAzureKeyVaultClient _client;

    public AzureKeyVaultSigner(IAzureKeyVaultClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public async Task<SigningResult> SignAsync(SigningRequest request, string certificateName, CancellationToken cancellationToken)
    {
        var properties = request.Properties ?? new Dictionary<string, string>();
        if (!properties.TryGetValue("windows.signing.azureKeyVaultUrl", out var vaultUrl) ||
            string.IsNullOrWhiteSpace(vaultUrl))
        {
            return SigningResult.Failed(new PackagingIssue(
                "windows.signing.azure.vault_missing",
                "Remote signing requested but 'windows.signing.azureKeyVaultUrl' was not provided.",
                PackagingIssueSeverity.Error));
        }

        var result = await _client.SignAsync(
            vaultUrl,
            certificateName,
            request.Artifact.Path,
            properties,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            return SigningResult.Failed(new PackagingIssue(
                "windows.signing.azure.failed",
                result.Error ?? "Azure Key Vault signing failed.",
                PackagingIssueSeverity.Error));
        }

        return SigningResult.Succeeded();
    }
}
