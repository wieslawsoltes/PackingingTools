using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PackagingTools.Core.Models;

namespace PackagingTools.Core.Linux.Container;

public sealed class DockerLinuxContainerBuildService : ILinuxContainerBuildService
{
    private readonly ILogger<DockerLinuxContainerBuildService>? _logger;

    public DockerLinuxContainerBuildService(ILogger<DockerLinuxContainerBuildService>? logger = null)
    {
        _logger = logger;
    }

    public Task<IReadOnlyCollection<PackagingIssue>> GenerateAsync(PackagingProject project, PackagingRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Properties is null ||
            !request.Properties.TryGetValue("linux.container.image", out var image) ||
            string.IsNullOrWhiteSpace(image))
        {
            return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(Array.Empty<PackagingIssue>());
        }

        try
        {
            var scriptPath = WriteScript(project, request, image!);
            var issue = new PackagingIssue(
                "linux.container.script_generated",
                $"Container build script generated at '{scriptPath}'.",
                PackagingIssueSeverity.Info);
            return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(new[] { issue });
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Failed to generate container build script");
            var issue = new PackagingIssue(
                "linux.container.script_failed",
                $"Failed to generate container build script: {ex.Message}",
                PackagingIssueSeverity.Warning);
            return Task.FromResult<IReadOnlyCollection<PackagingIssue>>(new[] { issue });
        }
    }

    private static string WriteScript(PackagingProject project, PackagingRequest request, string image)
    {
        var scriptPath = Path.Combine(request.OutputDirectory, "container-build.sh");
        Directory.CreateDirectory(request.OutputDirectory);

        var projectPath = request.Properties!.TryGetValue("linux.container.projectPath", out var path) && !string.IsNullOrWhiteSpace(path)
            ? path
            : project.Name + ".json";

        var formatsArgument = string.Join(" ", request.Formats.Select(f => $"--format {f}"));
        var propertiesArguments = BuildPropertyArguments(request.Properties);

        var script = new StringBuilder();
        script.AppendLine("#!/usr/bin/env bash");
        script.AppendLine("set -euo pipefail");
        script.AppendLine();
        script.AppendLine("docker run --rm \\");
        script.AppendLine("  -v \"$PWD:/workspace\" \\");
        script.AppendLine("  -w /workspace \\");
        script.AppendLine($"  {image} \\");
        script.Append("  packagingtools pack ");
        script.Append($"--project \"{projectPath}\" ");
        script.Append("--platform linux ");
        script.Append(formatsArgument);
        if (!string.IsNullOrWhiteSpace(request.Configuration))
        {
            script.Append($" --configuration {request.Configuration}");
        }
        if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
        {
            var containerOutput = request.Properties.TryGetValue("linux.container.output", out var outPath) && !string.IsNullOrWhiteSpace(outPath)
                ? outPath
                : request.OutputDirectory;
            script.Append($" --output \"{containerOutput}\"");
        }
        if (propertiesArguments.Length > 0)
        {
            script.Append(' ');
            script.Append(propertiesArguments);
        }
        script.AppendLine();

        File.WriteAllText(scriptPath, script.ToString());
        if (!OperatingSystem.IsWindows())
        {
            try
            {
                File.SetUnixFileMode(scriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            catch
            {
                // Ignore if not supported
            }
        }
        return scriptPath;
    }

    private static string BuildPropertyArguments(IReadOnlyDictionary<string, string> properties)
    {
        var builder = new StringBuilder();
        foreach (var kv in properties)
        {
            if (kv.Key.StartsWith("linux.container.", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            builder.Append(" --property ");
            builder.Append(kv.Key);
            builder.Append('=');
            builder.Append('"').Append(kv.Value.Replace("\"", "\\\"")).Append('"');
        }

        return builder.ToString().Trim();
    }
}
