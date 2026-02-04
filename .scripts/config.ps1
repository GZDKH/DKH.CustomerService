# Repository-wide configuration bootstrap.

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$MODULE_PATH = Join-Path $SCRIPT_DIR "modules\Dkh.Configuration.psm1"

if (-not (Test-Path $MODULE_PATH))
{
    Write-Host "Configuration module not found at $MODULE_PATH" -ForegroundColor Red
    exit 1
}

Import-Module $MODULE_PATH -Force

function Test-ConfigParameter
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParameterName,

        [Parameter(Mandatory = $false)]
        [object]$Value,

        [switch]$Required
    )

    $isMissing = ($null -eq $Value -or [string]::IsNullOrWhiteSpace([string]$Value))

    if ($isMissing -and $Required)
    {
        Write-Host ("ERROR: {0} is required but missing." -f $ParameterName) -ForegroundColor Red
        return $false
    }

    if ($isMissing)
    {
        Write-Host ("WARN : {0} is empty or missing." -f $ParameterName) -ForegroundColor Yellow
        return $true
    }

    Write-Host (" OK  : {0} = {1}" -f $ParameterName, $Value) -ForegroundColor Green
    return $true
}

try
{
    $projectContext = Get-DkhProjectContext -ScriptArgs $args
}
catch
{
    Write-Host "Failed to load project configuration: $_" -ForegroundColor Red
    exit 1
}

$CONFIG = $projectContext.Config.Raw
$FILE = $projectContext.Config.FilePath
$SOLUTION_PATH = $projectContext.Config.SolutionPath
$SOLUTION_NAME = $projectContext.Config.SolutionName
$ENV_FILE_PATH = $projectContext.Env.Path
$ENV_VARIABLES = $projectContext.Env.Values
$APP_CONFIG_RESULT = $projectContext.AppConfig
$MERGED_CONFIG = $APP_CONFIG_RESULT.MergedConfig
$SELECTED_ENV = $APP_CONFIG_RESULT.Environment
$GITHUB_NUGET_USERNAME = $projectContext.Github.Username
$GITHUB_NUGET_TOKEN = $projectContext.Github.Token

Write-Host "=== Validating Configuration Parameters ===" -ForegroundColor Cyan

$VALIDATION_FAILED = $false
$VALIDATION_FAILED = -not (Test-ConfigParameter "SOLUTION_NAME" $SOLUTION_NAME -Required) -or $VALIDATION_FAILED
$VALIDATION_FAILED = -not (Test-ConfigParameter "PROJECT.PROJECT_NAME" $CONFIG.PROJECT.PROJECT_NAME -Required) -or $VALIDATION_FAILED
$VALIDATION_FAILED = -not (Test-ConfigParameter "PROJECT.APPSETTINGS_FILE" $CONFIG.PROJECT.APPSETTINGS_FILE -Required) -or $VALIDATION_FAILED

# Docker configuration
$DOCKER_IMAGE_NAME = $CONFIG.DOCKER.IMAGE_NAME
$DOCKER_NETWORK_NAME = $CONFIG.DOCKER.NETWORK_NAME
$DOCKER_EXTERNAL_DB_PORT = $CONFIG.DOCKER.EXTERNAL_DB_PORT
$DOCKER_LOGS_VOLUME_NAME = $CONFIG.DOCKER.LOGS_VOLUME_NAME
$DOCKER_FILE_PATH = Join-Path $SOLUTION_PATH "$( $CONFIG.PROJECT.PROJECT_NAME )\Dockerfile"
$EXTERNAL_DB_PORT = $DOCKER_EXTERNAL_DB_PORT

Write-Host "`n--- Docker Configuration ---" -ForegroundColor Cyan
$VALIDATION_FAILED = -not (Test-ConfigParameter "DOCKER_IMAGE_NAME" $DOCKER_IMAGE_NAME -Required) -or $VALIDATION_FAILED
$VALIDATION_FAILED = -not (Test-ConfigParameter "DOCKER_NETWORK_NAME" $DOCKER_NETWORK_NAME -Required) -or $VALIDATION_FAILED
Test-ConfigParameter "DOCKER_EXTERNAL_DB_PORT" $DOCKER_EXTERNAL_DB_PORT | Out-Null
Test-ConfigParameter "DOCKER_LOGS_VOLUME_NAME" $DOCKER_LOGS_VOLUME_NAME | Out-Null

# Database configuration
$DB_HOST = $CONFIG.DATABASE.HOST
$DB_PORT = $CONFIG.DATABASE.PORT
$DB_NAME = $CONFIG.DATABASE.DATABASE_NAME
$DB_USERNAME = $CONFIG.DATABASE.USERNAME
$DB_PASSWORD = $CONFIG.DATABASE.PASSWORD
$DB_INCLUDE_ERROR_DETAIL = $CONFIG.DATABASE.INCLUDE_ERROR_DETAIL
$DB_VOLUME_NAME = $CONFIG.DATABASE.VOLUME_NAME
$DB_CONTAINER_NAME = "$DOCKER_IMAGE_NAME-db"
$DB_USER = $DB_USERNAME

Write-Host "`n--- Database Configuration ---" -ForegroundColor Cyan
$VALIDATION_FAILED = -not (Test-ConfigParameter "DB_HOST" $DB_HOST -Required) -or $VALIDATION_FAILED
$VALIDATION_FAILED = -not (Test-ConfigParameter "DB_PORT" $DB_PORT -Required) -or $VALIDATION_FAILED
$VALIDATION_FAILED = -not (Test-ConfigParameter "DB_NAME" $DB_NAME -Required) -or $VALIDATION_FAILED
$VALIDATION_FAILED = -not (Test-ConfigParameter "DB_USERNAME" $DB_USERNAME -Required) -or $VALIDATION_FAILED
$VALIDATION_FAILED = -not (Test-ConfigParameter "DB_PASSWORD" $DB_PASSWORD -Required) -or $VALIDATION_FAILED
Test-ConfigParameter "DB_INCLUDE_ERROR_DETAIL" $DB_INCLUDE_ERROR_DETAIL | Out-Null
Test-ConfigParameter "DB_VOLUME_NAME" $DB_VOLUME_NAME | Out-Null
Test-ConfigParameter "DB_CONTAINER_NAME" $DB_CONTAINER_NAME | Out-Null

# NuGet configuration
$NUGET_SOURCES = $CONFIG.NUGET.SOURCES
Write-Host "`n--- NuGet Configuration ---" -ForegroundColor Cyan
if ($NUGET_SOURCES -and $NUGET_SOURCES.Count -gt 0)
{
    foreach ($source in $NUGET_SOURCES)
    {
        Test-ConfigParameter ("NUGET_SOURCE_{0}" -f $source.NAME) $source.URL | Out-Null
    }
}
else
{
    Write-Host "WARN : No NuGet sources configured in config.json." -ForegroundColor Yellow
}

# GitHub credentials from .env
Write-Host "`n--- GitHub NuGet Credentials (.env) ---" -ForegroundColor Cyan
Test-ConfigParameter "GITHUB_NUGET_USERNAME" $GITHUB_NUGET_USERNAME | Out-Null
Test-ConfigParameter "GITHUB_NUGET_TOKEN" $GITHUB_NUGET_TOKEN | Out-Null
if (-not $GITHUB_NUGET_USERNAME -or -not $GITHUB_NUGET_TOKEN)
{
    Write-Host "TIP : Populate .env with GITHUB_NUGET_USERNAME / GITHUB_NUGET_TOKEN for private feed restore." -ForegroundColor Yellow
}

if ($VALIDATION_FAILED)
{
    Write-Host "`nConfiguration validation failed. Fix the errors above." -ForegroundColor Red
    exit 1
}

# Application configuration (appsettings merge)
$MERGED_CONFIG = $APP_CONFIG_RESULT.MergedConfig
$SELECTED_ENV = $APP_CONFIG_RESULT.Environment
$APPLICATION_NAME = $MERGED_CONFIG.Application
$GRPC_PORT_URL = $MERGED_CONFIG.Kestrel.Endpoints.Grpc.Url
$GRPC_PORT_NUMBER = $GRPC_PORT_URL -replace '.*:(\d+).*', '$1'
$CONNECTION_STRING = $MERGED_CONFIG.ConnectionStrings.DefaultConnection

$APP_VALIDATION_FAILED = $false
$APP_VALIDATION_FAILED = -not (Test-ConfigParameter "APPLICATION_NAME" $APPLICATION_NAME -Required) -or $APP_VALIDATION_FAILED
$APP_VALIDATION_FAILED = -not (Test-ConfigParameter "GRPC_PORT_URL" $GRPC_PORT_URL -Required) -or $APP_VALIDATION_FAILED
$APP_VALIDATION_FAILED = -not (Test-ConfigParameter "CONNECTION_STRING" $CONNECTION_STRING -Required) -or $APP_VALIDATION_FAILED

if ($APP_VALIDATION_FAILED)
{
    Write-Host "`nApplication configuration validation failed! Please review appsettings." -ForegroundColor Red
    exit 1
}

Write-Host "`n=== Configuration Loaded Successfully ===" -ForegroundColor Green
Write-Host "Environment: $SELECTED_ENV" -ForegroundColor Green
Write-Host "Application: $APPLICATION_NAME" -ForegroundColor Green
Write-Host "GRPC Port : $GRPC_PORT_NUMBER" -ForegroundColor Green

Write-Host "`n--- Summary ---" -ForegroundColor Magenta
Write-Host "Solution Path : $SOLUTION_PATH" -ForegroundColor Gray
Write-Host "Config JSON   : $FILE" -ForegroundColor Gray
Write-Host "Env File      : $ENV_FILE_PATH" -ForegroundColor Gray
Write-Host "Base Settings : $( $APP_CONFIG_RESULT.DefaultConfigPath )" -ForegroundColor Gray
Write-Host "Env Settings  : $( $APP_CONFIG_RESULT.EnvironmentConfigPath )" -ForegroundColor Gray
