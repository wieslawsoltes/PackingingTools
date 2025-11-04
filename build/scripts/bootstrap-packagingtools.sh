#!/usr/bin/env bash
set -euo pipefail

platform="${1:-linux}"
configuration="${2:-Release}"
solution="${3:-PackagingTools.sln}"

echo "Bootstrapping PackagingTools environment for platform '${platform}'..."

echo "Restoring .NET workloads..."
dotnet restore "$solution"

echo "Building solution in configuration '${configuration}'..."
dotnet build "$solution" -c "$configuration"

check_script="$(dirname "${BASH_SOURCE[0]}")/check-tools.sh"
if [[ -f "$check_script" ]]; then
  echo "Validating native tooling prerequisites..."
  if ! bash "$check_script" "$platform"; then
    exit_code=$?
    if [[ $exit_code -gt 1 ]]; then
      echo "Missing required tooling for platform '${platform}'. See messages above for remediation guidance."
      exit "$exit_code"
    fi
  fi
else
  echo "Warning: check-tools.sh not found; skipping native tooling validation." >&2
fi

echo "PackagingTools bootstrap complete."
