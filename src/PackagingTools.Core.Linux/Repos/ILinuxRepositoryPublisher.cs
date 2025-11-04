using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Linux.Repos;

/// <summary>
/// Publishes Linux packaging artifacts to distribution repositories (APT, YUM/DNF, Flatpak, Snap).
/// </summary>
public interface ILinuxRepositoryPublisher
{
    Task<IReadOnlyCollection<PackagingIssue>> PublishAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default);
}
