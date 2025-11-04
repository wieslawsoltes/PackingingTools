using PackagingTools.Core.Models;

namespace PackagingTools.Core.Abstractions;

/// <summary>
/// Allocates build agents capable of executing packaging workloads.
/// </summary>
public interface IBuildAgentBroker
{
    Task<IBuildAgentHandle> AcquireAsync(PackagingPlatform platform, CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents an allocated build agent.
/// </summary>
public interface IBuildAgentHandle : IAsyncDisposable
{
    string Name { get; }
    IReadOnlyDictionary<string, string> Capabilities { get; }
}
