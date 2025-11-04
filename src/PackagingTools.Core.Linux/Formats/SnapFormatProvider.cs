using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Linux.Tooling;

namespace PackagingTools.Core.Linux.Formats;

public sealed class SnapFormatProvider : IPackageFormatProvider
{
    private readonly ILinuxProcessRunner _processRunner;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<SnapFormatProvider>? _logger;

    public SnapFormatProvider(ILinuxProcessRunner processRunner, ITelemetryChannel telemetry, ILogger<SnapFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "snap";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        var snapDir = context.Request.OutputDirectory;
        Directory.CreateDirectory(snapDir);

        var manifestPath = ResolveManifest(context, issues);
        if (manifestPath is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var targetManifestPath = Path.Combine(snapDir, "snapcraft.yaml");
        File.Copy(manifestPath, targetManifestPath, overwrite: true);

        var args = new List<string> { "--destructive-mode" };
        if (context.Project.Metadata.TryGetValue("linux.snap.channel", out var channel))
        {
            args.Add("--channel");
            args.Add(channel);
        }

        var result = await _processRunner.ExecuteAsync(new LinuxProcessRequest(
            "snapcraft",
            args,
            WorkingDirectory: snapDir), cancellationToken);

        if (!result.IsSuccess)
        {
            issues.Add(new PackagingIssue(
                "linux.snap.snapcraft_failed",
                $"snapcraft failed with exit code {result.ExitCode}: {result.StandardError}",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var snapFile = context.Request.Properties?.TryGetValue("linux.snap.output", out var output) == true
            ? output
            : DetectSnapArtifact(snapDir);

        if (snapFile is null)
        {
            issues.Add(new PackagingIssue(
                "linux.snap.output_missing",
                "Unable to locate generated .snap artifact in output directory.",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var artifact = new PackagingArtifact(
            Format,
            snapFile,
            new Dictionary<string, string>
            {
                ["manifest"] = targetManifestPath
            });

        return new PackageFormatResult(new[] { artifact }, issues);
    }

    private static string? ResolveManifest(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("linux.snap.manifest", out var path) == true && File.Exists(path))
        {
            return path;
        }

        issues.Add(new PackagingIssue(
            "linux.snap.manifest_missing",
            "Property 'linux.snap.manifest' must reference an existing snapcraft.yaml.",
            PackagingIssueSeverity.Error));
        return null;
    }

    private static string? DetectSnapArtifact(string directory)
        => Directory.EnumerateFiles(directory, "*.snap", SearchOption.TopDirectoryOnly).FirstOrDefault();
}
