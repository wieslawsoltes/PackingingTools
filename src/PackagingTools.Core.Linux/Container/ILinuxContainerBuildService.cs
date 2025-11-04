using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Linux.Container;

public interface ILinuxContainerBuildService
{
    Task<IReadOnlyCollection<PackagingIssue>> GenerateAsync(PackagingProject project, PackagingRequest request, CancellationToken cancellationToken = default);
}
