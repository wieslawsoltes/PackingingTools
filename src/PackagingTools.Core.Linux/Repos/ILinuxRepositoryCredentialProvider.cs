using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Linux.Repos;

/// <summary>
/// Resolves credentials used for publishing Linux repositories.
/// </summary>
public interface ILinuxRepositoryCredentialProvider
{
    Task<RepositoryCredential?> GetCredentialAsync(PackageFormatContext context, string credentialId, CancellationToken cancellationToken = default);
}
