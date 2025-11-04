# CI/CD Starter Templates

`build/templates/github-actions` contains ready-to-adapt GitHub Actions workflows for each platform. They assume PackagingTools runs directly from the repository (no pre-built global tool required) and surface a consistent artifact folder for downstream deployment jobs.

## Windows
- Workflow: `build/templates/github-actions/windows-packaging.yml`
- Highlights:
  - Uses `actions/setup-dotnet` to install .NET 10.
  - Invokes `build/scripts/bootstrap-packagingtools.ps1` to restore the solution and validate WiX/MSIX tooling via `check-tools.ps1`.
  - Runs `packagingtools pack` targeting MSI/MSIX configuration from the project file.

## macOS
- Workflow: `build/templates/github-actions/macos-packaging.yml`
- Highlights:
  - Boots on `macos-13` runners, installs .NET, and reuses the cross-platform bootstrap script.
  - Produces notarization-ready artifacts (assuming signing credentials are provided via secrets/env variables).

## Linux
- Workflow: `build/templates/github-actions/linux-packaging.yml`
- Highlights:
  - Installs system dependencies (RPM, Flatpak, Snapcraft, FPM) before running PackagingTools.
  - Uploads generated DEB/RPM/AppImage outputs from `artifacts/linux`.

## Using the Templates
1. Copy the desired template into `.github/workflows/` (rename as needed).
2. Update `PROJECT_FILE` and `ARTIFACTS_DIR` to match your PackagingTools project.
3. Inject secrets for signing (e.g., `windows.signing.certificatePath`) as environment variables or GitHub Actions encrypted secrets.
4. Extend the workflow by adding deployment jobs (WinGet submission, notarization log upload, repo publishing) once packaging artifacts are generated.

## SDK Scaffolding Notes
- The bootstrap scripts (`build/scripts/bootstrap-packagingtools.ps1` and `.sh`) double as local SDK entry points. Developers can run them before invoking `packagingtools pack` to ensure the .NET projects compile and native tooling prerequisites are met.
- Tie these scripts into other CI platforms (Azure Pipelines, GitLab CI, etc.) by calling them from the platform-specific shell and passing the desired platform argument (`windows`, `mac`, or `linux`).
