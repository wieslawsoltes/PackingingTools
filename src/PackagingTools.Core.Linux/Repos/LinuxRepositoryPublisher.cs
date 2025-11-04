using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Linux.Repos;

/// <summary>
/// Writes repository publishing metadata to support downstream distribution automation.
/// </summary>
public sealed class LinuxRepositoryPublisher : ILinuxRepositoryPublisher
{
    private readonly ILinuxRepositoryCredentialProvider _credentialProvider;
    private readonly ILogger<LinuxRepositoryPublisher>? _logger;

    public LinuxRepositoryPublisher(
        ILinuxRepositoryCredentialProvider credentialProvider,
        ILogger<LinuxRepositoryPublisher>? logger = null)
    {
        _credentialProvider = credentialProvider ?? throw new ArgumentNullException(nameof(credentialProvider));
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<PackagingIssue>> PublishAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        if (!IsEnabled(context.Request))
        {
            return issues;
        }

        var targets = ResolveTargets(context, issues);
        if (targets.Count == 0)
        {
            return issues;
        }

        foreach (var target in targets)
        {
            RepositoryCredential? credential = null;
            if (!string.IsNullOrWhiteSpace(target.CredentialId))
            {
                try
                {
                    credential = await _credentialProvider.GetCredentialAsync(context, target.CredentialId!, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Credential provider threw while resolving {CredentialId}", target.CredentialId);
                }

                if (credential is null)
                {
                    issues.Add(new PackagingIssue(
                        "linux.repo.credential_missing",
                        $"Credential '{target.CredentialId}' could not be resolved for repository target '{target.Id}'.",
                        PackagingIssueSeverity.Error));
                    continue;
                }
            }

            try
            {
                IReadOnlyCollection<PackagingIssue> targetIssues = target.Type switch
                {
                    RepositoryTargetType.Apt => PublishAptRepository(context, result, target, credential),
                    RepositoryTargetType.Yum => PublishYumRepository(context, result, target, credential),
                    _ => Array.Empty<PackagingIssue>()
                };

                if (targetIssues.Count > 0)
                {
                    issues.AddRange(targetIssues);
                }
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to publish repository target {Target}", target.Id);
                issues.Add(new PackagingIssue(
                    "linux.repo.publish_failed",
                    $"Failed to publish repository target '{target.Id}': {ex.Message}",
                    PackagingIssueSeverity.Warning));
            }
        }

        return issues;
    }

    private static bool IsEnabled(PackagingRequest request)
        => request.Properties?.TryGetValue("linux.repo.enabled", out var enabled) == true &&
           (enabled.Equals("true", StringComparison.OrdinalIgnoreCase) || enabled == "1");

    private List<RepositoryTarget> ResolveTargets(PackageFormatContext context, IList<PackagingIssue> issues)
    {
        var targets = new List<RepositoryTarget>();
        var properties = context.Request.Properties;
        if (properties is null || !properties.TryGetValue("linux.repo.targets", out var raw) || string.IsNullOrWhiteSpace(raw))
        {
            issues.Add(new PackagingIssue(
                "linux.repo.targets_missing",
                "Repository publishing enabled but no targets were specified (expected 'linux.repo.targets').",
                PackagingIssueSeverity.Warning));
            return targets;
        }

        var segments = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var segment in segments)
        {
            var identifier = segment.Trim();
            if (string.IsNullOrWhiteSpace(identifier))
            {
                continue;
            }

            var prefix = $"linux.repo.target.{identifier}.";
            var typeValue = GetProperty(properties, prefix + "type");
            if (string.IsNullOrWhiteSpace(typeValue))
            {
                issues.Add(new PackagingIssue(
                    "linux.repo.target_type_missing",
                    $"Repository target '{identifier}' is missing required property '{prefix}type'.",
                    PackagingIssueSeverity.Warning));
                continue;
            }

            if (!Enum.TryParse<RepositoryTargetType>(typeValue, true, out var targetType))
            {
                issues.Add(new PackagingIssue(
                    "linux.repo.target_type_invalid",
                    $"Repository target '{identifier}' specified unsupported type '{typeValue}'.",
                    PackagingIssueSeverity.Warning));
                continue;
            }

            var destination = GetProperty(properties, prefix + "destination");
            var suite = GetProperty(properties, prefix + "suite");
            var componentsRaw = GetProperty(properties, prefix + "components");
            var components = string.IsNullOrWhiteSpace(componentsRaw)
                ? Array.Empty<string>()
                : componentsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var credentialId = GetProperty(properties, prefix + "credential");

            targets.Add(new RepositoryTarget(
                identifier,
                targetType,
                destination,
                suite,
                components,
                credentialId));
        }

        return targets;
    }

    private IReadOnlyCollection<PackagingIssue> PublishAptRepository(PackageFormatContext context, PackagingResult result, RepositoryTarget target, RepositoryCredential? credential)
    {
        var issues = new List<PackagingIssue>();
        var debArtifacts = result.Artifacts.Where(a => string.Equals(a.Format, "deb", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (debArtifacts.Length == 0)
        {
            issues.Add(new PackagingIssue(
                "linux.repo.apt.no_artifacts",
                $"Repository target '{target.Id}' expects Debian packages but none were produced.",
                PackagingIssueSeverity.Warning));
            return issues;
        }

        var suite = string.IsNullOrWhiteSpace(target.Suite) ? "stable" : target.Suite!;
        var components = target.Components.Count > 0 ? target.Components : new[] { "main" };
        var repoRoot = Path.Combine(context.Request.OutputDirectory, "_Repo", target.Id, "apt");
        Directory.CreateDirectory(repoRoot);

        var packagesByComponentArch = new Dictionary<(string Component, string Architecture), List<AptPackageEntry>>();

        foreach (var artifact in debArtifacts)
        {
            var fileInfo = new FileInfo(artifact.Path);
            if (!fileInfo.Exists)
            {
                issues.Add(new PackagingIssue(
                    "linux.repo.apt.artifact_missing",
                    $"Artifact '{artifact.Path}' could not be found for repository publishing.",
                    PackagingIssueSeverity.Warning));
                continue;
            }

            var packageName = GetMetadata(artifact.Metadata, "packageName") ?? Path.GetFileNameWithoutExtension(artifact.Path);
            var version = GetMetadata(artifact.Metadata, "packageVersion") ?? context.Project.Version;
        var architecture = GetMetadata(artifact.Metadata, "packageArchitecture");
        if (string.IsNullOrWhiteSpace(architecture) && context.Project.Metadata.TryGetValue("linux.architecture", out var projectArch))
        {
            architecture = projectArch;
        }
        architecture ??= "amd64";
            var description = GetMetadata(artifact.Metadata, "packageDescription") ?? string.Empty;
            var sha256 = ComputeSha256(artifact.Path);
            var fileName = Path.GetFileName(artifact.Path);

            foreach (var component in components)
            {
                var key = (component, architecture);
                if (!packagesByComponentArch.TryGetValue(key, out var entries))
                {
                    entries = new List<AptPackageEntry>();
                    packagesByComponentArch[key] = entries;
                }

                entries.Add(new AptPackageEntry(packageName, version, architecture, fileName, fileInfo.Length, sha256, description));
            }
        }

        foreach (var kvp in packagesByComponentArch)
        {
            var component = kvp.Key.Component;
            var architecture = kvp.Key.Architecture;
            var entries = kvp.Value;
            var packagesDir = Path.Combine(repoRoot, "dists", suite, component, $"binary-{architecture}");
            Directory.CreateDirectory(packagesDir);
            var packagesPath = Path.Combine(packagesDir, "Packages");
            using var writer = new StreamWriter(packagesPath, false, Encoding.UTF8);
            foreach (var entry in entries)
            {
                writer.WriteLine($"Package: {entry.Name}");
                writer.WriteLine($"Version: {entry.Version}");
                writer.WriteLine($"Architecture: {entry.Architecture}");
                if (!string.IsNullOrWhiteSpace(entry.Description))
                {
                    writer.WriteLine($"Description: {entry.Description}");
                }
                writer.WriteLine($"Filename: pool/{component}/{entry.FileName}");
                writer.WriteLine($"Size: {entry.Size}");
                writer.WriteLine($"SHA256: {entry.Sha256}");
                writer.WriteLine();
            }
        }

        var architectures = packagesByComponentArch.Keys.Select(k => k.Architecture).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var releasePath = Path.Combine(repoRoot, "dists", suite, "Release");
        Directory.CreateDirectory(Path.GetDirectoryName(releasePath)!);
        var release = new StringBuilder();
        release.AppendLine("Origin: PackagingTools");
        release.AppendLine($"Suite: {suite}");
        release.AppendLine($"Components: {string.Join(' ', components)}");
        release.AppendLine($"Architectures: {string.Join(' ', architectures)}");
        release.AppendLine($"Date: {DateTimeOffset.UtcNow:R}");
        File.WriteAllText(releasePath, release.ToString());

        WriteTargetMetadata(repoRoot, target, credential, new
        {
            type = "apt",
            suite,
            components,
            architectures
        });

        return issues;
    }

    private IReadOnlyCollection<PackagingIssue> PublishYumRepository(PackageFormatContext context, PackagingResult result, RepositoryTarget target, RepositoryCredential? credential)
    {
        var issues = new List<PackagingIssue>();
        var rpmArtifacts = result.Artifacts.Where(a => string.Equals(a.Format, "rpm", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (rpmArtifacts.Length == 0)
        {
            issues.Add(new PackagingIssue(
                "linux.repo.yum.no_artifacts",
                $"Repository target '{target.Id}' expects RPM packages but none were produced.",
                PackagingIssueSeverity.Warning));
            return issues;
        }

        var repoRoot = Path.Combine(context.Request.OutputDirectory, "_Repo", target.Id, "yum");
        Directory.CreateDirectory(repoRoot);

        var packages = new List<object>();
        var architectures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var artifact in rpmArtifacts)
        {
            var fileInfo = new FileInfo(artifact.Path);
            if (!fileInfo.Exists)
            {
                issues.Add(new PackagingIssue(
                    "linux.repo.yum.artifact_missing",
                    $"Artifact '{artifact.Path}' could not be found for repository publishing.",
                    PackagingIssueSeverity.Warning));
                continue;
            }

            var packageName = GetMetadata(artifact.Metadata, "packageName") ?? Path.GetFileNameWithoutExtension(artifact.Path);
            var version = GetMetadata(artifact.Metadata, "packageVersion") ?? context.Project.Version;
            var architecture = GetMetadata(artifact.Metadata, "packageArchitecture");
            if (string.IsNullOrWhiteSpace(architecture) && context.Project.Metadata.TryGetValue("linux.architecture", out var projectArch))
            {
                architecture = projectArch;
            }
            architecture ??= "x86_64";
            var sha256 = ComputeSha256(artifact.Path);
            var fileName = Path.GetFileName(artifact.Path);
            architectures.Add(architecture);

            packages.Add(new
            {
                name = packageName,
                version,
                architecture,
                filename = fileName,
                size = fileInfo.Length,
                sha256
            });
        }

        var repodata = new
        {
            generated = DateTimeOffset.UtcNow,
            packages
        };

        var options = new JsonSerializerOptions { WriteIndented = true };
        File.WriteAllText(Path.Combine(repoRoot, "repodata.json"), JsonSerializer.Serialize(repodata, options));

        var baseUrl = target.Destination ?? $"file://{repoRoot.Replace(Path.DirectorySeparatorChar, '/')}";
        var repoFile = new StringBuilder();
        repoFile.AppendLine($"[{target.Id}]");
        repoFile.AppendLine($"name={target.Id}");
        repoFile.AppendLine($"baseurl={baseUrl}");
        repoFile.AppendLine("enabled=1");
        repoFile.AppendLine("gpgcheck=0");
        File.WriteAllText(Path.Combine(repoRoot, $"{target.Id}.repo"), repoFile.ToString());

        WriteTargetMetadata(repoRoot, target, credential, new
        {
            type = "yum",
            architectures = architectures.ToArray()
        });

        return issues;
    }

    private void WriteTargetMetadata(string repoRoot, RepositoryTarget target, RepositoryCredential? credential, object additional)
    {
        var options = new JsonSerializerOptions { WriteIndented = true };
        var metadata = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["id"] = target.Id,
            ["type"] = target.Type.ToString().ToLowerInvariant(),
            ["destination"] = target.Destination,
            ["additional"] = additional
        };

        if (credential is not null)
        {
            metadata["credential"] = new
            {
                credential.Id,
                credential.Type,
                properties = credential.Properties.Keys
            };
        }

        Directory.CreateDirectory(repoRoot);
        File.WriteAllText(Path.Combine(repoRoot, "target.json"), JsonSerializer.Serialize(metadata, options));
    }

    private static string? GetMetadata(IReadOnlyDictionary<string, string> metadata, string key)
        => metadata.TryGetValue(key, out var value) ? value : null;

    private static string? GetProperty(IReadOnlyDictionary<string, string>? properties, string key)
    {
        if (properties is null)
        {
            return null;
        }

        return properties.TryGetValue(key, out var value) ? value : null;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(stream);
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
        {
            builder.Append(b.ToString("x2"));
        }
        return builder.ToString();
    }

    private sealed record RepositoryTarget(
        string Id,
        RepositoryTargetType Type,
        string? Destination,
        string? Suite,
        IReadOnlyList<string> Components,
        string? CredentialId);

    private enum RepositoryTargetType
    {
        Apt,
        Yum
    }

    private sealed record AptPackageEntry(
        string Name,
        string Version,
        string Architecture,
        string FileName,
        long Size,
        string Sha256,
        string Description);
}
