# Developer Onboarding

## Prerequisites
- .NET SDK 10 (preview acceptable; ensure `dotnet --version` prints 10.x).
- Platform tooling (install as needed):
  - Windows: Windows SDK (makeappx, signtool) and WiX Toolset 4 (`heat`, `candle`, `light`).
  - macOS: Xcode command-line tools providing `productbuild`, `codesign`, `notarytool`, `hdiutil`.
  - Linux: Packaging CLI dependencies (`fpm`, `appimagetool`, `flatpak-builder`, `snapcraft`, `gpg`).
- Git LFS if large assets will be stored (optional).

## Getting Started
1. Clone both the PackagingTools repo and the Avalonia reference repo specified in the plan (used for UI reuse).
2. Validate platform tooling (PowerShell or bash):
   ```powershell
   pwsh build/scripts/check-tools.ps1 -Platform windows
   ```
   ```bash
   ./build/scripts/check-tools.sh mac
   ```
   Review the output and install missing dependencies before running pipelines.
3. Restore dependencies and build all targets:
   ```bash
   dotnet build PackagingTools.sln
   ```
4. Run integration tests (stubs simulate external tooling output):
   ```bash
   dotnet test PackagingTools.sln
   ```
5. Review the configuration schema reference under `docs/configuration/index.md` for field descriptions.
6. Prepare platform payloads:
   - Windows: build your Avalonia app and stage binaries/assets into a directory referenced via `windows.msix.payloadDirectory` / `windows.msi.sourceDirectory`.
   - macOS: export the `.app` bundle to supply `mac.app.bundleSource` / `mac.pkg.component` properties.
   - Linux: stage a FHS-compliant root under `linux.packageRoot`, provide Flatpak manifest and snapcraft YAML through CLI properties or project file.

## CLI Workflow
1. Define a project JSON file describing metadata, platform configurations, and default properties:
   ```json
   {
     "id": "sample",
     "name": "Sample App",
     "version": "1.0.0",
     "metadata": {
       "windows.identityName": "Contoso.Sample",
       "windows.publisher": "CN=Contoso",
       "mac.bundleId": "com.contoso.sample",
       "linux.architecture": "amd64"
     },
     "platforms": {
       "Windows": {
         "formats": ["msix", "msi"],
         "properties": {
           "windows.msix.payloadDirectory": "./artifacts/win",
           "windows.msi.sourceDirectory": "./artifacts/win"
         }
       },
       "MacOS": {
         "formats": ["app", "pkg"],
         "properties": {
           "mac.app.bundleSource": "./artifacts/mac/Sample.app",
           "mac.pkg.component": "./artifacts/mac/Sample.app"
         }
       },
       "Linux": {
         "formats": ["deb", "rpm"],
         "properties": {
           "linux.packageRoot": "./artifacts/linux/root"
         }
       }
     }
   }
   ```
2. Run the CLI:
   ```bash
   dotnet run --project src/PackagingTools.Cli -- pack \
     --project sample.project.json \
     --platform windows \
     --format msix --format msi \
     --property windows.signing.certificatePath="certs/code-sign.pfx" \
     --property windows.signing.password="secret"
   ```
3. Artifacts write to `./artifacts` by default; override with `--output`.
4. Include `--save-project` to persist runtime overrides back to disk, keeping CLI and GUI views in sync.

## Visual Studio / IDE Notes
- Add `PackagingTools.sln` to your IDE workspace.
- For Avalonia GUI work, open `src/PackagingTools.App/PackagingTools.App.csproj`. The starter screen loads project definitions using the shared workspace services and will evolve with richer editing capabilities.
- When debugging pipelines, set environment variables `PACKAGINGTOOLS_TRACE=1` to enable verbose logging (future enhancement placeholder).

## Contributing
- Run `dotnet format` across changed projects before submitting PRs.
- Provide integration test coverage for new providers or pipeline behaviors using the stub runners in `PackagingTools.IntegrationTests`.
- Update ADRs when adopting new tooling or cross-platform strategies.
