# Project Configuration

PackagingTools stores packaging metadata in portable JSON documents that can be edited by the CLI, GUI, or automation.

```json
{
  "id": "sample",
  "name": "Sample App",
  "version": "1.0.0",
  "metadata": {
    "windows.identityName": "Contoso.SampleApp",
    "mac.bundleId": "com.contoso.sample",
    "linux.architecture": "amd64"
  },
  "platforms": {
    "Windows": {
      "formats": ["msix", "msi"],
      "properties": {
        "windows.msix.payloadDirectory": "./artifacts/win",
        "windows.msi.sourceDirectory": "./artifacts/win"
      }
    },
    "MacOS": {
      "formats": ["app", "pkg", "dmg"],
      "properties": {
        "mac.app.bundleSource": "./artifacts/mac/Sample.app",
        "mac.pkg.component": "./artifacts/mac/Sample.app",
        "mac.dmg.sourceDirectory": "./artifacts/mac/Sample.app"
      }
    },
    "Linux": {
      "formats": ["deb", "rpm", "appimage"],
      "properties": {
        "linux.packageRoot": "./artifacts/linux/root",
        "linux.appimage.appDir": "./artifacts/linux/AppDir"
      }
    }
  }
}
```

- `metadata` contains global values used across formats (identity, publisher, signing identities).
- `platforms.<Platform>.formats` lists default formats when the user does not specify `--format`.
- `platforms.<Platform>.properties` host provider-specific keys; CLI `--property key=value` overrides these at runtime.

Refer to the Windows, macOS, and Linux blueprint documents for provider key references.
