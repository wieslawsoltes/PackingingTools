using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Mac.Tooling;

namespace PackagingTools.Core.Mac.Formats;

/// <summary>
/// Creates DMG images for distribution using hdiutil.
/// </summary>
public sealed class DmgFormatProvider : IPackageFormatProvider
{
    private readonly IMacProcessRunner _processRunner;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<DmgFormatProvider>? _logger;

    public DmgFormatProvider(IMacProcessRunner processRunner, ITelemetryChannel telemetry, ILogger<DmgFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "dmg";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        var dmgPath = Path.Combine(context.Request.OutputDirectory, $"{context.Project.Name}.dmg");
        Directory.CreateDirectory(Path.GetDirectoryName(dmgPath)!);

        var sourceDirectory = ResolveSourceDirectory(context, issues);
        if (sourceDirectory is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var args = new List<string>
        {
            "create",
            "-fs",
            context.Project.Metadata.TryGetValue("mac.dmg.filesystem", out var fs) ? fs : "APFS",
            "-volname",
            context.Project.Name
        };

        if (context.Project.Metadata.TryGetValue("mac.dmg.size", out var size) && !string.IsNullOrWhiteSpace(size))
        {
            args.Add("-size");
            args.Add(size);
        }

        args.Add(dmgPath);
        args.Add("-srcfolder");
        args.Add(sourceDirectory);

        var hdiutil = await _processRunner.ExecuteAsync(new MacProcessRequest("hdiutil", args), cancellationToken);

        if (!hdiutil.IsSuccess)
        {
            issues.Add(new PackagingIssue(
                "mac.dmg.hdiutil_failed",
                $"hdiutil failed with exit code {hdiutil.ExitCode}: {hdiutil.StandardError}",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var artifact = new PackagingArtifact(
            Format,
            dmgPath,
            new Dictionary<string, string>
            {
                ["volumeName"] = context.Project.Name
            });

        return new PackageFormatResult(new[] { artifact }, issues);
    }

    private static string? ResolveSourceDirectory(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("mac.dmg.sourceDirectory", out var directory) == true && Directory.Exists(directory))
        {
            return directory;
        }

        var fallback = Path.Combine(context.Request.OutputDirectory, $"{context.Project.Name}.app");
        if (Directory.Exists(fallback))
        {
            return fallback;
        }

        issues.Add(new PackagingIssue(
            "mac.dmg.source_missing",
            "Property 'mac.dmg.sourceDirectory' must point to staged bundle content.",
            PackagingIssueSeverity.Error));
        return null;
    }
}
