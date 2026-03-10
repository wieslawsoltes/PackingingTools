---
title: "CI and Release Automation"
---

# CI and Release Automation

This repository ships with three complementary GitHub Actions workflows:

- `CI` validates the solution on Linux, macOS, and Windows, then packs the NuGet artifacts.
- `Release` runs the same validation for `v*` tags, packs all publishable packages with the tag version, publishes them to NuGet, and creates a GitHub release.
- `Docs` builds the Lunet site and publishes `site/.lunet/build/www` to GitHub Pages on `main`.

## Build scripts

The documentation pipeline intentionally mirrors the TreeDataGrid setup:

- `build-docs.sh`
- `build-docs.ps1`
- `.config/dotnet-tools.json`

Those scripts restore the local `lunet` tool and build the site from the `site/` folder, making the docs build reproducible both locally and in CI.

## Local validation

```bash
dotnet tool restore
./build-docs.sh
```

On Windows PowerShell:

```powershell
dotnet tool restore
./build-docs.ps1
```

## Extending the pipeline

- Use [Starter Workflows](starter-workflows.md) as the base for product-specific packaging jobs.
- Keep the docs job independent from release publication so site updates can ship with normal `main` pushes.
- Archive `test-results/`, `artifacts/nuget`, and the generated docs folder when troubleshooting CI failures.

## Repository workflows

- [CI workflow](https://github.com/wieslawsoltes/PackingingTools/blob/main/.github/workflows/ci.yml)
- [Release workflow](https://github.com/wieslawsoltes/PackingingTools/blob/main/.github/workflows/release.yml)
- [Docs workflow](https://github.com/wieslawsoltes/PackingingTools/blob/main/.github/workflows/docs.yml)
