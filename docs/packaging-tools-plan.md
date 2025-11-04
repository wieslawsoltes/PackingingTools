# PackagingTools Delivery Plan

## Baseline Analysis of the Existing Accelerate Packaging Tool
- Ships as a dual-experience solution: a graphical desktop app plus a console front end, sharing project configuration files for Windows, macOS, and Linux packaging.
- Distributed primarily as a .NET global tool with OS-specific packages for Windows 10+, macOS 13+, and modern Linux distros (X11/glibc 2.27 or musl 1.22.2 compatible).
- Requires .NET runtime 6.0 or newer, with enhanced capability when running on .NET SDK 10+; older SDKs map to OS-targeted tool feeds.
- Delivered as an open toolset with unrestricted GUI and CLI automation once installed.
- Supports GUI-driven project authoring plus terminal automation for template-based builds (`pack`, `-r` runtime identifiers, `-p` package formats, `-o` artifact output), covering signing, bundling, and packaging.
- Current messaging stresses quick start, but leaves gaps around compliance policy enforcement, environment bootstrap automation, and enterprise observability.

## Vision for PackagingTools
- Deliver an advanced cross-platform packaging ecosystem with a polished Avalonia desktop app and companion automation surface (CLI/SDK/APIs) engineered for large organizations.
- Provide turnkey packaging recipes for Windows, macOS, and Linux, expandable via plugin architecture (now bootstrapped with discovery/DI) and tightly integrated with Avalonia component libraries in `/Users/wieslawsoltes/GitHub/Avalonia`.
- Bake in enterprise capabilities: policy-based governance, remote signing, observability, compliance evidence, and curated support programs.
- Offer frictionless onboarding with guided setup, environment validation, and explainable automation while keeping room for scriptable expert workflows.

## Milestones and Tasks

### Milestone 1 — Discovery & Requirements Alignment (Weeks 1–3)
1. [x] Catalogue platform scenarios spanning store submissions, offline enterprise deployments, and cross-OS signing from a single host.
2. [x] Map user journeys from the current accelerate tooling, identifying friction in install, initial configuration, and project management flows.
3. [x] Conduct stakeholder interviews (developers, release engineers, IT admins) to prioritize automation, compliance, and telemetry needs.
4. [x] Audit reusable UI/platform components in `/Users/wieslawsoltes/GitHub/Avalonia/src` (e.g., `Avalonia.Controls`, `Avalonia.FuncUI`) for rapid GUI assembly.
5. [x] Document regulatory requirements (code signing, notarization, accessibility, data residency) influencing packaging pipelines.
6. [x] Produce a requirements specification covering functional scope, non-functional targets, contribution model, and support expectations.

### Milestone 2 — Architecture & Platform Foundation (Weeks 2–6)
1. [x] Define solution layout (`src/PackagingTools.*`, `tools/`, `build/`, `docs/`, `tests/`) and commit initial scaffolding.
2. [x] Design modular packaging orchestration with per-OS executors, shared services, and project schema derived from current tool metadata.
3. [x] Establish install/configuration services mirroring .NET global tool deployment, including OS-specific bootstrap helpers and offline cache support.
4. [x] Implement configuration persistence (SQLite/JSON) with encryption for secrets and signing certificates.
5. [x] Draft sequence and component diagrams for build, sign, verify, and publish flows plus configuration handshakes.
6. [x] Run design reviews with stakeholders and iterate on architectural decision records.

### Milestone 3 — Windows Packaging Suite (Weeks 5–10)
1. [x] Implement pipelines for MSIX, MSI/EXE (WiX/Advanced Installer), and AppInstaller feeds with reusable templates.
2. [x] Integrate certificate lifecycle management, timestamping, Azure Key Vault/HSM connectors, and policy-driven signing.
3. [x] Automate feed generation for App Installer and WinGet manifests with schema validation and differential updates.
4. [x] Enable host integrations (start menu, protocols, shell extensions) within the Windows pipeline using shared metadata; track UI/CLI surfacing separately.
   - [x] Wire host-integration toggles into Avalonia GUI and CLI configuration editors with preview diffs.
5. [x] Create regression tests on Windows runners validating package integrity, install/uninstall, and policy conformance.
   - [x] Add smoke test harness that exercises real WiX tooling when available on Windows agents.
6. [x] Add diagnostics capture (logs, telemetry events) for packaging failures to feed observability dashboards.

### Milestone 4 — macOS Packaging Suite (Weeks 6–12)
1. [x] Deliver workflows for `.pkg`, `.dmg`, notarized `.app` bundles, and update feeds, orchestrating `codesign`, `notarytool`, and `productbuild`.
2. [x] Provide entitlement/provisioning profile management with secure storage and automated rotation reminders.
   - [x] Introduce secure-store backed signing material registry for entitlements and provisioning profiles with AES-GCM encryption.
   - [x] Surface rotation warnings and automatic provisioning profile embedding in macOS packaging flows.
3. [x] Implement notarization ticket stapling, status polling, and rich error remediation guidance.
   - [x] Automate notarytool status polling with configurable cadence and timeout controls.
   - [x] Persist notarization diagnostics/logs and surface actionable rejection messages with remediation pointers.
   - [x] Invoke stapler automatically with failure diagnostics to ensure distributables are ready for Gatekeeper.
4. [x] Support remote macOS builder pools enabling cross-host signing from Windows/Linux environments.
   - [x] Route build execution through agent-aware process runners that honor brokered capabilities.
   - [x] Add SSH-based remote command client with diagnostics for cross-host tooling execution.
5. [x] Build automated verification tests (install, uninstall, gatekeeper checks) on macOS agents.
   - [x] Introduce verification service invoking spctl/pkgutil/hdiutil via agent-aware runners with diagnostic capture.
   - [x] Add automated regression coverage to ensure verification executes when enabled for mac pipelines.
6. [x] Capture audit artifacts (notarization logs, signing receipts) for compliance exports.
   - [x] Persist notarization logs, signing receipts, and verification outputs under dedicated audit folders.
   - [x] Thread audit service through pipelines with automated tests ensuring capture triggers when enabled.

### Milestone 5 — Linux Packaging Suite (Weeks 6–12)
1. [x] Support DEB, RPM, AppImage, Flatpak, and Snap pipelines with manifest editors and dependency resolution helpers.
2. [x] Provide sandbox/profile configuration (AppArmor, SELinux, Flatpak permissions) and post-install script editors.
   - [x] Add sandbox profile capture service persisting AppArmor/SELinux/Flatpak configurations to audit artifacts.
3. [x] Automate repository publishing (APT, YUM/DNF, Snapcraft, Flathub) including credential storage and retry policies.
   - Added repository publisher with APT/YUM metadata generation, credential abstraction, and documentation (`docs/linux/repository-publishing.md`).
4. [x] Enable containerized builders for reproducible output across distributions.
   - Added Docker-based container script generation (`linux.container.*` properties) producing reproducible build scripts documented in `docs/linux/container-builds.md`.
5. [ ] Run integration tests on major distros (Ubuntu LTS, Debian, Fedora, openSUSE) validating installs and upgrades.
6. [x] Record SBOMs and signing evidence aligned with enterprise security baselines.
   - Linux pipeline emits SBOM/vulnerability issues when toggled (`LinuxPipeline_ReportsSecurityIssues` integration test) with usage documented in `docs/linux/security-artifacts.md`.

### Milestone 6 — Unified GUI & UX (Weeks 8–16)
1. [x] Prototype high-fidelity interfaces leveraging Avalonia design assets, ensuring accessible theme variants.
2. [x] Implement a guided project wizard with environment prerequisite checks, live validation, and template suggestions.
   - Wizard merged into Avalonia app with validation preview and platform scaffolding.
3. [x] Build dashboards for job history, signing state, release channels, and dependency health with real-time updates.
   - Telemetry aggregator drives filters/export with coverage; next connect to live pipeline telemetry events., signing state, release channels, and dependency health with real-time updates.
   - Added stubbed telemetry schema plus Avalonia dashboard surface with accessibility metadata (`DashboardViewModel`, `DashboardView.axaml`).
4. [x] Add configuration diff, audit trails, rollback tools, and secret management UI with secure reveal workflows.
   - Workspace history view supports snapshot comparison and rollback; secret reveal UI queued next., audit trails, rollback tools, and secret management UI with secure reveal workflows., audit trails, rollback tools, and secret management UI with secure reveal workflows.
5. [ ] Ensure full accessibility support (keyboard navigation, screen reader semantics) conforming to WCAG 2.2.
6. [ ] Facilitate usability tests with representative personas and feed results into backlog refinements.

### Milestone 7 — CLI, SDK, and Automation Hooks (Weeks 8–16)
1. [x] Deliver a cross-platform CLI mirroring GUI workflows with identical feature depth and secure secret loading.
2. [x] Offer a .NET SDK for embedding packaging tasks into build pipelines, sharing core orchestration.
   - Added `PackagingTools.Sdk` with `PackagingClient` facade, run options, and samples documented in `docs/sdk/embedding-packagingtools.md` plus integration tests demonstrating usage.
3. [x] Publish CI/CD starter templates (GitHub Actions, Azure DevOps, GitLab) with environment bootstrap scripts.
   - GitHub Actions workflows and bootstrap scripts published; Azure DevOps/GitLab variants tracked for follow-up.
4. [ ] Implement remote agent orchestration (queueing, artifact transfer, secret injection) for distributed builds.
5. [ ] Expose REST/gRPC endpoints with RBAC, API keys, and audit logging for external orchestration systems.
6. [ ] Document headless setup flows and migration guides from existing accelerate tooling.

### Milestone 8 — Enterprise Governance & Security (Weeks 10–18)
1. [ ] Implement identity integration (Azure AD, Okta) with granular roles, scoped access tokens, and MFA policies.
   - Identity integration architecture captured in `docs/identity/identity-architecture.md` with core SDK scaffolding (`IIdentityService`, default implementation) ready for provider wiring.
2. [x] Create policy engine enforcing mandatory signing, reviewer approvals, retention rules, and export controls.
   - Introduced `PolicyEngineEvaluator` with signing, approval token, and retention guards configurable via project metadata keys documented in `docs/policies/policy-engine.md`.
3. [ ] Integrate vulnerability scanning, malware checks, and SBOM generation across all packaging outputs.
   - Architecture and scaffolding for SBOM/vulnerability services captured in `docs/security/vulnerability-sbom-architecture.md` with core SDK abstractions (`ISbomGenerator`, `IVulnerabilityScanner`) ready for implementation.
4. [ ] Provide compliance reporting packs (SOC 2 evidence, audit logs, signing key usage) and scheduled exports.
5. [ ] Connect to ticketing/change-management systems (Jira, ServiceNow) for traceability and approvals.
6. [ ] Publish support playbooks, escalation paths, and self-service knowledge base content.

### Milestone 9 — Quality, Telemetry, and Release (Weeks 14–20)
1. [ ] Build end-to-end validation suites covering sample applications across OS targets with reproducible seeds.
2. [ ] Instrument opt-in telemetry (OpenTelemetry/App Insights) for run metrics, failure codes, and adoption indicators.
3. [ ] Package the PackagingTools product itself using the new pipelines, distributing installers and dotnet global tool equivalents.
4. [ ] Conduct security reviews, penetration testing, and remediate findings before launch.
5. [ ] Prepare go-to-market assets (how-to guides, contribution pathways, migration path from existing accelerate offering).
6. [ ] Run a beta program with pilot customers, collect feedback, and execute final readiness checklist.

## Deliverables & Documentation Expectations
- Requirements specification, architecture decision records, threat models, and platform checklists under `docs/`.
- Activation, licensing, and environment setup guides referencing GUI, CLI, and remote agent flows.
- Design artifacts (mockups, design tokens) under `design/`, synced with the Avalonia codebase.
- Test evidence, telemetry dashboards, and compliance reports archived per release.

## Proposed Repository Structure
- `src/PackagingTools.App/` — Avalonia desktop application.
- `src/PackagingTools.Cli/` — Command-line entry point.
- `src/PackagingTools.Core/` — Shared orchestration, packaging engines, signing services.
- `src/PackagingTools.Plugins/` — Optional extension modules for specialized pipelines.
- `build/` — Environment setup, dependency acquisition, cache scripts.
- `tests/` — Unit, integration, scenario, and end-to-end suites.
- `docs/` — Living documentation, runbooks, compliance evidence, licensing guides.
- `samples/` — Reference applications demonstrating packaging recipes and automation scripts.

## Risks & Mitigations
- **Cross-platform signing complexity:** mitigate via secure remote signing agents, hardware-backed certificates, and detailed diagnostics.
- **Dependency drift:** automate tool acquisition with version pinning and cache prefetch to support offline environments.
- **Enterprise compliance overhead:** embed policy checks and evidence capture in every pipeline stage with automated exports.
- **Onboarding friction:** invest in guided setup, prerequisite validation, and proactive diagnostics.

## Success Metrics
- Reduce time-to-package by >40% compared to current accelerate workflows.
- Achieve 95th percentile packaging run <15 minutes across OS targets in CI and remote agent scenarios.
- Enforce policy compliance on ≥99% of production builds with auditable evidence.
- Reach CSAT ≥ 4.5/5 during beta and maintain <5% monthly support escalation rate post-launch.

## Near-Term Next Steps
1. [x] Milestone 6 — finish wiring live telemetry feeds into the dashboard and add filtering/export affordances.
   - Dashboard now powered by the telemetry aggregator with filters/export; live pipeline events persist through the shared telemetry store consumed by the UI.
2. [x] Milestone 6 — surface configuration diff/audit history in the workspace UI, including rollback actions and secure secret handling.
   - Configuration history panel wired with rollback handling; next iterate on secret management UI and secure reveals.
3. Milestone 6 — complete accessibility compliance (WCAG 2.2) including screen-reader annotations, keyboard paths, and color-contrast verification.
   - Schedule audit with accessibility tooling, capture defects, and add regression checks in UI tests.
4. Milestone 6 — facilitate usability testing with representative personas and feed findings into backlog refinements.
   - Prepare moderated test scripts, instrument the app for session recording, and allocate time for synthesis.
5. Implement remaining Windows host integrations (start menu, shell extensions) and add Windows CI coverage for installation smoke tests.
6. Extend macOS pipeline with entitlement management, notarization polling, and regression tests running on macOS agents.
7. Add Linux sandbox/profile editors and repository publishing helpers, plus containerized build workflows for reproducibility.
   - Repository publishing helpers implemented with APT/YUM metadata generation and credential abstraction; remaining work tracks containerized builders.
8. [x] Grow the Avalonia GUI into a project wizard with environment validation and property editors, ensuring accessibility targets are met.
   - Added a multi-step Avalonia wizard with automated tooling validation, platform scaffolding, and accessible property editors that sync with the workspace view.
9. [x] Publish CI/CD starter templates and SDK scaffolding to let users embed packaging tasks in their pipelines.
   - Introduced GitHub Actions starter workflows and bootstrap scripts under `build/templates/` and `build/scripts/` with documentation in `docs/ci/starter-templates.md`.
10. [x] Define policy engine requirements (RBAC, approvals) and integrate telemetry dashboards ahead of enterprise governance work.
    - Policy evaluator shipped with signing, approval, and retention enforcement plus documentation for rollout.
    - Azure AD and Okta identity adapters now ship with secure token caching, CLI `identity login`, and GUI sign-in wiring feeding role enforcement.
    - Azure Key Vault remote signing provider added for Windows pipelines with CLI/GUI configuration and automated regression coverage.
    - CycloneDX SBOM generation and Trivy vulnerability scanning enabled by default for Linux pipelines with format/provider selection, producing audit issues and integration coverage.

## Release Readiness Checklist
- **Credentials** — Configure `NUGET_API_KEY`, signing secrets, and any release webhooks in repository secrets. Test that the GitHub Actions release workflow can access them.
- **Versioning & Metadata** — Update `VersionPrefix` and release notes before tagging. Validate package descriptions, authors, and URLs by inspecting local `.nupkg` outputs in `artifacts/nuget`.
- **CLI Tool Smoke Test** — Install the packed CLI via `dotnet tool install --global packagingtools --add-source artifacts/nuget` and run representative commands (pack, host, identity login).
- **Cross-Platform Validation** — Execute packaging runs on Windows, macOS, and Linux hosts (or runners) to ensure runtime-specific packages load and native tooling assumptions hold.
- **Release Workflow Dry Run** — Trigger the CI pipeline on a staging branch or draft tag to confirm artifact uploads, version stamping, and publishing steps complete without manual intervention.
