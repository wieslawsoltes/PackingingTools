using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Models;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Linux.Sandbox;

/// <summary>
/// Applies sandbox and security profile configuration for Linux artifacts.
/// </summary>
public interface ILinuxSandboxProfileService
{
    Task<IReadOnlyCollection<PackagingIssue>> ApplyAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default);
}
