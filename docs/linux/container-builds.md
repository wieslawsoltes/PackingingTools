# Linux Containerized Builds

PackagingTools can emit a helper script for reproducing Linux packaging runs inside Docker containers. This keeps build environments consistent across distributions.

## Enable Container Script Generation

Set the following properties when invoking the CLI/SDK:

```
--property linux.container.image=mcr.microsoft.com/dotnet/sdk:7.0 \
--property linux.container.projectPath=projects/sample.json
```

Additional optional properties:

| Property | Description |
| --- | --- |
| `linux.container.output` | Override output folder inside the container (defaults to request output). |
| `linux.container.projectPath` | Path to the project file inside the mounted workspace (defaults to `<project>.json`). |

When the pipeline completes successfully, `container-build.sh` is written to the packaging output directory. The script mounts the current working directory into `/workspace` inside the chosen image and reruns `packagingtools pack` with the same formats, configuration, and properties.

```
./container-build.sh
```

The script assumes Docker is installed and the `packagingtools` CLI is available inside the container image. Custom images can preinstall prerequisites such as WiX, fpm, or custom toolchains.
