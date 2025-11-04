# ADR 0004 — Linux Packaging Toolchain Selection

## Status
Accepted

## Context
PackagingTools must generate installers for major Linux distribution formats, covering Debian/Ubuntu, RPM-based distros, universal bundles (AppImage), sandboxed stores (Flatpak, Snap), and repository integration. Each ecosystem has established command-line tools that teams already rely on (
`dpkg-deb`/`fpm`, `rpm`/`fpm`, `appimagetool`, `flatpak-builder`, `snapcraft`). Leveraging these tools preserves distro compatibility and aligns with compliance expectations (GPG signing, sandbox profiles).

## Decision
- Introduce `PackagingTools.Core.Linux` housing a pipeline that routes to format providers for DEB, RPM, AppImage, Flatpak, and Snap.
- Base initial implementations on wrapper commands (`fpm`, `appimagetool`, `flatpak-builder`, `snapcraft`) with placeholders where deeper manifest generation is required.
- Provide an `ILinuxProcessRunner` abstraction so execution can occur either locally or via containerized/remote builders.
- Gather metadata for repo publishing and sandbox configuration via project properties, ready to feed into specialized providers later.

## Consequences
- Linux agents must have required CLI tooling installed; bootstrap scripts will detect and install dependencies (apt, dnf, snapcraft, flatpak).
- Current format providers include placeholders for payload staging and manifest generation; subsequent milestones will replace them with full implementations.
- Additional formats (e.g., OCI images, Helm charts) can be added through plugin providers without altering the architecture.
