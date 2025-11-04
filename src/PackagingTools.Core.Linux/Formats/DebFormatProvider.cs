using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Linux.Tooling;

namespace PackagingTools.Core.Linux.Formats;

/// <summary>
/// Generates Debian packages using dpkg-deb or fpm.
/// </summary>
public sealed class DebFormatProvider : IPackageFormatProvider
{
    private readonly ILinuxProcessRunner _processRunner;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<DebFormatProvider>? _logger;

    public DebFormatProvider(ILinuxProcessRunner processRunner, ITelemetryChannel telemetry, ILogger<DebFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "deb";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        var outputPath = Path.Combine(context.Request.OutputDirectory, $"{context.Project.Name}.deb");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var root = ResolvePackageRoot(context, "linux.deb.root", issues);
        if (root is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var args = new List<string>
        {
            "-s",
            "dir",
            "-t",
            "deb",
            "-n",
            context.Project.Name,
            "-v",
            context.Project.Version,
            "--architecture",
            context.Project.Metadata.TryGetValue("linux.architecture", out var arch) ? arch : "amd64",
            "--prefix",
            context.Project.Metadata.TryGetValue("linux.deb.prefix", out var prefix) ? prefix : $"/opt/{context.Project.Name}"
        };

        if (context.Project.Metadata.TryGetValue("linux.deb.description", out var description))
        {
            args.Add("--description");
            args.Add(description);
        }

        if (context.Project.Metadata.TryGetValue("linux.deb.maintainer", out var maintainer))
        {
            args.Add("--maintainer");
            args.Add(maintainer);
        }

        if (context.Project.Metadata.TryGetValue("linux.deb.dependencies", out var dependencies))
        {
            foreach (var dep in dependencies.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                args.Add("-d");
                args.Add(dep.Trim());
            }
        }

        args.Add("--output");
        args.Add(outputPath);
        args.Add(root);

        var result = await _processRunner.ExecuteAsync(new LinuxProcessRequest("fpm", args), cancellationToken);
        if (!result.IsSuccess)
        {
            issues.Add(new PackagingIssue(
                "linux.deb.fpm_failed",
                $"fpm failed with exit code {result.ExitCode}: {result.StandardError}",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var architecture = context.Project.Metadata.TryGetValue("linux.architecture", out var archValue) ? archValue : "amd64";
        var metadata = new Dictionary<string, string>
        {
            ["tool"] = "fpm",
            ["root"] = root,
            ["packageName"] = context.Project.Name,
            ["packageVersion"] = context.Project.Version,
            ["packageArchitecture"] = architecture
        };

        if (context.Project.Metadata.TryGetValue("linux.deb.description", out var descValue))
        {
            metadata["packageDescription"] = descValue;
        }

        var artifact = new PackagingArtifact(Format, outputPath, metadata);

        return new PackageFormatResult(new[] { artifact }, issues);
    }

    private static string? ResolvePackageRoot(PackageFormatContext context, string propertyName, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue(propertyName, out var specific) == true && Directory.Exists(specific))
        {
            return specific;
        }

        if (context.Request.Properties?.TryGetValue("linux.packageRoot", out var generic) == true && Directory.Exists(generic))
        {
            return generic;
        }

        issues.Add(new PackagingIssue(
            "linux.deb.root_missing",
            $"Property '{propertyName}' or 'linux.packageRoot' must reference an existing directory.",
            PackagingIssueSeverity.Error));
        return null;
    }
}
