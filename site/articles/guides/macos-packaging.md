---
title: "macOS Packaging"
---

# macOS Packaging

`PackagingTools.Core.Mac` provides the macOS packaging pipeline for `.app`, `.pkg`, `.dmg`, signing, notarization, and audit capture. It is designed to use the same project model as the Windows and Linux providers while honoring the native Apple tooling requirements.

## Supported outputs

- `.app` bundle materialization for staged application payloads
- `.pkg` installers for enterprise or signed distribution flows
- `.dmg` disk images for end-user delivery
- Notarization handoff and verification-oriented result capture

## Required native tooling

Install the Xcode command-line tools and ensure the following commands are available:

- `codesign`
- `productbuild`
- `notarytool`
- `hdiutil`

The onboarding script under `build/scripts/check-tools.sh` validates these prerequisites before packaging.

## Common properties

Typical request or project properties include:

- `mac.app.bundleSource`
- `mac.pkg.component`
- `mac.dmg.sourceDirectory`
- `mac.signing.identity`
- `mac.notarytool.profile`

These values can live in the project JSON, be overridden on the CLI, or be injected at runtime through `PackagingRunOptions.Properties`.

## Example CLI invocation

```bash
dotnet run --project src/PackagingTools.Cli -- pack \
  --project ./samples/sample-project.json \
  --platform mac \
  --format app --format pkg --format dmg \
  --output ./artifacts/mac \
  --property mac.app.bundleSource=./artifacts/mac/Sample.app \
  --property mac.pkg.component=./artifacts/mac/Sample.app \
  --property mac.signing.identity="Developer ID Application: Contoso"
```

## Operational notes

- Keep bundle identifiers and signing identities in stable project metadata.
- Inject short-lived notarization or credential values at runtime instead of persisting them to source control.
- Use the same policy model described in [Policy Engine](../concepts/policy-engine.md) to require signing and notarization evidence for release runs.

## Related documentation

- [Installation and Onboarding](../getting-started/installation.md)
- [SDK Embedding](sdk-embedding.md)
- [Security and SBOM](../concepts/security-and-sbom.md)
