---
title: "PackagingTools"
layout: simple
og_type: website
---

<div class="pt-hero">
  <div class="pt-eyebrow"><i class="bi bi-box-seam" aria-hidden="true"></i> Cross-Platform Packaging for .NET</div>
  <h1>PackagingTools</h1>

  <p class="lead"><strong>PackagingTools</strong> is a shared packaging stack for building, signing, validating, and publishing Windows, macOS, and Linux installers from one project model. It combines a desktop workspace, CLI automation, reusable SDK, platform engines, and generated API reference in one repository.</p>

  <div class="pt-hero-actions">
    <a class="btn btn-primary btn-lg" href="articles/getting-started/overview"><i class="bi bi-rocket-takeoff" aria-hidden="true"></i> Start Getting Started</a>
    <a class="btn btn-outline-secondary btn-lg" href="api"><i class="bi bi-braces" aria-hidden="true"></i> Browse API</a>
    <a class="btn btn-outline-secondary btn-lg" href="https://github.com/wieslawsoltes/PackingingTools"><i class="bi bi-github" aria-hidden="true"></i> GitHub Repository</a>
  </div>

  <div class="pt-stat-grid">
    <div class="pt-stat-card">
      <span class="pt-stat-value">7</span>
      <span class="pt-stat-label">NuGet packages</span>
    </div>
    <div class="pt-stat-card">
      <span class="pt-stat-value">3</span>
      <span class="pt-stat-label">platform engines</span>
    </div>
    <div class="pt-stat-card">
      <span class="pt-stat-value">3</span>
      <span class="pt-stat-label">automation surfaces</span>
    </div>
  </div>
</div>

## Start Here

<div class="pt-link-grid">
  <a class="pt-link-card" href="articles/getting-started/installation">
    <span class="pt-link-card-title"><i class="bi bi-download" aria-hidden="true"></i> Installation and Onboarding</span>
    <p>Install .NET 10, validate native toolchains, and build the solution locally.</p>
  </a>
  <a class="pt-link-card" href="articles/getting-started/project-configuration">
    <span class="pt-link-card-title"><i class="bi bi-sliders" aria-hidden="true"></i> Project Configuration</span>
    <p>Learn the JSON project model, platform properties, and runtime overrides.</p>
  </a>
  <a class="pt-link-card" href="articles/getting-started/cli-quickstart">
    <span class="pt-link-card-title"><i class="bi bi-terminal" aria-hidden="true"></i> CLI Quickstart</span>
    <p>Run cross-platform packaging flows from scripts or CI with repeatable arguments.</p>
  </a>
  <a class="pt-link-card" href="articles/guides/windows-packaging">
    <span class="pt-link-card-title"><i class="bi bi-windows" aria-hidden="true"></i> Windows Packaging</span>
    <p>Build MSIX, MSI, App Installer, and WinGet outputs with signing and host integration.</p>
  </a>
</div>

## Documentation Sections

<div class="pt-link-grid pt-link-grid--wide">
  <a class="pt-link-card" href="articles/getting-started">
    <span class="pt-link-card-title"><i class="bi bi-signpost-split" aria-hidden="true"></i> Getting Started</span>
    <p>Choose between the desktop app, CLI, and SDK and get to a first successful packaging run.</p>
  </a>
  <a class="pt-link-card" href="articles/concepts">
    <span class="pt-link-card-title"><i class="bi bi-diagram-3" aria-hidden="true"></i> Concepts</span>
    <p>Understand the architecture, identity model, policy engine, and security pipeline.</p>
  </a>
  <a class="pt-link-card" href="articles/guides">
    <span class="pt-link-card-title"><i class="bi bi-journal-code" aria-hidden="true"></i> Guides</span>
    <p>Platform-focused documentation for Windows, macOS, Linux, plugins, and SDK embedding.</p>
  </a>
  <a class="pt-link-card" href="articles/advanced">
    <span class="pt-link-card-title"><i class="bi bi-gear-wide-connected" aria-hidden="true"></i> Advanced</span>
    <p>Use the repository workflows, starter templates, and docs publishing pipeline in CI/CD.</p>
  </a>
  <a class="pt-link-card" href="articles/reference">
    <span class="pt-link-card-title"><i class="bi bi-collection" aria-hidden="true"></i> Reference</span>
    <p>Review package responsibilities, repository layout, roadmap artifacts, and ADRs.</p>
  </a>
  <a class="pt-link-card" href="api">
    <span class="pt-link-card-title"><i class="bi bi-braces-asterisk" aria-hidden="true"></i> API Documentation</span>
    <p>Generated .NET API pages for the core libraries, platform engines, SDK, plugins, and CLI.</p>
  </a>
</div>

## Project Surfaces

- <span class="pt-pill">Desktop App</span> `src/PackagingTools.App` provides a guided Avalonia workspace for onboarding, environment validation, and project authoring.
- <span class="pt-pill">CLI</span> `src/PackagingTools.Cli` runs packaging, validation, host integration, and release-oriented automation from scripts or GitHub Actions.
- <span class="pt-pill">SDK</span> `src/PackagingTools.Sdk` embeds the same orchestration pipeline inside custom services and enterprise build systems.

## Repository

- Source code and issues: [github.com/wieslawsoltes/PackingingTools](https://github.com/wieslawsoltes/PackingingTools)
- Published docs: [wieslawsoltes.github.io/PackingingTools](https://wieslawsoltes.github.io/PackingingTools)
