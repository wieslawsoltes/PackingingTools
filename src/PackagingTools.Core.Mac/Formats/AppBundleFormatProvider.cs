using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Mac.Signing;
using PackagingTools.Core.Mac.Tooling;
using PackagingTools.Core.Utilities;

namespace PackagingTools.Core.Mac.Formats;

/// <summary>
/// Produces signed .app bundles ready for distribution.
/// </summary>
public sealed class AppBundleFormatProvider : IPackageFormatProvider
{
    private readonly IMacProcessRunner _processRunner;
    private readonly ISigningService _signingService;
    private readonly ITelemetryChannel _telemetry;
    private readonly MacSigningMaterialService _signingMaterialService;
    private readonly ILogger<AppBundleFormatProvider>? _logger;

    public AppBundleFormatProvider(
        IMacProcessRunner processRunner,
        ISigningService signingService,
        ITelemetryChannel telemetry,
        MacSigningMaterialService signingMaterialService,
        ILogger<AppBundleFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _signingService = signingService;
        _telemetry = telemetry;
        _signingMaterialService = signingMaterialService;
        _logger = logger;
    }

    public string Format => "app";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();

        var sourceBundle = ResolveBundleSource(context, issues);
        if (sourceBundle is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var stagingBundle = Path.Combine(context.WorkingDirectory, $"{context.Project.Name}.app");
        DirectoryUtilities.CopyRecursive(sourceBundle, stagingBundle);

        var materials = await _signingMaterialService.PrepareAsync(context, cancellationToken);
        foreach (var issue in materials.Issues)
        {
            issues.Add(issue);
        }

        if (!materials.Success)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var bundleIdentifier = context.Project.Metadata.TryGetValue("mac.bundleId", out var id) ? id : "com.example.app";
        var artifact = new PackagingArtifact(
            Format,
            stagingBundle,
            new Dictionary<string, string>
            {
                ["bundleIdentifier"] = bundleIdentifier
            });

        var signingProperties = new Dictionary<string, string>
        {
            ["mac.signing.identity"] = context.Project.Metadata.TryGetValue("mac.signing.identity", out var identity) ? identity : string.Empty
        };
        if (!string.IsNullOrEmpty(materials.EntitlementsPath))
        {
            signingProperties["mac.signing.entitlements"] = materials.EntitlementsPath;
        }
        else if (context.Project.Metadata.TryGetValue("mac.signing.entitlements", out var entitlements))
        {
            signingProperties["mac.signing.entitlements"] = entitlements;
        }

        if (!string.IsNullOrEmpty(materials.ProvisioningProfilePath))
        {
            EmbedProvisioningProfile(stagingBundle, materials.ProvisioningProfilePath);
        }

        var signingResult = await _signingService.SignAsync(new SigningRequest(artifact, Format, signingProperties), cancellationToken);
        issues.AddRange(signingResult.Issues);
        if (!signingResult.Success)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var outputBundle = Path.Combine(context.Request.OutputDirectory, $"{context.Project.Name}.app");
        if (Directory.Exists(outputBundle))
        {
            Directory.Delete(outputBundle, true);
        }
        DirectoryUtilities.CopyRecursive(stagingBundle, outputBundle);

        var finalMetadata = new Dictionary<string, string>(artifact.Metadata);
        if (context.Project.Metadata.TryGetValue(MacSigningMaterialService.EntitlementsMetadataKey, out var entitlementsEntryId))
        {
            finalMetadata["entitlementsEntryId"] = entitlementsEntryId;
        }
        if (context.Project.Metadata.TryGetValue(MacSigningMaterialService.ProvisioningMetadataKey, out var provisioningEntryId))
        {
            finalMetadata["provisioningProfileEntryId"] = provisioningEntryId;
        }

        var finalArtifact = new PackagingArtifact(Format, outputBundle, finalMetadata);
        return new PackageFormatResult(new[] { finalArtifact }, issues);
    }

    private static void EmbedProvisioningProfile(string bundlePath, string provisioningProfilePath)
    {
        var contentsDir = Path.Combine(bundlePath, "Contents");
        Directory.CreateDirectory(contentsDir);
        var destination = Path.Combine(contentsDir, "embedded.provisionprofile");
        File.Copy(provisioningProfilePath, destination, overwrite: true);
    }

    private static string? ResolveBundleSource(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("mac.app.bundleSource", out var source) == true && Directory.Exists(source))
        {
            return source;
        }

        issues.Add(new PackagingIssue(
            "mac.app.source_missing",
            "Property 'mac.app.bundleSource' must point to an existing .app bundle template.",
            PackagingIssueSeverity.Error));
        return null;
    }
}
