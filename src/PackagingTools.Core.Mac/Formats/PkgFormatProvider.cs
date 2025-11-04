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
/// Builds installer PKG using productbuild.
/// </summary>
public sealed class PkgFormatProvider : IPackageFormatProvider
{
    private readonly IMacProcessRunner _processRunner;
    private readonly ISigningService _signingService;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<PkgFormatProvider>? _logger;

    public PkgFormatProvider(
        IMacProcessRunner processRunner,
        ISigningService signingService,
        ITelemetryChannel telemetry,
        ILogger<PkgFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _signingService = signingService;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "pkg";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        var pkgPath = Path.Combine(context.Request.OutputDirectory, $"{context.Project.Name}.pkg");
        Directory.CreateDirectory(Path.GetDirectoryName(pkgPath)!);

        var componentPath = ResolveComponentPath(context, issues);
        if (componentPath is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var installLocation = context.Project.Metadata.TryGetValue("mac.pkg.installLocation", out var location)
            ? location
            : "/Applications";

        var args = new List<string>
        {
            "--identifier",
            context.Project.Metadata.TryGetValue("mac.bundleId", out var id) ? id : "com.example.app",
            "--version",
            context.Project.Version,
            "--component",
            componentPath,
            installLocation
        };

        if (context.Project.Metadata.TryGetValue("mac.pkg.resources", out var resources) && Directory.Exists(resources))
        {
            args.Add("--resources");
            args.Add(resources);
        }

        if (context.Project.Metadata.TryGetValue("mac.pkg.scripts", out var scripts) && Directory.Exists(scripts))
        {
            args.Add("--scripts");
            args.Add(scripts);
        }

        if (context.Project.Metadata.TryGetValue("mac.pkg.signingIdentity", out var pkgIdentity) && !string.IsNullOrWhiteSpace(pkgIdentity))
        {
            args.Add("--sign");
            args.Add(pkgIdentity);
        }

        args.Add(pkgPath);

        var productbuild = await _processRunner.ExecuteAsync(new MacProcessRequest("productbuild", args), cancellationToken);

        if (!productbuild.IsSuccess)
        {
            issues.Add(new PackagingIssue(
                "mac.pkg.productbuild_failed",
                $"productbuild failed with exit code {productbuild.ExitCode}: {productbuild.StandardError}",
                PackagingIssueSeverity.Error));
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var artifact = new PackagingArtifact(
            Format,
            pkgPath,
            new Dictionary<string, string>
            {
                ["identifier"] = context.Project.Metadata.TryGetValue("mac.bundleId", out var bundleId) ? bundleId : "com.example.app"
            });

        var signingResult = await _signingService.SignAsync(new SigningRequest(artifact, Format, context.Request.Properties), cancellationToken);
        issues.AddRange(signingResult.Issues);
        if (!signingResult.Success)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        return new PackageFormatResult(new[] { artifact }, issues);
    }

    private static string? ResolveComponentPath(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("mac.pkg.component", out var component) == true && Directory.Exists(component))
        {
            return component;
        }

        var candidate = Path.Combine(context.Request.OutputDirectory, $"{context.Project.Name}.app");
        if (Directory.Exists(candidate))
        {
            return candidate;
        }

        issues.Add(new PackagingIssue(
            "mac.pkg.component_missing",
            "Property 'mac.pkg.component' must point to a built .app bundle.",
            PackagingIssueSeverity.Error));
        return null;
    }
}
