using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Windows.Formats;

/// <summary>
/// Generates WinGet manifest YAML files for publishing to repositories.
/// </summary>
public sealed class WinGetManifestProvider : IPackageFormatProvider
{
    public string Format => "winget";

    public Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();

        var manifestDirectory = Path.Combine(context.Request.OutputDirectory, "winget");
        Directory.CreateDirectory(manifestDirectory);

        var packageIdentifier = context.Project.Metadata.TryGetValue("windows.winget.id", out var id)
            ? id
            : $"{NormalizePublisher(context)}.{NormalizeName(context.Project.Name)}";
        var installerType = DetermineInstallerType(context.Request.OutputDirectory);
        if (installerType is null)
        {
            issues.Add(new PackagingIssue(
                "windows.winget.installer_missing",
                "No MSIX or MSI installers found in output directory for WinGet manifest generation.",
                PackagingIssueSeverity.Error));
            return Task.FromResult(new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues));
        }

        var manifest = BuildManifest(context, packageIdentifier, installerType.Value);
        var fileName = Path.Combine(manifestDirectory, "manifest.yaml");
        File.WriteAllText(fileName, manifest, Encoding.UTF8);

        var artifact = new PackagingArtifact(
            Format,
            fileName,
            new Dictionary<string, string>
            {
                ["packageIdentifier"] = packageIdentifier,
                ["installerType"] = installerType.Value.type,
                ["installerPath"] = installerType.Value.path
            });

        return Task.FromResult(new PackageFormatResult(new[] { artifact }, issues));
    }

    private static string NormalizePublisher(PackageFormatContext context)
    {
        if (context.Project.Metadata.TryGetValue("windows.publisher", out var value))
        {
            return value.Replace(" ", string.Empty).Replace(".", string.Empty);
        }

        return "Contoso";
    }

    private static string NormalizeName(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
            }
        }

        return builder.Length > 0 ? builder.ToString() : "App";
    }

    private static (string type, string path)? DetermineInstallerType(string outputDirectory)
    {
        if (!Directory.Exists(outputDirectory))
        {
            return null;
        }

        var msix = Directory.EnumerateFiles(outputDirectory, "*.msix", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (msix is not null)
        {
            return ("msix", msix);
        }

        var msi = Directory.EnumerateFiles(outputDirectory, "*.msi", SearchOption.TopDirectoryOnly).FirstOrDefault();
        if (msi is not null)
        {
            return ("msi", msi);
        }

        return null;
    }

    private static string BuildManifest(PackageFormatContext context, string packageIdentifier, (string type, string path) installer)
    {
        var locale = context.Request.Properties?.TryGetValue("windows.winget.locale", out var localeValue) == true
            ? localeValue
            : "en-US";
        var channel = context.Request.Configuration;
        var sha256 = context.Request.Properties?.TryGetValue("windows.winget.sha256", out var hash) == true
            ? hash
            : "REPLACE_WITH_SHA256";

        var metadata = context.Project.Metadata;
        var publisher = metadata.TryGetValue("windows.publisher", out var publisherValue) ? publisherValue : "Contoso";
        var installerTypeUpper = installer.type.ToUpperInvariant();
        var installerUrl = installer.path.Replace("\\", "/");

        var sb = new StringBuilder();
        sb.AppendLine($"Id: {packageIdentifier}");
        sb.AppendLine($"Name: {context.Project.Name}");
        sb.AppendLine($"Publisher: {publisher}");
        sb.AppendLine($"Version: {context.Project.Version}");
        sb.AppendLine($"Channel: {channel}");
        sb.AppendLine($"Locale: {locale}");
        sb.AppendLine("Installers:");
        sb.AppendLine("  - InstallerType: " + installerTypeUpper);
        sb.AppendLine("    InstallerSha256: " + sha256);
        sb.AppendLine("    InstallerUrl: " + installerUrl);

        return sb.ToString();
    }
}
