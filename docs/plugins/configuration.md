# Plugin Configuration

PackagingTools probes multiple locations for plugin manifests so desktop, CLI, and SDK hosts behave consistently.

## Probing Order

1. **Runtime overrides**
   - `PackagingClientOptions.PluginDirectories` (SDK/global)
   - `PackagingRunOptions.PluginDirectories` (per execution; relative to the project file)
2. **Project metadata** &mdash; `plugins.directories`, editable from the Avalonia workspace (one path per line) or directly in the JSON project.
3. **Environment variable** &mdash; set `PACKAGINGTOOLS_PLUGIN_PATHS` to a path-separated list for machine-specific locations.
4. **Defaults** &mdash; `<application base>/plugins` and `%APPDATA%/PackagingTools/plugins` (or the macOS/Linux equivalents).

Each directory is scanned for `*.json` manifests shaped like:

```json
{
  "assemblyPath": "SamplePlugin.dll",
  "pluginType": "Contoso.SamplePlugin",
  "disabled": false
}
```

Relative paths inside manifests are resolved against the manifest directory. Disabled manifests remain on disk but are skipped.

## GUI Workflow

The workspace view exposes a **Plugin Directories** editor (one path per line). When the project is saved, values are stored in `plugins.directories`, and the CLI/SDK resolve them automatically.

## CLI / SDK Tips

- Ship customer plugins alongside the CLI in a `plugins` folder; they are picked up automatically.
- Use `PACKAGINGTOOLS_PLUGIN_PATHS` on build agents to point at shared plugin bundles.
- In code, call:

```csharp
var client = PackagingClient.CreateDefault(options =>
{
    options.PluginDirectories.Add("/opt/plugins");
});
```

to ensure bespoke locations are considered for every run.
