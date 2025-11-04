using PackagingTools.Core.Models;

namespace PackagingTools.Core.Abstractions;

/// <summary>
/// Provides access to persisted packaging project definitions.
/// </summary>
public interface IPackagingProjectStore
{
    Task<PackagingProject?> TryLoadAsync(string projectId, CancellationToken cancellationToken = default);
}
