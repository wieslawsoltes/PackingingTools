using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Linux.Container;
using PackagingTools.Core.Linux.Repos;
using PackagingTools.Core.Linux.Sandbox;
using PackagingTools.Core.Models;
using PackagingTools.Core.Security.Sbom;
using PackagingTools.Core.Security.Vulnerability;
using PackagingTools.Core.Utilities;
using PackagingTools.Core.Security.Identity;

namespace PackagingTools.Core.Linux.Pipelines;

/// <summary>
/// Orchestrates Linux packaging flows (deb, rpm, AppImage, Flatpak, Snap).
/// </summary>
public sealed class LinuxPackagingPipeline : IPackagingPipeline
{
    private readonly IPackagingProjectStore _projectStore;
    private readonly IEnumerable<IPackageFormatProvider> _providers;
    private readonly IPolicyEvaluator _policyEvaluator;
    private readonly IBuildAgentBroker _agentBroker;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILinuxSandboxProfileService _sandboxProfileService;
    private readonly ILinuxRepositoryPublisher _repositoryPublisher;
    private readonly IReadOnlyDictionary<string, ISbomGenerator> _sbomGenerators;
    private readonly ISbomGenerator _defaultSbomGenerator;
    private readonly IReadOnlyDictionary<string, IVulnerabilityScanner> _vulnerabilityScanners;
    private readonly IVulnerabilityScanner _defaultVulnerabilityScanner;
    private readonly ILinuxContainerBuildService _containerBuildService;
    private readonly IIdentityContextAccessor _identityContext;
    private readonly ILogger<LinuxPackagingPipeline>? _logger;

    public LinuxPackagingPipeline(
        IPackagingProjectStore projectStore,
        IEnumerable<IPackageFormatProvider> providers,
        IPolicyEvaluator policyEvaluator,
        IBuildAgentBroker agentBroker,
        ITelemetryChannel telemetry,
        ILinuxSandboxProfileService sandboxProfileService,
        ILinuxRepositoryPublisher repositoryPublisher,
        IEnumerable<ISbomGenerator> sbomGenerators,
        IEnumerable<IVulnerabilityScanner> vulnerabilityScanners,
        ILinuxContainerBuildService containerBuildService,
        IIdentityContextAccessor identityContextAccessor,
        ILogger<LinuxPackagingPipeline>? logger = null)
    {
        _projectStore = projectStore;
        _providers = providers;
        _policyEvaluator = policyEvaluator;
        _agentBroker = agentBroker;
        _telemetry = telemetry;
        _sandboxProfileService = sandboxProfileService;
        _repositoryPublisher = repositoryPublisher;
        if (sbomGenerators is null)
        {
            throw new ArgumentNullException(nameof(sbomGenerators));
        }
        if (vulnerabilityScanners is null)
        {
            throw new ArgumentNullException(nameof(vulnerabilityScanners));
        }

        _sbomGenerators = sbomGenerators.ToDictionary(g => g.Format, StringComparer.OrdinalIgnoreCase);
        if (_sbomGenerators.Count == 0)
        {
            throw new ArgumentException("At least one SBOM generator must be registered.", nameof(sbomGenerators));
        }
        _defaultSbomGenerator = _sbomGenerators.Values.First();

        _vulnerabilityScanners = vulnerabilityScanners.ToDictionary(s => s.Name, StringComparer.OrdinalIgnoreCase);
        if (_vulnerabilityScanners.Count == 0)
        {
            throw new ArgumentException("At least one vulnerability scanner must be registered.", nameof(vulnerabilityScanners));
        }
        _defaultVulnerabilityScanner = _vulnerabilityScanners.Values.First();
        _containerBuildService = containerBuildService;
        _identityContext = identityContextAccessor ?? throw new ArgumentNullException(nameof(identityContextAccessor));
        _logger = logger;
    }

    public PackagingPlatform Platform => PackagingPlatform.Linux;

    public async Task<PackagingResult> ExecuteAsync(PackagingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Platform != PackagingPlatform.Linux)
        {
            return PackagingResult.Failed(new[]
            {
                new PackagingIssue(
                    "linux.platform_mismatch",
                    $"Pipeline only supports Linux requests but received '{request.Platform}'.",
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
                    "linux.project_not_found",
                    $"Project '{request.ProjectId}' could not be located.",
                    PackagingIssueSeverity.Error)
            });
        }

        var policyResult = await _policyEvaluator.EvaluateAsync(new PolicyEvaluationContext(project, request, _identityContext.Identity), cancellationToken);
        if (!policyResult.IsAllowed)
        {
            return PackagingResult.Failed(policyResult.Issues);
        }

        await using var agentHandle = await _agentBroker.AcquireAsync(PackagingPlatform.Linux, cancellationToken);
        using var agentScope = BuildAgentExecutionScope.Push(agentHandle);

        var selectedProviders = ResolveProviders(request.Formats);
        if (selectedProviders.Count == 0)
        {
            return PackagingResult.Failed(new[]
            {
                new PackagingIssue(
                    "linux.no_providers",
                    "No Linux packaging providers matched the requested formats.",
                    PackagingIssueSeverity.Error)
            });
        }

        var artifacts = new ConcurrentBag<PackagingArtifact>();
        var issues = new ConcurrentBag<PackagingIssue>();

        foreach (var provider in selectedProviders)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var context = new PackageFormatContext(project, request, request.OutputDirectory);
            _telemetry.TrackEvent(
                "linux.provider.start",
                new Dictionary<string, object?>
                {
                    ["provider"] = provider.Format,
                    ["projectId"] = request.ProjectId,
                    ["configuration"] = request.Configuration
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
                _logger?.LogError(ex, "Linux provider {Provider} failed", provider.Format);
                issues.Add(new PackagingIssue(
                    $"linux.{provider.Format}.exception",
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

        if (ShouldCaptureSandboxProfiles(request))
        {
            var sandboxIssues = await _sandboxProfileService.ApplyAsync(new PackageFormatContext(project, request, request.OutputDirectory), packagingResult, cancellationToken).ConfigureAwait(false);
            if (sandboxIssues.Count > 0)
            {
                success &= sandboxIssues.All(i => i.Severity != PackagingIssueSeverity.Error);
                var merged = new List<PackagingIssue>(resultIssues.Length + sandboxIssues.Count);
                merged.AddRange(resultIssues);
                merged.AddRange(sandboxIssues);
                resultIssues = merged.ToArray();
                packagingResult = new PackagingResult(success, resultArtifacts, resultIssues);
            }
        }

        if (ShouldPublishRepositories(request))
        {
            var repoIssues = await _repositoryPublisher.PublishAsync(new PackageFormatContext(project, request, request.OutputDirectory), packagingResult, cancellationToken).ConfigureAwait(false);
            if (repoIssues.Count > 0)
            {
                success &= repoIssues.All(i => i.Severity != PackagingIssueSeverity.Error);
                var merged = new List<PackagingIssue>(resultIssues.Length + repoIssues.Count);
                merged.AddRange(resultIssues);
                merged.AddRange(repoIssues);
                resultIssues = merged.ToArray();
                packagingResult = new PackagingResult(success, resultArtifacts, resultIssues);
            }
        }

        var securityIssues = await GenerateSecurityArtifactsAsync(project, request, resultArtifacts, cancellationToken).ConfigureAwait(false);
        if (securityIssues.Count > 0)
        {
            success &= securityIssues.All(i => i.Severity != PackagingIssueSeverity.Error);
            var merged = new List<PackagingIssue>(resultIssues.Length + securityIssues.Count);
            merged.AddRange(resultIssues);
            merged.AddRange(securityIssues);
            resultIssues = merged.ToArray();
            packagingResult = new PackagingResult(success, resultArtifacts, resultIssues);
        }

        PublishPipelineTelemetry(project, request, packagingResult, pipelineStart, DateTimeOffset.UtcNow);
        return packagingResult;
    }

    private async Task<IReadOnlyCollection<PackagingIssue>> GenerateSecurityArtifactsAsync(
        PackagingProject project,
        PackagingRequest request,
        IReadOnlyCollection<PackagingArtifact> artifacts,
        CancellationToken cancellationToken)
    {
        var issues = new List<PackagingIssue>();
        var sbomEnabled = IsPropertyEnabled(request.Properties, "security.sbom.enabled");
        var vulnEnabled = IsPropertyEnabled(request.Properties, "security.vuln.enabled");

        if (sbomEnabled || vulnEnabled)
        {
            foreach (var artifact in artifacts)
            {
                var context = new PackageFormatContext(project, request, request.OutputDirectory);

                if (sbomEnabled)
                {
                    var sbomGenerator = ResolveSbomGenerator(request, issues);
                    if (sbomGenerator is null)
                    {
                        continue;
                    }

                    var sbomResult = await sbomGenerator.GenerateAsync(context, artifact, cancellationToken).ConfigureAwait(false);
                    if (!string.IsNullOrWhiteSpace(sbomResult.Path))
                    {
                        issues.Add(new PackagingIssue(
                            "security.sbom.generated",
                            $"SBOM ({sbomGenerator.Format}) generated at '{Path.GetFullPath(sbomResult.Path)}'.",
                            PackagingIssueSeverity.Info));
                    }

                    if (sbomResult.Issue is not null)
                    {
                        issues.Add(sbomResult.Issue);
                    }
                }

                if (vulnEnabled)
                {
                    var vulnerabilityScanner = ResolveVulnerabilityScanner(request, issues);
                    if (vulnerabilityScanner is null)
                    {
                        continue;
                    }

                    var vulnResult = await vulnerabilityScanner.ScanAsync(context, artifact, cancellationToken).ConfigureAwait(false);
                    if (vulnResult.Issue is not null)
                    {
                        issues.Add(vulnResult.Issue);
                    }

                    foreach (var finding in vulnResult.Findings)
                    {
                        var severity = MapSeverity(finding.Severity);
                        var message = string.IsNullOrWhiteSpace(finding.AdvisoryUrl)
                            ? $"{finding.Id} ({finding.Severity}): {finding.Description}"
                            : $"{finding.Id} ({finding.Severity}): {finding.Description} ({finding.AdvisoryUrl})";
                        issues.Add(new PackagingIssue($"security.vuln.{finding.Id.ToLowerInvariant()}", message, severity));
                    }
                }
            }
        }

        var containerIssues = await _containerBuildService.GenerateAsync(project, request, cancellationToken).ConfigureAwait(false);
        issues.AddRange(containerIssues);
        return issues;
    }

    private static PackagingIssueSeverity MapSeverity(string severity)
        => severity?.ToUpperInvariant() switch
        {
            "CRITICAL" => PackagingIssueSeverity.Error,
            "HIGH" => PackagingIssueSeverity.Warning,
            "MEDIUM" => PackagingIssueSeverity.Warning,
            _ => PackagingIssueSeverity.Info
        };

    private static bool IsPropertyEnabled(IReadOnlyDictionary<string, string>? properties, string key)
        => properties?.TryGetValue(key, out var value) == true &&
           (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private List<IPackageFormatProvider> ResolveProviders(IReadOnlyCollection<string> requestedFormats)
        => _providers
            .Where(p => requestedFormats.Any(format => string.Equals(format, p.Format, StringComparison.OrdinalIgnoreCase)))
            .ToList();

    private static bool ShouldCaptureSandboxProfiles(PackagingRequest request)
        => request.Properties?.TryGetValue("linux.sandbox.enabled", out var value) == true &&
           (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private static bool ShouldPublishRepositories(PackagingRequest request)
        => request.Properties?.TryGetValue("linux.repo.enabled", out var value) == true &&
           (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

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

    private ISbomGenerator? ResolveSbomGenerator(PackagingRequest request, List<PackagingIssue> issues)
    {
        if (request.Properties?.TryGetValue("security.sbom.format", out var format) == true && !string.IsNullOrWhiteSpace(format))
        {
            var trimmed = format.Trim();
            if (_sbomGenerators.TryGetValue(trimmed, out var generator))
            {
                return generator;
            }

            issues.Add(new PackagingIssue(
                "security.sbom.format.unsupported",
                $"SBOM format '{trimmed}' is not supported.",
                PackagingIssueSeverity.Warning));
        }

        return _defaultSbomGenerator;
    }

    private IVulnerabilityScanner? ResolveVulnerabilityScanner(PackagingRequest request, List<PackagingIssue> issues)
    {
        string? provider = null;
        if (request.Properties?.TryGetValue("security.vuln.providers", out var providers) == true && !string.IsNullOrWhiteSpace(providers))
        {
            provider = providers.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
        }
        else if (request.Properties?.TryGetValue("security.vuln.provider", out var single) == true && !string.IsNullOrWhiteSpace(single))
        {
            provider = single.Trim();
        }

        if (!string.IsNullOrWhiteSpace(provider))
        {
            if (_vulnerabilityScanners.TryGetValue(provider, out var scanner))
            {
                return scanner;
            }

            issues.Add(new PackagingIssue(
                "security.vuln.provider.unsupported",
                $"Vulnerability scanner '{provider}' is not supported.",
                PackagingIssueSeverity.Warning));
        }

        return _defaultVulnerabilityScanner;
    }

}