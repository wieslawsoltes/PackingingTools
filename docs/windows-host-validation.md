# Windows Host Integration Validation Guide

Use this checklist to exercise the `packagingtools host` workflow on a Windows runner and confirm the WiX metadata is honored end-to-end.

## Prerequisites
- Windows 11/Windows Server 2022 runner with the WiX v4 toolset (`heat.exe`, `candle.exe`, `light.exe`) available on `PATH`.
- .NET SDK 10.x preview (matching the repo) installed.
- PowerShell 7+ recommended for strict mode support.

## Steps
1. Clone the repository and open an elevated PowerShell prompt.
2. Execute the validation script:
   ```powershell
   pwsh tools/windows/validate-host-integration.ps1
   ```
   - The script provisions a sample payload under `samples/sample/payload`, runs `packagingtools host` in preview mode, applies the configuration, and finally invokes `packagingtools pack` to build an MSI.
3. Inspect the console output:
   - Preview phase should list property diffs and any warnings (for example missing custom icon).
   - Apply phase must succeed without errors.
   - Packaging phase should complete and produce an MSI inside `artifacts/host-validation`.
4. Optionally open the generated `Product.wxs` file in the output directory to confirm the shortcut, protocol handler, and file association fragments match the applied metadata.

## CI Integration
Add the following job to GitHub Actions (Windows runner) once WiX tooling is available:
```yaml
jobs:
  windows-host-validation:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - name: Install .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - name: Install WiX Toolset v4
        run: choco install wix --version=4.0.1 --no-progress
      - name: Validate host integration
        run: pwsh tools/windows/validate-host-integration.ps1
```
Ensure the WiX installation path is added to `PATH` when running outside of Chocolatey.
