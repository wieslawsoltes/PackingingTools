using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PackagingTools.Core.Windows.Signing.Azure;

public interface IAzureKeyVaultClient
{
    Task<AzureKeyVaultSignResult> SignAsync(
        string vaultUrl,
        string certificateName,
        string artifactPath,
        IReadOnlyDictionary<string, string> properties,
        CancellationToken cancellationToken);
}

public sealed record AzureKeyVaultSignResult(bool Success, string? Error, string? SignaturePath);

