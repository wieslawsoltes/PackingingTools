# Milestone 1 — Discovery & Requirements Alignment

## Task 1. Platform Scenario Catalogue

### Windows
- **Primary packages:** MSIX, MSIX App Attach, MSI/EXE (WiX), App Installer feeds, portable EXE for side-loading.
- **Distribution targets:** Microsoft Store, enterprise Endpoint Manager/Intune, WinGet community/private repos, offline media.
- **Key workflows:** Continuous packaging from CI, delta updates, desktop bridge migrations, legacy Win32 wrapping.
- **Compliance checkpoints:** EV code signing, timestamping (RFC 3161), Device Guard policies, SmartScreen reputation, accessibility statements, export controls for crypto.

### macOS
- **Primary packages:** Notarized `.app` bundles, `.pkg` installers, `.dmg` images, Sparkle/Microsoft AutoUpdate feeds.
- **Distribution targets:** Mac App Store, direct download with notarization, enterprise MDM deployment, TestFlight equivalents via private feeds.
- **Key workflows:** Cross-host signing from non-macOS, entitlements management, universal binary handling, auto-update channels.
- **Compliance checkpoints:** Apple Developer ID program, notarization ticket stapling, Gatekeeper verification, privacy usage descriptions, export compliance declarations.

### Linux
- **Primary packages:** DEB, RPM, AppImage, Flatpak, Snap, OCI-based images.
- **Distribution targets:** Native distro repositories, private APT/YUM repos, Snap Store, Flathub, enterprise artifact caches, container registries.
- **Key workflows:** Dependency discovery, post-install hooks, sandbox permissions, multi-distro testing, reproducible builds.
- **Compliance checkpoints:** GPG signing for repos, SBOM publication, FIPS-aligned crypto libraries, sandbox confinement profiles, supply-chain attestation (in-toto/SLSA).

### Cross-Cutting Scenarios
- Unified project definition shared across GUI and automation.
- Remote signing services for hardware-backed certs.
- Offline/air-gapped packaging with pre-fetched toolchains.
- Telemetry opt-in for health metrics while preserving compliance boundaries (GDPR/CCPA).

## Task 2. Existing Tool User Journey Mapping

### 1. Onboard & Install
1. Install .NET SDK (6.0+, ideal 10+).
2. Install global tool via `dotnet tool install --global` (platform-specific packages for SDK < 10).
3. Launch GUI via the PackagingTools desktop app or run the `packagingtools` command to start the CLI once the tool is installed.
4. Configure optional telemetry settings or plugin dependencies as needed.

### 2. Project Authoring in GUI
1. Create or open a PackagingTools project JSON file.
2. Configure app metadata, target runtimes, package formats, signing credentials.
3. Save project for reuse across GUI and CLI workflows.

### 3. CLI Automation
1. Execute `packagingtools pack <project> -r <RID> -p <format> -o <outDir>`.
2. Tool bundles, signs, and packages according to project configuration.
3. Outputs installers and logs to artifacts directory for downstream release processes.

### 4. Maintenance & Updates
1. Update tool via `dotnet tool update` or platform-specific packages.
2. Monitor community releases for updates using `dotnet tool update` automation.
3. Troubleshoot via logs inside project folders; limited built-in telemetry/alerting.

**Friction Points Identified**
- Manual prerequisite verification (dotnet SDK, OS tooling) prior to install.
- Lack of guided setup for automation prerequisites and integration with existing build pipelines.
- Minimal policy enforcement or compliance visibility within project schema.
- Sparse observability: no built-in telemetry dashboards or diagnostics bundling.

## Task 3. Stakeholder Needs Summary

| Persona | Goals | Pain Points Today | Priority Needs |
| --- | --- | --- | --- |
| Application Developer | Package multi-platform release builds quickly. | Toolchain setup per OS, manual signing steps, limited automation documentation. | Guided setup, repeatable recipes, integration with build scripts.|
| Release/DevOps Engineer | Automate pipelines with auditability. | CLI feature gaps, limited API surface, scattered secrets handling. | Programmatic APIs/CLI with RBAC, policy enforcement, secret vault integration. |
| IT Administrator/Security | Ensure compliance and fleet governance. | Little visibility into signing/audit data, no role separation, manual evidence collection. | Central policy engine, audit logging, compliance exports, SSO integration. |
| Support/Customer Success | Maintain SLAs and troubleshoot packages. | Limited telemetry, fragmented logs, no proactive health metrics. | Unified diagnostics exports, telemetry pipeline, knowledge base integration. |

## Task 4. Avalonia Reuse Assessment

| Area | Candidate Sources | Notes |
| --- | --- | --- |
| Desktop UI Shell | `../Avalonia/src/Avalonia.Controls`, `../Avalonia/src/Avalonia.Themes.Fluent` | Provides windowing, navigation, docking patterns aligned with Fluent design. |
| Data Visualization & Controls | `../Avalonia/samples/ControlCatalog`, `../Avalonia/src/Avalonia.Controls.DataGrid` | Offers templates for dashboards, configuration grids, and diff views. |
| Functional UI Composition | `../Avalonia/src/Avalonia.FuncUI` | Enables declarative composition for wizard flows and dynamic validation. |
| Cross-Platform Services | `../Avalonia/src/Shared`, `../Avalonia/src/Avalonia.Platform` | Reusable abstractions for storage, platform detection, threading. |
| Diagnostics & Tooling | `../Avalonia/src/Avalonia.Diagnostics`, `../Avalonia/src/Avalonia.Desktop` | Potential for embedded inspector, logging viewers, and window management. |

**Reuse Opportunities**
- Adopt Fluent theme tokens for consistent branding.
- Leverage ControlCatalog examples as UX starting point for configuration editors and dashboards.
- Utilize `Avalonia.FuncUI` for testable wizard flows with strong typing.

## Task 5. Regulatory & Compliance Landscape

| Domain | Windows | macOS | Linux |
| --- | --- | --- | --- |
| Code Signing | EV/OV certificates, SmartScreen reputation, timestamping. | Developer ID certs, Apple notarization, ticket stapling. | GPG signatures for packages and repos; optional S/MIME for AppImage/AppStream metadata. |
| Store Submission | Microsoft Store certification, MSIX requirements. | App Store review guidelines, sandbox entitlements. | Snap Store/Flathub review policies; distro-specific QA. |
| Security Standards | Device Guard, Defender SmartScreen, optional FIPS. | Gatekeeper, TCC privacy prompts, hardened runtime. | SELinux/AppArmor policies, sandbox manifest requirements, supply-chain attestations. |
| Data Protection | GDPR/CCPA compliance in telemetry, crash reporting, credential storage. | Same as Windows; additional Apple privacy labeling. | Same as Windows; OSS licensing disclosures for bundled deps. |
| Accessibility | WCAG 2.2, Section 508 VPAT for packaging UI. | WCAG 2.2, EN 301 549. | WCAG 2.2 where mandated, plus distro-specific accessibility guidelines. |

## Task 6. Requirements Specification (Version 0.1)

### Functional Requirements
- **F1.** Provide GUI wizard to define multi-platform packaging projects with shared metadata and per-OS overrides.
- **F2.** Support remote and local signing flows with policy enforcement and secure secret storage.
- **F3.** Offer automation interfaces (CLI, SDK, APIs) fully feature-equivalent with GUI to support scripting and CI/CD workflows.
- **F4.** Enable cross-host packaging scenarios, including remote build agent orchestration and offline-ready tool caches.
- **F5.** Deliver compliance and audit outputs (logs, SBOMs, notarization receipts, manifest summaries) per build.
- **F6.** Integrate telemetry and diagnostics exports configurable by organization policy.

### Non-Functional Requirements
- **N1.** Cross-platform availability on Windows 10+, macOS 13+, Linux (glibc 2.27+/musl 1.22.2+) with consistent UX.
- **N2.** End-to-end packaging pipeline completes ≤15 minutes at 95th percentile for representative workloads.
- **N3.** Sensitive data (certificates, secrets) stored encrypted at rest using platform keystores or managed vaults.
- **N4.** Provide localization hooks and accessible UI meeting WCAG 2.2 AA.
- **N5.** Achieve 99% automated policy compliance enforcement coverage with audit trail.

### Community & Support Requirements
- **C1.** Provide clear contribution guidelines, governance model, and code of conduct for the OSS community.
- **C2.** Maintain active community support channels (forum, chat, issue tracker) with documented triage expectations.
- **C3.** Define optional commercial engagement points (e.g., consulting, long-term support) without restricting OSS access.

### Documentation & Enablement
- **D1.** Publish onboarding guides mirroring current global-tool steps plus enhanced environment checks.
- **D2.** Provide migration documentation for existing accelerate projects, including import tooling.
- **D3.** Maintain knowledge base and troubleshooting playbooks tailored to different adopter personas (individuals, teams, enterprises).

### Open Questions
1. Scope of offline/air-gapped environment support, including bootstrap of dependencies and signing assets distribution.
2. Preferred identity providers for SSO (Azure AD, Okta, others) and required protocols (OIDC, SAML).
3. Budget for remote builder infrastructure and whether managed service is desired.
4. Extent of telemetry data customers are willing to share; need consent flows.

---
**Milestone Outcome:** Discovery groundwork completed with actionable requirements, reuse targets, and prioritized stakeholder needs ready for architectural planning (Milestone 2).
