#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Release Contracts package to GitLab Package Registry
.DESCRIPTION
    Builds, packs, and publishes the Contracts package using local modular scripts.
    All dependencies are self-contained - no Infrastructure required.
.PARAMETER Version
    Version for the Contracts package (required)
.PARAMETER DryRun
    Preview actions without making changes
.EXAMPLE
    ./.scripts/release/release-contracts.ps1 -Version 1.2.0
    ./.scripts/release/release-contracts.ps1 -Version 1.2.0 -DryRun
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Contracts Release (GitLab)" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "⚠ DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Paths
$serviceRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$serviceName = Split-Path $serviceRoot -Leaf
$contractsProject = "$serviceName.Contracts"
$contractsProjectPath = "$contractsProject/$contractsProject.csproj"
$nupkgDir = Join-Path $serviceRoot "nupkgs-release"

Write-Host "Service:  $serviceName" -ForegroundColor White
Write-Host "Package:  $contractsProject" -ForegroundColor White
Write-Host "Version:  $Version" -ForegroundColor White
Write-Host ""

# Validate Contracts project exists
$contractsFullPath = Join-Path $serviceRoot $contractsProjectPath
if (-not (Test-Path $contractsFullPath)) {
    Write-Error "Contracts project not found: $contractsFullPath"
}

# ═══════════════════════════════════════════════════════════════════════
# Step 1/4: Build
# ═══════════════════════════════════════════════════════════════════════
Write-Host "Step 1/4: Build" -ForegroundColor Cyan

if (-not $DryRun) {
    & "$PSScriptRoot/build.ps1" -ProjectPath $contractsProjectPath -Configuration Release
} else {
    Write-Host "  [DRY RUN] Would build $contractsProject" -ForegroundColor Gray
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# Step 2/4: Pack
# ═══════════════════════════════════════════════════════════════════════
Write-Host "Step 2/4: Pack" -ForegroundColor Cyan

if (-not $DryRun) {
    # Clean output directory
    if (Test-Path $nupkgDir) {
        Remove-Item -Path $nupkgDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $nupkgDir -Force | Out-Null

    & "$PSScriptRoot/pack.ps1" `
        -ProjectPath $contractsProjectPath `
        -OutputDir $nupkgDir `
        -Version $Version `
        -Configuration Release
} else {
    Write-Host "  [DRY RUN] Would pack $contractsProject@$Version to $nupkgDir" -ForegroundColor Gray
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# Step 3/4: Publish to GitLab
# ═══════════════════════════════════════════════════════════════════════
Write-Host "Step 3/4: Publish to GitLab" -ForegroundColor Cyan

if (-not $DryRun) {
    & "$PSScriptRoot/publish-gitlab.ps1" -NupkgDir $nupkgDir
} else {
    Write-Host "  [DRY RUN] Would publish to GitLab Package Registry" -ForegroundColor Gray
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# Step 4/4: Tag
# ═══════════════════════════════════════════════════════════════════════
Write-Host "Step 4/4: Git Tag" -ForegroundColor Cyan

$tag = "v$Version"

if (-not $DryRun) {
    & "$PSScriptRoot/tag.ps1" -Version $Version -Message "Release $contractsProject $tag"
} else {
    Write-Host "  [DRY RUN] Would create and push tag: $tag" -ForegroundColor Gray
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# Summary
# ═══════════════════════════════════════════════════════════════════════
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host "✓ Release completed successfully!" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "Package:   $contractsProject" -ForegroundColor White
Write-Host "Version:   $Version" -ForegroundColor White
Write-Host "Tag:       $tag" -ForegroundColor White
Write-Host "Registry:  GitLab Package Registry (gitlab-gzdkh)" -ForegroundColor White
Write-Host ""

if (-not $DryRun) {
    Write-Host "Next steps:" -ForegroundColor Cyan
    Write-Host "  1. Verify package: https://gitlab.com/gzdkh/dkh-packages/-/packages" -ForegroundColor Gray
    Write-Host "  2. Update consumers with new version $Version" -ForegroundColor Gray
    Write-Host "  3. Create GitHub Release (optional): gh release create $tag" -ForegroundColor Gray
} else {
    Write-Host "This was a dry run. Run without -DryRun to execute." -ForegroundColor Yellow
}

Write-Host ""
