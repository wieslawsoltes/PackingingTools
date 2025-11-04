# ADR 0003 — macOS Packaging Toolchain Selection

## Status
Accepted

## Context
PackagingTools must support native macOS distribution paths: signed app bundles, installer PKGs, DMG images, and notarized artifacts. The macOS ecosystem prescribes specific CLI tooling (`codesign`, `productbuild`, `hdiutil`, `notarytool`) for these tasks, and adopting them preserves compatibility with Apple's notarization and Gatekeeper requirements.

## Decision
- Provide a dedicated `PackagingTools.Core.Mac` module responsible for macOS packaging orchestration.
- Use `productbuild` to generate installer packages, `hdiutil` for DMG images, and `notarytool` for notarization workflows.
- App bundles are staged locally before signing via the shared `ISigningService` abstraction, allowing hardware-backed or remote signing providers.
- The pipelines rely on an injectable `IMacProcessRunner` so hosts can execute commands locally or via remote mac builder agents.

## Consequences
- macOS build agents must have Xcode command-line tools installed (providing `productbuild`, `notarytool`, `hdiutil`, `codesign`).
- Payload staging is currently mocked and needs integration with actual Avalonia build outputs in future milestones.
- Further enhancements (Sparkle feeds, pkgbuild customization) can extend the module without changing the core decision.
