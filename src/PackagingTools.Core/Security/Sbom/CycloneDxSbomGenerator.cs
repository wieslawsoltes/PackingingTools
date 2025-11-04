using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Abstractions;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Security.Sbom;

public sealed class CycloneDxSbomGenerator : ISbomGenerator
{
    private readonly ILogger<CycloneDxSbomGenerator>? _logger;

    public CycloneDxSbomGenerator(ILogger<CycloneDxSbomGenerator>? logger = null)
    {
        _logger = logger;
    }

    public string Format => "cyclonedx-json";

    public Task<SbomGenerationResult> GenerateAsync(PackageFormatContext context, PackagingArtifact artifact, CancellationToken cancellationToken = default)
    {
        try
        {
            var sbomPath = Path.Combine(context.Request.OutputDirectory, "_Sbom", Path.GetFileNameWithoutExtension(artifact.Path) + ".cdx.json");
            Directory.CreateDirectory(Path.GetDirectoryName(sbomPath)!);

            var components = new List<Dictionary<string, string?>>
            {
                new()
                {
                    ["type"] = "application",
                    ["name"] = artifact.Metadata.TryGetValue("packageName", out var name) ? name : context.Project.Name,
                    ["version"] = artifact.Metadata.TryGetValue("packageVersion", out var version) ? version : context.Project.Version
                }
            };

            var sbom = new Dictionary<string, object?>
            {
                ["bomFormat"] = "CycloneDX",
                ["specVersion"] = "1.4",
                ["version"] = 1,
                ["metadata"] = new Dictionary<string, object?>
                {
                    ["timestamp"] = DateTimeOffset.UtcNow.ToString("O"),
                    ["tools"] = new[]
                    {
                        new Dictionary<string, string?>
                        {
                            ["name"] = "PackagingTools",
                            ["version"] = typeof(CycloneDxSbomGenerator).Assembly.GetName().Version?.ToString()
                        }
                    }
                },
                ["components"] = components
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(sbomPath, JsonSerializer.Serialize(sbom, options));
            return Task.FromResult(new SbomGenerationResult(sbomPath, null));
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate SBOM");
            var issue = new PackagingIssue(
                "security.sbom.generate_failed",
                $"Failed to generate SBOM: {ex.Message}",
                PackagingIssueSeverity.Warning);
            return Task.FromResult(new SbomGenerationResult(string.Empty, issue));
        }
    }
}
