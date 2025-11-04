# Policy Engine Overview

PackagingTools now ships with a lightweight policy engine that evaluates every packaging request before orchestration begins. The evaluator surfaces blocking `PackagingIssue` entries when a run violates organization rules, keeping pipelines aligned with compliance requirements.

## Configuration

Policies are declared in the project metadata dictionary (shared between CLI/GUI) using the following keys:

| Key | Description |
| --- | --- |
| `policy.signing.required` | When `true`, packaging fails if no signing material is configured for the target platform (certificate path, signing identity, or GPG key). |
| `policy.signing.timestampRequired` | When `true`, ensures timestamping or notarization inputs are present (e.g., `windows.signing.timestampUrl`). |
| `policy.approval.required` | When `true`, packaging requires an approval token before execution. |
| `policy.approval.tokenProperty` | (Optional) Overrides the property name expected on the request; defaults to `policy.approvalToken`. |
| `policy.retention.maxDays` | Upper bound for artifact retention. Requests exceeding the limit are blocked. |
| `policy.retention.metadataKey` | (Optional) Metadata key storing the requested retention duration; defaults to `retention.days`. |

Policy metadata can be edited via JSON project files, the Avalonia GUI, or CLI property overrides.

## Required Run Inputs

- **Signing:** Provide signing configuration in project metadata, platform properties, or CLI overrides (e.g., `--property windows.signing.certificatePath=certs/code-sign.pfx`). When signing is required, the evaluator checks the platform-specific keys below:
  - Windows: `windows.signing.certificatePath`, `windows.signing.certificateThumbprint`, or `windows.signing.azureKeyVaultCertificate`
  - macOS: `mac.signing.identity`
  - Linux: `linux.signing.keyId` or `linux.signing.gpgKeyPath`
- **Timestamping / Notarization:** Supply services such as `windows.signing.timestampUrl`, `mac.notarytool.profile`, etc., when `policy.signing.timestampRequired` is enabled.
- **Approvals:** Provide the token via CLI (`--property policy.approvalToken=CAB-12345`) or persist it in metadata if your process allows.
- **Retention:** Set `retention.days` (or the configured metadata key) to the desired retention period; packaging fails when it exceeds the policy cap.

## Integration Surface

- The CLI registers the policy engine by default, so `packagingtools pack` immediately enforces rules.
- Pipelines using the core APIs receive the same enforcement because `IPolicyEvaluator` now resolves to `PolicyEngineEvaluator`.
- Tests leverage a new `PolicyEngineEvaluatorTests` suite to validate signing, approval, and retention scenarios.

## Extensibility

The evaluator is intentionally modular: new rules can extend `PolicyConfiguration` and augment `PolicyEngineEvaluator` without breaking existing consumers. Future work will introduce rule sources beyond project metadata (e.g., centralized governance services), but projects using the current keys will continue to function.
