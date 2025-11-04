# PackagingTools.Plugins

Extension surface for optional packaging providers, enterprise integrations, and customer-specific automations. Plugins interact with `PackagingTools.Core` through a stable contract and can be loaded by the Avalonia UI, CLI, or SDK hosts.

## Plugin Contracts

- Derive from one of the base adapters in `PackagingTools.Core.Plugins.Providers` (`PackageFormatProviderPlugin`, `SigningProviderPlugin`, `TelemetryPlugin`, etc.).
- Override `ConfigureServices` to register the plugin instance (defaults add the plugin as a singleton service) and optionally `InitialiseAsync` for any startup work.

## Manifest Format

Plugins are discovered via JSON manifests placed in a probing directory.

```json
{
  "assemblyPath": "SamplePlugin.dll",
  "pluginType": "Contoso.Packaging.SamplePlugin",
  "disabled": false
}
```

- `assemblyPath` can be relative to the manifest location or absolute.
- `pluginType` is optional when the assembly exposes a single public `IPlugin` implementation.
- Set `disabled` to `true` to leave the manifest in place but skip loading.

## Probing Order

`PluginManager` resolves directories from multiple sources before loading manifests:

1. Explicit overrides:
   - `PackagingClientOptions.PluginDirectories`
   - `PackagingRunOptions.PluginDirectories`
2. Project metadata (`plugins.directories`), editable from the Avalonia workspace (one path per line).
3. Environment variable `PACKAGINGTOOLS_PLUGIN_PATHS` (path separator delimited).
4. Defaults:
   - `%APPDATA%/PackagingTools/plugins` (or the equivalent on macOS/Linux)
   - `<ApplicationBase>/plugins`

Relative paths in overrides or project metadata are resolved against the project file location.

## Authoring Tips

- Ship a manifest alongside the plugin assembly and any dependencies.
- Keep plugin dependencies private to avoid colliding with the host app.
- Prefer `InitialiseAsync` for expensive setup (network calls, cache warm-up) so failures can be surfaced gracefully.
