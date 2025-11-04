param(
    [ValidateSet("windows", "mac", "linux")]
    [string]$Platform = "windows",
    [string]$Configuration = "Release",
    [string]$Solution = "PackagingTools.sln"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Write-Host "Bootstrapping PackagingTools environment for platform '$Platform'..."

Write-Host "Restoring .NET workloads..."
dotnet restore $Solution | Out-Host

Write-Host "Building solution in configuration '$Configuration'..."
dotnet build $Solution -c $Configuration | Out-Host

$checkScript = Join-Path $PSScriptRoot "check-tools.ps1"
if (Test-Path $checkScript) {
    Write-Host "Validating native tooling prerequisites..."
    & $checkScript -Platform $Platform
    $exit = $LASTEXITCODE
    if ($exit -gt 1) {
        throw "Missing required tooling for platform '$Platform'. See output above for remediation guidance."
    }
} else {
    Write-Warning "Could not locate check-tools.ps1; skipping native tooling validation."
}

Write-Host "PackagingTools bootstrap complete."
