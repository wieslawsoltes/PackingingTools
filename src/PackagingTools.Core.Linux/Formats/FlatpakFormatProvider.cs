using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Linux.Tooling;

namespace PackagingTools.Core.Linux.Formats;

public sealed class FlatpakFormatProvider : IPackageFormatProvider
{
    private readonly ILinuxProcessRunner _processRunner;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<FlatpakFormatProvider>? _logger;

    public FlatpakFormatProvider(ILinuxProcessRunner processRunner, ITelemetryChannel telemetry, ILogger<FlatpakFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "flatpak";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        var buildDir = Path.Combine(context.Request.OutputDirectory, "flatpak-build");
        Directory.CreateDirectory(buildDir);

        var manifestPath = ResolveManifestPath(context, issues);
        if (manifestPath is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var repoPath = context.Request.Properties?.TryGetValue("linux.flatpak.repo", out var repo) == true
            ? repo
            : Path.Combine(context.Request.OutputDirectory, "flatpak-repo");
        Directory.CreateDirectory(repoPath);

        var args = new List<string>
        {
            "--repo",
            repoPath,
            "--force-clean"
        };

        if (context.Project.Metadata.TryGetValue("linux.flatpak.gpgKey", out var gpgKey) && !string.IsNullOrWhiteSpace(gpgKey))
        {
            args.Add("--gpg-key");
            args.Add(gpgKey);
        }

        args.Add(buildDir);
        args.Add(manifestPath);

        var result = await _processRunner.ExecuteAsync(new LinuxProcessRequest("flatpak-builder", args), cancellationToken);

        if (!result.IsSuccess)
        {
            issues.Add(new PackagingIssue(
                "linux.flatpak.builder_failed",
                $"flatpak-builder failed with exit code {result.ExitCode}: {result.StandardError}",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var artifact = new PackagingArtifact(
            Format,
            repoPath,
            new Dictionary<string, string>
            {
                ["manifest"] = manifestPath
            });

        return new PackageFormatResult(new[] { artifact }, issues);
    }

    private static string? ResolveManifestPath(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("linux.flatpak.manifest", out var manifest) == true && File.Exists(manifest))
        {
            return manifest;
        }

        issues.Add(new PackagingIssue(
            "linux.flatpak.manifest_missing",
            "Property 'linux.flatpak.manifest' must reference an existing Flatpak manifest (JSON/YAML).",
            PackagingIssueSeverity.Error));
        return null;
    }
}
