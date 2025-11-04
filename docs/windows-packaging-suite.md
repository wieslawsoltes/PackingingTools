# Windows Packaging Suite Blueprint

## 1. Packaging Pipelines

### 1.1 MSIX Pipeline
- **Inputs:** Compiled app binaries, assets, manifest template, signing profile.
- **Steps:**
  1. Generate AppxManifest via template expansion (App ID, display name, capabilities, dependencies).
  2. Stage payload directory structure (`VFS`, `Assets`, `AppxManifest.xml`, match architecture RID).
  3. Invoke `makeappx pack` (or MSIX SDK API) to produce `.msix`/`.appx` package.
  4. Apply optional App Installer configuration for self-updates (URI endpoints, version metadata).
  5. Sign package via `signtool`/MSIX signing API (hash algorithm SHA-256, timestamp RFC 3161).

### 1.2 MSI/EXE Pipeline
- **Inputs:** WiX template fragments, custom action scripts, branding resources.
- **Steps:**
  1. Generate WiX source (.wxs) using project metadata (ProductCode, UpgradeCode, features, components).
  2. Compile WiX with `candle.exe` and link via `light.exe`, injecting CAB compression settings.
  3. For bootstrapper EXEs, wrap MSI using Burn or `Advanced Installer` templates as plugin implementations.
  4. Embed custom actions for prerequisites (.NET Desktop runtime, VC++), scheduled tasks, and environment variables.
  5. Sign MSI/EXE artifacts and any embedded binaries.

### 1.3 App Installer & WinGet Outputs
- **App Installer:** Emit `.appinstaller` XML referencing MSIX packages with version and update URIs; optional automatic update cadence.
- **WinGet:** Generate YAML manifests (default, installer, locale files) aligned with Microsoft schema; run validation (`winget validate`) before publishing.

#### Provider Configuration Keys
- `windows.msix.payloadDirectory` — source directory for MSIX staging.
- `windows.msix.executable`, `windows.msix.entryPoint`, `windows.msix.logo` — manifest overrides for executable and assets.
- `windows.msi.sourceDirectory` — directory harvested into MSI components.
- `windows.msi.shortcutName`, `windows.msi.shortcutTarget`, `windows.msi.shortcutDescription`, `windows.msi.shortcutIcon` — start menu shortcut configuration for MSI packages.
- `windows.msi.protocolName`, `windows.msi.protocolDisplayName`, `windows.msi.protocolCommand` — custom URI handler registration.
- `windows.msi.shellExtensionExtension`, `windows.msi.shellExtensionProgId`, `windows.msi.shellExtensionDescription`, `windows.msi.shellExtensionCommand` — file association and shell extension wiring.
- `windows.signing.certificatePath`, `windows.signing.password`, `windows.signing.timestampUrl` — code signing inputs.
- `windows.signing.azureKeyVaultCertificate`, `windows.signing.azureKeyVaultUrl`, `windows.signing.azureKeyVaultTenantId`, `windows.signing.azureKeyVaultClientId` — remote signing configuration for Azure Key Vault connectors.
- `windows.appinstaller.msixPath`, `windows.appinstaller.uri`, `windows.appinstaller.hoursBetweenUpdates` — App Installer feed values.
- `windows.winget.id`, `windows.winget.locale`, `windows.winget.sha256` — WinGet manifest metadata hints.

## 2. Certificate & Signing Management
- Maintain signing profiles in secure store (local cert store, Azure Key Vault, hashicorp vault).
- Support dual-signing (SHA-256/384) for legacy compatibility.
- Integrate RFC 3161 timestamp services with retry/backoff.
- Provide UI/CLI to:
  - Import/export PFX with role-based permissions.
  - Configure remote signing via Azure Key Vault/HSM connectors (shipped in current milestone).
  - Schedule certificate rotation alerts based on expiry.
- Enforce policy rules: packaging blocked if cert expired/soon-to-expire, or timestamping fails.

## 3. Automation Assets
- **App Installer:** Template engine with tokens for CDN endpoints, min version, force update flags; ability to version URIs per release channel (Stable/Beta/Insider).
- **WinGet:**
  - Manifest builder that maps PackagingTools project metadata to fields (PackageIdentifier, Publisher, InstallerType, InstallModes).
  - Validation pipeline running `winget validate` and PowerShell Pester tests.
  - Optional GitHub PR automation for community repository submissions (create branch, commit manifests, open PR).

## 4. Host Integration Toggles
- Provide configuration surface (GUI/CLI) for:
  - Start menu shortcuts, taskbar pins, desktop icons.
  - Shell extension registration (context menus, file associations, protocol handlers).
  - Services/scheduled tasks definitions with `sc.exe` scripts or WiX ServiceInstall elements.
  - App Installer custom actions (repair, modify, uninstall commands).
- Generate diff preview comparing current vs desired integrations for review before packaging.
- CLI support: `packagingtools host` previews and applies metadata changes, aligning with the Avalonia host integration editor and emitting property-level diffs.
- GUI support: the Windows platform panel exposes checkboxes/text inputs for shortcuts, protocols, and file associations with a live property change preview.

## 5. Testing Strategy
- **Unit Tests:** Validate manifest generation, WiX templates, configuration diff logic.
- **Integration Tests (Windows runners):**
  - Build sample app packages for MSIX & MSI; install/uninstall headlessly.
  - Run `signtool verify /pa` and `Get-AppPackage` checks.
  - Validate WinGet manifest using official validator.
- **Current coverage:** cross-platform integration tests validate WiX generation for shortcuts, protocols, and associations; smoke tests optionally exercise real WiX tooling when present on Windows agents.
- **End-to-End:** Use Windows Server 2022/Windows 11 agents to execute packaging flows triggered by CLI, capturing logs and verifying installed app behavior.
- Incorporate virtualization snapshots to ensure clean state per run.

## 6. Diagnostics & Telemetry
- Capture packaging step metrics (duration, tool exit codes, signature details) via OpenTelemetry events.
- Bundle log files (`makeappx`, `signtool`, WiX) into artifact zip for support.
- Provide error classification: manifest validation, signing, upload, integration conflicts.
- Expose dashboards in GUI showing recent runs, failure rates, certificate health.

## 7. Dependencies & Toolchain Management
- Manage versions for:
  - Windows SDK (MSIX tooling).
  - WiX Toolset v4 (MSI authoring).
  - WinGet CLI for manifest validation.
  - Signtool/Timestamp servers.
- Build scripts (`build/win/setup.ps1`) to install/update dependencies, support offline caching, and run checks (`Get-Package`, `winget` availability).

## 8. Roadmap Actions
1. Implement `PackagingTools.Core.Windows` module encapsulating pipelines and certificate services.
2. Build GUI panels for signing profiles, integration toggles, and artifact previews.
3. Ship CLI commands `pack windows msix`, `pack windows msi`, `generate winget` with consistent options.
4. Add Azure Key Vault connector plugin for remote signing (optional dependency).
5. Configure Windows GitHub Actions workflow running integration suite on each PR.
