# Linux Security Artifacts

The Linux packaging pipeline can emit software bills of materials (SBOMs) and vulnerability findings alongside traditional packaging artifacts. These outputs help satisfy compliance requirements and feed enterprise governance workflows.

## Enabling Security Outputs

Set the following request properties (CLI arguments, project metadata, or SDK invocation) to opt into security outputs:

| Property | Description |
| --- | --- |
| `security.sbom.enabled` | Enables SBOM generation for each produced artifact. Values accepted: `true`, `True`, or `1`. |
| `security.sbom.format` | Optional. Desired SBOM format. Default generator is CycloneDX JSON (`cyclonedx-json`). |
| `security.vuln.enabled` | Enables vulnerability scanning for each produced artifact. Values accepted: `true`, `True`, or `1`. |
| `security.vuln.provider` | Optional. Select a specific vulnerability scanner (default `trivy`). |

Example JSON snippet in a packaging request payload:

```json
{
  "platform": "Linux",
  "formats": [ "deb" ],
  "properties": {
    "linux.packageRoot": "artifacts/linux/app",
    "security.sbom.enabled": "true",
    "security.vuln.enabled": "true"
  }
}
```

## SBOM Generation

- The default `CycloneDxSbomGenerator` writes SBOMs to `_Sbom/<artifact-name>.cdx.json` inside the run's output directory.
- Successful generation adds an informational issue with code `security.sbom.generated`, pointing to the full path of the SBOM and indicating the generator format.
- Failures are surfaced as warnings (`security.sbom.generate_failed`) and do not block the pipeline unless policy evaluation explicitly requires SBOMs.
- Custom generators can be registered via dependency injection by implementing `ISbomGenerator`.

## Vulnerability Scanning

- When enabled, the pipeline runs the configured `IVulnerabilityScanner` (default `TrivyVulnerabilityScanner`) against every produced artifact.
- Findings are mapped to packaging issues with codes like `security.vuln.cve-2024-1234` and severities derived from the scanner (`Critical` → error, `High`/`Medium` → warning, otherwise informational).
- Use `security.vuln.provider` (or the first entry in `security.vuln.providers`) to select an alternative scanner; unsupported values produce a warning and fall back to the default.
- Scanner errors raise warnings (`security.vuln.trivy_failed` or `security.vuln.trivy_exception`) so pipeline execution can continue while flagging incomplete scans.
- Integrations can consume the aggregated issues from `PackagingResult.Issues` or monitor telemetry events tagged with `security.vuln.*`.

## Reporting and Evidence

- SBOM files and vulnerability issues are captured in standard pipeline output folders, allowing downstream CI jobs to archive evidence.
- Combined with repository publishing and sandbox capture features, Linux runs now emit a comprehensive evidence bundle suitable for compliance review.

Refer to `docs/security/vulnerability-sbom-architecture.md` for a deeper overview of the security component design and extension points.
