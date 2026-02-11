#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Releases Service Contracts package to GitLab Package Registry
.DESCRIPTION
    Builds, packs, and publishes the Contracts package for this service.
    Uses the monorepo's modular release scripts.
.PARAMETER Version
    Version for the Contracts package (required)
.PARAMETER DryRun
    Preview actions without making changes
.EXAMPLE
    # Release with version
    ./.scripts/release/release-contracts.ps1 -Version 1.0.1

    # Preview only
    ./.scripts/release/release-contracts.ps1 -Version 1.0.1 -DryRun
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Paths
$serviceRoot = Resolve-Path "$PSScriptRoot/.."
$monoRepoRoot = Resolve-Path "$serviceRoot/../.."
$serviceName = Split-Path -Leaf $serviceRoot
$contractsProject = "$serviceName.Contracts"
$nupkgDir = Join-Path $serviceRoot "nupkgs-release"

# Load GitLab config from monorepo
$gitlabConfig = Join-Path $monoRepoRoot ".scripts/config/gitlab.conf"
if (-not (Test-Path $gitlabConfig)) {
    Write-Error "GitLab config not found: $gitlabConfig"
}

# Parse config
$config = @{}
Get-Content $gitlabConfig | Where-Object { $_ -match '^\s*(\w+)="(.+)"' } | ForEach-Object {
    $matches[1] | Out-Null
    $config[$matches[1]] = $matches[2]
}

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "$contractsProject Release to GitLab" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "⚠ DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Validate Contracts project exists
$contractsPath = Join-Path $serviceRoot $contractsProject
if (-not (Test-Path $contractsPath)) {
    Write-Error "Contracts project not found: $contractsPath"
}

# Step 1: Build
Write-Host "Step 1/4: Build" -ForegroundColor Yellow
if (-not $DryRun) {
    & "$monoRepoRoot/DKH.Infrastructure/scripts/release/build-project.ps1" -ProjectPath $serviceRoot -Mode ContractsOnly
}
else {
    Write-Host "  [DRY RUN] Would build $contractsProject" -ForegroundColor Gray
}
Write-Host ""

# Step 2: Pack
Write-Host "Step 2/4: Pack" -ForegroundColor Yellow

if (-not $DryRun) {
    # Clean output directory
    if (Test-Path $nupkgDir) {
        Remove-Item -Path $nupkgDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $nupkgDir -Force | Out-Null

    & "$monoRepoRoot/DKH.Infrastructure/scripts/release/pack-project.ps1" `
        -ProjectPath $serviceRoot `
        -OutputDir $nupkgDir `
        -Version $Version `
        -Mode ContractsOnly
}
else {
    Write-Host "  [DRY RUN] Would pack $contractsProject@$Version to $nupkgDir" -ForegroundColor Gray
}
Write-Host ""

# Step 3: Publish to GitLab
Write-Host "Step 3/4: Publish to GitLab" -ForegroundColor Yellow
if (-not $DryRun) {
    & "$monoRepoRoot/DKH.Infrastructure/scripts/release/publish-packages.ps1" `
        -NupkgDir $nupkgDir `
        -SourceName $config['GITLAB_SOURCE_NAME'] `
        -SourceUrl $config['GITLAB_SOURCE_URL'] `
        -ApiKey $config['GITLAB_TOKEN'] `
        -Username $config['GITLAB_USERNAME']
}
else {
    Write-Host "  [DRY RUN] Would publish to GitLab Package Registry" -ForegroundColor Gray
    Write-Host "    Source: $($config['GITLAB_SOURCE_NAME'])" -ForegroundColor Gray
    Write-Host "    URL: $($config['GITLAB_SOURCE_URL'])" -ForegroundColor Gray
}
Write-Host ""

# Step 4: Tag and Release
Write-Host "Step 4/4: Tag and Release" -ForegroundColor Yellow
$tag = "v$Version"

if (-not $DryRun) {
    & "$monoRepoRoot/DKH.Infrastructure/scripts/release/tag-and-release.ps1" `
        -ProjectPath $serviceRoot `
        -TagName $tag `
        -NupkgDir $nupkgDir
}
else {
    Write-Host "  [DRY RUN] Would create tag: $tag" -ForegroundColor Gray
    Write-Host "  [DRY RUN] Would create GitHub Release with packages from $nupkgDir" -ForegroundColor Gray
}
Write-Host ""

# Summary
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host "✓ Release completed successfully!" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "Package:  $contractsProject"
Write-Host "Version:  $Version"
Write-Host "Tag:      $tag"
Write-Host "Registry: https://gitlab.com/gzdkh/dkh-packages/-/packages"
Write-Host ""

if (-not $DryRun) {
    Write-Host "Next steps:"
    Write-Host "  1. Update Directory.Build.props with new version (if not already done)"
    Write-Host "  2. Commit: git add . && git commit -m 'chore(contracts): release v$Version'"
    Write-Host "  3. Push tag: git push origin $tag"
    Write-Host "  4. Test in consumer projects"
}
else {
    Write-Host "This was a dry run. Run without -DryRun to execute." -ForegroundColor Yellow
}
Write-Host ""
