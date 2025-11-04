using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Windows.Signing.Azure;

namespace PackagingTools.IntegrationTests;

internal sealed class RecordingTelemetry : ITelemetryChannel
{
    public ConcurrentQueue<(string Event, IReadOnlyDictionary<string, object?>? Properties)> Events { get; } = new();

    public void TrackDependency(string dependencyName, TimeSpan duration, bool success, IReadOnlyDictionary<string, object?>? properties = null)
        => Events.Enqueue(($"dep:{dependencyName}", properties));

    public void TrackEvent(string eventName, IReadOnlyDictionary<string, object?>? properties = null)
        => Events.Enqueue((eventName, properties));
}

internal sealed class InMemoryProjectStore : IPackagingProjectStore
{
    private readonly PackagingProject _project;

    public InMemoryProjectStore(PackagingProject project) => _project = project;

    public Task<PackagingProject?> TryLoadAsync(string projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(_project.Id == projectId ? _project : null);
}

internal sealed class StubSigningService : ISigningService
{
    public List<SigningRequest> Requests { get; } = new();

    public Task<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Add(request);
        return Task.FromResult(SigningResult.Succeeded());
    }
}

internal sealed class AllowAllPolicy : IPolicyEvaluator
{
    public static AllowAllPolicy Instance { get; } = new();

    public Task<PolicyEvaluationResult> EvaluateAsync(PolicyEvaluationContext context, CancellationToken cancellationToken = default)
        => Task.FromResult(PolicyEvaluationResult.Allowed());
}

internal sealed class TestIdentityContextAccessor : IIdentityContextAccessor
{
    private IdentityResult? _identity;

    public IdentityResult? Identity => _identity;

    public void SetIdentity(IdentityResult identity)
    {
        _identity = identity;
    }

    public void Clear()
    {
        _identity = null;
    }
}

internal sealed class StubAzureKeyVaultClient : IAzureKeyVaultClient
{
    public List<string> SignedArtifacts { get; } = new();
    public bool ShouldFail { get; set; }

    public Task<AzureKeyVaultSignResult> SignAsync(string vaultUrl, string certificateName, string artifactPath, IReadOnlyDictionary<string, string> properties, CancellationToken cancellationToken)
    {
        if (ShouldFail)
        {
            return Task.FromResult(new AzureKeyVaultSignResult(false, "Stub failure", null));
        }

        SignedArtifacts.Add(artifactPath);
        var signature = Path.ChangeExtension(artifactPath, ".stub.sig");
        File.WriteAllText(signature, $"Signed by {certificateName} from {vaultUrl}");
        return Task.FromResult(new AzureKeyVaultSignResult(true, null, signature));
    }
}

internal sealed class NoopAgentBroker : IBuildAgentBroker, IBuildAgentHandle
{
    public static NoopAgentBroker Instance { get; } = new();

    public IReadOnlyDictionary<string, string> Capabilities { get; } = new Dictionary<string, string>();

    public string Name => "local";

    public Task<IBuildAgentHandle> AcquireAsync(PackagingPlatform platform, CancellationToken cancellationToken = default)
        => Task.FromResult<IBuildAgentHandle>(this);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
