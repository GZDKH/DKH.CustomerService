#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Release Contracts package to GitLab Package Registry
.DESCRIPTION
    Executes complete release workflow: Build → Pack → Publish → Tag
    Uses modular scripts - no duplication across projects.
.PARAMETER Version
    Version for the Contracts package (required)
.PARAMETER DryRun
    Preview actions without making changes
.EXAMPLE
    ./.scripts/workflows/release-contracts.ps1 -Version 1.2.0
    ./.scripts/workflows/release-contracts.ps1 -Version 1.2.0 -DryRun
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Import modules
$modulesPath = Join-Path $PSScriptRoot ".." "modules"
Import-Module "$modulesPath/Project.psm1" -Force
Import-Module "$modulesPath/DotNet.psm1" -Force
Import-Module "$modulesPath/NuGet.psm1" -Force
Import-Module "$modulesPath/Git.psm1" -Force

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Contracts Release (GitLab)" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "⚠ DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Get project info
$projectRoot = Get-ProjectRoot
$serviceName = Get-ServiceName
$contractsProjectPath = Get-ContractsProject
$nupkgDir = Join-Path $projectRoot "nupkgs-release"

Write-Host "Service:  $serviceName" -ForegroundColor White
Write-Host "Package:  $serviceName.Contracts" -ForegroundColor White
Write-Host "Version:  $Version" -ForegroundColor White
Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# Step 1/4: Build
# ═══════════════════════════════════════════════════════════════════════
Write-Host "Step 1/4: Build" -ForegroundColor Cyan

if (-not $DryRun) {
    Invoke-DotNetBuild -ProjectPath $contractsProjectPath -Configuration Release
} else {
    Write-Host "  [DRY RUN] Would build $serviceName.Contracts" -ForegroundColor Gray
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

    Invoke-DotNetPack `
        -ProjectPath $contractsProjectPath `
        -OutputDir $nupkgDir `
        -Version $Version `
        -Configuration Release
} else {
    Write-Host "  [DRY RUN] Would pack $serviceName.Contracts@$Version to $nupkgDir" -ForegroundColor Gray
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# Step 3/4: Publish to GitLab
# ═══════════════════════════════════════════════════════════════════════
Write-Host "Step 3/4: Publish to GitLab" -ForegroundColor Cyan

if (-not $DryRun) {
    Publish-ToGitLab -NupkgDir $nupkgDir
} else {
    Write-Host "  [DRY RUN] Would publish to GitLab Package Registry" -ForegroundColor Gray
}

Write-Host ""

# ═══════════════════════════════════════════════════════════════════════
# Step 4/4: Git Tag
# ═══════════════════════════════════════════════════════════════════════
Write-Host "Step 4/4: Git Tag" -ForegroundColor Cyan

$tag = "v$Version"

if (-not $DryRun) {
    New-GitTag -Version $Version -Message "Release $serviceName.Contracts $tag"
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
Write-Host "Package:   $serviceName.Contracts" -ForegroundColor White
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
