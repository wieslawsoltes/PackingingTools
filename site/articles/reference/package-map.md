---
title: "Package Map"
---

# Package Map

The repository publishes seven NuGet packages. The desktop app stays repository-only.

| Package | Responsibility |
| --- | --- |
| `PackagingTools.Core` | Shared orchestration engine, project model, policy services, secure storage, audit, and telemetry primitives. |
| `PackagingTools.Core.Windows` | Windows format providers, signing integrations, WiX/MSIX support, and host integration tooling. |
| `PackagingTools.Core.Mac` | macOS bundle, package, disk image, signing, notarization, verification, and audit services. |
| `PackagingTools.Core.Linux` | Linux format providers, repository publishing, container build scripts, sandbox profiles, and security evidence services. |
| `PackagingTools.Plugins` | Contracts and helpers for externally loaded plugins that extend formats, telemetry, policy, or signing behavior. |
| `PackagingTools.Sdk` | High-level .NET client API centered on `PackagingClient` and `PackagingRunOptions`. |
| `PackagingTools.Cli` | Global tool and repository CLI entry point for scripted packaging, validation, and automation. |

## Non-packaged application

`src/PackagingTools.App` hosts the Avalonia desktop experience and is intentionally not published as a NuGet package.

## API entry points

Start with these namespaces when integrating:

- `PackagingTools.Core.Models`
- `PackagingTools.Core.Abstractions`
- `PackagingTools.Sdk`
- `PackagingTools.Core.Windows`
- `PackagingTools.Core.Mac`
- `PackagingTools.Core.Linux`

Use the [API Reference](/api) for generated type and member navigation.
