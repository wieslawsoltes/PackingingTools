using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Utilities;

namespace PackagingTools.Core.Mac.Tooling;

/// <summary>
/// Routes macOS tooling invocations to either the local runner or a remote agent-aware dispatcher.
/// </summary>
public sealed class AgentAwareMacProcessRunner : IMacProcessRunner
{
    private readonly IMacProcessRunner _localRunner;
    private readonly IReadOnlyList<IRemoteMacCommandClient> _remoteClients;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<AgentAwareMacProcessRunner>? _logger;

    public AgentAwareMacProcessRunner(
        IMacProcessRunner localRunner,
        IEnumerable<IRemoteMacCommandClient> remoteClients,
        ITelemetryChannel telemetry,
        ILogger<AgentAwareMacProcessRunner>? logger = null)
    {
        _localRunner = localRunner;
        _remoteClients = remoteClients.ToList();
        _telemetry = telemetry;
        _logger = logger;
    }

    public async Task<MacProcessResult> ExecuteAsync(MacProcessRequest request, CancellationToken cancellationToken = default)
    {
        var agent = BuildAgentExecutionScope.Current;
        if (agent is null)
        {
            return await _localRunner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
        }

        foreach (var client in _remoteClients)
        {
            if (!client.CanExecute(agent))
            {
                continue;
            }

            _telemetry.TrackEvent(
                "mac.remote.execute",
                new Dictionary<string, object?>
                {
                    ["agent"] = agent.Name,
                    ["tool"] = request.FileName
                });

            _logger?.LogDebug("Dispatching macOS tooling '{Tool}' to remote agent '{Agent}'", request.FileName, agent.Name);
            return await client.ExecuteAsync(agent, request, cancellationToken).ConfigureAwait(false);
        }

        _logger?.LogTrace("No remote client matched agent '{Agent}', falling back to local execution", agent.Name);
        return await _localRunner.ExecuteAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
