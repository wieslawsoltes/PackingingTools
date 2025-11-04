using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Windows.Formats;

/// <summary>
/// Generates App Installer XML manifests referencing MSIX artifacts.
/// </summary>
public sealed class AppInstallerFormatProvider : IPackageFormatProvider
{
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<AppInstallerFormatProvider>? _logger;

    public AppInstallerFormatProvider(ITelemetryChannel telemetry, ILogger<AppInstallerFormatProvider>? logger = null)
    {
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "appinstaller";

    public Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();

        var msixPath = ResolveMsixPath(context);
        if (msixPath is null)
        {
            issues.Add(new PackagingIssue(
                "windows.appinstaller.msix_missing",
                "Unable to locate an MSIX package in the output directory to reference.",
                PackagingIssueSeverity.Error));
            return Task.FromResult(new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues));
        }

        var appInstallerPath = Path.Combine(context.Request.OutputDirectory, $"{Path.GetFileNameWithoutExtension(msixPath)}.appinstaller");
        var updateUri = context.Request.Properties?.TryGetValue("windows.appinstaller.uri", out var uri) == true
            ? uri
            : msixPath;
        var metadata = context.Project.Metadata;
        var publisher = metadata.TryGetValue("windows.publisher", out var value) ? value : "CN=Contoso";

        var xml = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<AppInstaller Uri=""{updateUri}""
              Version=""{context.Project.Version}""
              xmlns=""http://schemas.microsoft.com/appx/appinstaller/2017/2""
              HoursBetweenUpdateChecks=""{ResolveUpdateCadence(context)}"">
  <MainPackage Name=""{context.Project.Name}""
               Version=""{context.Project.Version}""
               Publisher=""{publisher}""
               ProcessorArchitecture=""x64""
               Uri=""{msixPath}"" />
</AppInstaller>";

        File.WriteAllText(appInstallerPath, xml, Encoding.UTF8);

        var artifact = new PackagingArtifact(
            Format,
            appInstallerPath,
            new Dictionary<string, string>
            {
                ["msixPath"] = msixPath,
                ["updateUri"] = updateUri
            });

        return Task.FromResult(new PackageFormatResult(new[] { artifact }, issues));
    }

    private static string? ResolveMsixPath(PackageFormatContext context)
    {
        if (context.Request.Properties?.TryGetValue("windows.appinstaller.msixPath", out var configuredPath) == true && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        if (context.Request.Properties?.TryGetValue("windows.msix.path", out var explicitPath) == true && File.Exists(explicitPath))
        {
            return explicitPath;
        }

        var outputDir = context.Request.OutputDirectory;
        if (!Directory.Exists(outputDir))
        {
            return null;
        }

        return Directory.EnumerateFiles(outputDir, "*.msix", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(outputDir, "*.appx", SearchOption.TopDirectoryOnly))
            .FirstOrDefault();
    }

    private static string ResolveUpdateCadence(PackageFormatContext context)
    {
        if (context.Request.Properties?.TryGetValue("windows.appinstaller.hoursBetweenUpdates", out var cadence) == true)
        {
            return cadence;
        }

        return "24";
    }
}
