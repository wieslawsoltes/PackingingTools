using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Mac.Tooling;
using PackagingTools.Core.Utilities;
using PackagingTools.Core.Models;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class AgentAwareMacProcessRunnerTests
{
    [Fact]
    public async Task ExecutesRemoteClientWhenCapabilitiesMatch()
    {
        var remoteClient = new StubRemoteClient(canExecute: true, new MacProcessResult(0, "remote", string.Empty));
        var localRunner = new StubLocalRunner();
        var telemetry = new RecordingTelemetry();

        var runner = new AgentAwareMacProcessRunner(
            localRunner,
            new[] { remoteClient },
            telemetry,
            NullLogger<AgentAwareMacProcessRunner>.Instance);

        using var scope = BuildAgentExecutionScope.Push(new StubAgentHandle(new Dictionary<string, string>
        {
            ["mac.remote.sshHost"] = "builder.example.com"
        }));

        var request = new MacProcessRequest("notarytool", new[] { "--version" });
        var result = await runner.ExecuteAsync(request);

        Assert.Equal(0, result.ExitCode);
        Assert.True(remoteClient.Executed);
        Assert.False(localRunner.Executed);
        Assert.Contains(telemetry.Events, e => e.Event == "mac.remote.execute");
    }

    [Fact]
    public async Task FallsBackToLocalWhenNoRemoteClientMatches()
    {
        var remoteClient = new StubRemoteClient(canExecute: false, new MacProcessResult(0, "remote", string.Empty));
        var localRunner = new StubLocalRunner();
        var telemetry = new RecordingTelemetry();

        var runner = new AgentAwareMacProcessRunner(
            localRunner,
            new[] { remoteClient },
            telemetry,
            NullLogger<AgentAwareMacProcessRunner>.Instance);

        using var scope = BuildAgentExecutionScope.Push(new StubAgentHandle(new Dictionary<string, string>()));

        var request = new MacProcessRequest("notarytool", new[] { "--version" });
        var result = await runner.ExecuteAsync(request);

        Assert.Equal(0, result.ExitCode);
        Assert.False(remoteClient.Executed);
        Assert.True(localRunner.Executed);
    }

    private sealed class StubRemoteClient : IRemoteMacCommandClient
    {
        private readonly bool _canExecute;
        private readonly MacProcessResult _result;

        public StubRemoteClient(bool canExecute, MacProcessResult result)
        {
            _canExecute = canExecute;
            _result = result;
        }

        public bool Executed { get; private set; }

        public bool CanExecute(IBuildAgentHandle agent) => _canExecute;

        public Task<MacProcessResult> ExecuteAsync(IBuildAgentHandle agent, MacProcessRequest request, CancellationToken cancellationToken = default)
        {
            Executed = true;
            return Task.FromResult(_result);
        }
    }

    private sealed class StubLocalRunner : IMacProcessRunner
    {
        public bool Executed { get; private set; }

        public Task<MacProcessResult> ExecuteAsync(MacProcessRequest request, CancellationToken cancellationToken = default)
        {
            Executed = true;
            return Task.FromResult(new MacProcessResult(0, "local", string.Empty));
        }
    }

    private sealed class StubAgentHandle : IBuildAgentHandle
    {
        public StubAgentHandle(IReadOnlyDictionary<string, string> capabilities)
        {
            Capabilities = capabilities;
        }

        public string Name => "stub-agent";

        public IReadOnlyDictionary<string, string> Capabilities { get; }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
