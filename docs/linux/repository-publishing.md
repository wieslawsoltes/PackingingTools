# Linux Repository Publishing

PackagingTools can emit repository metadata that downstream automation can push to APT or YUM/DNF endpoints. Configure repository targets via packaging request properties (CLI `--property` flags, project metadata, or SDK overrides).

## Common Settings

- `linux.repo.enabled` — enable repository generation (`true`/`1`).
- `linux.repo.targets` — semicolon separated list of target identifiers (e.g., `stable;prod`).

For each target `<id>`:

| Property | Description |
| --- | --- |
| `linux.repo.target.<id>.type` | `apt` or `yum`. |
| `linux.repo.target.<id>.destination` | Optional URI used in generated references (S3 path, HTTPS endpoint, etc.). |
| `linux.repo.target.<id>.suite` | APT suite (default `stable`). |
| `linux.repo.target.<id>.components` | Comma-separated components for APT (default `main`). |
| `linux.repo.target.<id>.credential` | Optional credential identifier resolved via the credential provider. |

### Credentials

The default `PropertyLinuxRepositoryCredentialProvider` reads values from request properties using the prefix `linux.repo.credential.<name>.`. Example:

```
--property linux.repo.target.prod.credential=artifact-bucket \
--property linux.repo.credential.artifact-bucket.type=aws-s3 \
--property linux.repo.credential.artifact-bucket.accessKey=${AWS_ACCESS_KEY_ID} \
--property linux.repo.credential.artifact-bucket.secretKey=${AWS_SECRET_ACCESS_KEY}
```

Custom providers can be registered via DI to integrate with vaults or secret managers. Generated repository metadata includes only credential identifiers and property names—never secret values.

## Outputs

Repository files are written under `<output>/_Repo/<targetId>/`.

### APT (`type=apt`)
- `apt/dists/<suite>/<component>/binary-<arch>/Packages` – minimal package manifest (Package, Version, Architecture, Size, SHA256).
- `apt/dists/<suite>/Release` – suite metadata.
- `apt/target.json` – summary containing destination URI, credential reference, suite/components/architectures.

### YUM (`type=yum`)
- `yum/repodata.json` – package list (name/version/architecture/sha256).
- `yum/<targetId>.repo` – sample `.repo` file referencing the configured destination.
- `yum/target.json` – summary including credential reference.

Use these files to feed publishing processes (e.g., uploading to S3 and running `createrepo_c`). Because PackagingTools preserves artifact metadata (`packageName`, `packageVersion`, `packageArchitecture`), repository manifests remain consistent across runs.
