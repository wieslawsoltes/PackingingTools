using PackagingTools.Core.Models;

namespace PackagingTools.Core.Abstractions;

/// <summary>
/// Coordinates the execution of packaging operations for a specific platform.
/// </summary>
public interface IPackagingPipeline
{
    PackagingPlatform Platform { get; }

    Task<PackagingResult> ExecuteAsync(PackagingRequest request, CancellationToken cancellationToken = default);
}
