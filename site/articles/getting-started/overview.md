---
title: "Overview"
---

# Getting Started Overview

PackagingTools is organized around one shared project model that can be executed from three entry points:

- **Desktop app** for interactive onboarding, environment validation, and editing packaging metadata.
- **CLI** for scripted local runs and CI/CD automation.
- **SDK** for embedding packaging orchestration inside custom services or build systems.

## Recommended path

1. Start with [Installation and Onboarding](installation.md) to install the .NET SDK and native platform toolchains.
2. Read [Project Configuration](project-configuration.md) to understand the portable JSON schema and provider-specific properties.
3. Use [CLI Quickstart](cli-quickstart.md) for the first end-to-end packaging run.
4. If your team prefers a guided workflow, use the [Desktop Project Wizard](desktop-project-wizard.md) to create and refine the same configuration interactively.

## What the repository gives you

- Shared orchestration through `PackagingTools.Core`
- Platform engines for Windows, macOS, and Linux
- Plugin contracts for extending formats, signing, telemetry, and policy
- GitHub Actions workflows for validation, release publishing, and docs deployment
- Generated .NET API documentation for the reusable packages

## Next reading

- [Solution Architecture](../concepts/solution-architecture.md)
- [Windows Packaging](../guides/windows-packaging.md)
- [SDK Embedding](../guides/sdk-embedding.md)
