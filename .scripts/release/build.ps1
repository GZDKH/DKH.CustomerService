#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Build .NET project
.DESCRIPTION
    Restores and builds a .NET project with specified configuration
.PARAMETER ProjectPath
    Path to .csproj file (relative to service root)
.PARAMETER Configuration
    Build configuration (default: Release)
.EXAMPLE
    ./.scripts/release/build.ps1 -ProjectPath "DKH.CartService.Contracts/DKH.CartService.Contracts.csproj"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$ProjectPath,

    [Parameter(Mandatory=$false)]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

# Resolve paths relative to service root (parent of .scripts/)
$serviceRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent

if (-not (Test-Path $ProjectPath)) {
    # Try relative to service root
    $ProjectPath = Join-Path $serviceRoot $ProjectPath
}

if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project not found: $ProjectPath"
}

Push-Location (Split-Path $ProjectPath -Parent)
try {
    Write-Host "🔨 Building $(Split-Path $ProjectPath -Leaf)..." -ForegroundColor Cyan

    Write-Host "  → Restoring..." -ForegroundColor Gray
    dotnet restore --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet restore failed with exit code $LASTEXITCODE"
    }

    Write-Host "  → Building ($Configuration)..." -ForegroundColor Gray
    dotnet build -c $Configuration --no-restore --verbosity quiet

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet build failed with exit code $LASTEXITCODE"
    }

    Write-Host "✓ Build completed successfully" -ForegroundColor Green
}
finally {
    Pop-Location
}
