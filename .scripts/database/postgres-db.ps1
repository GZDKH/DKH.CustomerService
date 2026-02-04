#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Provisions a PostgreSQL container for the service.
#>
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptsRoot = Split-Path -Parent $PSScriptRoot
$modulePath = Join-Path $scriptsRoot "modules\Dkh.Configuration.psm1"

if (-not (Test-Path $modulePath))
{
    Write-Host "Configuration module not found at $modulePath" -ForegroundColor Red
    exit 1
}

Import-Module $modulePath -Force

$context = Get-DkhProjectContext
$manifestData = $context.Manifest.Data

if (-not (Test-DkhCapability -Name "database" -ManifestData $manifestData))
{
    Write-Host "Database capability disabled; skipping Postgres provisioning." -ForegroundColor Yellow
    exit 0
}

if (-not (Test-DkhCapability -Name "docker" -ManifestData $manifestData))
{
    Write-Host "Docker capability disabled; cannot run Postgres container." -ForegroundColor Yellow
    exit 0
}

if (-not (Test-DkhDockerAvailable))
{
    Write-Host "Docker CLI not found in PATH." -ForegroundColor Red
    exit 1
}

$dockerConfig = $context.Config.Raw.DOCKER
$dbConfig = $context.Config.Raw.DATABASE

$dockerImageName = $dockerConfig.IMAGE_NAME
$dockerNetworkName = $dockerConfig.NETWORK_NAME
$externalDbPort = $dockerConfig.EXTERNAL_DB_PORT
$dbVolumeName = $dbConfig.VOLUME_NAME
$dbPort = $dbConfig.PORT
$dbUser = $dbConfig.USERNAME
$dbPassword = $dbConfig.PASSWORD
$dbName = $dbConfig.DATABASE_NAME
$dbContainerName = "$dockerImageName-db"

if (-not $dockerNetworkName -or -not $dockerImageName -or -not $dbVolumeName)
{
    Write-Host "Missing required Docker/Database configuration in config.json." -ForegroundColor Red
    exit 1
}

docker volume create $dbVolumeName | Out-Null

# Ensure network exists
Ensure-DkhDockerNetwork -NetworkName $dockerNetworkName -Silent

# Stop and remove existing container
Stop-DkhDockerContainer -ContainerName $dbContainerName -Silent

docker run -d --name $dbContainerName `
    --network $dockerNetworkName `
    -p "${externalDbPort}:${dbPort}" `
    -e POSTGRES_USER=$dbUser `
    -e POSTGRES_PASSWORD=$dbPassword `
    -e POSTGRES_DB=$dbName `
    -v ${dbVolumeName}:/var/lib/postgresql/data `
    postgres:latest | Out-Null

Write-Host "`n$dbContainerName container started" -ForegroundColor Green
