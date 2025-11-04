# PackagingTools.Cli

Cross-platform CLI entry point aligning with GUI workflows. Exposes commands for packaging, signing, policy validation, and artifact publishing. Wraps orchestration services from `PackagingTools.Core` and shares configuration models.

## Commands

- `packagingtools pack` — run packaging pipelines for a specified project/platform combination.
- `packagingtools host` — preview and apply Windows host integration metadata (shortcuts, protocols, file associations) with property diff output shared with the Avalonia UI.

All packaging invocations automatically run the policy engine, ensuring mandatory signing, approvals, and retention rules configured in project metadata are enforced before any pipelines execute.
