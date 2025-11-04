# CLI Overview

The PackagingTools CLI provides a cross-platform entry point for invoking packaging pipelines without a GUI. It is designed to mirror the GUI feature set while integrating with automation pipelines.

## Commands
Currently the CLI exposes a single `pack` command:

```bash
dotnet run --project src/PackagingTools.Cli -- pack \
  --project sample.project.json \
  --platform windows \
  --format msix \
  --output ./artifacts \
  --property windows.msix.payloadDirectory=./payload
```

### Options
- `--project <path>`: Path to the project definition JSON file (see Developer Onboarding guide).
- `--platform <windows|mac|linux>`: Target platform.
- `--format <value>`: Requested packaging format; repeat to queue multiple outputs.
- `--configuration <value>`: Build configuration string used in telemetry/audit records (defaults to `Release`).
- `--output <path>`: Output directory for produced artifacts (defaults to `./artifacts`).
- `--property key=value`: Additional provider or signing properties.
- `--save-project <path>`: Persist the merged configuration back to disk, enabling CLI-driven edits that the GUI can reload.

### Exit Codes
- `0` – packaging succeeded (warnings may still be emitted to stdout).
- `1` – validation or packaging failure.

## JSON Project Schema (Draft)
```json
{
  "id": "sample",
  "name": "Sample App",
  "version": "1.0.0",
  "metadata": { "windows.identityName": "Contoso.Sample" },
  "platforms": {
    "Windows": {
      "formats": ["msix", "msi"],
      "properties": {
        "windows.msix.payloadDirectory": "./payload/windows",
        "windows.msi.sourceDirectory": "./payload/windows"
      }
    }
  }
}
```

Project-level metadata feeds manifest generation and signing defaults. Platform `properties` supply provider-specific configuration; command-line `--property` values override these at runtime.

## Extending the CLI
- Additional commands (e.g., `validate`, `sign`, `publish`) should follow the same `CliOptions` parsing utilities.
- Use the dependency injection registrations in `Program.cs` to add new services (e.g., remote build brokers, telemetry sinks).
- Keep the CLI thin—continue to push packaging logic into the `PackagingTools.Core` libraries so GUI and automation surfaces stay in sync.
