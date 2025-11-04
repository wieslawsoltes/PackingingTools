#!/usr/bin/env bash
set -euo pipefail

platform=${1:-linux}
missing=()

check_tool() {
  local cmd=$1
  local source=$2
  if ! command -v "$cmd" >/dev/null 2>&1; then
    missing+=("$cmd:$source")
  fi
}

case "${platform,,}" in
  windows)
    check_tool "makeappx.exe" "Windows SDK (App Packaging)"
    check_tool "signtool.exe" "Windows SDK (signtool)"
    check_tool "heat.exe" "WiX Toolset 4"
    check_tool "candle.exe" "WiX Toolset 4"
    check_tool "light.exe" "WiX Toolset 4"
    ;;
  mac|macos)
    check_tool "productbuild" "Xcode Command Line Tools"
    check_tool "codesign" "Xcode Command Line Tools"
    check_tool "notarytool" "Xcode Command Line Tools"
    check_tool "hdiutil" "macOS base system"
    ;;
  linux)
    check_tool "fpm" "fpm (Ruby gem)"
    check_tool "appimagetool" "AppImageKit"
    check_tool "flatpak-builder" "Flatpak"
    check_tool "snapcraft" "Snapcraft"
    check_tool "gpg" "GnuPG"
    ;;
  *)
    echo "Unknown platform: $platform" >&2
    exit 1
    ;;
 esac

if [ ${#missing[@]} -eq 0 ]; then
  echo "All required tools were found for platform '$platform'."
  exit 0
fi

echo "Missing tooling for platform '$platform':"
for entry in "${missing[@]}"; do
  IFS=":" read -r tool source <<<"$entry"
  echo " - $tool ($source)"
 done

exit 2
