#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Clean → Restore → Build workflow
.DESCRIPTION
    Executes full clean build pipeline using modular scripts.
.PARAMETER ProjectPath
    Path to .csproj or .sln file (default: auto-detect solution)
.PARAMETER Configuration
    Build configuration (default: Release)
.EXAMPLE
    ./.scripts/workflows/clean-restore-build.ps1
    ./.scripts/workflows/clean-restore-build.ps1 -Configuration Debug
#>

param(
    [Parameter(Mandatory=$false)]
    [string]$ProjectPath,

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Import modules
$modulesPath = Join-Path $PSScriptRoot ".." "modules"
Import-Module "$modulesPath/Project.psm1" -Force
Import-Module "$modulesPath/DotNet.psm1" -Force

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Clean → Restore → Build" -ForegroundColor Cyan
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
Write-Host "Step 0/3: Remove build artifacts (bin/obj)" -ForegroundColor Cyan
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

# Step 1: Clean
Write-Host "Step 1/3: Clean" -ForegroundColor Cyan
Invoke-DotNetClean -ProjectPath $ProjectPath -Configuration $Configuration
Write-Host ""

# Step 2: Restore
Write-Host "Step 2/3: Restore" -ForegroundColor Cyan
Invoke-DotNetRestore -ProjectPath $ProjectPath
Write-Host ""

# Step 3: Build
Write-Host "Step 3/3: Build" -ForegroundColor Cyan
Invoke-DotNetBuild -ProjectPath $ProjectPath -Configuration $Configuration -NoRestore
Write-Host ""

Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host "✓ Build completed successfully!" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
