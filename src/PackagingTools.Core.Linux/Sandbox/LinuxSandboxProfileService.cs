using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Linux.Sandbox;

/// <summary>
/// Writes sandbox profile artifacts (AppArmor, SELinux, Flatpak) for Linux packages.
/// </summary>
public sealed class LinuxSandboxProfileService : ILinuxSandboxProfileService
{
    private readonly ILogger<LinuxSandboxProfileService>? _logger;

    public LinuxSandboxProfileService(ILogger<LinuxSandboxProfileService>? logger = null)
    {
        _logger = logger;
    }

    public Task<IReadOnlyCollection<PackagingIssue>> ApplyAsync(PackageFormatContext context, PackagingResult result, CancellationToken cancellationToken = default)
    {
        var issues = new List<PackagingIssue>();
        if (context.Request.Properties is null)
        {
            return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(issues);
        }

        var props = context.Request.Properties;
        var appArmor = GetValue(props, "linux.sandbox.apparmorProfile");
        var selinux = GetValue(props, "linux.sandbox.selinuxContext");
        var flatpak = GetValue(props, "linux.sandbox.flatpakPermissions");
        var script = GetValue(props, "linux.sandbox.postInstallScript");

        if (string.IsNullOrWhiteSpace(appArmor) && string.IsNullOrWhiteSpace(selinux) && string.IsNullOrWhiteSpace(flatpak) && string.IsNullOrWhiteSpace(script))
        {
            issues.Add(new PackagingIssue(
                "linux.sandbox.missing_configuration",
                "Sandboxing enabled but no AppArmor, SELinux, Flatpak permissions, or post-install script were provided.",
                PackagingIssueSeverity.Warning));
            return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(issues);
        }

        foreach (var artifact in result.Artifacts)
        {
            try
            {
                var baseDir = Path.Combine(context.Request.OutputDirectory, "_Audit", "sandbox", Path.GetFileNameWithoutExtension(artifact.Path) ?? artifact.Format);
                Directory.CreateDirectory(baseDir);
                var profilePath = Path.Combine(baseDir, "profile.json");

                var profile = new SandboxProfile
                {
                    ArtifactPath = artifact.Path,
                    Format = artifact.Format,
                    AppArmorProfile = appArmor,
                    SelinuxContext = selinux,
                    FlatpakPermissions = flatpak,
                    PostInstallScript = script
                };

                var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(profilePath, json);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Failed to capture sandbox profile for {Artifact}", artifact.Path);
                issues.Add(new PackagingIssue(
                    "linux.sandbox.capture_failed",
                    $"Failed to capture sandbox configuration for '{artifact.Path}': {ex.Message}",
                    PackagingIssueSeverity.Warning));
            }
        }

        return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(issues);
    }

    private static string? GetValue(IReadOnlyDictionary<string, string> props, string key)
        => props.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private sealed class SandboxProfile
    {
        public string ArtifactPath { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string? AppArmorProfile { get; set; }
        public string? SelinuxContext { get; set; }
        public string? FlatpakPermissions { get; set; }
        public string? PostInstallScript { get; set; }
    }
}
