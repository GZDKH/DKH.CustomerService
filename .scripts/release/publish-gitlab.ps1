#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Publish NuGet packages to GitLab Package Registry
.DESCRIPTION
    Pushes .nupkg files to GitLab Package Registry
.PARAMETER NupkgDir
    Directory containing .nupkg files
.PARAMETER SourceName
    NuGet source name (default: gitlab-gzdkh)
.PARAMETER SourceUrl
    GitLab Package Registry URL (optional, read from config if not provided)
.PARAMETER Username
    GitLab username (optional, read from config/env if not provided)
.PARAMETER Token
    GitLab access token (optional, read from config/env if not provided)
.EXAMPLE
    ./.scripts/release/publish-gitlab.ps1 -NupkgDir "./nupkgs"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$NupkgDir,

    [Parameter(Mandatory=$false)]
    [string]$SourceName = "gitlab-gzdkh",

    [Parameter(Mandatory=$false)]
    [string]$SourceUrl,

    [Parameter(Mandatory=$false)]
    [string]$Username,

    [Parameter(Mandatory=$false)]
    [string]$Token
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $NupkgDir)) {
    Write-Error "NuGet package directory not found: $NupkgDir"
}

# Try to load GitLab config from .scripts/config/gitlab.conf
$serviceRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$gitlabConfigPath = Join-Path $serviceRoot ".scripts/config/gitlab.conf"

if ((Test-Path $gitlabConfigPath) -and (-not $SourceUrl -or -not $Username -or -not $Token)) {
    Write-Host "→ Loading GitLab config from gitlab.conf..." -ForegroundColor Gray

    $config = @{}
    Get-Content $gitlabConfigPath | Where-Object { $_ -match '^\s*(\w+)="(.+)"' } | ForEach-Object {
        $matches[1] | Out-Null
        $config[$matches[1]] = $matches[2]
    }

    if (-not $SourceUrl -and $config.ContainsKey('GITLAB_SOURCE_URL')) {
        $SourceUrl = $config['GITLAB_SOURCE_URL']
    }
    if (-not $Username -and $config.ContainsKey('GITLAB_USERNAME')) {
        $Username = $config['GITLAB_USERNAME']
    }
    if (-not $Token -and $config.ContainsKey('GITLAB_TOKEN')) {
        $Token = $config['GITLAB_TOKEN']
    }
}

# Fallback to environment variables
if (-not $Username -and $env:GITLAB_NUGET_USERNAME) {
    $Username = $env:GITLAB_NUGET_USERNAME
}
if (-not $Token -and $env:GITLAB_NUGET_TOKEN) {
    $Token = $env:GITLAB_NUGET_TOKEN
}

if (-not $Username -or -not $Token) {
    Write-Error "GitLab credentials not found. Provide via parameters, gitlab.conf, or environment variables (GITLAB_NUGET_USERNAME, GITLAB_NUGET_TOKEN)"
}

Write-Host "📤 Publishing to GitLab Package Registry..." -ForegroundColor Cyan
Write-Host "  → Source: $SourceName" -ForegroundColor Gray
if ($SourceUrl) {
    Write-Host "  → URL: $SourceUrl" -ForegroundColor Gray
}

# Find .nupkg files
$packages = Get-ChildItem -Path $NupkgDir -Filter "*.nupkg" -File

if ($packages.Count -eq 0) {
    Write-Error "No .nupkg files found in $NupkgDir"
}

Write-Host "  → Found $($packages.Count) package(s)" -ForegroundColor Gray
Write-Host ""

foreach ($package in $packages) {
    Write-Host "  → Publishing $($package.Name)..." -ForegroundColor Yellow

    dotnet nuget push $package.FullName `
        --source $SourceName `
        --skip-duplicate `
        2>&1 | Out-Null

    if ($LASTEXITCODE -eq 0) {
        Write-Host "    ✓ Published successfully" -ForegroundColor Green
    } elseif ($LASTEXITCODE -eq 409) {
        Write-Host "    ⊘ Already exists (skipped)" -ForegroundColor Yellow
    } else {
        Write-Error "Failed to publish $($package.Name) (exit code: $LASTEXITCODE)"
    }
}

Write-Host ""
Write-Host "✓ Publishing completed" -ForegroundColor Green
