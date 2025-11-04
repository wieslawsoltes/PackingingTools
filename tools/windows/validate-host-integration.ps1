param(
    [string]$ProjectPath = "samples\sample-project.json",
    [string]$Configuration = "Release",
    [string]$Output = "artifacts\host-validation"
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repoRoot = Resolve-Path "$PSScriptRoot\..\.."
Set-Location $repoRoot

$projectFullPath = Resolve-Path $ProjectPath

# Ensure sample payload exists
$payloadRoot = Join-Path $repoRoot 'samples\sample\payload'
$assetDir = Join-Path $payloadRoot 'Assets'
if (-not (Test-Path $payloadRoot)) {
    New-Item -ItemType Directory -Path $payloadRoot | Out-Null
}
if (-not (Test-Path $assetDir)) {
    New-Item -ItemType Directory -Path $assetDir | Out-Null
}
$exePath = Join-Path $payloadRoot 'Sample.exe'
if (-not (Test-Path $exePath)) {
    Set-Content -Path $exePath -Value 'echo sample' -Encoding ascii
}
$iconPath = Join-Path $assetDir 'Sample.ico'
if (-not (Test-Path $iconPath)) {
    Set-Content -Path $iconPath -Value '' -Encoding ascii
}

# Preview host integration configuration
$hostArgs = @(
    'host',
    '--project', $projectFullPath,
    '--enable-shortcut',
    '--shortcut-name', 'Packaging Tools Sample',
    '--shortcut-target', 'Sample.exe',
    '--shortcut-icon', 'Assets\\Sample.ico',
    '--enable-protocol',
    '--protocol-name', 'packagingtools',
    '--protocol-display-name', 'PackagingTools URI',
    '--protocol-command', 'Sample.exe "%1"',
    '--enable-file-association',
    '--file-extension', '.ptsample',
    '--file-progid', 'PackagingTools.Sample',
    '--file-description', 'PackagingTools Sample Document',
    '--file-command', 'Sample.exe "%1"'
)

dotnet run --project 'src/PackagingTools.Cli/PackagingTools.Cli.csproj' -- $hostArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Host integration preview failed.'
}

# Apply configuration
$applyArgs = $hostArgs + '--apply'
dotnet run --project 'src/PackagingTools.Cli/PackagingTools.Cli.csproj' -- $applyArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Host integration apply failed.'
}

# Package to verify WiX metadata
$detailedOutput = Resolve-Path $Output -ErrorAction SilentlyContinue
if (-not $detailedOutput) {
    $null = New-Item -ItemType Directory -Path (Join-Path $repoRoot $Output)
}

$packArgs = @(
    'pack',
    '--project', $projectFullPath,
    '--platform', 'windows',
    '--format', 'msi',
    '--configuration', $Configuration,
    '--output', (Join-Path $repoRoot $Output)
)

dotnet run --project 'src/PackagingTools.Cli/PackagingTools.Cli.csproj' -- $packArgs
if ($LASTEXITCODE -ne 0) {
    throw 'Packaging run failed.'
}

Write-Host "Host integration validation succeeded. MSI output located in '$Output'."
