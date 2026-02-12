#!/usr/bin/env pwsh
<#
.SYNOPSIS
    DKH Infrastructure PowerShell Module
.DESCRIPTION
    Shared functions for managing DKH infrastructure and services
#>

# =============================================================================
# Cross-platform compatibility
# =============================================================================

# Detect PowerShell edition: 'Core' (pwsh 6+) or 'Desktop' (Windows PowerShell 5.1)
$Global:DkhIsPwshCore = $PSVersionTable.PSEdition -eq 'Core'

# Define platform variables
# In PowerShell Core 6+, $IsWindows, $IsMacOS, $IsLinux are automatic variables
# In Windows PowerShell 5.1, these don't exist (it only runs on Windows)
if ($Global:DkhIsPwshCore) {
    $Global:DkhIsWindows = $IsWindows
    $Global:DkhIsMacOS = $IsMacOS
    $Global:DkhIsLinux = $IsLinux
}
else {
    # Windows PowerShell 5.1 only runs on Windows
    $Global:DkhIsWindows = $true
    $Global:DkhIsMacOS = $false
    $Global:DkhIsLinux = $false
}

# Set console encoding to UTF-8 for proper unicode support
if ($Global:DkhIsWindows) {
    try {
        [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
        $OutputEncoding = [System.Text.Encoding]::UTF8
    }
    catch {
        # Ignore encoding errors in non-interactive sessions
    }
}

# =============================================================================
# Color output functions
# =============================================================================

function Write-DkhInfo {
    param([string]$Message)
    Write-Host "[i] $Message" -ForegroundColor Cyan
}

function Write-DkhSuccess {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-DkhWarning {
    param([string]$Message)
    Write-Host "[!] $Message" -ForegroundColor Yellow
}

function Write-DkhError {
    param([string]$Message)
    Write-Host "[X] $Message" -ForegroundColor Red
}

# =============================================================================
# Environment validation
# =============================================================================

function Test-DkhPrerequisites {
    [CmdletBinding()]
    param()
    
    $missingTools = @()
    
    # Check Docker
    if (-not (Get-Command "docker" -ErrorAction SilentlyContinue)) {
        $missingTools += "Docker"
    }
    
    # Check Docker Compose
    $composeVersion = docker compose version 2>&1
    if ($LASTEXITCODE -ne 0) {
        $missingTools += "Docker Compose v2"
    }
    
    # Check .NET SDK
    if (-not (Get-Command "dotnet" -ErrorAction SilentlyContinue)) {
        Write-DkhWarning ".NET SDK not found (optional for infrastructure management)"
    }
    
    if ($missingTools.Count -gt 0) {
        Write-DkhError "Missing required tools: $($missingTools -join ', ')"
        Write-Host "`nPlease install missing tools:"
        Write-Host "  - Docker: https://www.docker.com/get-started"
        Write-Host "  - Docker Compose: Included in Docker Desktop"
        return $false
    }
    
    Write-DkhSuccess "All prerequisites are installed"
    return $true
}

function Test-DkhDockerRunning {
    [CmdletBinding()]
    param(
        [switch]$ThrowOnError
    )

    # Quick check if Docker daemon is running
    $dockerInfo = docker info 2>&1
    $running = $LASTEXITCODE -eq 0

    if (-not $running) {
        $message = "Docker daemon is not running. Please start Docker Desktop and try again."
        if ($ThrowOnError) {
            Write-DkhError $message
            throw $message
        }
        else {
            Write-DkhWarning $message
        }
        return $false
    }

    return $true
}

# =============================================================================
# Environment file management
# =============================================================================

function Get-DkhEnvFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$EnvFile = ".env.local"
    )
    
    $infraRoot = Get-DkhInfrastructureRoot
    $envPath = Join-Path $infraRoot "docker-compose" $EnvFile
    
    if (-not (Test-Path $envPath)) {
        Write-DkhWarning "Environment file not found: $envPath"
        Write-DkhInfo "Run 'setup-dev-env.ps1' to create it"
        return $null
    }
    
    return $envPath
}

function Test-DkhEnvFile {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$EnvFile = ".env.local"
    )
    
    $envPath = Get-DkhEnvFile -EnvFile $EnvFile
    if (-not $envPath) {
        return $false
    }
    
    # Check for required variables
    $content = Get-Content $envPath -Raw
    $requiredVars = @(
        "POSTGRES_PASSWORD",
        "DATABASE_PASSWORD",
        "KEYCLOAK_ADMIN_PASSWORD"
    )
    
    $missingVars = @()
    foreach ($var in $requiredVars) {
        if ($content -notmatch "$var=\S+") {
            $missingVars += $var
        }
    }
    
    if ($missingVars.Count -gt 0) {
        Write-DkhError "Missing required environment variables: $($missingVars -join ', ')"
        Write-DkhInfo "Please edit: $envPath"
        return $false
    }
    
    return $true
}

# =============================================================================
# Path utilities
# =============================================================================

function Get-DkhInfrastructureRoot {
    [CmdletBinding()]
    param()
    
    $scriptPath = $PSScriptRoot
    # Navigate up from scripts/modules to root
    return Split-Path (Split-Path $scriptPath -Parent) -Parent
}

function Get-DkhDockerComposePath {
    [CmdletBinding()]
    param()
    
    $infraRoot = Get-DkhInfrastructureRoot
    return Join-Path $infraRoot "docker-compose"
}

# =============================================================================
# Docker Compose helpers
# =============================================================================

function Invoke-DkhDockerCompose {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        
        [Parameter(Mandatory = $false)]
        [string]$EnvFile = ".env.local"
    )
    
    $composePath = Get-DkhDockerComposePath
    $envPath = Get-DkhEnvFile -EnvFile $EnvFile
    
    if (-not $envPath) {
        throw "Environment file not configured"
    }
    
    Push-Location $composePath
    try {
        $cmd = @("docker", "compose", "--env-file", $envPath) + $Arguments
        Write-DkhInfo "Running: $($cmd -join ' ')"
        & $cmd[0] $cmd[1..($cmd.Length - 1)]
        
        if ($LASTEXITCODE -ne 0) {
            throw "Docker Compose command failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

function Get-DkhRunningServices {
    [CmdletBinding()]
    param()
    
    $composePath = Get-DkhDockerComposePath
    Push-Location $composePath
    try {
        $output = docker compose ps --format json 2>$null | ConvertFrom-Json
        return $output
    }
    catch {
        return @()
    }
    finally {
        Pop-Location
    }
}

# =============================================================================
# Service management
# =============================================================================

function Start-DkhServices {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string[]]$Profiles = @("infra"),
        
        [Parameter(Mandatory = $false)]
        [switch]$Detached = $true,
        
        [Parameter(Mandatory = $false)]
        [string]$EnvFile = ".env.local"
    )
    
    $profileArgs = $Profiles | ForEach-Object { "--profile", $_ }
    $args = @("up") + $profileArgs
    
    if ($Detached) {
        $args += "-d"
    }
    
    Invoke-DkhDockerCompose -Arguments $args -EnvFile $EnvFile
}

function Stop-DkhServices {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $false)]
        [string]$EnvFile = ".env.local"
    )
    
    Invoke-DkhDockerCompose -Arguments @("down") -EnvFile $EnvFile
}

# =============================================================================
# Export module members
# =============================================================================

Export-ModuleMember -Function @(
    'Write-DkhInfo',
    'Write-DkhSuccess',
    'Write-DkhWarning',
    'Write-DkhError',
    'Test-DkhPrerequisites',
    'Test-DkhDockerRunning',
    'Get-DkhEnvFile',
    'Test-DkhEnvFile',
    'Get-DkhInfrastructureRoot',
    'Get-DkhDockerComposePath',
    'Invoke-DkhDockerCompose',
    'Get-DkhRunningServices',
    'Start-DkhServices',
    'Stop-DkhServices'
)

# Note: $Global:IsWindows, $Global:IsMacOS, $Global:IsLinux, $Global:IsPwshCore, $Global:UseAsciiSymbols
# are set as global variables and don't need to be exported
