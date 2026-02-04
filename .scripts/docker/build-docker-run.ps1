#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds and runs the service Docker container.
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

if (-not (Test-DkhCapability -Name "docker" -ManifestData $manifestData))
{
    Write-Host "Docker capability disabled; skipping container build/run." -ForegroundColor Yellow
    exit 0
}

if (-not (Test-DkhDockerAvailable))
{
    Write-Host "Docker CLI not found in PATH." -ForegroundColor Red
    exit 1
}

$solutionPath = $context.Config.SolutionPath
$projectName = $context.Config.ProjectName
$appConfig = $context.AppConfig.MergedConfig
$selectedEnv = $context.AppConfig.Environment
$grpcUrl = $appConfig.Kestrel.Endpoints.Grpc.Url
$grpcPort = $grpcUrl -replace '.*:(\d+).*', '$1'

$dockerConfig = $context.Config.Raw.DOCKER
$dockerImageName = $dockerConfig.IMAGE_NAME
$dockerNetworkName = $dockerConfig.NETWORK_NAME
$dockerFilePath = Join-Path $solutionPath "$projectName\Dockerfile"
$solutionName = $context.Config.SolutionName

$githubUser = $context.Github.Username
$githubToken = $context.Github.Token

if (Test-DkhCapability -Name "database" -ManifestData $manifestData)
{
    Write-Host "Ensuring database container is running..." -ForegroundColor Cyan
    & (Join-Path $scriptsRoot "database\postgres-db.ps1")
}

Write-Host "Waiting for database to be ready..." -ForegroundColor Yellow
Start-Sleep -Seconds 5

$buildArgs = @()
if ($githubUser -and $githubToken)
{
    Write-Host "Using GitHub NuGet credentials from .env/.manifest" -ForegroundColor Cyan
    $buildArgs += "--build-arg", "GITHUB_NUGET_USERNAME=$githubUser"
    $buildArgs += "--build-arg", "GITHUB_NUGET_TOKEN=$githubToken"
}
else
{
    Write-Host "WARNING: GitHub NuGet credentials missing; private feeds unavailable in docker build." -ForegroundColor Yellow
}

# Stop and remove existing container
Stop-DkhDockerContainer -ContainerName $dockerImageName -Silent

$dockerBuildCmd = @(
    "docker", "build", "-t", $dockerImageName,
    "-f", $dockerFilePath
) + $buildArgs + @($solutionPath)

Write-Host "Building Docker image:" -ForegroundColor Cyan
Write-Host ($dockerBuildCmd -join " ") -ForegroundColor Gray
& $dockerBuildCmd[0] @($dockerBuildCmd[1..($dockerBuildCmd.Length - 1)])

# Ensure network exists
Ensure-DkhDockerNetwork -NetworkName $dockerNetworkName -Silent

$dockerEnvVars = @(
    "-e", "ASPNETCORE_ENVIRONMENT=$selectedEnv"
)

if ($githubUser -and $githubToken)
{
    $dockerEnvVars += "-e", "GITHUB_NUGET_USERNAME=$githubUser"
    $dockerEnvVars += "-e", "GITHUB_NUGET_TOKEN=$githubToken"
}

docker run -d --name $dockerImageName `
    --network $dockerNetworkName `
    -p "${grpcPort}:${grpcPort}" `
    @dockerEnvVars `
    $dockerImageName | Out-Null

Write-Host "`n$solutionName ($selectedEnv) container started" -ForegroundColor Green
