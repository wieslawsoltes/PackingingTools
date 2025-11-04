using System.Collections.Generic;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Policies;
using Xunit;

namespace PackagingTools.IntegrationTests;

public sealed class PolicyEngineEvaluatorTests
{
    private static PackagingProject CreateProject(
        Dictionary<string, string>? metadata = null,
        Dictionary<string, string>? platformProperties = null,
        PackagingPlatform platform = PackagingPlatform.Windows)
    {
        metadata ??= new Dictionary<string, string>();
        platformProperties ??= new Dictionary<string, string>();

        return new PackagingProject(
            "sample",
            "Sample Project",
            "1.0.0",
            metadata,
            new Dictionary<PackagingPlatform, PlatformConfiguration>
            {
                [platform] = new PlatformConfiguration(new[] { "format" }, platformProperties)
            });
    }

    private static PackagingRequest CreateRequest(PackagingPlatform platform, IReadOnlyDictionary<string, string>? properties = null)
        => new("sample", platform, new[] { "format" }, "Release", "/tmp/output", properties);

    [Fact]
    public async Task SigningRequiredWithoutConfigurationBlocksExecution()
    {
        var project = CreateProject(
            new Dictionary<string, string>
            {
                ["policy.signing.required"] = "true"
            });

        var request = CreateRequest(PackagingPlatform.Windows);
        var evaluator = new PolicyEngineEvaluator();

        var result = await evaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, null));

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, i => i.Code == "policy.signing.required");
    }

    [Fact]
    public async Task SigningRequirementSatisfiedWhenCertificateConfigured()
    {
        var project = CreateProject(
            new Dictionary<string, string>
            {
                ["policy.signing.required"] = "true"
            },
            new Dictionary<string, string>
            {
                ["windows.signing.certificatePath"] = "certs/code-sign.pfx"
            });

        var request = CreateRequest(PackagingPlatform.Windows);
        var evaluator = new PolicyEngineEvaluator();

        var result = await evaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, null));

        Assert.True(result.IsAllowed);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task TimestampPolicyEnforcedWhenRequired()
    {
        var project = CreateProject(
            new Dictionary<string, string>
            {
                ["policy.signing.required"] = "true",
                ["policy.signing.timestampRequired"] = "true"
            },
            new Dictionary<string, string>
            {
                ["windows.signing.certificatePath"] = "certs/code-sign.pfx"
            });

        var request = CreateRequest(PackagingPlatform.Windows);
        var evaluator = new PolicyEngineEvaluator();

        var result = await evaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, null));

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, i => i.Code == "policy.signing.timestamp_missing");
    }

    [Fact]
    public async Task ApprovalTokenRequiredByPolicy()
    {
        var project = CreateProject(
            new Dictionary<string, string>
            {
                ["policy.approval.required"] = "true"
            });

        var request = CreateRequest(PackagingPlatform.Windows);
        var evaluator = new PolicyEngineEvaluator();

        var result = await evaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, null));

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, i => i.Code == "policy.approval.missing_token");
    }

    [Fact]
    public async Task ApprovalTokenSatisfiedWhenProvided()
    {
        var project = CreateProject(
            new Dictionary<string, string>
            {
                ["policy.approval.required"] = "true"
            });

        var request = CreateRequest(
            PackagingPlatform.Windows,
            new Dictionary<string, string>
            {
                ["policy.approvalToken"] = "CAB-12345"
            });

        var evaluator = new PolicyEngineEvaluator();
        var result = await evaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, null));

        Assert.True(result.IsAllowed);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task RetentionEnforcementBlocksWhenExceeded()
    {
        var project = CreateProject(
            new Dictionary<string, string>
            {
                ["policy.retention.maxDays"] = "30",
                ["retention.days"] = "60"
            });

        var request = CreateRequest(PackagingPlatform.Windows);
        var evaluator = new PolicyEngineEvaluator();

        var result = await evaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, null));

        Assert.False(result.IsAllowed);
        Assert.Contains(result.Issues, i => i.Code == "policy.retention.exceeds_limit");
    }

    [Fact]
    public async Task RetentionWithinLimitIsAllowed()
    {
        var project = CreateProject(
            new Dictionary<string, string>
            {
                ["policy.retention.maxDays"] = "30",
                ["retention.days"] = "14"
            });

        var request = CreateRequest(PackagingPlatform.Windows);
        var evaluator = new PolicyEngineEvaluator();

        var result = await evaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, null));

        Assert.True(result.IsAllowed);
        Assert.Empty(result.Issues);
    }
}
