using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Windows.Tooling;
using PackagingTools.Core.Utilities;

namespace PackagingTools.Core.Windows.Formats;

/// <summary>
/// Builds MSIX packages using the Windows SDK toolchain.
/// </summary>
public sealed class MsixPackageFormatProvider : IPackageFormatProvider
{
    private readonly IProcessRunner _processRunner;
    private readonly ISigningService _signingService;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<MsixPackageFormatProvider>? _logger;

    public MsixPackageFormatProvider(
        IProcessRunner processRunner,
        ISigningService signingService,
        ITelemetryChannel telemetry,
        ILogger<MsixPackageFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _signingService = signingService;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "msix";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var artifacts = new List<PackagingArtifact>();
        var issues = new List<PackagingIssue>();

        var stagingDir = Path.Combine(context.WorkingDirectory, "msix", "staging");
        Directory.CreateDirectory(stagingDir);

        var payloadSource = ResolvePayloadSource(context, issues);
        if (payloadSource is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var payloadDir = Path.Combine(stagingDir, "App");
        DirectoryUtilities.CopyRecursive(payloadSource, payloadDir);

        // Step 1: materialize manifest
        var manifestPath = Path.Combine(stagingDir, "AppxManifest.xml");
        issues.AddRange(await TryWriteManifestAsync(context, manifestPath, payloadDir, cancellationToken));

        // Step 3: run makeappx
        var outputFile = Path.Combine(context.Request.OutputDirectory, $"{SanitizeFileName(context.Project.Name)}.msix");
        Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);

        var processRequest = new ProcessExecutionRequest(
            FileName: "makeappx.exe",
            Arguments: $"pack /d \"{stagingDir}\" /p \"{outputFile}\" /o",
            WorkingDirectory: context.WorkingDirectory);

        var result = await _processRunner.ExecuteAsync(processRequest, cancellationToken);
        if (!result.IsSuccess)
        {
            RecordToolFailure(context, issues, "windows.msix.makeappx_failed", processRequest, result);
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        // Step 4: sign package
        var artifact = new PackagingArtifact(
            Format,
            outputFile,
            new Dictionary<string, string>
            {
                ["stagingPath"] = stagingDir,
                ["makeappxOutput"] = result.StandardOutput
            });

        var signing = await _signingService.SignAsync(new SigningRequest(artifact, Format, context.Request.Properties), cancellationToken);
        issues.AddRange(signing.Issues);

        if (!signing.Success)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        artifacts.Add(artifact);
        return new PackageFormatResult(artifacts, issues);
    }

    private void RecordToolFailure(
        PackageFormatContext context,
        List<PackagingIssue> issues,
        string issueCode,
        ProcessExecutionRequest request,
        ProcessExecutionResult result)
    {
        var toolName = Path.GetFileName(request.FileName);
        var logPath = ProcessDiagnosticsWriter.TryWriteProcessLog(
            context.Request.OutputDirectory,
            $"{Format}-{toolName}",
            request,
            result);

        var builder = new StringBuilder();
        builder.Append($"{toolName} failed with exit code {result.ExitCode}.");

        var errorPreview = Truncate(result.StandardError);
        if (!string.IsNullOrWhiteSpace(errorPreview))
        {
            builder.Append(' ');
            builder.Append(errorPreview);
            if (!errorPreview.EndsWith("...", StringComparison.Ordinal))
            {
                builder.Append('.');
            }
        }

        if (!string.IsNullOrEmpty(logPath))
        {
            builder.Append($" See '{logPath}' for details.");
        }

        issues.Add(new PackagingIssue(issueCode, builder.ToString(), PackagingIssueSeverity.Error));

        var telemetryProperties = new Dictionary<string, object?>
        {
            ["tool"] = toolName,
            ["exitCode"] = result.ExitCode,
            ["projectId"] = context.Project.Id,
            ["format"] = Format,
            ["logPath"] = logPath,
            ["workingDirectory"] = request.WorkingDirectory,
            ["stderrPreview"] = Truncate(result.StandardError),
            ["stdoutPreview"] = Truncate(result.StandardOutput)
        };

        _telemetry.TrackEvent("windows.tool.failure", telemetryProperties);
        _logger?.LogError("Tool {Tool} failed with exit code {ExitCode}. Diagnostics: {LogPath}", toolName, result.ExitCode, logPath ?? "<not captured>");
    }

    private static string? Truncate(string? value, int maxLength = 512)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= maxLength)
        {
            return trimmed;
        }

        return $"{trimmed[..maxLength]}...";
    }

    private async Task<IReadOnlyCollection<PackagingIssue>> TryWriteManifestAsync(PackageFormatContext context, string manifestPath, string payloadDirectory, CancellationToken cancellationToken)
    {
        var issues = new List<PackagingIssue>();
        var metadata = context.Project.Metadata;

        var identityName = TryGetMetadata(metadata, "windows.identityName", context.Project.Name, issues);
        var publisher = TryGetMetadata(metadata, "windows.publisher", "CN=Contoso", issues);
        var displayName = TryGetMetadata(metadata, "windows.displayName", context.Project.Name, issues);
        var version = metadata.TryGetValue("windows.version", out var manifestVersion) ? manifestVersion : context.Project.Version;
        var executable = metadata.TryGetValue("windows.msix.executable", out var exec) ? exec : "App.exe";
        var entryPoint = metadata.TryGetValue("windows.msix.entryPoint", out var entry) ? entry : "App.App";
        var logoPath = metadata.TryGetValue("windows.msix.logo", out var logo) ? logo : "Assets\\Square150x150Logo.png";

        EnsureAssetsIfPresent(payloadDirectory, logoPath, issues);

        var manifest = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10""
         xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10""
         IgnorableNamespaces=""uap"">
  <Identity Name=""{identityName}"" Publisher=""{publisher}"" Version=""{version}"" />
  <Properties>
    <DisplayName>{displayName}</DisplayName>
    <PublisherDisplayName>{publisher}</PublisherDisplayName>
    <Logo>{logoPath}</Logo>
  </Properties>
  <Dependencies>
    <TargetDeviceFamily Name=""Windows.Desktop"" MinVersion=""10.0.19041.0"" MaxVersionTested=""10.0.26100.0"" />
  </Dependencies>
  <Resources>
    <Resource Language=""en-us"" />
  </Resources>
  <Applications>
    <Application Id=""App"" Executable=""App\{executable}"" EntryPoint=""{entryPoint}"">
      <uap:VisualElements DisplayName=""{displayName}"" Square150x150Logo=""{logoPath}"" Description=""{displayName}"" />
    </Application>
  </Applications>
</Package>";

        await using var stream = new FileStream(manifestPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await using var writer = new StreamWriter(stream, Encoding.UTF8);
        await writer.WriteAsync(manifest.AsMemory(), cancellationToken);

        return issues;
    }

    private static string TryGetMetadata(IReadOnlyDictionary<string, string> metadata, string key, string fallback, ICollection<PackagingIssue> issues)
    {
        if (!metadata.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            issues.Add(new PackagingIssue(
                $"windows.msix.metadata_missing.{key}",
                $"Metadata '{key}' not found. Using fallback value '{fallback}'.",
                PackagingIssueSeverity.Warning));
            return fallback;
        }

        return value;
    }

    private static string? ResolvePayloadSource(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("windows.msix.payloadDirectory", out var payload) == true && !string.IsNullOrWhiteSpace(payload))
        {
            if (!Directory.Exists(payload))
            {
                issues.Add(new PackagingIssue(
                    "windows.msix.payload_missing",
                    $"Configured payload directory '{payload}' does not exist.",
                    PackagingIssueSeverity.Error));
                return null;
            }

            return payload;
        }

        issues.Add(new PackagingIssue(
            "windows.msix.payload_unset",
            "Property 'windows.msix.payloadDirectory' is required to stage MSIX payloads.",
            PackagingIssueSeverity.Error));
        return null;
    }

    private static void EnsureAssetsIfPresent(string payloadDirectory, string logoPath, ICollection<PackagingIssue> issues)
    {
        var assetFullPath = Path.Combine(payloadDirectory, logoPath.Replace('\\', Path.DirectorySeparatorChar));
        if (!File.Exists(assetFullPath))
        {
            issues.Add(new PackagingIssue(
                "windows.msix.logo_missing",
                $"Expected logo asset '{logoPath}' was not found in payload directory.",
                PackagingIssueSeverity.Warning));
        }
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }
}
