using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Linux.Tooling;

namespace PackagingTools.Core.Linux.Formats;

public sealed class AppImageFormatProvider : IPackageFormatProvider
{
    private readonly ILinuxProcessRunner _processRunner;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<AppImageFormatProvider>? _logger;

    public AppImageFormatProvider(ILinuxProcessRunner processRunner, ITelemetryChannel telemetry, ILogger<AppImageFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "appimage";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        var outputPath = Path.Combine(context.Request.OutputDirectory, $"{context.Project.Name}.AppImage");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var appDir = ResolveAppDir(context, issues);
        if (appDir is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var result = await _processRunner.ExecuteAsync(new LinuxProcessRequest(
            "appimagetool",
            new[]
            {
                appDir,
                outputPath
            }), cancellationToken);

        if (!result.IsSuccess)
        {
            issues.Add(new PackagingIssue(
                "linux.appimage.tool_failed",
                $"appimagetool failed with exit code {result.ExitCode}: {result.StandardError}",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var artifact = new PackagingArtifact(
            Format,
            outputPath,
            new Dictionary<string, string>
            {
                ["tool"] = "appimagetool",
                ["appDir"] = appDir
            });

        return new PackageFormatResult(new[] { artifact }, issues);
    }

    private static string? ResolveAppDir(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("linux.appimage.appDir", out var appDir) == true && Directory.Exists(appDir))
        {
            return appDir;
        }

        issues.Add(new PackagingIssue(
            "linux.appimage.appdir_missing",
            "Property 'linux.appimage.appDir' must reference an AppDir structure.",
            PackagingIssueSeverity.Error));
        return null;
    }
}
