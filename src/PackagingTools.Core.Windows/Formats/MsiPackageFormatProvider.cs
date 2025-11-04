using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;
using PackagingTools.Core.Windows.Tooling;
using PackagingTools.Core.Utilities;

namespace PackagingTools.Core.Windows.Formats;

/// <summary>
/// Builds Windows Installer (MSI) packages using the WiX toolset.
/// </summary>
public sealed class MsiPackageFormatProvider : IPackageFormatProvider
{
    private readonly IProcessRunner _processRunner;
    private readonly ISigningService _signingService;
    private readonly ITelemetryChannel _telemetry;
    private readonly ILogger<MsiPackageFormatProvider>? _logger;

    public MsiPackageFormatProvider(
        IProcessRunner processRunner,
        ISigningService signingService,
        ITelemetryChannel telemetry,
        ILogger<MsiPackageFormatProvider>? logger = null)
    {
        _processRunner = processRunner;
        _signingService = signingService;
        _telemetry = telemetry;
        _logger = logger;
    }

    public string Format => "msi";

    public async Task<PackageFormatResult> PackageAsync(PackageFormatContext context, CancellationToken cancellationToken = default)
    {
        var artifacts = new List<PackagingArtifact>();
        var issues = new List<PackagingIssue>();

        var sourceRoot = ResolvePayloadSource(context, issues);
        if (sourceRoot is null)
        {
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var wixDir = Path.Combine(context.WorkingDirectory, "msi");
        Directory.CreateDirectory(wixDir);

        var payloadDir = Path.Combine(wixDir, "payload");
        DirectoryUtilities.CopyRecursive(sourceRoot, payloadDir);

        var shortcut = BuildShortcutConfiguration(context, payloadDir, issues);
        var protocol = BuildProtocolConfiguration(context, payloadDir, issues);
        var shellExtension = BuildShellExtensionConfiguration(context, payloadDir, issues);

        var harvestedPath = Path.Combine(wixDir, "Harvested.wxs");
        var heatRequest = new ProcessExecutionRequest(
            FileName: "heat.exe",
            Arguments: $"dir \"{payloadDir}\" -cg ProductComponents -dr INSTALLFOLDER -nologo -scom -sreg -srd -gg -out Harvested.wxs",
            WorkingDirectory: wixDir);
        var heat = await _processRunner.ExecuteAsync(heatRequest, cancellationToken);

        if (!heat.IsSuccess)
        {
            RecordToolFailure(context, issues, "windows.msi.heat_failed", heatRequest, heat);
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var sourcePath = Path.Combine(wixDir, "Product.wxs");
        await WriteWixSourceAsync(context, sourcePath, shortcut, protocol, shellExtension, cancellationToken);

        var wixObj = Path.Combine(wixDir, "Product.wixobj");
        var candleRequest = new ProcessExecutionRequest(
            FileName: "candle.exe",
            Arguments: $"-nologo -o \"{wixObj}\" \"{sourcePath}\" \"{harvestedPath}\"",
            WorkingDirectory: wixDir);
        var candle = await _processRunner.ExecuteAsync(candleRequest, cancellationToken);

        if (!candle.IsSuccess)
        {
            RecordToolFailure(context, issues, "windows.msi.candle_failed", candleRequest, candle);
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        var outputPath = Path.Combine(context.Request.OutputDirectory, $"{SanitizeFileName(context.Project.Name)}.msi");
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);

        var lightRequest = new ProcessExecutionRequest(
            FileName: "light.exe",
            Arguments: $"-nologo -spdb -o \"{outputPath}\" \"{wixObj}\"",
            WorkingDirectory: wixDir);
        var light = await _processRunner.ExecuteAsync(lightRequest, cancellationToken);

        if (!light.IsSuccess)
        {
            RecordToolFailure(context, issues, "windows.msi.light_failed", lightRequest, light);
            return new PackageFormatResult(Array.Empty<PackagingArtifact>(), issues);
        }

        Directory.CreateDirectory(context.Request.OutputDirectory);
        var persistedSourcePath = Path.Combine(context.Request.OutputDirectory, Path.GetFileName(sourcePath));
        File.Copy(sourcePath, persistedSourcePath, overwrite: true);
        var persistedHarvestedPath = Path.Combine(context.Request.OutputDirectory, Path.GetFileName(harvestedPath));
        File.Copy(harvestedPath, persistedHarvestedPath, overwrite: true);

        var metadata = new Dictionary<string, string>
        {
            ["wixSource"] = persistedSourcePath,
            ["harvested"] = persistedHarvestedPath
        };

        if (shortcut is not null)
        {
            metadata["shortcutName"] = shortcut.Name;
        }
        if (protocol is not null)
        {
            metadata["protocol"] = protocol.Name;
        }
        if (shellExtension is not null)
        {
            metadata["shellExtension"] = shellExtension.Extension;
        }

        var artifact = new PackagingArtifact(
            Format,
            outputPath,
            metadata);

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

        var messageBuilder = new StringBuilder();
        messageBuilder.Append($"{toolName} failed with exit code {result.ExitCode}.");

        var errorPreview = Truncate(result.StandardError, 320);
        if (!string.IsNullOrWhiteSpace(errorPreview))
        {
            messageBuilder.Append(' ');
            messageBuilder.Append(errorPreview);
            if (!errorPreview.EndsWith("...", StringComparison.Ordinal))
            {
                messageBuilder.Append('.');
            }
        }

        if (!string.IsNullOrEmpty(logPath))
        {
            messageBuilder.Append($" See '{logPath}' for details.");
        }

        issues.Add(new PackagingIssue(issueCode, messageBuilder.ToString(), PackagingIssueSeverity.Error));

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

    private static async Task WriteWixSourceAsync(
        PackageFormatContext context,
        string sourcePath,
        ShortcutConfiguration? shortcut,
        ProtocolConfiguration? protocol,
        ShellExtensionConfiguration? shellExtension,
        CancellationToken cancellationToken)
    {
        var metadata = context.Project.Metadata;
        var productCode = metadata.TryGetValue("windows.msi.productCode", out var pc) && !string.IsNullOrWhiteSpace(pc)
            ? pc!
            : Guid.NewGuid().ToString("B");
        var upgradeCode = metadata.TryGetValue("windows.msi.upgradeCode", out var uc) && !string.IsNullOrWhiteSpace(uc)
            ? uc!
            : Guid.NewGuid().ToString("B");
        var manufacturer = metadata.TryGetValue("windows.publisher", out var publisher) && !string.IsNullOrWhiteSpace(publisher)
            ? publisher!
            : "Contoso";
        var productName = context.Project.Name;
        var version = metadata.TryGetValue("windows.msi.productVersion", out var overriddenVersion) && !string.IsNullOrWhiteSpace(overriddenVersion)
            ? overriddenVersion!
            : context.Project.Version;
        var sanitizedName = SanitizeDirectoryName(productName);

        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);

        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            Indent = true,
            OmitXmlDeclaration = false
        };

        await using var stream = new FileStream(sourcePath, FileMode.Create, FileAccess.Write, FileShare.None);
        using var writer = XmlWriter.Create(stream, settings);

        writer.WriteStartDocument();
        writer.WriteStartElement("Wix", "http://wixtoolset.org/schemas/v4/wxs");

        writer.WriteStartElement("Package");
        writer.WriteAttributeString("Name", productName);
        writer.WriteAttributeString("Manufacturer", manufacturer);
        writer.WriteAttributeString("Version", version);
        writer.WriteAttributeString("ProductCode", productCode);
        writer.WriteAttributeString("UpgradeCode", upgradeCode);

        writer.WriteStartElement("MajorUpgrade");
        writer.WriteAttributeString("DowngradeErrorMessage", $"A newer version of {productName} is already installed.");
        writer.WriteEndElement();

        writer.WriteStartElement("MediaTemplate");
        writer.WriteEndElement();

        writer.WriteStartElement("Feature");
        writer.WriteAttributeString("Id", "ProductFeature");
        writer.WriteAttributeString("Title", productName);
        writer.WriteAttributeString("Level", "1");

        writer.WriteStartElement("ComponentGroupRef");
        writer.WriteAttributeString("Id", "ProductComponents");
        writer.WriteEndElement();

        if (shortcut is not null)
        {
            writer.WriteStartElement("ComponentGroupRef");
            writer.WriteAttributeString("Id", "ProductShortcuts");
            writer.WriteEndElement();
        }

        if (protocol is not null)
        {
            writer.WriteStartElement("ComponentGroupRef");
            writer.WriteAttributeString("Id", "ProductProtocol");
            writer.WriteEndElement();
        }

        if (shellExtension is not null)
        {
            writer.WriteStartElement("ComponentGroupRef");
            writer.WriteAttributeString("Id", "ProductFileAssociations");
            writer.WriteEndElement();
        }

        writer.WriteEndElement(); // Feature
        writer.WriteEndElement(); // Package

        writer.WriteStartElement("Fragment");
        writer.WriteStartElement("Directory");
        writer.WriteAttributeString("Id", "TARGETDIR");
        writer.WriteAttributeString("Name", "SourceDir");

        writer.WriteStartElement("Directory");
        writer.WriteAttributeString("Id", "ProgramFilesFolder");

        writer.WriteStartElement("Directory");
        writer.WriteAttributeString("Id", "INSTALLFOLDER");
        writer.WriteAttributeString("Name", sanitizedName);
        writer.WriteEndElement(); // INSTALLFOLDER

        writer.WriteEndElement(); // ProgramFilesFolder

        if (shortcut is not null)
        {
            writer.WriteStartElement("Directory");
            writer.WriteAttributeString("Id", "ProgramMenuFolder");

            writer.WriteStartElement("Directory");
            writer.WriteAttributeString("Id", "ApplicationShortcutFolder");
            writer.WriteAttributeString("Name", sanitizedName);
            writer.WriteEndElement(); // ApplicationShortcutFolder

            writer.WriteEndElement(); // ProgramMenuFolder
        }

        writer.WriteEndElement(); // TARGETDIR
        writer.WriteEndElement(); // Fragment

        if (shortcut is not null)
        {
            writer.WriteStartElement("Fragment");
            writer.WriteStartElement("DirectoryRef");
            writer.WriteAttributeString("Id", "ApplicationShortcutFolder");

            writer.WriteStartElement("Component");
            writer.WriteAttributeString("Id", "ApplicationShortcutComponent");
            writer.WriteAttributeString("Guid", "*");

            writer.WriteStartElement("Shortcut");
            writer.WriteAttributeString("Id", "ApplicationShortcut");
            writer.WriteAttributeString("Name", shortcut.Name);
            writer.WriteAttributeString("Target", $"[INSTALLFOLDER]{shortcut.Target}");
            writer.WriteAttributeString("WorkingDirectory", "INSTALLFOLDER");
            if (!string.IsNullOrWhiteSpace(shortcut.Description))
            {
                writer.WriteAttributeString("Description", shortcut.Description);
            }

            if (!string.IsNullOrWhiteSpace(shortcut.Icon))
            {
                writer.WriteAttributeString("Icon", $"[INSTALLFOLDER]{shortcut.Icon}");
            }

            writer.WriteEndElement(); // Shortcut

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Root", "HKCU");
            writer.WriteAttributeString("Key", $@"Software\\PackagingTools\\{sanitizedName}");
            writer.WriteAttributeString("Name", "installed");
            writer.WriteAttributeString("Type", "integer");
            writer.WriteAttributeString("Value", "1");
            writer.WriteAttributeString("KeyPath", "yes");
            writer.WriteEndElement(); // RegistryValue

            writer.WriteEndElement(); // Component
            writer.WriteEndElement(); // DirectoryRef
            writer.WriteEndElement(); // Fragment

            writer.WriteStartElement("Fragment");
            writer.WriteStartElement("ComponentGroup");
            writer.WriteAttributeString("Id", "ProductShortcuts");
            writer.WriteStartElement("ComponentRef");
            writer.WriteAttributeString("Id", "ApplicationShortcutComponent");
            writer.WriteEndElement(); // ComponentRef
            writer.WriteEndElement(); // ComponentGroup
            writer.WriteEndElement(); // Fragment
        }

        if (protocol is not null)
        {
            var commandValue = FormatInstalledCommand(protocol.Command);

            writer.WriteStartElement("Fragment");
            writer.WriteStartElement("DirectoryRef");
            writer.WriteAttributeString("Id", "TARGETDIR");

            writer.WriteStartElement("Component");
            writer.WriteAttributeString("Id", "ProtocolComponent");
            writer.WriteAttributeString("Guid", "*");

            writer.WriteStartElement("RegistryKey");
            writer.WriteAttributeString("Root", "HKCR");
            writer.WriteAttributeString("Key", protocol.Name);

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Type", "string");
            writer.WriteAttributeString("Value", protocol.DisplayName);
            writer.WriteAttributeString("KeyPath", "yes");
            writer.WriteEndElement(); // RegistryValue

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Name", "URL Protocol");
            writer.WriteAttributeString("Type", "string");
            writer.WriteAttributeString("Value", string.Empty);
            writer.WriteEndElement(); // RegistryValue URL Protocol

            writer.WriteStartElement("RegistryKey");
            writer.WriteAttributeString("Key", "shell/open/command");

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Type", "string");
            writer.WriteAttributeString("Value", commandValue);
            writer.WriteEndElement(); // RegistryValue command

            writer.WriteEndElement(); // RegistryKey shell/open/command
            writer.WriteEndElement(); // RegistryKey protocol.Name

            writer.WriteEndElement(); // Component
            writer.WriteEndElement(); // DirectoryRef
            writer.WriteEndElement(); // Fragment

            writer.WriteStartElement("Fragment");
            writer.WriteStartElement("ComponentGroup");
            writer.WriteAttributeString("Id", "ProductProtocol");
            writer.WriteStartElement("ComponentRef");
            writer.WriteAttributeString("Id", "ProtocolComponent");
            writer.WriteEndElement(); // ComponentRef
            writer.WriteEndElement(); // ComponentGroup
            writer.WriteEndElement(); // Fragment
        }

        if (shellExtension is not null)
        {
            var commandValue = FormatInstalledCommand(shellExtension.Command);

            writer.WriteStartElement("Fragment");
            writer.WriteStartElement("DirectoryRef");
            writer.WriteAttributeString("Id", "TARGETDIR");

            writer.WriteStartElement("Component");
            writer.WriteAttributeString("Id", "FileAssociationComponent");
            writer.WriteAttributeString("Guid", "*");

            writer.WriteStartElement("RegistryKey");
            writer.WriteAttributeString("Root", "HKCR");
            writer.WriteAttributeString("Key", shellExtension.Extension);

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Type", "string");
            writer.WriteAttributeString("Value", shellExtension.ProgId);
            writer.WriteEndElement(); // RegistryValue extension -> ProgId

            writer.WriteEndElement(); // RegistryKey extension

            writer.WriteStartElement("RegistryKey");
            writer.WriteAttributeString("Root", "HKCR");
            writer.WriteAttributeString("Key", shellExtension.ProgId);

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Type", "string");
            writer.WriteAttributeString("Value", shellExtension.Description);
            writer.WriteEndElement(); // RegistryValue description

            writer.WriteStartElement("RegistryKey");
            writer.WriteAttributeString("Key", "shell/open/command");

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Type", "string");
            writer.WriteAttributeString("Value", commandValue);
            writer.WriteEndElement(); // RegistryValue command

            writer.WriteEndElement(); // RegistryKey shell/open/command
            writer.WriteEndElement(); // RegistryKey ProgId

            writer.WriteStartElement("RegistryValue");
            writer.WriteAttributeString("Root", "HKCU");
            writer.WriteAttributeString("Key", $@"Software\\PackagingTools\\{sanitizedName}");
            writer.WriteAttributeString("Name", "fileAssociation");
            writer.WriteAttributeString("Type", "integer");
            writer.WriteAttributeString("Value", "1");
            writer.WriteAttributeString("KeyPath", "yes");
            writer.WriteEndElement(); // RegistryValue marker

            writer.WriteEndElement(); // Component
            writer.WriteEndElement(); // DirectoryRef
            writer.WriteEndElement(); // Fragment

            writer.WriteStartElement("Fragment");
            writer.WriteStartElement("ComponentGroup");
            writer.WriteAttributeString("Id", "ProductFileAssociations");
            writer.WriteStartElement("ComponentRef");
            writer.WriteAttributeString("Id", "FileAssociationComponent");
            writer.WriteEndElement(); // ComponentRef
            writer.WriteEndElement(); // ComponentGroup
            writer.WriteEndElement(); // Fragment
        }

        writer.WriteEndElement(); // Wix
        writer.WriteEndDocument();
        writer.Flush();

        await stream.FlushAsync(cancellationToken);
    }

    private static string? ResolvePayloadSource(PackageFormatContext context, ICollection<PackagingIssue> issues)
    {
        if (context.Request.Properties?.TryGetValue("windows.msi.sourceDirectory", out var directory) == true && !string.IsNullOrWhiteSpace(directory))
        {
            if (!Directory.Exists(directory))
            {
                issues.Add(new PackagingIssue(
                    "windows.msi.source_missing",
                    $"Configured MSI source directory '{directory}' does not exist.",
                    PackagingIssueSeverity.Error));
                return null;
            }

            return directory;
        }

        issues.Add(new PackagingIssue(
            "windows.msi.source_unset",
            "Property 'windows.msi.sourceDirectory' is required for MSI packaging.",
            PackagingIssueSeverity.Error));
        return null;
    }

    private static string NormalizeForFilesystem(string path)
        => path.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar);

    private static string NormalizeToWindowsPath(string path)
        => path.Replace('/', '\\');

    private static string FormatInstalledCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "\"[INSTALLFOLDER]\"";
        }

        var trimmed = command.Trim();

        if (trimmed.Contains("[INSTALLFOLDER]", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("[", StringComparison.Ordinal) ||
            trimmed.StartsWith("\"[", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var executable = parts[0];
        var arguments = parts.Length > 1 ? " " + parts[1] : string.Empty;

        if (Path.IsPathRooted(executable))
        {
            return trimmed;
        }

        executable = executable.Trim('"');

        return $"\"[INSTALLFOLDER]{executable}\"{arguments}";
    }

    private static string SanitizeFileName(string value)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            value = value.Replace(invalid, '_');
        }

        return value;
    }

    private static string SanitizeDirectoryName(string value) => SanitizeFileName(value);

    private static ShortcutConfiguration? BuildShortcutConfiguration(PackageFormatContext context, string payloadDir, ICollection<PackagingIssue> issues)
    {
        if (!TryGetValue(context, "windows.msi.shortcutName", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        string? target = null;
        if (!TryGetValue(context, "windows.msi.shortcutTarget", out target) || string.IsNullOrWhiteSpace(target))
        {
            if (!TryGetValue(context, "windows.msix.executable", out target) || string.IsNullOrWhiteSpace(target))
            {
                issues.Add(new PackagingIssue(
                    "windows.msi.shortcut_target_missing",
                    "Shortcut requested but 'windows.msi.shortcutTarget' was not provided.",
                    PackagingIssueSeverity.Warning));
                return null;
            }
        }

        var description = TryGetValue(context, "windows.msi.shortcutDescription", out var desc) ? desc : null;
        var icon = TryGetValue(context, "windows.msi.shortcutIcon", out var iconValue) ? iconValue : null;

        var normalizedTargetCheck = NormalizeForFilesystem(target!);
        if (!string.IsNullOrWhiteSpace(target) && !File.Exists(Path.Combine(payloadDir, normalizedTargetCheck)))
        {
            issues.Add(new PackagingIssue(
                "windows.msi.shortcut_target_not_found",
                $"Shortcut target '{target}' was not found in payload directory.",
                PackagingIssueSeverity.Warning));
        }
        var normalizedTarget = NormalizeToWindowsPath(target!);

        if (!string.IsNullOrWhiteSpace(icon))
        {
            var normalizedIconCheck = NormalizeForFilesystem(icon!);
            if (!File.Exists(Path.Combine(payloadDir, normalizedIconCheck)))
            {
                issues.Add(new PackagingIssue(
                    "windows.msi.shortcut_icon_missing",
                    $"Shortcut icon '{icon}' was not found in payload directory.",
                    PackagingIssueSeverity.Warning));
                icon = null;
            }
            else
            {
                icon = NormalizeToWindowsPath(icon!);
            }
        }

        return new ShortcutConfiguration(name!, normalizedTarget, description, icon);
    }

    private static ProtocolConfiguration? BuildProtocolConfiguration(PackageFormatContext context, string payloadDir, ICollection<PackagingIssue> issues)
    {
        if (!TryGetValue(context, "windows.msi.protocolName", out var name) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        if (!TryGetValue(context, "windows.msi.protocolCommand", out var command) || string.IsNullOrWhiteSpace(command))
        {
            issues.Add(new PackagingIssue(
                "windows.msi.protocol_command_missing",
                "Protocol registration requested but 'windows.msi.protocolCommand' was not provided.",
                PackagingIssueSeverity.Warning));
            return null;
        }

        var displayName = TryGetValue(context, "windows.msi.protocolDisplayName", out var dn) ? dn : name;
        return new ProtocolConfiguration(name!, displayName!, command!);
    }

    private static ShellExtensionConfiguration? BuildShellExtensionConfiguration(PackageFormatContext context, string payloadDir, ICollection<PackagingIssue> issues)
    {
        if (!TryGetValue(context, "windows.msi.shellExtensionExtension", out var extension) || string.IsNullOrWhiteSpace(extension))
        {
            return null;
        }

        if (!extension.StartsWith(".", StringComparison.Ordinal))
        {
            extension = "." + extension;
        }

        if (!TryGetValue(context, "windows.msi.shellExtensionProgId", out var progId) || string.IsNullOrWhiteSpace(progId))
        {
            issues.Add(new PackagingIssue(
                "windows.msi.shell_progid_missing",
                "File association requested but 'windows.msi.shellExtensionProgId' was not provided.",
                PackagingIssueSeverity.Warning));
            return null;
        }

        if (!TryGetValue(context, "windows.msi.shellExtensionCommand", out var command) || string.IsNullOrWhiteSpace(command))
        {
            issues.Add(new PackagingIssue(
                "windows.msi.shell_command_missing",
                "File association requested but 'windows.msi.shellExtensionCommand' was not provided.",
                PackagingIssueSeverity.Warning));
            return null;
        }

        var description = TryGetValue(context, "windows.msi.shellExtensionDescription", out var desc) ? desc : progId;

        return new ShellExtensionConfiguration(progId!, extension!, command!, description!);
    }

    private static bool TryGetValue(PackageFormatContext context, string key, out string? value)
    {
        value = null;
        if (context.Request.Properties?.TryGetValue(key, out var requestValue) == true && !string.IsNullOrWhiteSpace(requestValue))
        {
            value = requestValue;
            return true;
        }

        if (context.Project.Metadata.TryGetValue(key, out var metadataValue) && !string.IsNullOrWhiteSpace(metadataValue))
        {
            value = metadataValue;
            return true;
        }

        return false;
    }

    private sealed record ShortcutConfiguration(string Name, string Target, string? Description, string? Icon);
    private sealed record ProtocolConfiguration(string Name, string DisplayName, string Command);
    private sealed record ShellExtensionConfiguration(string ProgId, string Extension, string Command, string Description);
}
