#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Validates and displays the current DKH Infrastructure port configuration.

.DESCRIPTION
    This script:
    - Verifies .env.local exists and loads configuration
    - Shows all configured ports (external and internal)
    - Detects potential port conflicts
    - Tests connectivity to running services
    - Displays service access URLs

.EXAMPLE
    .\verify-ports.ps1
    
.EXAMPLE
    .\verify-ports.ps1 -Verbose -CheckConnectivity

.PARAMETER EnvFile
    Path to environment file. Defaults to .env.local in docker-compose directory.

.PARAMETER CheckConnectivity
    Test connectivity to running services on their configured ports.

.PARAMETER ShowDefaults
    Display default port values in addition to configured values.
#>

param(
    [string]$EnvFile,
    [switch]$CheckConnectivity,
    [switch]$ShowDefaults
)

# Import the infrastructure module (sets up cross-platform compatibility)
$moduleFile = Join-Path $PSScriptRoot "modules" "dkh.infrastructure.psm1"
if (-not (Test-Path $moduleFile)) {
    Write-Error "Infrastructure module not found at $moduleFile"
    exit 1
}
Import-Module $moduleFile -Force

# Define symbols for cross-platform compatibility
$checkSymbol = "[OK]"
$crossSymbol = "[X]"

# ============================================================================
# Port Definitions (defaults)
# ============================================================================
$defaultPorts = @{
    "PostgreSQL" = @{ external = 5432; internal = 5432; container = "dkh-postgres"; endpoint = ""; protocol = "postgres" }
    "RabbitMQ" = @{ external = 5672; internal = 5672; container = "dkh-rabbitmq"; endpoint = ""; protocol = "amqp" }
    "RabbitMQ Management" = @{ external = 15672; internal = 15672; container = "dkh-rabbitmq"; endpoint = "/"; protocol = "http" }
    "Keycloak" = @{ external = 8080; internal = 8080; container = "dkh-keycloak"; endpoint = "/"; protocol = "http" }
    "Prometheus" = @{ external = 9090; internal = 9090; container = "dkh-prometheus"; endpoint = "/"; protocol = "http" }
    "Grafana" = @{ external = 3030; internal = 3000; container = "dkh-grafana"; endpoint = "/"; protocol = "http" }
    "Seq (API)" = @{ external = 5341; internal = 5341; container = "dkh-seq"; endpoint = "/api/events"; protocol = "http" }
    "Seq (UI)" = @{ external = 8081; internal = 80; container = "dkh-seq"; endpoint = "/"; protocol = "http" }
    "Telegram Bot" = @{ external = 5001; internal = 5001; container = "dkh-telegram-bot"; endpoint = "/health"; protocol = "http" }
    "Notification Service" = @{ external = 5002; internal = 5002; container = "dkh-notification-service"; endpoint = "/health"; protocol = "http" }
    "Product Catalog" = @{ external = 5003; internal = 5003; container = "dkh-product-catalog"; endpoint = "/health"; protocol = "http" }
    "Reference Service" = @{ external = 5004; internal = 5004; container = "dkh-reference-service"; endpoint = "/health"; protocol = "http" }
    "Admin Gateway" = @{ external = 5005; internal = 5005; container = "dkh-admin-gateway"; endpoint = "/health"; protocol = "http" }
    "Storefront Gateway" = @{ external = 5006; internal = 5006; container = "dkh-storefront-gateway"; endpoint = "/health"; protocol = "http" }
    "Order Service" = @{ external = 5007; internal = 5007; container = "dkh-order-service"; endpoint = "/health"; protocol = "http" }
    "Cart Service" = @{ external = 5008; internal = 5008; container = "dkh-cart-service"; endpoint = "/health"; protocol = "http" }
}

# Environment variable mapping to port defaults
$envVariableMap = @{
    "POSTGRES_PORT" = "PostgreSQL"; "POSTGRES_INTERNAL_PORT" = "PostgreSQL"
    "RABBITMQ_PORT" = "RabbitMQ"; "RABBITMQ_INTERNAL_PORT" = "RabbitMQ"
    "RABBITMQ_MANAGEMENT_PORT" = "RabbitMQ Management"; "RABBITMQ_MANAGEMENT_INTERNAL_PORT" = "RabbitMQ Management"
    "KEYCLOAK_PORT" = "Keycloak"; "KEYCLOAK_INTERNAL_PORT" = "Keycloak"
    "PROMETHEUS_PORT" = "Prometheus"; "PROMETHEUS_INTERNAL_PORT" = "Prometheus"
    "GRAFANA_PORT" = "Grafana"; "GRAFANA_INTERNAL_PORT" = "Grafana"
    "SEQ_PORT" = "Seq (API)"; "SEQ_INTERNAL_PORT" = "Seq (API)"
    "SEQ_UI_PORT" = "Seq (UI)"; "SEQ_UI_INTERNAL_PORT" = "Seq (UI)"
    "TELEGRAM_BOT_PORT" = "Telegram Bot"; "TELEGRAM_BOT_INTERNAL_PORT" = "Telegram Bot"
    "NOTIFICATION_PORT" = "Notification Service"; "NOTIFICATION_INTERNAL_PORT" = "Notification Service"
    "PRODUCT_CATALOG_PORT" = "Product Catalog"; "PRODUCT_CATALOG_INTERNAL_PORT" = "Product Catalog"
    "REFERENCE_PORT" = "Reference Service"; "REFERENCE_INTERNAL_PORT" = "Reference Service"
    "ADMIN_GATEWAY_PORT" = "Admin Gateway"; "ADMIN_GATEWAY_INTERNAL_PORT" = "Admin Gateway"
    "STOREFRONT_GATEWAY_PORT" = "Storefront Gateway"; "STOREFRONT_GATEWAY_INTERNAL_PORT" = "Storefront Gateway"
    "ORDER_PORT" = "Order Service"; "ORDER_INTERNAL_PORT" = "Order Service"
    "CART_PORT" = "Cart Service"; "CART_INTERNAL_PORT" = "Cart Service"
}

# ============================================================================
# Helper Functions
# ============================================================================

function Get-EnvironmentValues {
    param([string]$EnvFile)
    
    $envValues = @{}
    
    if (-not (Test-Path $EnvFile)) {
        Write-DkhWarning "Environment file not found: $EnvFile"
        Write-DkhInfo "Run './scripts/dkh.ps1 setup' to create it"
        return $envValues
    }
    
    Get-Content $EnvFile | ForEach-Object {
        $line = $_.Trim()
        if ($line -and -not $line.StartsWith("#")) {
            $parts = $line -split "=", 2
            if ($parts.Count -eq 2) {
                $envValues[$parts[0]] = $parts[1]
            }
        }
    }
    
    return $envValues
}

function Test-PortConflict {
    param([int]$Port, [string]$ServiceName)

    if ($Global:DkhIsWindows) {
        $result = netstat -ano | Select-String ":$Port.*LISTENING" | Select-Object -First 1
    } else {
        # macOS/Linux
        $result = lsof -i ":$Port" 2>$null | Select-Object -Skip 1
    }

    if ($result) {
        Write-DkhWarning "Port $Port (${ServiceName}) appears to be in use!"
        return $true
    }

    return $false
}

function Test-ServiceConnectivity {
    param([string]$Hostname, [int]$Port, [string]$Protocol, [string]$ServiceName)

    try {
        if ($Protocol -eq "postgres") {
            # PostgreSQL uses port check with Test-NetConnection (Windows) or nc (Unix)
            if ($Global:DkhIsWindows) {
                $result = Test-NetConnection -ComputerName $Hostname -Port $Port -WarningAction SilentlyContinue -ErrorAction SilentlyContinue
                $connected = $result.TcpTestSucceeded
            } else {
                # macOS/Linux: use nc (netcat)
                $null = nc -z $Hostname $Port 2>$null
                $connected = $LASTEXITCODE -eq 0
            }
            if ($connected) {
                Write-DkhSuccess "$checkSymbol ${ServiceName}: Connected on port $Port"
                return $true
            } else {
                Write-DkhWarning "$crossSymbol ${ServiceName}: Cannot connect on port $Port"
                return $false
            }
        } else {
            # HTTP services
            $uri = "$Protocol`://$Hostname`:$Port"
            try {
                $null = Invoke-WebRequest -Uri $uri -TimeoutSec 2 -ErrorAction SilentlyContinue -WarningAction SilentlyContinue
                Write-DkhSuccess "$checkSymbol ${ServiceName}: Responding on $uri"
                return $true
            } catch {
                Write-DkhWarning "$crossSymbol ${ServiceName}: No response on $uri"
                return $false
            }
        }
    } catch {
        Write-DkhWarning "$crossSymbol ${ServiceName}: Error testing connectivity - $_"
        return $false
    }
}

# ============================================================================
# Main Script
# ============================================================================

Write-DkhInfo "╔════════════════════════════════════════════════════════════════╗"
Write-DkhInfo "║          DKH Infrastructure - Port Configuration Validator      ║"
Write-DkhInfo "╚════════════════════════════════════════════════════════════════╝"
Write-Output ""

# Determine env file location
if (-not $EnvFile) {
    $EnvFile = Join-Path (Split-Path -Parent $PSCommandPath) "..\docker-compose\.env.local"
}

Write-DkhInfo "Loading configuration from: $EnvFile"

# Load environment values
$envValues = Get-EnvironmentValues -EnvFile $EnvFile

# Current port status
$currentPorts = @{}
$defaultUsed = @{}

Write-Output ""
Write-DkhInfo "╭─ Port Configuration ─────────────────────────────────────────╮"

foreach ($service in $defaultPorts.Keys | Sort-Object) {
    $defaults = $defaultPorts[$service]
    $externalPort = $null
    $internalPort = $null
    
    # Find environment variable names for this service
    foreach ($envVar in $envVariableMap.Keys) {
        if ($envVariableMap[$envVar] -eq $service) {
            if ($envVar -match "_PORT$" -and $envVar -notmatch "_INTERNAL_") {
                $externalPort = $envValues[$envVar]
                if (-not $externalPort) {
                    $externalPort = $defaults.external
                    $defaultUsed[$service] = $true
                }
            }
            if ($envVar -match "_INTERNAL_PORT$") {
                $internalPort = $envValues[$envVar]
                if (-not $internalPort) {
                    $internalPort = $defaults.internal
                    $defaultUsed[$service] = $true
                }
            }
        }
    }
    
    $currentPorts[$service] = @{ external = $externalPort; internal = $internalPort }
    
    $portDisplay = "$externalPort → $internalPort"
    $marker = if ($defaultUsed[$service]) { "[default]" } else { "[custom] " }
    
    Write-Output "  $service"
    Write-Output "    Ports: $portDisplay $marker"
}

Write-Output "╰────────────────────────────────────────────────────────────────╯"

# Check for port conflicts
Write-Output ""
Write-DkhInfo "╭─ Port Conflict Detection ─────────────────────────────────────╮"

$conflictFound = $false
$testedPorts = @{}

foreach ($service in $currentPorts.Keys) {
    $externalPort = $currentPorts[$service].external
    
    if ($testedPorts.ContainsKey($externalPort)) {
        Write-DkhWarning "$crossSymbol Conflict: Multiple services use external port $externalPort"
        Write-DkhWarning "  - $($testedPorts[$externalPort])"
        Write-DkhWarning "  - $service"
        $conflictFound = $true
    } else {
        $testedPorts[$externalPort] = $service
    }
}

if (-not $conflictFound) {
    Write-DkhSuccess "$checkSymbol No port conflicts detected"
}

Write-Output "╰────────────────────────────────────────────────────────────────╯"

# Service connectivity check
if ($CheckConnectivity) {
    Write-Output ""
    Write-DkhInfo "╭─ Service Connectivity ───────────────────────────────────────╮"
    
    foreach ($service in $defaultPorts.Keys | Sort-Object) {
        $port = $currentPorts[$service].external
        $protocol = $defaultPorts[$service].protocol
        $container = $defaultPorts[$service].container
        
        Test-ServiceConnectivity -Hostname "localhost" -Port $port -Protocol $protocol -ServiceName $service
    }
    
    Write-Output "╰────────────────────────────────────────────────────────────────╯"
}

# Service access URLs
Write-Output ""
Write-DkhInfo "╭─ Service Access URLs ────────────────────────────────────────╮"

$httpServices = @{
    "RabbitMQ Management" = $currentPorts["RabbitMQ Management"].external
    "Keycloak" = $currentPorts["Keycloak"].external
    "Prometheus" = $currentPorts["Prometheus"].external
    "Grafana" = $currentPorts["Grafana"].external
    "Seq (UI)" = $currentPorts["Seq (UI)"].external
    "Telegram Bot" = $currentPorts["Telegram Bot"].external
    "Notification Service" = $currentPorts["Notification Service"].external
    "Product Catalog" = $currentPorts["Product Catalog"].external
    "Reference Service" = $currentPorts["Reference Service"].external
    "Admin Gateway" = $currentPorts["Admin Gateway"].external
    "Storefront Gateway" = $currentPorts["Storefront Gateway"].external
    "Order Service" = $currentPorts["Order Service"].external
    "Cart Service" = $currentPorts["Cart Service"].external
}

foreach ($service in $httpServices.Keys | Sort-Object) {
    $port = $httpServices[$service]
    $url = "http://localhost:$port"
    Write-Output "  $service`t→ $url"
}

Write-Output "╰────────────────────────────────────────────────────────────────╯"

# Database connection strings
Write-Output ""
Write-DkhInfo "╭─ Database Connection (from host) ────────────────────────────╮"

$psqlPort = $currentPorts["PostgreSQL"].external
$rabbitPort = $currentPorts["RabbitMQ"].external

Write-Output "  PostgreSQL:"
Write-Output "    psql -h localhost -p $psqlPort -U dkh"

Write-Output ""
Write-Output "  RabbitMQ:"
Write-Output "    amqp://localhost:$rabbitPort"

Write-Output "╰────────────────────────────────────────────────────────────────╯"

# Environment file status
Write-Output ""
Write-DkhInfo "╭─ Environment File Status ────────────────────────────────────╮"

if (Test-Path $EnvFile) {
    $fileSize = (Get-Item $EnvFile).Length
    $fileAge = ((Get-Date) - (Get-Item $EnvFile).LastWriteTime).TotalMinutes
    
    Write-DkhSuccess "$checkSymbol .env.local file exists"
    Write-Output "  Location: $EnvFile"
    Write-Output "  Size: $fileSize bytes"
    Write-Output "  Last modified: $([Math]::Round($fileAge)) minutes ago"
    
    # Check if contains required variables
    $hasRequiredVars = @()
    @("POSTGRES_PASSWORD", "GITHUB_NUGET_USER", "GITHUB_NUGET_TOKEN", "KEYCLOAK_ADMIN_PASSWORD") | ForEach-Object {
        if ($envValues.ContainsKey($_) -and $envValues[$_]) {
            $hasRequiredVars += $_
        }
    }
    
    Write-Output "  Required variables set: $($hasRequiredVars.Count)/4"
} else {
    Write-DkhWarning "$crossSymbol .env.local file not found"
    Write-DkhWarning "  Create it by running: ./scripts/dkh.ps1 setup"
}

Write-Output "╰────────────────────────────────────────────────────────────────╯"

Write-Output ""
Write-DkhSuccess "Port configuration validation complete!"
