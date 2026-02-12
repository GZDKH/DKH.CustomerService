#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Setup DKH development environment
.DESCRIPTION
    Creates .env.local file and validates prerequisites for local development
#>

[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"

# Import infrastructure module (sets up cross-platform compatibility)
$modulePath = Join-Path $PSScriptRoot "modules" "dkh.infrastructure.psm1"
Import-Module $modulePath -Force

# Use ASCII fallback for headers on Windows PowerShell 5.1
$rocketIcon = ">>>"
$noteIcon = "[*]"
$wrenchIcon = "[>]"
$checkIcon = "[OK]"

Write-Host "`n$rocketIcon DKH Infrastructure Setup" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

# Step 1: Check prerequisites
Write-DkhInfo "Step 1: Checking prerequisites..."
if (-not (Test-DkhPrerequisites)) {
    exit 1
}

# Step 2: Check if .env.local already exists
$infraRoot = Get-DkhInfrastructureRoot
$dockerComposePath = Join-Path $infraRoot "docker-compose"
$envLocalPath = Join-Path $dockerComposePath ".env.local"
$envExamplePath = Join-Path $dockerComposePath ".env.example"

if (Test-Path $envLocalPath) {
    Write-DkhWarning ".env.local already exists"
    $overwrite = Read-Host "Do you want to overwrite it? (y/N)"
    if ($overwrite -ne "y" -and $overwrite -ne "Y") {
        Write-DkhInfo "Keeping existing .env.local"
        exit 0
    }
}

# Step 3: Copy from example
Write-DkhInfo "Step 2: Creating .env.local from template..."
if (Test-Path $envExamplePath) {
    Copy-Item $envExamplePath $envLocalPath
    Write-DkhSuccess "Created .env.local from .env.example"
} else {
    Write-DkhError "Template file not found: .env.example"
    exit 1
}

# Step 4: Interactive configuration
Write-Host "`n$noteIcon Configuration" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

Write-DkhInfo "Please provide values for required secrets:"
Write-Host "(Press Enter to keep default values from example)`n"

# Read current content
$envContent = Get-Content $envLocalPath -Raw

# PostgreSQL Password
$postgresPassword = Read-Host "PostgreSQL Password [dev123]"
if ($postgresPassword) {
    $envContent = $envContent -replace "POSTGRES_PASSWORD=.*", "POSTGRES_PASSWORD=$postgresPassword"
    # Update connection strings
    $envContent = $envContent -replace "Password=dev123", "Password=$postgresPassword"
}

# Keycloak admin password
$keycloakAdminPassword = Read-Host "Keycloak Admin Password [admin]"
if ($keycloakAdminPassword) {
    $envContent = $envContent -replace "KEYCLOAK_ADMIN_PASSWORD=.*", "KEYCLOAK_ADMIN_PASSWORD=$keycloakAdminPassword"
}
# Note: Keycloak uses shared DATABASE_PASSWORD for DB connection

# Telegram Bot Token (optional for infra only)
Write-Host "`nTelegram Bot Token (optional, get from @BotFather):"
$telegramToken = Read-Host "Telegram Bot Token (optional)"
if ($telegramToken) {
    $envContent = $envContent -replace "TELEGRAM_BOT_TOKEN=.*", "TELEGRAM_BOT_TOKEN=$telegramToken"
}

# Grafana password
$grafanaPassword = Read-Host "Grafana Admin Password [admin]"
if ($grafanaPassword) {
    $envContent = $envContent -replace "GRAFANA_ADMIN_PASSWORD=.*", "GRAFANA_ADMIN_PASSWORD=$grafanaPassword"
}

# Save updated content
Set-Content -Path $envLocalPath -Value $envContent -NoNewline
Write-DkhSuccess "Configuration saved to .env.local"

# Step 5: Create docker network if needed
Write-Host "`n$wrenchIcon Docker Configuration" -ForegroundColor Cyan
Write-Host "================================`n" -ForegroundColor Cyan

$networkExists = docker network ls --format "{{.Name}}" | Where-Object { $_ -eq "dkh-network" }
if (-not $networkExists) {
    Write-DkhInfo "Creating Docker network: dkh-network"
    docker network create dkh-network
    Write-DkhSuccess "Docker network created"
} else {
    Write-DkhInfo "Docker network already exists: dkh-network"
}

# Step 6: Summary
Write-Host "`n$checkIcon Setup Complete!" -ForegroundColor Green
Write-Host "================================`n" -ForegroundColor Green

Write-Host "Next steps:`n"
Write-Host "  1. Review your configuration:"
Write-Host "     $envLocalPath`n"
Write-Host "  2. Setup GitHub NuGet auth (required for building services):"
Write-Host "     ./scripts/dkh.ps1 nuget-auth`n"
Write-Host "  3. Start infrastructure services:"
Write-Host "     ./scripts/dkh.ps1 start`n"
Write-Host "  4. Start all services:"
Write-Host "     ./scripts/dkh.ps1 start -All`n"
Write-Host "  5. Check status:"
Write-Host "     ./scripts/dkh.ps1 status`n"

Write-DkhInfo "Documentation: docs/setup-guide.md"
