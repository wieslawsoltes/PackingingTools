---
title: "Repository Layout"
---

# Repository Layout

```text
PackingingTools/
├── src/                # App, CLI, SDK, core libraries, platform engines, plugin contracts
├── build/              # Bootstrap scripts, tool checks, and reusable workflow templates
├── docs/               # Source markdown and architecture artifacts that back the Lunet site
├── site/               # Lunet site configuration, menus, theme overrides, and generated API docs config
├── samples/            # Sample project definitions and packaging payload recipes
├── tests/              # Cross-platform integration coverage and regression suites
└── tools/              # Supporting validation and diagnostics scripts
```

## Notable folders

- `src/PackagingTools.Core*` contains the reusable packaging engines.
- `build/templates/github-actions` contains starter GitHub Actions workflow templates for downstream consumers.
- `site/.lunet` contains the Lunet-specific styling and layout overrides used to render this documentation site.
- `docs/architecture/adr` stores the architecture decision records mirrored into the site reference section.

## Recommended contributor flow

1. Run the bootstrap or tool-check scripts for your target platform.
2. Build and test the solution from the repository root.
3. Update both the markdown source and the Lunet site structure when documentation needs to move.
4. Validate the site with `./build-docs.sh` before merging changes that affect docs or API surface.
