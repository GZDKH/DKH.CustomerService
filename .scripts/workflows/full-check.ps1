#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Full quality check workflow
.DESCRIPTION
    Executes complete quality pipeline: Clean → Restore → Build → Format → Test
.PARAMETER ProjectPath
    Path to .csproj or .sln file (default: auto-detect solution)
.PARAMETER Configuration
    Build configuration (default: Release)
.PARAMETER SkipFormat
    Skip format check
.PARAMETER SkipTest
    Skip tests
.EXAMPLE
    ./.scripts/workflows/full-check.ps1
    ./.scripts/workflows/full-check.ps1 -SkipFormat
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath,

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release",

    [Parameter(Mandatory=$false)]
    [switch]$SkipFormat,

    [Parameter(Mandatory=$false)]
    [switch]$SkipTest
)

$ErrorActionPreference = "Stop"

# Import modules
$modulesPath = Join-Path $PSScriptRoot ".." "modules"
Import-Module "$modulesPath/Project.psm1" -Force
Import-Module "$modulesPath/DotNet.psm1" -Force

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Full Quality Check" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Auto-detect project path if not provided
if (-not $ProjectPath) {
    $projectRoot = Get-ProjectRoot
    $serviceName = Get-ServiceName

    # Try to find .slnx or .sln file
    $slnFile = Get-ChildItem -Path $projectRoot -Filter "*.slnx" -File | Select-Object -First 1
    if (-not $slnFile) {
        $slnFile = Get-ChildItem -Path $projectRoot -Filter "*.sln" -File | Select-Object -First 1
    }

    if ($slnFile) {
        $ProjectPath = $slnFile.FullName
    } else {
        $ProjectPath = $projectRoot
    }

    Write-Host "Project:       $serviceName" -ForegroundColor White
}

Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host ""

# Step 0: Remove bin/obj directories
Write-Host "Step 0: Remove build artifacts (bin/obj)" -ForegroundColor Cyan
$projectRoot = if (Test-Path $ProjectPath -PathType Leaf) { Split-Path $ProjectPath -Parent } else { $ProjectPath }
$binObjDirs = Get-ChildItem -Path $projectRoot -Recurse -Directory -Force |
    Where-Object { $_.Name -eq "bin" -or $_.Name -eq "obj" }

if ($binObjDirs) {
    Write-Host "  Removing $($binObjDirs.Count) directories..." -ForegroundColor Gray
    $binObjDirs | ForEach-Object { Remove-Item -Path $_.FullName -Recurse -Force }
    Write-Host "  Done" -ForegroundColor Green
} else {
    Write-Host "  No bin/obj directories found" -ForegroundColor Green
}
Write-Host ""

$totalSteps = 5
if ($SkipFormat) { $totalSteps-- }
if ($SkipTest) { $totalSteps-- }

$currentStep = 1

# Step 1: Clean
Write-Host "Step $currentStep/${totalSteps}: Clean" -ForegroundColor Cyan
Invoke-DotNetClean -ProjectPath $ProjectPath -Configuration $Configuration
Write-Host ""
$currentStep++

# Step 2: Restore
Write-Host "Step $currentStep/${totalSteps}: Restore" -ForegroundColor Cyan
Invoke-DotNetRestore -ProjectPath $ProjectPath
Write-Host ""
$currentStep++

# Step 3: Build
Write-Host "Step $currentStep/${totalSteps}: Build" -ForegroundColor Cyan
Invoke-DotNetBuild -ProjectPath $ProjectPath -Configuration $Configuration -NoRestore
Write-Host ""
$currentStep++

# Step 4: Format (optional)
if (-not $SkipFormat) {
    Write-Host "Step $currentStep/${totalSteps}: Format" -ForegroundColor Cyan
    Invoke-DotNetFormat -ProjectPath $ProjectPath
    Invoke-DotNetFormat -ProjectPath $ProjectPath -Verify
    Write-Host ""
    $currentStep++
}

# Step 5: Test (optional)
if (-not $SkipTest) {
    Write-Host "Step $currentStep/${totalSteps}: Test" -ForegroundColor Cyan
    Invoke-DotNetTest -ProjectPath $ProjectPath -Configuration $Configuration -NoBuild
    Write-Host ""
}

Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host "✓ Quality check passed!" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
