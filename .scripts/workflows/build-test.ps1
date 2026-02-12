#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Build → Test workflow
.DESCRIPTION
    Executes build and test pipeline using modular scripts.
.PARAMETER ProjectPath
    Path to .csproj or .sln file (default: auto-detect solution)
.PARAMETER Configuration
    Build configuration (default: Release)
.EXAMPLE
    ./.scripts/workflows/build-test.ps1
    ./.scripts/workflows/build-test.ps1 -Configuration Debug
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
Write-Host "Build → Test" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Auto-detect project path if not provided
if (-not $ProjectPath) {
    $projectRoot = Get-ProjectRoot
    $serviceName = Get-ServiceName

    # Try to find .sln file
    $slnFile = Get-ChildItem -Path $projectRoot -Filter "*.sln" -File | Select-Object -First 1

    if ($slnFile) {
        $ProjectPath = $slnFile.FullName
    } else {
        $ProjectPath = $projectRoot
    }

    Write-Host "Project:       $serviceName" -ForegroundColor White
}

Write-Host "Configuration: $Configuration" -ForegroundColor White
Write-Host ""

# Step 1: Build
Write-Host "Step 1/2: Build" -ForegroundColor Cyan
Invoke-DotNetBuild -ProjectPath $ProjectPath -Configuration $Configuration
Write-Host ""

# Step 2: Test
Write-Host "Step 2/2: Test" -ForegroundColor Cyan
Invoke-DotNetTest -ProjectPath $ProjectPath -Configuration $Configuration -NoBuild
Write-Host ""

Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host "✓ Tests completed successfully!" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
