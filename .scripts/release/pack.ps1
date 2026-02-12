#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Pack .NET project into NuGet package
.DESCRIPTION
    Creates .nupkg file from a .NET project
.PARAMETER ProjectPath
    Path to .csproj file (relative to service root)
.PARAMETER OutputDir
    Directory to save .nupkg files
.PARAMETER Version
    Package version
.PARAMETER Configuration
    Build configuration (default: Release)
.EXAMPLE
    ./.scripts/release/pack.ps1 -ProjectPath "DKH.CartService.Contracts/DKH.CartService.Contracts.csproj" -OutputDir "./nupkgs" -Version "1.2.0"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,

    [Parameter(Mandatory=$true)]
    [string]$OutputDir,

    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Resolve paths relative to service root
$serviceRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

if (-not (Test-Path $ProjectPath)) {
    $ProjectPath = Join-Path $serviceRoot $ProjectPath
}

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project not found: $ProjectPath"
}

# Create output directory if it doesn't exist
if (-not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

$OutputDir = Resolve-Path $OutputDir

Push-Location (Split-Path $ProjectPath -Parent)
try {
    Write-Host "📦 Packing $(Split-Path $ProjectPath -Leaf)..." -ForegroundColor Cyan
    Write-Host "  → Version: $Version" -ForegroundColor Gray
    Write-Host "  → Output: $OutputDir" -ForegroundColor Gray

    dotnet pack -c $Configuration `
        --no-build `
        --output $OutputDir `
        /p:PackageVersion=$Version `
        --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet pack failed with exit code $LASTEXITCODE"
    }

    $packageName = "$(Split-Path (Split-Path $ProjectPath -Parent) -Leaf).$Version.nupkg"
    $packagePath = Join-Path $OutputDir $packageName

    if (Test-Path $packagePath) {
        Write-Host "✓ Package created: $packageName" -ForegroundColor Green
    } else {
        Write-Error "Package not found: $packagePath"
    }
}
finally {
    Pop-Location
}
