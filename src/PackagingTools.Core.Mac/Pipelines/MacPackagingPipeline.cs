using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Mac.Audit;
using PackagingTools.Core.Mac.Verification;
using PackagingTools.Core.Models;
using PackagingTools.Core.Utilities;
using PackagingTools.Core.Security.Identity;

namespace PackagingTools.Core.Mac.Pipelines;

/// <summary>
/// Orchestrates macOS packaging flows (app bundles, pkg, dmg, notarization).
/// </summary>
public sealed class MacPackagingPipeline : IPackagingPipeline
{
    private readonly IPackagingProjectStore _projectStore;
    private readonly IEnumerable<IPackageFormatProvider> _formatProviders;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IBuildAgentBroker _agentBroker;
    private readonly ITelemetryChannel _telemetry;
    private readonly IMacVerificationService _verificationService;
    private readonly AuditIntegrationService _auditService;
    private readonly IIdentityContextAccessor _identityContext;
    private readonly ILogger<MacPackagingPipeline>? _logger;

    public MacPackagingPipeline(
        IPackagingProjectStore projectStore,
        IEnumerable<IPackageFormatProvider> formatProviders,
        IPolicyEvaluator policyEvaluator,
        IBuildAgentBroker agentBroker,
        ITelemetryChannel telemetry,
        IMacVerificationService verificationService,
        AuditIntegrationService auditService,
        IIdentityContextAccessor identityContextAccessor,
        ILogger<MacPackagingPipeline>? logger = null)
    {
        _projectStore = projectStore;
        _formatProviders = formatProviders;
        _policyEvaluator = policyEvaluator;
        _agentBroker = agentBroker;
        _telemetry = telemetry;
        _verificationService = verificationService;
        _auditService = auditService;
        _identityContext = identityContextAccessor ?? throw new ArgumentNullException(nameof(identityContextAccessor));
        _logger = logger;
    }

    public PackagingPlatform Platform => PackagingPlatform.MacOS;

    public async Task<PackagingResult> ExecuteAsync(PackagingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != PackagingPlatform.MacOS)
        {
            return PackagingResult.Failed(new[]
            {
                new PackagingIssue(
                    "mac.platform_mismatch",
                    $"Pipeline only supports macOS requests but received '{request.Platform}'.",
                    PackagingIssueSeverity.Error)
            });
        }

        var pipelineStart = DateTimeOffset.UtcNow;
        var project = await _projectStore.TryLoadAsync(request.ProjectId, cancellationToken);
        if (project is null)
        {
            return PackagingResult.Failed(new[]
            {
                new PackagingIssue(
                    "mac.project_not_found",
                    $"Project '{request.ProjectId}' could not be located.",
                    PackagingIssueSeverity.Error)
            });
        }

        var policyResult = await _policyEvaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, _identityContext.Identity), cancellationToken);
        if (!policyResult.IsAllowed)
        {
            return PackagingResult.Failed(policyResult.Issues);
        }

        using var workingDirectory = TemporaryDirectoryScope.Create();
        await using var agentHandle = await _agentBroker.AcquireAsync(PackagingPlatform.MacOS, cancellationToken);
        using var agentScope = BuildAgentExecutionScope.Push(agentHandle);
        var selectedProviders = ResolveProviders(request.Formats);
        if (selectedProviders.Count == 0)
        {
            return PackagingResult.Failed(new[]
            {
                new PackagingIssue(
                    "mac.no_providers",
                    "No macOS packaging providers matched the requested formats.",
                    PackagingIssueSeverity.Error)
            });
        }

        var artifacts = new ConcurrentBag<PackagingArtifact>();
        var issues = new ConcurrentBag<PackagingIssue>();

        foreach (var provider in selectedProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = new PackageFormatContext(project, request, workingDirectory.DirectoryPath);
            _telemetry.TrackEvent(
                "mac.provider.start",
                new Dictionary<string, object?>
                {
                    ["provider"] = provider.Format,
                    ["projectId"] = request.ProjectId
                });

            var start = DateTimeOffset.UtcNow;
            try
            {
                var result = await provider.PackageAsync(context, cancellationToken);
                foreach (var artifact in result.Artifacts)
                {
                    artifacts.Add(artifact);
                }

                foreach (var issue in result.Issues)
                {
                    issues.Add(issue);
                }

                if (ShouldVerify(request))
                {
                    foreach (var artifact in result.Artifacts)
                    {
                        var verification = await _verificationService.VerifyAsync(context, artifact, cancellationToken).ConfigureAwait(false);
                        foreach (var issue in verification.Issues)
                        {
                            issues.Add(issue);
                        }

                    }
                }

                _telemetry.TrackDependency(
                    provider.Format,
                    DateTimeOffset.UtcNow - start,
                    success: true,
                    properties: new Dictionary<string, object?>
                    {
                        ["artifactCount"] = result.Artifacts.Count,
                        ["issueCount"] = result.Issues.Count
                    });
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "macOS provider {Provider} failed", provider.Format);
                issues.Add(new PackagingIssue(
                    $"mac.{provider.Format}.exception",
                    ex.Message,
                    PackagingIssueSeverity.Error));

                _telemetry.TrackDependency(
                    provider.Format,
                    DateTimeOffset.UtcNow - start,
                    success: false,
                    properties: new Dictionary<string, object?>
                    {
                        ["exception"] = ex.GetType().FullName
                    });
            }
        }

        var resultArtifacts = artifacts.ToArray();
        var resultIssues = issues.ToArray();

        var success = resultIssues.All(i => i.Severity != PackagingIssueSeverity.Error);
        var packagingResult = new PackagingResult(success, resultArtifacts, resultIssues);

        if (ShouldCaptureAudit(request))
        {
            var auditIssues = await _auditService.CaptureAsync(new PackageFormatContext(project, request, workingDirectory.DirectoryPath), packagingResult, cancellationToken).ConfigureAwait(false);
            if (auditIssues.Count > 0)
            {
                success &= auditIssues.All(i => i.Severity != PackagingIssueSeverity.Error);
                var mergedIssues = new List<PackagingIssue>(resultIssues.Length + auditIssues.Count);
                mergedIssues.AddRange(resultIssues);
                mergedIssues.AddRange(auditIssues);
                resultIssues = mergedIssues.ToArray();
            }

            packagingResult = new PackagingResult(success, resultArtifacts, resultIssues);
        }

        PublishPipelineTelemetry(project, request, packagingResult, pipelineStart, DateTimeOffset.UtcNow);
        return packagingResult;
    }

    private void PublishPipelineTelemetry(PackagingProject project, PackagingRequest request, PackagingResult result, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var duration = Math.Max(0, (completedAt - startedAt).TotalSeconds);
        var blockingIssues = result.Issues.Count(i => i.Severity == PackagingIssueSeverity.Error);

        _telemetry.TrackEvent(
            "pipeline.completed",
            new Dictionary<string, object?>
            {
                ["jobId"] = jobId,
                ["projectId"] = project.Id,
                ["displayName"] = $"{project.Name} ({request.Platform})",
                ["channel"] = request.Configuration ?? "default",
                ["platform"] = request.Platform.ToString(),
                ["status"] = result.Success ? "succeeded" : "failed",
                ["durationSeconds"] = duration,
                ["completedAt"] = completedAt.ToString("O"),
                ["blockingIssues"] = blockingIssues
            });

        foreach (var artifact in result.Artifacts)
        {
            _telemetry.TrackEvent(
                "pipeline.artifact",
                new Dictionary<string, object?>
                {
                    ["jobId"] = jobId,
                    ["projectId"] = project.Id,
                    ["format"] = artifact.Format,
                    ["path"] = artifact.Path,
                    ["platform"] = request.Platform.ToString(),
                    ["channel"] = request.Configuration ?? "default"
                });
        }
    }

    private List<IPackageFormatProvider> ResolveProviders(IReadOnlyCollection<string> requestedFormats)
    {
        return _formatProviders
            .Where(p => requestedFormats.Any(format => string.Equals(format, p.Format, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static bool ShouldVerify(PackagingRequest request)
    {
        if (request.Properties is null)
        {
            return false;
        }

        if (!request.Properties.TryGetValue("mac.verify.enabled", out var enabled))
        {
            return false;
        }

        return enabled.Equals("true", StringComparison.OrdinalIgnoreCase) || enabled == "1";
    }

    private static bool ShouldCaptureAudit(PackagingRequest request)
    {
        if (request.Properties is null)
        {
            return false;
        }

        if (!request.Properties.TryGetValue("mac.audit.enabled", out var enabled))
        {
            return false;
        }

        return enabled.Equals("true", StringComparison.OrdinalIgnoreCase) || enabled == "1";
    }
}
