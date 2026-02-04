# Environment Manager Module
# Module for managing environments and loading configuration

function Get-AvailableEnvironments
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppsettingsFilePath,

        [Parameter(Mandatory = $true)]
        [string]$BaseAppsettingsFile
    )

    $AVAILABLE_ENVIRONMENTS = @()

    # Search for all appsettings.{Environment}.json files
    $PATTERN = $BaseAppsettingsFile.Replace(".json", ".*.json")
    $APPSETTINGS_FILES = Get-ChildItem -Path $AppsettingsFilePath -Name $PATTERN -ErrorAction SilentlyContinue

    foreach ($FILE in $APPSETTINGS_FILES)
    {
        # Extract environment name from file name
        $ENV_NAME = $FILE -replace "$($BaseAppsettingsFile.Replace('.json', '') )\.(.+)\.json", '$1'
        if ($ENV_NAME -and $ENV_NAME -ne $FILE)
        {
            $AVAILABLE_ENVIRONMENTS += $ENV_NAME
        }
    }

    # Sort environments for consistency
    return $AVAILABLE_ENVIRONMENTS | Sort-Object
}

function Get-EnvironmentFromArgs
{
    param(
        [Parameter(Mandatory = $false)]
        [string[]]$Args,

        [Parameter(Mandatory = $true)]
        [string[]]$ValidEnvironments
    )

    # Search for -env or -environment parameter in arguments
    for ($i = 0; $i -lt $Args.Length; $i++) {
        if ($Args[$i] -eq "-env" -or $Args[$i] -eq "-environment")
        {
            if ($i + 1 -lt $Args.Length)
            {
                $ENV_VALUE = $Args[$i + 1]
                if ($ENV_VALUE -in $ValidEnvironments)
                {
                    return $ENV_VALUE
                }
                else
                {
                    Write-Host "Invalid environment: $ENV_VALUE. Available options: $( $ValidEnvironments -join ', ' )" -ForegroundColor Red
                    return $null
                }
            }
        }
    }

    return $null
}

function Get-EnvironmentFromEnvVar
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$ValidEnvironments
    )

    $ENV_VAR = $env:DKH_ENVIRONMENT
    if ($ENV_VAR)
    {
        if ($ENV_VAR -in $ValidEnvironments)
        {
            Write-Host "Using environment from environment variable: $ENV_VAR" -ForegroundColor Green
            return $ENV_VAR
        }
        else
        {
            Write-Host "Invalid environment variable value: $ENV_VAR. Available options: $( $ValidEnvironments -join ', ' )" -ForegroundColor Yellow
        }
    }

    return $null
}

function Get-EnvironmentChoice
{
    param(
        [Parameter(Mandatory = $false)]
        [string[]]$AvailableEnvironments,

        [Parameter(Mandatory = $false)]
        [string[]]$ScriptArgs
    )

    if (-not $AvailableEnvironments -or $AvailableEnvironments.Count -eq 0)
    {
        Write-Host "No environment configuration files found!" -ForegroundColor Red
        return $null
    }

    if ($AvailableEnvironments.Count -eq 1)
    {
        $global:DKH_SELECTED_ENVIRONMENT = $AvailableEnvironments[0]
        return $AvailableEnvironments[0]
    }

    # Check if environment is already cached in global variable
    if ($global:DKH_SELECTED_ENVIRONMENT -and ($global:DKH_SELECTED_ENVIRONMENT -in $AvailableEnvironments))
    {
        Write-Host "Using previously selected environment: $global:DKH_SELECTED_ENVIRONMENT" -ForegroundColor Green
        return $global:DKH_SELECTED_ENVIRONMENT
    }

    # 1. First check command line arguments
    if ($ScriptArgs)
    {
        $ENV_FROM_ARGS = Get-EnvironmentFromArgs -Args $ScriptArgs -ValidEnvironments $AvailableEnvironments
        if ($ENV_FROM_ARGS)
        {
            Write-Host "Using environment from command line arguments: $ENV_FROM_ARGS" -ForegroundColor Green
            # Cache the selected environment
            $global:DKH_SELECTED_ENVIRONMENT = $ENV_FROM_ARGS
            return $ENV_FROM_ARGS
        }
    }

    # 2. Then check environment variable
    $ENV_FROM_VAR = Get-EnvironmentFromEnvVar -ValidEnvironments $AvailableEnvironments
    if ($ENV_FROM_VAR)
    {
        # Cache the selected environment
        $global:DKH_SELECTED_ENVIRONMENT = $ENV_FROM_VAR
        return $ENV_FROM_VAR
    }

    # 3. If nothing found, ask user to select manually
    Write-Host "Available environments:" -ForegroundColor Cyan
    for ($i = 0; $i -lt $AvailableEnvironments.Count; $i++) {
        Write-Host "$( $i + 1 ). $( $AvailableEnvironments[$i] )"
    }

    $CHOICE = Read-Host "Enter the number (1-$( $AvailableEnvironments.Count ))"

    if ($CHOICE -lt 1 -or $CHOICE -gt $AvailableEnvironments.Count)
    {
        Write-Host "Invalid choice. Please select 1-$( $AvailableEnvironments.Count )." -ForegroundColor Red
        return $null
    }

    $SELECTED_ENV = $AvailableEnvironments[$CHOICE - 1]
    # Cache the selected environment
    $global:DKH_SELECTED_ENVIRONMENT = $SELECTED_ENV
    return $SELECTED_ENV
}

function Get-AppSettingsConfig
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppsettingsFilePath,

        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Config,

        [Parameter(Mandatory = $false)]
        [string]$Environment,

        [Parameter(Mandatory = $false)]
        [string[]]$ScriptArgs
    )

    $BASE_APPSETTINGS_FILE = $Config.PROJECT.APPSETTINGS_FILE
    $DEFAULT_APPSETTINGS_FILE = "{0}\{1}" -f $AppsettingsFilePath, $BASE_APPSETTINGS_FILE

    # Check if base configuration file exists
    if (-not (Test-Path $DEFAULT_APPSETTINGS_FILE))
    {
        Write-Host "Default configuration file $DEFAULT_APPSETTINGS_FILE not found!" -ForegroundColor Red
        return $null
    }

    # Get list of available environments
    $AVAILABLE_ENVIRONMENTS = @(Get-AvailableEnvironments -AppsettingsFilePath $AppsettingsFilePath -BaseAppsettingsFile $BASE_APPSETTINGS_FILE)

    if ($AVAILABLE_ENVIRONMENTS.Count -eq 0)
    {
        Write-Host "No environment-specific configuration files found!" -ForegroundColor Red
        return $null
    }

    Write-Host "Found environments: $( $AVAILABLE_ENVIRONMENTS -join ', ' )" -ForegroundColor Green

    # If environment is not provided, determine it automatically
    if (-not $Environment)
    {
        $Environment = Get-EnvironmentChoice -AvailableEnvironments $AVAILABLE_ENVIRONMENTS -ScriptArgs $ScriptArgs
        if (-not $Environment)
        {
            return $null
        }
    }
    else
    {
        # Check if provided environment exists
        if ($Environment -notin $AVAILABLE_ENVIRONMENTS)
        {
            Write-Host "Environment '$Environment' not found. Available environments: $( $AVAILABLE_ENVIRONMENTS -join ', ' )" -ForegroundColor Red
            return $null
        }
    }

    $APPSETTINGS_FILE = $DEFAULT_APPSETTINGS_FILE.Replace(".json", ".$Environment.json")

    # Check if environment configuration file exists (additional check)
    if (-not (Test-Path $APPSETTINGS_FILE))
    {
        Write-Host "Environment configuration file $APPSETTINGS_FILE not found!" -ForegroundColor Red
        return $null
    }

    # Load and merge configurations
    $APP_DEFAULT_CONFIG = Get-Content $DEFAULT_APPSETTINGS_FILE -Raw | ConvertFrom-Json
    $APP_CONFIG = Get-Content $APPSETTINGS_FILE -Raw | ConvertFrom-Json

    $MERGED_CONFIG = Merge-Config $APP_DEFAULT_CONFIG $APP_CONFIG

    return @{
        Environment = $Environment
        AvailableEnvironments = $AVAILABLE_ENVIRONMENTS
        MergedConfig = $MERGED_CONFIG
        DefaultConfigPath = $DEFAULT_APPSETTINGS_FILE
        EnvironmentConfigPath = $APPSETTINGS_FILE
    }
}

function Merge-Config
{
    param(
        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Default,

        [Parameter(Mandatory = $true)]
        [PSCustomObject]$Override
    )

    foreach ($KEY in $Override.PSObject.Properties.Name)
    {
        if ($Default.PSObject.Properties[$KEY])
        {
            if (($Default.$KEY -is [System.Management.Automation.PSCustomObject]) -and ($Override.$KEY -is [System.Management.Automation.PSCustomObject]))
            {
                $Default.$KEY = Merge-Config $Default.$KEY $Override.$KEY
            }
            else
            {
                $Default.$KEY = $Override.$KEY
            }
        }
        else
        {
            $Default | Add-Member -MemberType NoteProperty -Name $KEY -Value $Override.$KEY -Force
        }
    }
    return $Default
}

# Functions are now available in the current scope through dot sourcing
