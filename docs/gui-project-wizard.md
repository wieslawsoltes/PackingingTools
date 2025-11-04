# PackagingTools GUI Project Wizard

The Avalonia desktop application now includes a guided project wizard that streamlines initial setup and improves accessibility:

- **Project details** &mdash; capture identifiers, default versioning, and target platforms with keyboard-friendly controls.
- **Environment validation** &mdash; runs automated checks for critical tooling (dotnet, WiX, notarytool, rpm-build, etc.) using the new `EnvironmentValidationService`, surfacing remediation guidance when dependencies are missing.
- **Platform configuration** &mdash; pre-populates sensible defaults for Windows, macOS, and Linux and exposes editable format lists plus key/value property editors with remove/add actions.

Completing the wizard materializes a `PackagingProject` inside the workspace editor, keeping the workspace and CLI in sync. The workspace view reuses the same accessible property editing controls, so teams can tweak metadata after onboarding without losing usability improvements.
