using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Windows.Signing.Azure;

public interface IAzureKeyVaultSigner
{
    Task<SigningResult> SignAsync(SigningRequest request, string certificateName, CancellationToken cancellationToken);
}
