# Dkh.Configuration.psm1
# Shared helpers for loading repository configuration, .env secrets, and appsettings.

Set-StrictMode -Version Latest

$script:ConfigurationCache = $null
$script:EnvironmentVariablesCache = $null
$script:AppSettingsCache = @{ }
$script:EnvManagerImported = $false
$script:ManifestCache = $null

function Get-DkhScriptsRoot
{
    return (Split-Path -Parent $PSScriptRoot)
}

function Get-DkhSolutionRoot
{
    $scriptsRoot = Get-DkhScriptsRoot
    return (Get-Item -Path (Join-Path $scriptsRoot "..")).FullName
}

function Get-DkhConfigFilePath
{
    return Join-Path (Join-Path (Get-DkhScriptsRoot) "config") "config.json"
}

function Get-DkhEnvFilePath
{
    return Join-Path (Get-DkhSolutionRoot) ".env"
}

function Get-DkhConfiguration
{
    [CmdletBinding()]
    param(
        [switch]$ForceRefresh
    )

    if (-not $script:ConfigurationCache -or $ForceRefresh)
    {
        $configFile = Get-DkhConfigFilePath
        if (-not (Test-Path $configFile))
        {
            throw "Configuration file not found at $configFile"
        }

        $configObject = Get-Content $configFile -Raw | ConvertFrom-Json
        $solutionPath = Get-DkhSolutionRoot

        $script:ConfigurationCache = [PSCustomObject]@{
            FilePath = $configFile
            Raw = $configObject
            SolutionPath = $solutionPath
            ScriptsRoot = Get-DkhScriptsRoot
            SolutionName = $configObject.PROJECT.SOLUTION_NAME
            ProjectName = $configObject.PROJECT.PROJECT_NAME
            AppSettingsFile = $configObject.PROJECT.APPSETTINGS_FILE
        }
    }

    return $script:ConfigurationCache
}

function ConvertTo-DkhEnvHashtable
{
    param(
        [string[]]$Content
    )

    $result = @{ }
    foreach ($line in $Content)
    {
        $trimmed = $line.Trim()
        if (-not $trimmed -or $trimmed.StartsWith("#"))
        {
            continue
        }

        if ($trimmed -match "^([^=]+)=(.*)$")
        {
            $key = $matches[1].Trim()
            $value = $matches[2].Trim()
            if (
            ($value.StartsWith('"') -and $value.EndsWith('"')) -or
                    ($value.StartsWith("'") -and $value.EndsWith("'"))
            )
            {
                $value = $value.Substring(1, $value.Length - 2)
            }
            $result[$key] = $value
        }
    }

    return $result
}

function Get-DkhEnvVariables
{
    [CmdletBinding()]
    param(
        [switch]$ForceRefresh
    )

    if (-not $script:EnvironmentVariablesCache -or $ForceRefresh)
    {
        $envPath = Get-DkhEnvFilePath
        $values = @{ }

        if (Test-Path $envPath)
        {
            $values = ConvertTo-DkhEnvHashtable -Content (Get-Content $envPath)
        }

        $script:EnvironmentVariablesCache = [PSCustomObject]@{
            Path = $envPath
            Values = $values
        }
    }

    return $script:EnvironmentVariablesCache
}

function Get-DkhGithubCredentials
{
    [CmdletBinding()]
    param(
        [string]$UsernameKey = "GITHUB_NUGET_USERNAME",
        [string]$TokenKey = "GITHUB_NUGET_TOKEN",
        [switch]$ForceRefresh
    )

    $envData = Get-DkhEnvVariables -ForceRefresh:$ForceRefresh
    $values = $envData.Values

    $username = [System.Environment]::GetEnvironmentVariable($UsernameKey)
    if (-not $username -and $values.ContainsKey($UsernameKey))
    {
        $username = $values[$UsernameKey]
    }

    $token = [System.Environment]::GetEnvironmentVariable($TokenKey)
    if (-not $token -and $values.ContainsKey($TokenKey))
    {
        $token = $values[$TokenKey]
    }

    return [PSCustomObject]@{
        Username = $username
        Token = $token
    }
}

function Set-DkhGithubNugetEnvironment
{
    [CmdletBinding()]
    param(
        [string]$UsernameKey = "GITHUB_NUGET_USERNAME",
        [string]$TokenKey = "GITHUB_NUGET_TOKEN",
        [switch]$ForceRefresh
    )

    $creds = Get-DkhGithubCredentials -UsernameKey $UsernameKey -TokenKey $TokenKey -ForceRefresh:$ForceRefresh

    $updated = $false
    if ($creds.Username)
    {
        $target = "Env:{0}" -f $UsernameKey
        if (-not (Test-Path $target) -or (Get-Item $target).Value -ne $creds.Username)
        {
            Set-Item -Path $target -Value $creds.Username
            $updated = $true
        }
    }

    if ($creds.Token)
    {
        $target = "Env:{0}" -f $TokenKey
        if (-not (Test-Path $target) -or (Get-Item $target).Value -ne $creds.Token)
        {
            Set-Item -Path $target -Value $creds.Token
            $updated = $true
        }
    }

    return [PSCustomObject]@{
        Username = $creds.Username
        Token = $creds.Token
        Updated = $updated
    }
}

function Test-DkhGlobalNugetCredentials
{
    [CmdletBinding()]
    param(
        [string]$SourceName = "github-dotnet-gzdkh"
    )

    # Cross-platform: Windows uses %APPDATA%\NuGet, Unix uses ~/.nuget/NuGet
    $globalConfigPath = if ($env:APPDATA)
    {
        Join-Path $env:APPDATA "NuGet/NuGet.Config"
    }
    else
    {
        Join-Path $HOME ".nuget/NuGet/NuGet.Config"
    }
    if (-not (Test-Path $globalConfigPath))
    {
        return $false
    }

    try
    {
        [xml]$config = Get-Content $globalConfigPath -Raw
        $credentialsNode = $config.SelectSingleNode("//packageSourceCredentials/$SourceName")
        if ($credentialsNode)
        {
            $username = $credentialsNode.SelectSingleNode("add[@key='Username']")
            $password = $credentialsNode.SelectSingleNode("add[@key='ClearTextPassword']")
            return ($null -ne $username -and $null -ne $password)
        }
    }
    catch
    {
        Write-Host "WARN : Failed to parse global NuGet.Config: $_" -ForegroundColor Yellow
    }

    return $false
}

function New-DkhNugetConfigWithSecrets
{
    [CmdletBinding()]
    param(
        [string]$TemplatePath = $( Join-Path (Get-DkhSolutionRoot) "nuget.config" ),
        [string]$UsernameKey = "GITHUB_NUGET_USERNAME",
        [string]$TokenKey = "GITHUB_NUGET_TOKEN"
    )

    if (-not (Test-Path $TemplatePath))
    {
        throw "NuGet template not found at $TemplatePath"
    }

    $username = [System.Environment]::GetEnvironmentVariable($UsernameKey)
    $token = [System.Environment]::GetEnvironmentVariable($TokenKey)
    if (-not $username -or -not $token)
    {
        throw "Missing $UsernameKey or $TokenKey in environment; cannot create NuGet config with credentials."
    }

    $content = Get-Content $TemplatePath -Raw
    $content = $content.Replace("%$UsernameKey%", $username).Replace("%$TokenKey%", $token)

    $tmpFile = [System.IO.Path]::GetTempFileName()
    Set-Content -Path $tmpFile -Value $content -Encoding UTF8
    return $tmpFile
}

function Get-DkhScriptsManifest
{
    [CmdletBinding()]
    param(
        [switch]$ForceRefresh
    )

    if (-not $script:ManifestCache -or $ForceRefresh)
    {
        $manifestPath = Join-Path (Join-Path (Get-DkhScriptsRoot) "config") "scripts.manifest.json"
        $manifestData = $null

        if (Test-Path $manifestPath)
        {
            try
            {
                $manifestData = Get-Content $manifestPath -Raw | ConvertFrom-Json
            }
            catch
            {
                Write-Host "WARN : Failed to parse scripts manifest ($manifestPath). $_" -ForegroundColor Yellow
            }
        }

        if (-not $manifestData)
        {
            $manifestData = [PSCustomObject]@{
                capabilities = [PSCustomObject]@{ }
                hooks = [PSCustomObject]@{ }
            }
        }

        $script:ManifestCache = [PSCustomObject]@{
            Path = $manifestPath
            Data = $manifestData
        }
    }

    return $script:ManifestCache
}

function Get-DkhAppSettingsContext
{
    [CmdletBinding()]
    param(
        [string]$EnvironmentName,
        [string[]]$ScriptArgs,
        [switch]$ForceRefresh
    )

    $config = Get-DkhConfiguration
    $cacheEnv = if ( [string]::IsNullOrEmpty($EnvironmentName))
    {
        "<auto>"
    }
    else
    {
        $EnvironmentName
    }
    $cacheArgs = if ($ScriptArgs -and $ScriptArgs.Count -gt 0)
    {
        $ScriptArgs -join " "
    }
    else
    {
        "<none>"
    }
    $cacheKey = "{0}|{1}" -f $cacheEnv, $cacheArgs

    if (-not $ForceRefresh -and $script:AppSettingsCache.ContainsKey($cacheKey))
    {
        return $script:AppSettingsCache[$cacheKey]
    }

    if (-not $script:EnvManagerImported)
    {
        $envManagerPath = Join-Path (Join-Path (Get-DkhScriptsRoot) "config") "environment-manager.ps1"
        if (-not (Test-Path $envManagerPath))
        {
            throw "Environment manager script not found at $envManagerPath"
        }

        if (-not (Get-Variable -Name DKH_SELECTED_ENVIRONMENT -Scope Global -ErrorAction SilentlyContinue))
        {
            Set-Variable -Name DKH_SELECTED_ENVIRONMENT -Scope Global -Value $null -Force
        }

        . $envManagerPath
        $script:EnvManagerImported = $true
    }

    $appsettingsPath = Join-Path $config.SolutionPath $config.ProjectName
    $result = Get-AppSettingsConfig `
        -AppsettingsFilePath $appsettingsPath `
        -Config $config.Raw `
        -Environment $EnvironmentName `
        -ScriptArgs $ScriptArgs

    if (-not $result)
    {
        throw "Failed to load application configuration from appsettings."
    }

    $script:AppSettingsCache[$cacheKey] = $result
    return $result
}

function Get-DkhProjectContext
{
    [CmdletBinding()]
    param(
        [string]$EnvironmentName,
        [string[]]$ScriptArgs,
        [switch]$ForceRefresh
    )

    $config = Get-DkhConfiguration -ForceRefresh:$ForceRefresh
    $envVars = Get-DkhEnvVariables -ForceRefresh:$ForceRefresh
    $appConfig = Get-DkhAppSettingsContext -EnvironmentName $EnvironmentName -ScriptArgs $ScriptArgs -ForceRefresh:$ForceRefresh
    $githubCreds = Get-DkhGithubCredentials -ForceRefresh:$ForceRefresh
    $manifest = Get-DkhScriptsManifest -ForceRefresh:$ForceRefresh

    return [PSCustomObject]@{
        Config = $config
        Env = $envVars
        Github = $githubCreds
        AppConfig = $appConfig
        Manifest = $manifest
    }
}

function Test-DkhCapability
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [bool]$Default = $true,

        [PSCustomObject]$ManifestData
    )

    if (-not $ManifestData)
    {
        $ManifestData = (Get-DkhScriptsManifest).Data
    }

    if (-not $ManifestData -or -not $ManifestData.capabilities)
    {
        return $Default
    }

    $capabilities = $ManifestData.capabilities
    if (-not $capabilities.PSObject.Properties[$Name])
    {
        return $Default
    }

    $value = $capabilities.$Name
    if ($value -is [bool])
    {
        return $value
    }

    if ($value -is [string])
    {
        switch ( $value.ToLowerInvariant())
        {
            "true" {
                return $true
            }
            "false" {
                return $false
            }
        }
    }

    if ($value -is [System.Management.Automation.PSCustomObject] -and $value.PSObject.Properties["enabled"])
    {
        return [bool]$value.enabled
    }

    return $Default
}

function Invoke-DkhHook
{
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [hashtable]$Arguments = @{ },

        [switch]$StopOnError
    )

    $hooksRoot = Join-Path (Get-DkhScriptsRoot) "hooks"
    $hookFolder = Join-Path $hooksRoot $Name

    if (-not (Test-Path $hookFolder))
    {
        return 0
    }

    $scripts = Get-ChildItem -Path $hookFolder -Filter *.ps1 -File | Sort-Object Name
    if (-not $scripts)
    {
        return 0
    }
    foreach ($script in $scripts)
    {
        Write-Host ("[Hook:{0}] {1}" -f $Name, $script.Name) -ForegroundColor Cyan
        & $script.FullName @Arguments
        if ($LASTEXITCODE -ne 0)
        {
            $message = "Hook '$( $script.FullName )' failed with exit code $LASTEXITCODE."
            if ($StopOnError)
            {
                throw $message
            }
            else
            {
                Write-Host "WARN : $message" -ForegroundColor Yellow
            }
        }
    }

    return $scripts.Count
}

#region Docker Helpers

function Test-DkhDockerAvailable
{
    <#
    .SYNOPSIS
        Checks if Docker CLI is available in PATH.
    #>
    [CmdletBinding()]
    param()

    return $null -ne (Get-Command docker -ErrorAction SilentlyContinue)
}

function Ensure-DkhDockerNetwork
{
    <#
    .SYNOPSIS
        Creates a Docker network if it doesn't exist.
    .PARAMETER NetworkName
        Name of the Docker network to ensure.
    .PARAMETER Silent
        Suppress output messages.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$NetworkName,

        [switch]$Silent
    )

    if (-not (Test-DkhDockerAvailable))
    {
        throw "Docker CLI not found in PATH."
    }

    $networkExists = docker network ls --format '{{.Name}}' | Select-String -Pattern "^$NetworkName$"
    if (-not $networkExists)
    {
        docker network create $NetworkName | Out-Null
        if (-not $Silent)
        {
            Write-Host "Created Docker network: $NetworkName" -ForegroundColor Green
        }
        return $true
    }

    return $false
}

function Stop-DkhDockerContainer
{
    <#
    .SYNOPSIS
        Stops and removes a Docker container if it exists.
    .PARAMETER ContainerName
        Name of the container to stop and remove.
    .PARAMETER Silent
        Suppress output messages.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContainerName,

        [switch]$Silent
    )

    if (-not (Test-DkhDockerAvailable))
    {
        throw "Docker CLI not found in PATH."
    }

    $containerExists = docker ps -a --format '{{.Names}}' | Select-String -Pattern "^$ContainerName$"
    if ($containerExists)
    {
        $isRunning = docker ps --format '{{.Names}}' | Select-String -Pattern "^$ContainerName$"
        if ($isRunning)
        {
            docker stop $ContainerName | Out-Null
        }
        docker rm $ContainerName | Out-Null
        if (-not $Silent)
        {
            Write-Host "Removed container: $ContainerName" -ForegroundColor Yellow
        }
        return $true
    }

    return $false
}

#endregion

Export-ModuleMember -Function `
    Get-DkhConfiguration, `
    Get-DkhEnvVariables, `
    Get-DkhGithubCredentials, `
    Set-DkhGithubNugetEnvironment, `
    Get-DkhProjectContext, `
    Get-DkhScriptsRoot, `
    Get-DkhSolutionRoot, `
    Get-DkhAppSettingsContext, `
    Get-DkhScriptsManifest, `
    Test-DkhCapability, `
    Invoke-DkhHook, `
    New-DkhNugetConfigWithSecrets, `
    Test-DkhGlobalNugetCredentials, `
    Test-DkhDockerAvailable, `
    Ensure-DkhDockerNetwork, `
    Stop-DkhDockerContainer
