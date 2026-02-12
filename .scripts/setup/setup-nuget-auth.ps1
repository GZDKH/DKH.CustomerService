#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup GitHub NuGet authentication for DKH packages
.DESCRIPTION
    Configures global NuGet credentials for accessing private packages from GitHub Packages (GZDKH org).
    This is required for building services that depend on DKH.Platform and DKH.Architecture.
.EXAMPLE
    ./scripts/setup-nuget-auth.ps1
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [string]$Username,

    [Parameter(Mandatory = $false)]
    [string]$Token,

    [Parameter(Mandatory = $false)]
    [string]$SourceName = "github-dotnet-gzdkh",

    [Parameter(Mandatory = $false)]
    [string]$SourceUrl = "https://nuget.pkg.github.com/GZDKH/index.json"
)

$ErrorActionPreference = "Stop"

# Import infrastructure module if available
$modulePath = Join-Path $PSScriptRoot "modules" "dkh.infrastructure.psm1"
if (Test-Path $modulePath) {
    Import-Module $modulePath -Force
} else {
    # Fallback functions if module not available
    function Write-DkhInfo { param([string]$Message) Write-Host "[i] $Message" -ForegroundColor Cyan }
    function Write-DkhSuccess { param([string]$Message) Write-Host "[OK] $Message" -ForegroundColor Green }
    function Write-DkhWarning { param([string]$Message) Write-Host "[!] $Message" -ForegroundColor Yellow }
    function Write-DkhError { param([string]$Message) Write-Host "[X] $Message" -ForegroundColor Red }
}

Write-Host ""
Write-Host "+----------------------------------------------+" -ForegroundColor Cyan
Write-Host "|    GitHub NuGet Authentication Setup         |" -ForegroundColor Cyan
Write-Host "+----------------------------------------------+" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet is installed
if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
    Write-DkhError ".NET SDK is not installed"
    Write-Host "  Install from: https://dotnet.microsoft.com/download"
    exit 1
}

# Show current sources
Write-DkhInfo "Current NuGet sources:"
$sources = dotnet nuget list source 2>&1
Write-Host $sources -ForegroundColor DarkGray
Write-Host ""

# Check if source already exists
$sourceExists = $sources -match $SourceName

# Get credentials if not provided
if (-not $Username) {
    Write-Host "GitHub Username:" -ForegroundColor Yellow
    Write-Host "  (Your GitHub username for GZDKH organization)"
    $Username = Read-Host "Username"
    if (-not $Username) {
        Write-DkhError "Username is required"
        exit 1
    }
}

if (-not $Token) {
    Write-Host ""
    Write-Host "GitHub Personal Access Token (PAT):" -ForegroundColor Yellow
    Write-Host "  Create at: https://github.com/settings/tokens"
    Write-Host "  Required scope: read:packages"
    Write-Host ""
    $secureToken = Read-Host "Token" -AsSecureString

    # Convert SecureString to plain text
    $BSTR = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureToken)
    $Token = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($BSTR)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($BSTR)

    if (-not $Token) {
        Write-DkhError "Token is required"
        exit 1
    }
}

Write-Host ""

# Add or update the source
try {
    if ($sourceExists) {
        Write-DkhInfo "Updating existing source: $SourceName"

        # Remove and re-add (update doesn't always work with credentials)
        dotnet nuget remove source $SourceName 2>&1 | Out-Null

        dotnet nuget add source $SourceUrl `
            --name $SourceName `
            --username $Username `
            --password $Token `
            --store-password-in-clear-text 2>&1 | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-DkhSuccess "Source updated with credentials"
        } else {
            throw "Failed to update source"
        }
    } else {
        Write-DkhInfo "Adding new source: $SourceName"

        dotnet nuget add source $SourceUrl `
            --name $SourceName `
            --username $Username `
            --password $Token `
            --store-password-in-clear-text 2>&1 | Out-Null

        if ($LASTEXITCODE -eq 0) {
            Write-DkhSuccess "Source added with credentials"
        } else {
            throw "Failed to add source"
        }
    }
} catch {
    Write-DkhError "Failed to configure NuGet source: $_"
    exit 1
}

# Verify the configuration
Write-Host ""
Write-DkhInfo "Verifying configuration..."

$testResult = dotnet nuget list source 2>&1
if ($testResult -match $SourceName) {
    Write-DkhSuccess "NuGet source configured successfully"
} else {
    Write-DkhWarning "Source may not be configured correctly"
}

# Show location of NuGet.Config
$globalConfigPath = if ($Global:DkhIsWindows -or $env:OS -eq 'Windows_NT') {
    Join-Path $env:APPDATA "NuGet" "NuGet.Config"
} else {
    Join-Path $HOME ".nuget" "NuGet" "NuGet.Config"
}

Write-Host ""
Write-DkhInfo "Configuration saved to: $globalConfigPath"

# Test package restore (optional)
Write-Host ""
$testRestore = Read-Host "Test package restore now? (y/N)"
if ($testRestore -eq "y" -or $testRestore -eq "Y") {
    Write-DkhInfo "Testing package restore..."

    # Find a project to test with
    $infraRoot = Split-Path $PSScriptRoot -Parent
    $testProjects = @(
        (Join-Path $infraRoot ".." "services" "DKH.OrderService" "src" "DKH.OrderService.Api" "DKH.OrderService.Api.csproj"),
        (Join-Path $infraRoot ".." "services" "DKH.TelegramBotService" "src" "DKH.TelegramBotService.Api" "DKH.TelegramBotService.Api.csproj"),
        (Join-Path $infraRoot ".." "libraries" "DKH.Platform" "src" "DKH.Platform" "DKH.Platform.csproj")
    )

    $foundProject = $null
    foreach ($proj in $testProjects) {
        if (Test-Path $proj) {
            $foundProject = $proj
            break
        }
    }

    if ($foundProject) {
        Write-DkhInfo "Testing with: $foundProject"
        $restoreResult = dotnet restore $foundProject 2>&1
        if ($LASTEXITCODE -eq 0) {
            Write-DkhSuccess "Package restore successful!"
        } else {
            Write-DkhError "Package restore failed:"
            Write-Host $restoreResult -ForegroundColor Red
        }
    } else {
        Write-DkhWarning "No test project found. Try running 'dotnet restore' manually in a service directory."
    }
}

Write-Host ""
Write-DkhSuccess "Setup complete!"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  1. Try building a service:"
Write-Host "     cd ../services/DKH.OrderService"
Write-Host "     dotnet build"
Write-Host ""
Write-Host "  2. Or use the infrastructure script:"
Write-Host "     ./scripts/dkh.ps1 start -All -Build"
Write-Host ""
