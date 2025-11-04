using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Utilities;
using PackagingTools.Core.Security.Identity;
using PackagingTools.Core.Windows.Tooling;

namespace PackagingTools.Core.Windows.Pipelines;

/// <summary>
/// Orchestrates Windows packaging workflows (MSIX, MSI/EXE, App Installer, WinGet).
/// </summary>
public sealed class WindowsPackagingPipeline : IPackagingPipeline
{
    private static readonly StringComparer FormatComparer = StringComparer.OrdinalIgnoreCase;

    private readonly IPackagingProjectStore _projectStore;
    private readonly IEnumerable<IPackageFormatProvider> _formatProviders;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IBuildAgentBroker _agentBroker;
    private readonly ITelemetryChannel _telemetry;
    private readonly IIdentityContextAccessor _identityContext;
    private readonly ILogger<WindowsPackagingPipeline>? _logger;

    public WindowsPackagingPipeline(
        IPackagingProjectStore projectStore,
        IEnumerable<IPackageFormatProvider> formatProviders,
        IPolicyEvaluator policyEvaluator,
        IBuildAgentBroker agentBroker,
        ITelemetryChannel telemetry,
        IIdentityContextAccessor identityContextAccessor,
        ILogger<WindowsPackagingPipeline>? logger = null)
    {
        _projectStore = projectStore;
        _formatProviders = formatProviders;
        _policyEvaluator = policyEvaluator;
        _agentBroker = agentBroker;
        _telemetry = telemetry;
        _identityContext = identityContextAccessor ?? throw new ArgumentNullException(nameof(identityContextAccessor));
        _logger = logger;
    }

    public PackagingPlatform Platform => PackagingPlatform.Windows;

    public async Task<PackagingResult> ExecuteAsync(PackagingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != PackagingPlatform.Windows)
        {
            return PackagingResult.Failed(new[]
            {
                new PackagingIssue(
                    "windows.platform_mismatch",
                    $"Pipeline only supports Windows requests but received '{request.Platform}'.",
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
                    "windows.project_not_found",
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
        await using var agentHandle = await _agentBroker.AcquireAsync(PackagingPlatform.Windows, cancellationToken);
        using var agentScope = BuildAgentExecutionScope.Push(agentHandle);

        var selectedProviders = ResolveProviders(request.Formats);
        if (selectedProviders.Count == 0)
        {
            return PackagingResult.Failed(new[]
            {
                new PackagingIssue(
                    "windows.no_providers",
                    "No Windows packaging providers matched the requested formats.",
                    PackagingIssueSeverity.Error)
            });
        }

        var artifacts = new ConcurrentBag<PackagingArtifact>();
        var issues = new ConcurrentBag<PackagingIssue>();

        foreach (var provider in selectedProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var providerContext = new PackageFormatContext(project, request, workingDirectory.DirectoryPath);
            _telemetry.TrackEvent(
                "windows.provider.start",
                new Dictionary<string, object?>
                {
                    ["provider"] = provider.Format,
                    ["projectId"] = request.ProjectId,
                    ["configuration"] = request.Configuration
                });

            var start = DateTimeOffset.UtcNow;
            try
            {
                var result = await provider.PackageAsync(providerContext, cancellationToken);
                foreach (var artifact in result.Artifacts)
                {
                    artifacts.Add(artifact);
                }

                foreach (var issue in result.Issues)
                {
                    issues.Add(issue);
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
                _logger?.LogError(ex, "Packaging provider {Provider} failed", provider.Format);
                issues.Add(new PackagingIssue(
                    $"windows.{provider.Format}.exception",
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

        var blockingIssues = issues.Where(i => i.Severity == PackagingIssueSeverity.Error).ToArray();
        var success = blockingIssues.Length == 0;
        var artifactArray = artifacts.ToArray();
        var issueArray = issues.ToArray();
        var pipelineResult = new PackagingResult(success, artifactArray, issueArray);

        PublishPipelineTelemetry(project, request, pipelineResult, pipelineStart, DateTimeOffset.UtcNow);
        return pipelineResult;
    }

    private List<IPackageFormatProvider> ResolveProviders(IReadOnlyCollection<string> requestedFormats)
    {
        var providers = _formatProviders
            .Where(p => requestedFormats.Any(format => FormatComparer.Equals(format, p.Format)))
            .ToList();

        return providers;
    }

    private void PublishPipelineTelemetry(PackagingProject project, PackagingRequest request, PackagingResult result, DateTimeOffset startedAt, DateTimeOffset completedAt)
    {
        var jobId = Guid.NewGuid().ToString("N");
        var duration = Math.Max(0, (completedAt - startedAt).TotalSeconds);
        var blockingIssues = result.Issues.Count(i => i.Severity == PackagingIssueSeverity.Error);
        var properties = new Dictionary<string, object?>
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
        };

        _telemetry.TrackEvent("pipeline.completed", properties);

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
}
