# PackagingTools

PackagingTools is a cross-platform packaging ecosystem that unifies installer generation, signing, policy enforcement, and operational telemetry for Windows, macOS, and Linux applications. It combines an Avalonia desktop experience, a rich CLI, and an embeddable .NET SDK so teams can deliver compliant packages from laptops, CI/CD pipelines, or fully automated release services.

## At a Glance
- Single project model for MSIX/MSI/App Installer, notarized `.app`/`.pkg`/`.dmg`, and DEB/RPM/AppImage/Flatpak outputs.
- Built-in governance: identity-aware approvals, policy gates, SBOM + vulnerability evidence, remote signing.
- Reusable automation: CLI for scripting, SDK for orchestration services, CI starter templates, and plugin discovery.
- Operational insight: shared telemetry aggregator powering dashboards, audit trails, and diagnostics bundles.

## Contents
- [Solution Components](#solution-components)
- [Key Capabilities](#key-capabilities)
- [Supported Packaging Workflows](#supported-packaging-workflows)
- [Security, Identity, and Compliance](#security-identity-and-compliance)
- [Telemetry and Observability](#telemetry-and-observability)
- [Plugin Ecosystem](#plugin-ecosystem)
- [Getting Started](#getting-started)
- [CLI Usage](#cli-usage)
- [Desktop App](#desktop-app)
- [Embedding with the .NET SDK](#embedding-with-the-net-sdk)
- [Project Configuration](#project-configuration)
- [CI/CD Integration](#cicd-integration)
- [Repository Layout](#repository-layout)
- [Contributing & Next Steps](#contributing--next-steps)

## Solution Components
- `src/PackagingTools.App` – Avalonia desktop workspace with onboarding wizard, environment validation, configuration editors, telemetry dashboards, and host integration tooling.
- `src/PackagingTools.Cli` – Cross-platform CLI mirroring GUI workflows; commands for packaging runs, Windows host metadata, and identity bootstrap.
- `src/PackagingTools.Core` – Shared orchestration engine (pipelines, policy, signing, secure store, telemetry, audit).
- `src/PackagingTools.Core.Windows|Mac|Linux` – Platform-specific pipelines and format providers.
- `src/PackagingTools.Plugins` – Base contracts plus helper utilities for runtime-loaded extensions.
- `src/PackagingTools.Sdk` – High-level `PackagingClient` facade for embedding orchestration in custom services.
- `build/` – Bootstrap scripts, dependency acquisition helpers, and reusable CI templates.
- `docs/` – Deep-dive guidance (platform blueprints, configuration schema, identity, security, plugin setup).
- `samples/` – Ready-to-run project definitions such as `samples/sample-project.json` to exercise pipelines.
- `tests/` – Unit, integration, and scenario suites covering pipelines, security, identity, telemetry, plugins, and host integration logic.

## Key Capabilities
- **Unified Project Schema** – JSON-based project descriptions (`PackagingProject`) shared across GUI, CLI, SDK, and plugins with schema validation under `docs/configuration/schema.json`.
- **Policy Enforcement** – `IPolicyEvaluator` blocks packaging when approvals, signing assets, SBOMs, or vulnerability scans violate governance thresholds.
- **Identity-Aware Operations** – Pluggable `IIdentityService` providers (Azure AD, Okta, local) ensure RBAC, MFA enforcement, and scoped access tokens across hosts.
- **Secure Secrets** – `ISecureStore` abstractions wrap OS keychains or remote vaults for certificates, tokens, and signing keys.
- **Remote/Local Build Agents** – `IBuildAgentBroker` negotiates agents for each platform, supporting local execution and future remote pools.
- **Extensibility-First** – `IPackageFormatProvider`, `ISigningService`, `ISbomGenerator`, `IVulnerabilityScanner`, and plugin adapters keep the core lightweight while enabling bespoke integrations.

## Supported Packaging Workflows
### Windows (`docs/windows-packaging-suite.md`)
- MSIX/AppX packaging with manifest templating and App Installer feed generation.
- MSI/EXE authoring via WiX v4, bootstrapper support, and optional Advanced Installer plugins.
- WinGet manifest creation and validation.
- Host integration editing (`packagingtools host`) for shortcuts, URI handlers, shell extensions, and task/service definitions.
- Signing via local certificates or Azure Key Vault/HSM connectors with rotation alerts.

### macOS (`docs/packaging-tools-plan.md`, `docs/architecture/adr/0003-macos-toolchain-selection.md`)
- `.app` bundle materialisation, notarised `.pkg` and `.dmg` generation, entitlement management, and provisioning profile automation.
- Remote signing/notarization workflows with progress polling and evidence capture.
- Secure store-backed credential management with AES-GCM encryption.

### Linux (`docs/linux/repository-publishing.md`, `docs/linux/container-builds.md`, `docs/linux/security-artifacts.md`)
- DEB/RPM/AppImage/Flatpak/Snap packaging with reproducible containerised build scripts.
- Repository metadata (APT/YUM) generation for multi-channel publishing.
- SBOM and vulnerability evidence bundles (`_Sbom/`, `security.vuln.*` issues) with policy-controlled gating.

## Security, Identity, and Compliance
- **Identity Architecture** – See `docs/identity/identity-architecture.md`. Azure AD and Okta providers surface roles (Admin, SecurityOfficer, ReleaseEngineer, Developer) and enforce MFA when required.
- **Policy Engine** – Declarative JSON/YAML rules manage approvals, required scanners, signing freshness, and artifact retention.
- **SBOM & Vulnerability Scanning** – `ISbomGenerator` (CycloneDX JSON by default) and `IVulnerabilityScanner` (Trivy stub) emit artifacts and issues for compliance export (see `docs/security/vulnerability-sbom-architecture.md`).
- **Secure Stores** – `FileSecureStore` plus provider hooks keep certificates, tokens, and secrets off disk in plain text; compatible with OS vaults and cloud key stores.
- **Audit Trails** – Pipelines record issues, signing receipts, and identity principals for downstream attestations and evidence packages.

## Telemetry and Observability
- `DashboardTelemetryAggregator` (under `src/PackagingTools.Core/Telemetry`) normalises pipeline events, signing health, and dependency signals.
- Shared dashboard state persists via `DashboardTelemetryStore`, surfaced in the GUI and consumable via the SDK.
- Telemetry events include packaging job summaries, format-level dependency timings, repository updates, and security findings, enabling real-time dashboards and historical exports.
- Diagnostics bundles capture tool output, telemetry snapshots, and agent health for support scenarios.

## Plugin Ecosystem
- Manifests (`*.json`) declare plugin assemblies and optional explicit types. Disabled manifests remain discoverable but are skipped at runtime (see `docs/plugins/configuration.md`).
- Probing order: runtime overrides (`PackagingClientOptions.PluginDirectories`, `PackagingRunOptions.PluginDirectories`), project metadata (`plugins.directories`), environment `PACKAGINGTOOLS_PLUGIN_PATHS`, then default app/user plugin folders.
- Plugins can register DI services, new format providers, policy evaluators, telemetry sinks, or CLI command enrichments through `PluginManager`.
- Integration tests (`tests/PackagingTools.IntegrationTests/Plugins/SamplePackageFormatPlugin.cs`) showcase minimal scaffolding for new format providers.

## Getting Started
1. **Install prerequisites**  
   - .NET SDK 10 (preview OK).  
   - Platform tooling as outlined in `docs/developer-onboarding.md` (`build/scripts/check-tools.ps1|.sh` help verify).
2. **Clone and build**
   ```bash
   git clone https://github.com/<org>/PackagingTools.git
   cd PackagingTools
   dotnet build PackagingTools.sln
   ```
3. **Run tests**
   ```bash
   dotnet test PackagingTools.sln
   ```
4. **Explore samples**  
   Use `samples/sample-project.json` as a starting point. Copy it into your project and adjust metadata/properties before invoking the CLI, GUI, or SDK.

## CLI Usage
The CLI is available via `dotnet run` during development and will ship as a .NET global tool.

```bash
dotnet run --project src/PackagingTools.Cli -- pack \
  --project ./samples/sample-project.json \
  --platform windows \
  --format msix --format msi \
  --output ./artifacts/windows \
  --property windows.msix.payloadDirectory=./payload/win \
  --property windows.signing.certificatePath=certs/code-sign.pfx
```

### Commands
- `pack`: Execute packaging pipelines for the selected platform and formats. Accepts `--configuration`, `--property key=value`, `--output`, and `--save-project`. Properties merge with project defaults.
- `host`: Preview and optionally persist Windows host integration metadata. Provides property-level diff output and validation messages before applying.
- `identity login`: Acquire tokens (Azure AD, Okta, local) and persist them in the secure store so subsequent pack runs inherit identity context.

Exit codes: `0` (success with optional warnings) and `1` (validation or pipeline failure).

## Desktop App
- Avalonia-based workspace mirroring CLI capabilities with added context—multi-step onboarding wizard, environment validation (`EnvironmentValidationService`), property editors, telemetry dashboards, and Windows host integration UI.
- Accessibility investments include keyboard-first navigation, screen-reader annotations, and color contrast compliance (see `docs/pre-mvp-tasks.md` milestones).
- Workspace saves the same project JSON consumed by CLI/SDK, ensuring seamless handoff between experiences.

## Embedding with the .NET SDK
Use `PackagingTools.Sdk` to orchestrate packaging from custom automation:

```csharp
using PackagingTools.Core.Models;
using PackagingTools.Sdk;

var client = PackagingClient.CreateDefault(options =>
{
    options.PluginDirectories.Add("/opt/packaging-plugins");
});

var run = new PackagingRunOptions("./projects/sample.json", PackagingPlatform.Windows)
{
    Configuration = "Release",
    OutputDirectory = "./artifacts/windows"
};
run.Formats.Add("msi");
run.Properties["windows.signing.azureKeyVaultCertificate"] = "contoso-signing-cert";

var result = await client.PackAsync(run);
if (!result.Success)
{
    foreach (var issue in result.Issues)
    {
        Console.Error.WriteLine($"{issue.Severity}: {issue.Code} - {issue.Message}");
    }
}
```

SDK options let you swap policy evaluators, build agent brokers, telemetry channels, and platform registrations. See `docs/sdk/embedding-packagingtools.md` for advanced usage.

## Project Configuration
- Projects persist as JSON (see `docs/configuration/index.md`) with global `metadata`, per-platform `formats`, and provider `properties`.
- CLI `--property` arguments override stored values without mutating the source file unless `--save-project` is supplied.
- Plugin directories can be stored in `metadata["plugins.directories"]`, edited via the GUI or directly in JSON.
- Security, signing, and repository behaviours are customised via namespaced property keys (`windows.signing.*`, `mac.notarization.*`, `linux.repo.*`, `security.*`).

## CI/CD Integration
- GitHub Actions starter templates live under `build/templates/github-actions` for Windows, macOS, and Linux runners. They bootstrap tooling, run `packagingtools pack`, and publish outputs.
- Scripts in `build/scripts/` validate prerequisites (`check-tools`), download dependencies, and prepare offline caches.
- Combine with `docs/ci/starter-templates.md` guidance to adapt workflows for Azure Pipelines, GitLab CI, or bespoke orchestrators.
- Telemetry and policy enforcement behave identically in CI and local runs, ensuring consistent governance.

## Repository Layout
```
PackagingTools/
├── src/                # App, CLI, core libraries, SDK, platform modules, plugins
├── build/              # Bootstrap scripts, CI templates, infra helpers
├── tools/              # Auxiliary diagnostics and utilities
├── tests/              # Unit/integration/e2e suites
├── samples/            # Reference projects and payloads
└── docs/               # Architecture, platform blueprints, policies, onboarding
```

## Contributing & Next Steps
- Follow `docs/developer-onboarding.md` for environment setup, then use `dotnet format` and `dotnet test` before submitting PRs.
- Add integration test coverage when introducing new format providers, signing adapters, or policy rules (`tests/PackagingTools.IntegrationTests` contains helpful stubs and doubles).
- Update architecture decision records under `docs/architecture/adr/` when changing platform strategy, persistence, or plugin models.
- Planned work items include expanded accessibility coverage, macOS entitlement tooling, Linux sandbox/profile editing, and extended telemetry exports—see `docs/pre-mvp-tasks.md` and `docs/packaging-tools-plan.md` for roadmap context.

For detailed walkthroughs, dive into the platform blueprints and architecture documents inside `docs/`. Contributions, feedback, and bug reports are welcome via issues or pull requests.
