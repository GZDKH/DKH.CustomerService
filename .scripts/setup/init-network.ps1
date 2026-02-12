#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Initialize and manage DKH Docker network
.DESCRIPTION
    Creates or updates the dkh-network Docker network
    Handles network cleanup and recreation if needed
.PARAMETER NetworkName
    Name of the network (default: dkh-network)
.PARAMETER Recreate
    Force recreate the network even if it exists
.PARAMETER Clean
    Remove the network if it exists
.EXAMPLE
    ./init-network.ps1
    ./init-network.ps1 -Recreate
    ./init-network.ps1 -Clean
#>

param(
    [string]$NetworkName = "dkh-network",
    [switch]$Recreate,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "`n   Docker Network Manager for $NetworkName`n" -ForegroundColor Cyan

# Check if Docker is running
try {
    $null = docker ps 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker is not running"
    }
}
catch {
    Write-Host "  Error: Docker daemon is not running" -ForegroundColor Red
    Write-Host "   Please start Docker and try again" -ForegroundColor Yellow
    exit 1
}

# Function to check if network exists
function Test-NetworkExists {
    param([string]$Name)
    $result = docker network ls --filter "name=^${Name}$" --format "{{.Name}}"
    return $result -eq $Name
}

# Function to inspect network
function Get-NetworkInfo {
    param([string]$Name)
    try {
        $info = docker network inspect $Name 2>$null | ConvertFrom-Json
        return $info[0]
    }
    catch {
        return $null
    }
}

# Check current network status
$networkExists = Test-NetworkExists -Name $NetworkName

if ($Clean) {
    Write-Host "     Cleaning up network: $NetworkName" -ForegroundColor Yellow
    
    if ($networkExists) {
        Write-Host "   Removing network..." -ForegroundColor Gray
        try {
            docker network rm $NetworkName 2>$null
            Write-Host "     Network removed" -ForegroundColor Green
        }
        catch {
            Write-Host "       Could not remove network (containers might be using it)" -ForegroundColor Yellow
            Write-Host "   Try stopping containers first: docker-compose down" -ForegroundColor Gray
            exit 1
        }
    }
    else {
        Write-Host "   Network does not exist" -ForegroundColor Gray
    }
    exit 0
}

# Check and create/update network
if ($networkExists -and -not $Recreate) {
    Write-Host "  Network already exists: $NetworkName" -ForegroundColor Green
    
    $info = Get-NetworkInfo -Name $NetworkName
    
    Write-Host "`n   Network Details:" -ForegroundColor Cyan
    Write-Host "   Name: $($info.Name)" -ForegroundColor Gray
    Write-Host "   Driver: $($info.Driver)" -ForegroundColor Gray
    Write-Host "   Subnet: $($info.IPAM.Config[0].Subnet)" -ForegroundColor Gray
    Write-Host "   Gateway: $($info.IPAM.Config[0].Gateway)" -ForegroundColor Gray
    
    $containerCount = ($info.Containers | Measure-Object).Count
    Write-Host "   Connected containers: $containerCount" -ForegroundColor Gray
    
    if ($containerCount -gt 0) {
        Write-Host "`n   Connected containers:" -ForegroundColor Cyan
        foreach ($container in $info.Containers) {
            Write-Host "        $($container.Name)" -ForegroundColor Gray
        }
    }
}
else {
    if ($Recreate -and $networkExists) {
        Write-Host "   Recreating network: $NetworkName" -ForegroundColor Yellow
        
        # Check for running containers
        $containers = docker network inspect $NetworkName --format "{{json .Containers}}" 2>$null | ConvertFrom-Json
        
        if ($containers.Count -gt 0) {
            Write-Host "       Containers are using this network:" -ForegroundColor Yellow
            foreach ($container in $containers) {
                Write-Host "        $($container.Name)" -ForegroundColor Gray
            }
            Write-Host ""
            Write-Host "   Please stop these containers first:" -ForegroundColor Yellow
            Write-Host "   docker-compose down" -ForegroundColor Gray
            exit 1
        }
        
        Write-Host "   Removing old network..." -ForegroundColor Gray
        docker network rm $NetworkName 2>$null
    }
    
    Write-Host "  Creating network: $NetworkName" -ForegroundColor Green
    
    try {
        docker network create `
            --driver bridge `
            --opt "com.docker.network.driver.mtu=1500" `
            $NetworkName 2>$null
        
        $info = Get-NetworkInfo -Name $NetworkName
        
        Write-Host "`n   New Network Details:" -ForegroundColor Cyan
        Write-Host "   Name: $($info.Name)" -ForegroundColor Green
        Write-Host "   Driver: $($info.Driver)" -ForegroundColor Green
        Write-Host "   Subnet: $($info.IPAM.Config[0].Subnet)" -ForegroundColor Green
        Write-Host "   Gateway: $($info.IPAM.Config[0].Gateway)" -ForegroundColor Green
    }
    catch {
        Write-Host "  Failed to create network: $_" -ForegroundColor Red
        exit 1
    }
}

Write-Host ""
Write-Host "  Network ready for docker-compose!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "   ./scripts/dkh.ps1 start  # Start services" -ForegroundColor Yellow
Write-Host "   docker network inspect $NetworkName  # View network details" -ForegroundColor Yellow
Write-Host ""

