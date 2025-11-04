# Embedding PackagingTools via .NET SDK

The `PackagingTools.Sdk` assembly provides a thin `PackagingClient` facade for running packaging pipelines directly inside custom build automation, MSBuild tasks, or orchestration services.

## Quick Start

```csharp
using PackagingTools.Core.Models;
using PackagingTools.Sdk;

var client = PackagingClient.CreateDefault();
var options = new PackagingRunOptions("./projects/sample.json", PackagingPlatform.Windows)
{
    Configuration = "Release",
    OutputDirectory = "./artifacts/windows"
};
options.Formats.Add("msi");
options.Properties["windows.signing.certificatePath"] = "certs/code-sign.pfx";

var result = await client.PackAsync(options);
if (!result.Success)
{
    foreach (var issue in result.Issues)
    {
        Console.Error.WriteLine($"{issue.Severity}: {issue.Code} - {issue.Message}");
    }
    return 1;
}

foreach (var artifact in result.Artifacts)
{
    Console.WriteLine($"Created {artifact.Format} -> {artifact.Path}");
}
```

## Service Customisation

- **Platforms:** Disable built-in platform registration via `IncludeWindows`, `IncludeMac`, or `IncludeLinux` when you want to inject custom pipelines.
- **Policy & Agents:** Override governance and agent orchestration by supplying `options.PolicyEvaluator` and `options.AgentBroker`.
- **DI Hooks:** Use `options.ConfigureServices` to extend the internal service collection (e.g., register stub pipelines in tests or swap telemetry channels).

## Plugin Probing

- **Global overrides:** Populate `PackagingClientOptions.PluginDirectories` to add folders that every run should probe.
- **Per-run overrides:** Append to `PackagingRunOptions.PluginDirectories`; relative paths are resolved against the project file.
- **Project metadata:** Persist directories in the `plugins.directories` metadata key (the Avalonia workspace exposes a multi-line editor).
- **Environment & defaults:** Set `PACKAGINGTOOLS_PLUGIN_PATHS` for environment-specific locations. The SDK also probes `<app base>/plugins` and `%APPDATA%/PackagingTools/plugins` (or their macOS/Linux equivalents).
- Each resolved directory is scanned for JSON manifests describing `IPlugin` implementations; disabled manifests are skipped so the host keeps running even if individual plugins fail to load.

## Advanced Usage

- Use `PackAsync(PackagingProject, PackagingRequest)` when you already have a deserialised project and wish to control the request lifecycle explicitly.
- Merge run-level properties with project metadata to inject secrets at runtime without modifying the persisted project file.
- Combine the SDK with CI starter templates (see `docs/ci/starter-templates.md`) to author bespoke pipeline steps while reusing the same orchestration core.
