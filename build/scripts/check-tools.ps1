param(
    [string]$Platform = "windows"
)

$tools = @{}

switch ($Platform.ToLowerInvariant()) {
    "windows" {
        $tools = @{
            "makeappx.exe" = "Windows SDK (App Packaging Tools)"
            "signtool.exe" = "Windows SDK (signtool)"
            "heat.exe" = "WiX Toolset 4"
            "candle.exe" = "WiX Toolset 4"
            "light.exe" = "WiX Toolset 4"
        }
    }
    "mac" {
        $tools = @{
            "productbuild" = "Xcode Command Line Tools"
            "codesign" = "Xcode Command Line Tools"
            "notarytool" = "Xcode Command Line Tools (Xcode 13+)"
            "hdiutil" = "macOS base system"
        }
    }
    "linux" {
        $tools = @{
            "fpm" = "fpm (Ruby gem)"
            "appimagetool" = "AppImageKit"
            "flatpak-builder" = "Flatpak"
            "snapcraft" = "Snapcraft"
            "gpg" = "GnuPG"
        }
    }
    default {
        Write-Error "Unknown platform: $Platform"
        exit 1
    }
}

$missing = @()
foreach ($tool in $tools.Keys) {
    $resolved = Get-Command $tool -ErrorAction SilentlyContinue
    if (-not $resolved) {
        $missing += @{
            Name = $tool
            Source = $tools[$tool]
        }
    }
}

if ($missing.Count -eq 0) {
    Write-Output "All required tools were found for platform '$Platform'."
    exit 0
}

Write-Output "Missing tooling for platform '$Platform':"
foreach ($item in $missing) {
    Write-Output " - $($item.Name) ($($item.Source))"
}

exit 2
