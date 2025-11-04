using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;

namespace PackagingTools.Core.Mac.Tooling;

/// <summary>
/// Dispatches macOS tooling invocations to a remote build agent.
/// </summary>
public interface IRemoteMacCommandClient
{
    /// <summary>
    /// Determines whether this client can execute requests against the provided agent.
    /// </summary>
    bool CanExecute(IBuildAgentHandle agent);

    /// <summary>
    /// Executes the specified macOS tooling request on the remote agent.
    /// </summary>
    Task<MacProcessResult> ExecuteAsync(IBuildAgentHandle agent, MacProcessRequest request, CancellationToken cancellationToken = default);
}
