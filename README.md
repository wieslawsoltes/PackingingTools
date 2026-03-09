# PackagingTools

Cross-platform .NET 10 packaging stack for building, signing, validating, and publishing Windows, macOS, and Linux application installers from a shared project model. PackagingTools combines an Avalonia desktop app, a .NET global tool, reusable SDK packages, and platform-specific engines so the same workflow can run locally or inside CI/CD.

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)](https://dotnet.microsoft.com)
[![Avalonia](https://img.shields.io/badge/Avalonia-11.x-8B44AC)](https://avaloniaui.net)
[![CI](https://img.shields.io/github/actions/workflow/status/wieslawsoltes/PackingingTools/ci.yml?branch=main)](https://github.com/wieslawsoltes/PackingingTools/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/wieslawsoltes/PackingingTools/blob/main/LICENSE)

## NuGet Packages

### Primary packages

| Package | NuGet | Description |
|---------|-------|-------------|
| **PackagingTools.Core** | [![NuGet](https://img.shields.io/nuget/v/PackagingTools.Core.svg)](https://www.nuget.org/packages/PackagingTools.Core) | Shared orchestration engine, project model, policy services, security primitives, audit, and telemetry |
| **PackagingTools.Core.Windows** | [![NuGet](https://img.shields.io/nuget/v/PackagingTools.Core.Windows.svg)](https://www.nuget.org/packages/PackagingTools.Core.Windows) | Windows packaging providers for MSIX/MSI/App Installer flows, signing, and host integration |
| **PackagingTools.Core.Mac** | [![NuGet](https://img.shields.io/nuget/v/PackagingTools.Core.Mac.svg)](https://www.nuget.org/packages/PackagingTools.Core.Mac) | macOS packaging providers for `.app`, `.pkg`, `.dmg`, notarization, and entitlement workflows |
| **PackagingTools.Core.Linux** | [![NuGet](https://img.shields.io/nuget/v/PackagingTools.Core.Linux.svg)](https://www.nuget.org/packages/PackagingTools.Core.Linux) | Linux packaging providers for DEB/RPM/AppImage/Flatpak/Snap, repositories, SBOM, and vulnerability evidence |
| **PackagingTools.Plugins** | [![NuGet](https://img.shields.io/nuget/v/PackagingTools.Plugins.svg)](https://www.nuget.org/packages/PackagingTools.Plugins) | Extensibility contracts and helpers for runtime-loaded format, signing, policy, and telemetry plugins |
| **PackagingTools.Sdk** | [![NuGet](https://img.shields.io/nuget/v/PackagingTools.Sdk.svg)](https://www.nuget.org/packages/PackagingTools.Sdk) | High-level .NET client API for embedding PackagingTools orchestration into custom services and release systems |
| **PackagingTools.Cli** | [![NuGet](https://img.shields.io/nuget/v/PackagingTools.Cli.svg)](https://www.nuget.org/packages/PackagingTools.Cli) | .NET global tool for packaging runs, host integration automation, policy validation, and scripted release flows |

`src/PackagingTools.App` is the desktop host and is intentionally not published as a NuGet package.

## Solution Components

| Area | Path / Package | Responsibility |
|------|----------------|----------------|
| Desktop app | `src/PackagingTools.App` | Avalonia workspace for onboarding, project authoring, environment validation, dashboards, and operator workflows |
| CLI | `src/PackagingTools.Cli` | Scriptable entry point for package generation, validation, signing, and Windows host integration automation |
| Shared core | `src/PackagingTools.Core` | Common project model, orchestration pipeline, policy engine, secure storage, audit, and telemetry services |
| Platform engines | `src/PackagingTools.Core.Windows`, `src/PackagingTools.Core.Mac`, `src/PackagingTools.Core.Linux` | Platform-specific packaging providers, tool abstractions, signing integrations, and format-specific artifacts |
| SDK | `src/PackagingTools.Sdk` | Embeddable API surface centered on `PackagingClient` and `PackagingRunOptions` |
| Plugins | `src/PackagingTools.Plugins` | Extensibility contracts for external format providers, signing adapters, telemetry sinks, and enterprise integrations |
| Build and CI | `build/` | Bootstrap scripts, tool checks, and reusable GitHub Actions starter templates |
| Docs | `docs/` | Architecture, configuration schema, onboarding, CI, identity, security, and platform guidance |
| Samples and tests | `samples/`, `tests/` | Reference project definitions plus integration coverage for Windows, macOS, Linux, plugins, policy, telemetry, and SDK scenarios |

## Supported Packaging Workflows

| Platform | Outputs | Highlights |
|----------|---------|------------|
| Windows | MSIX, MSI, App Installer, WinGet manifests | WiX v4 authoring, signing via local certificates or Azure Key Vault/HSM, and host integration editing for shortcuts, protocols, shell extensions, tasks, and services |
| macOS | `.app`, `.pkg`, `.dmg` | Bundle materialization, signing, notarization, entitlement handling, provisioning assets, verification, and audit evidence capture |
| Linux | DEB, RPM, AppImage, Flatpak, Snap | Repository metadata publishing, container-oriented build scripts, sandbox profiles, SBOM generation, and vulnerability evidence gating |

## Features

- **Shared project schema** with JSON configuration consumed consistently by the app, CLI, SDK, and plugins. The schema lives at [docs/configuration/schema.json](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/configuration/schema.json).
- **Policy-driven packaging** through `IPolicyEvaluator`, allowing approval checks, signing requirements, SBOM rules, vulnerability thresholds, and retention constraints to block unsafe releases.
- **Identity-aware operations** via pluggable identity services and secure stores for certificates, tokens, provisioning materials, and signing secrets.
- **Extensible packaging pipeline** with plugin hooks for new formats, signing providers, telemetry sinks, vulnerability scanners, and organization-specific automation.
- **Operational telemetry and audit trails** through the dashboard aggregation model, diagnostics bundles, packaging evidence, and verification results.
- **Local and CI parity** so the same project definition, property model, and policy behavior apply on developer machines and in build pipelines.

## Getting Started

1. Install the .NET 10 SDK and platform tooling described in [Developer Onboarding](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/developer-onboarding.md).
2. Clone the repository and build the solution:

```bash
git clone https://github.com/wieslawsoltes/PackingingTools.git
cd PackingingTools
dotnet build PackagingTools.sln -c Release
```

3. Run the integration suite:

```bash
dotnet test tests/PackagingTools.IntegrationTests/PackagingTools.IntegrationTests.csproj -c Release
```

4. Produce NuGet artifacts locally:

```bash
dotnet pack PackagingTools.sln -c Release
```

Local packages are written to `artifacts/nuget`.

## CLI Usage

During development you can run the CLI directly:

```bash
dotnet run --project src/PackagingTools.Cli -- pack \
  --project ./samples/sample-project.json \
  --platform windows \
  --format msix --format msi \
  --output ./artifacts/windows \
  --property windows.msix.payloadDirectory=./payload/win \
  --property windows.signing.certificatePath=certs/code-sign.pfx
```

After publishing, install the tool from NuGet:

```bash
dotnet tool install --global PackagingTools.Cli
```

Common commands:

- `pack` executes packaging pipelines for the selected platform and formats.
- `host` previews and applies Windows host integration metadata with diff-oriented output.
- `identity login` acquires and stores credentials for identity-aware packaging flows.

## SDK Usage

`PackagingTools.Sdk` exposes the same orchestration model to custom services and automation hosts:

```csharp
using PackagingTools.Core.Models;
using PackagingTools.Sdk;

var client = PackagingClient.CreateDefault();

var run = new PackagingRunOptions("./projects/sample.json", PackagingPlatform.Windows)
{
    Configuration = "Release",
    OutputDirectory = "./artifacts/windows"
};

run.Formats.Add("msi");
run.Properties["windows.signing.azureKeyVaultCertificate"] = "contoso-signing-cert";

var result = await client.PackAsync(run);
```

See [docs/sdk/embedding-packagingtools.md](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/sdk/embedding-packagingtools.md) for advanced composition and host customization.

## CI/CD and Release Flow

- `CI` builds and tests the solution on Linux, macOS, and Windows, then packs all NuGet packages on Ubuntu and uploads both `.nupkg` and `.snupkg` artifacts.
- `Release` runs the same validation on tag pushes matching `v*`, packs all publishable projects with the tag version, pushes packages to NuGet.org when `NUGET_API_KEY` is configured, and creates a GitHub release with generated release notes.
- Reusable starter workflows for packaging jobs live under `build/templates/github-actions/` and are documented in [docs/ci/starter-templates.md](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/ci/starter-templates.md).

## Documentation

- [Solution architecture](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/architecture/solution-architecture.md)
- [Windows packaging suite](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/windows-packaging-suite.md)
- [Configuration guide](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/configuration/index.md)
- [Plugin configuration](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/plugins/configuration.md)
- [Identity architecture](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/identity/identity-architecture.md)
- [Security and SBOM architecture](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/security/vulnerability-sbom-architecture.md)
- [Packaging roadmap](https://github.com/wieslawsoltes/PackingingTools/blob/main/docs/packaging-tools-plan.md)

## Repository Layout

```text
PackingingTools/
├── src/                # App, CLI, SDK, core libraries, platform engines, plugin contracts
├── build/              # Bootstrap scripts, tool checks, CI templates
├── docs/               # Architecture, onboarding, CI, security, identity, platform guidance
├── samples/            # Sample project definitions and payload references
├── tests/              # Integration coverage and scenario validation
└── tools/              # Auxiliary utilities and diagnostics
```

## Contributing

Use the onboarding guide to set up the toolchain, then run `dotnet build`, `dotnet test`, and `dotnet pack` before opening a pull request. When changing packaging behavior, update the relevant docs and add integration coverage under `tests/PackagingTools.IntegrationTests`.
