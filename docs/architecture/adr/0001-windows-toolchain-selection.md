# ADR 0001 — Windows Packaging Toolchain Selection

## Status
Accepted

## Context
The PackagingTools project must deliver first-class Windows packaging support that covers the major installer/update channels used by enterprise teams (MSIX, MSI/EXE, App Installer feeds, and WinGet manifests). The existing Accelerate tooling uses the Windows SDK (`makeappx`), WiX toolset, and WinGet CLI for these flows. Reusing the same ecosystem keeps parity for Avalonia developers while aligning with Windows certification/compliance requirements.

## Decision
- **MSIX** packaging is implemented via the Windows SDK's `makeappx.exe` utility, invoked through the `IProcessRunner` abstraction.
- **MSI/EXE** packaging is authored with WiX v4 (`candle.exe`/`light.exe`) to retain full control over component tables and custom actions.
- **App Installer** feeds are generated locally via templated XML referencing the produced MSIX artifacts.
- **WinGet** manifests are emitted as YAML using schema-aligned fields, sourcing installer metadata from packaging outputs.
- All tooling is orchestrated from the new `PackagingTools.Core.Windows` module, ensuring cross-surface reuse (GUI, CLI, automation).

## Consequences
- Windows packaging requires the Windows SDK and WiX toolset to be present on build agents. Bootstrap scripts will validate and install the dependencies.
- The pipelines can plug into existing enterprise processes that already depend on MSIX/MSI/WinGet without custom adapters.
- Additional future work may be required to support alternative toolchains (e.g., Advanced Installer) but can be added through the plugin system without altering the baseline decision.
