#!/usr/bin/env pwsh
[CmdletBinding()]
param(
    [switch]$SkipClean,
    [switch]$ClearNugetCache,
    [switch]$RunTests,
    [switch]$RunFormat,
    [ValidateSet("full", "build-test", "build-only", "restore-only", "build-format")]
    [string]$Mode,
    [switch]$NoPrompt,
    [string]$Environment
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptsRoot = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $scriptsRoot "modules/dkh.configuration.psm1"

if (-not (Test-Path $modulePath))
{
    Write-Host "Configuration module not found at $modulePath" -ForegroundColor Red
    exit 1
}

Import-Module $modulePath -Force

# Default to Development environment in non-interactive mode
if ($NoPrompt -and -not $Environment)
{
    $Environment = "Development"
}

$context = Get-DkhProjectContext -EnvironmentName $Environment
$manifestData = $context.Manifest.Data
$solutionPath = $context.Config.SolutionPath
$solutionName = $context.Config.SolutionName
$slnxPath = Join-Path $solutionPath "$solutionName.slnx"
$slnPath = if (Test-Path $slnxPath) { $slnxPath } else { Join-Path $solutionPath "$solutionName.sln" }

# Decide execution mode (interactive prompt when allowed and switches are not specified)
$isCi = $env:CI -eq "true" -or $env:GITHUB_ACTIONS -eq "true"
$hasExplicitFlags = $Mode -or $RunTests -or $RunFormat -or $SkipClean -or $ClearNugetCache -or $NoPrompt
if (-not $isCi -and -not $hasExplicitFlags)
{
    Write-Host ""
    Write-Host "Select build mode:" -ForegroundColor Cyan
    Write-Host "  [1] full          (clean -> restore -> build -> format check -> tests)"
    Write-Host "  [2] build-test    (clean -> restore -> build -> tests)"
    Write-Host "  [3] build-only    (clean -> restore -> build)"
    Write-Host "  [4] restore-only  (restore only, skips clean/build)"
    Write-Host "  [5] build-format  (clean -> restore -> build -> format check)"
    $choice = Read-Host "Enter choice [1-5]"
    switch ($choice)
    {
        "1" { $Mode = "full" }
        "2" { $Mode = "build-test" }
        "3" { $Mode = "build-only" }
        "4" { $Mode = "restore-only" }
        "5" { $Mode = "build-format" }
        default { $Mode = "build-only" }
    }

    Write-Host ""
    $clearCache = Read-Host "Clear NuGet cache before restore? [y/N]"
    if ($clearCache -eq "y" -or $clearCache -eq "Y")
    {
        $ClearNugetCache = $true
    }
}

switch ($Mode)
{
    "full"         { $RunTests = $true; $RunFormat = $true }
    "build-test"   { $RunTests = $true }
    "build-format" { $RunFormat = $true }
    "restore-only" { $SkipClean = $true }
}

Invoke-DkhHook -Name "pre-build" -Arguments @{ Context = $context } | Out-Null

# Check global NuGet credentials
$hasGlobalCredentials = Test-DkhGlobalNugetCredentials
if ($hasGlobalCredentials)
{
    Write-Host "Using NuGet credentials from global NuGet.Config" -ForegroundColor Green
}
else
{
    Write-Host "ERROR: GitHub NuGet credentials not configured." -ForegroundColor Red
    Write-Host "Run: pwsh DKH.Infrastructure/scripts/setup-nuget-auth.ps1" -ForegroundColor Yellow
    throw "NuGet credentials not configured. Run setup-nuget-auth.ps1 first."
}

if (Test-DkhCapability -Name "dotnet" -ManifestData $manifestData)
{
    # Show execution plan
    $plan = @()
    if (-not $SkipClean) { $plan += "dotnet clean" }
    if ($ClearNugetCache) { $plan += "dotnet nuget locals all --clear" }
    $plan += "dotnet restore"
    $plan += "dotnet build (Release)"
    if ($RunTests) { $plan += "dotnet test (Release)" }
    if ($RunFormat) { $plan += "dotnet format --verify-no-changes" }
    Write-Host ("Execution plan: {0}" -f ($plan -join " -> ")) -ForegroundColor DarkCyan

    if ($ClearNugetCache)
    {
        Write-Host "Clearing NuGet caches..." -ForegroundColor Yellow
        dotnet nuget locals all --clear
        if ($LASTEXITCODE -ne 0)
        {
            Write-Host "WARN: dotnet nuget locals failed (files may be locked). Continuing..." -ForegroundColor Yellow
        }
    }

    Write-Host "Running dotnet restore..." -ForegroundColor Cyan
    dotnet restore $slnPath /p:ExcludeRestoreProjects="docker-compose.dcproj"
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet restore failed with exit code $LASTEXITCODE."
    }

    if (-not $SkipClean)
    {
        Write-Host "Running dotnet clean..." -ForegroundColor Yellow
        dotnet clean $slnPath
        if ($LASTEXITCODE -ne 0)
        {
            throw "dotnet clean failed with exit code $LASTEXITCODE."
        }
    }

    Write-Host "Running dotnet build (Release)..." -ForegroundColor Cyan
    dotnet build $slnPath -c Release --no-restore
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet build failed with exit code $LASTEXITCODE."
    }

    if ($RunTests)
    {
        Write-Host "Running dotnet test (Release)..." -ForegroundColor Cyan
        dotnet test $slnPath -c Release --no-build --no-restore
        if ($LASTEXITCODE -ne 0)
        {
            throw "dotnet test failed with exit code $LASTEXITCODE."
        }
    }

    if ($RunFormat)
    {
        Write-Host "Running dotnet format --verify-no-changes ..." -ForegroundColor Cyan
        dotnet format $slnPath --verify-no-changes --exclude "docker-compose.dcproj"
        if ($LASTEXITCODE -ne 0)
        {
            throw "dotnet format failed with exit code $LASTEXITCODE."
        }
        Write-Host "dotnet format completed successfully." -ForegroundColor Green
    }
}
else
{
    Write-Host "Dotnet capability disabled in scripts.manifest.json; skipping dotnet build." -ForegroundColor Yellow
}

Invoke-DkhHook -Name "post-build" -Arguments @{ Context = $context } | Out-Null
Write-Host "Build script finished successfully." -ForegroundColor Green
