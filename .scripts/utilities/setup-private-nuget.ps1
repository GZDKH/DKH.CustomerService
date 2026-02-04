#!/usr/bin/env pwsh
[CmdletBinding(SupportsShouldProcess = $true)]
param(
    [string]$Username,
    [string]$Token,
    [switch]$SyncProjectConfig,
    [switch]$TestRestore,
    [string]$SourceName,
    [string]$SourceUrl,
    [string]$Environment
)

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

$targetEnvironment = if ($Environment)
{
    $Environment
}
elseif ($env:DKH_ENVIRONMENT)
{
    $env:DKH_ENVIRONMENT
}
else
{
    "Development"
}

$context = Get-DkhProjectContext -EnvironmentName $targetEnvironment
$config = $context.Config.Raw
$solutionPath = $context.Config.SolutionPath
$solutionName = $context.Config.SolutionName
$nugetSources = $config.NUGET.SOURCES

Write-Host "`n=== NuGet Private Source Setup ===" -ForegroundColor Cyan
Write-Host ("Environment: {0}" -f $targetEnvironment) -ForegroundColor Gray

if (-not $nugetSources -or $nugetSources.Count -eq 0)
{
    Write-Host "ERROR: No NuGet sources found in configuration!" -ForegroundColor Red
    exit 1
}

$privateSources = @()
foreach ($source in $nugetSources)
{
    $isPrivate = $false
    if ($source.PSObject.Properties["IS_PRIVATE"])
    {
        $isPrivate = [bool]$source.IS_PRIVATE
    }
    if ($isPrivate)
    {
        $privateSources += $source
    }
}
if (-not $privateSources -or $privateSources.Count -eq 0)
{
    Write-Host "No private NuGet sources configured. Nothing to do." -ForegroundColor Yellow
    exit 0
}

if ($SourceName)
{
    $filtered = $privateSources | Where-Object { $_.NAME -eq $SourceName }
    if (-not $filtered)
    {
        Write-Host "ERROR: Source '$SourceName' not found in private sources!" -ForegroundColor Red
        Write-Host "Available private sources: $( $privateSources.NAME -join ', ' )" -ForegroundColor Yellow
        exit 1
    }

    if ($SourceUrl)
    {
        $filtered = $filtered | ForEach-Object {
            $_ | Add-Member -NotePropertyName Url -NotePropertyValue $SourceUrl -Force
            $_
        }
    }

    $privateSources = @($filtered)
    Write-Host "Configuring only source: $SourceName" -ForegroundColor Cyan
}
elseif ($SourceUrl)
{
    Write-Host "WARNING: -SourceUrl is ignored unless -SourceName is provided." -ForegroundColor Yellow
}

Write-Host "Found $( $privateSources.Count ) private NuGet source(s):" -ForegroundColor Green
foreach ($source in $privateSources)
{
    Write-Host ("  -> {0}: {1}" -f $source.NAME, $source.URL) -ForegroundColor Gray
}

if (-not $Username)
{
    $Username = $context.Github.Username
}
if (-not $Token)
{
    $Token = $context.Github.Token
}

if (-not $Username -or -not $Token)
{
    Write-Host "ERROR: Credentials missing. Provide -Username/-Token or populate GITHUB_NUGET_USERNAME and GITHUB_NUGET_TOKEN in .env." -ForegroundColor Red
    exit 1
}

function Get-UserNuGetConfigPath
{
    $isWindows = $false
    try
    {
        $isWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform(
                [System.Runtime.InteropServices.OSPlatform]::Windows
        )
    }
    catch
    {
        $isWindows = $env:OS -like "*Windows*"
    }

    if ($isWindows)
    {
        return Join-Path $env:APPDATA 'NuGet\NuGet.Config'
    }

    $xdg = $env:XDG_CONFIG_HOME
    if ( [string]::IsNullOrWhiteSpace($xdg))
    {
        return Join-Path $HOME '.config/NuGet/NuGet.Config'
    }

    return Join-Path $xdg 'NuGet/NuGet.Config'
}

function Invoke-DotnetNugetCommand
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $nugetArgs = @("nuget") + $Arguments
    Write-Host ("    dotnet {0}" -f ($nugetArgs -join " ")) -ForegroundColor DarkGray

    if (-not $PSCmdlet.ShouldProcess($Description))
    {
        return $true
    }

    & dotnet @nugetArgs
    if ($LASTEXITCODE -ne 0)
    {
        throw "dotnet $( $nugetArgs -join ' ' ) failed with exit code $LASTEXITCODE"
    }
    return $true
}

function Ensure-Dir($path)
{
    $dir = Split-Path -Parent $path
    if (-not (Test-Path $dir))
    {
        New-Item -ItemType Directory -Path $dir | Out-Null
    }
}

function Get-ProjectNuGetConfigPath
{
    return Join-Path $solutionPath "nuget.config"
}

function Ensure-Source([xml]$XmlConfig, [string]$Name, [string]$Url, [string]$ProtocolVersion = "3")
{
    $configuration = $XmlConfig.configuration
    if (-not $configuration)
    {
        $configuration = $XmlConfig.CreateElement("configuration")
        $XmlConfig.AppendChild($configuration) | Out-Null
    }

    $sources = $configuration.packageSources
    if (-not $sources)
    {
        $sources = $XmlConfig.CreateElement("packageSources")
        $configuration.AppendChild($sources) | Out-Null
    }

    $existing = $sources.SelectNodes("add") | Where-Object { $_.GetAttribute("key") -eq $Name } | Select-Object -First 1
    if (-not $existing)
    {
        $existing = $XmlConfig.CreateElement("add")
        $sources.AppendChild($existing) | Out-Null
    }

    $existing.SetAttribute("key", $Name)
    $existing.SetAttribute("value", $Url)
    if ($ProtocolVersion)
    {
        $existing.SetAttribute("protocolVersion", $ProtocolVersion)
    }
}

function Sync-ProjectNuGetConfig
{
    $projectConfigPath = Get-ProjectNuGetConfigPath
    Write-Host "`nSyncing project nuget.config..." -ForegroundColor Cyan
    Write-Host "Project config: $projectConfigPath" -ForegroundColor Gray

    [xml]$projectXml = New-Object xml
    if (Test-Path $projectConfigPath)
    {
        try
        {
            $projectXml.Load($projectConfigPath)
        }
        catch
        {
            $projectXml = New-Object xml
        }
    }

    if ($projectXml.DocumentElement -eq $null -or $projectXml.DocumentElement.Name -ne 'configuration')
    {
        $decl = $projectXml.CreateXmlDeclaration('1.0', 'utf-8', $null)
        $projectXml.AppendChild($decl) | Out-Null
        $root = $projectXml.CreateElement('configuration')
        $projectXml.AppendChild($root) | Out-Null
    }

    foreach ($source in $nugetSources)
    {
        Write-Host ("  ensuring {0} => {1}" -f $source.NAME, $source.URL) -ForegroundColor Gray
        Ensure-Source $projectXml $source.NAME $source.URL $source.PROTOCOL_VERSION
    }

    if ( $PSCmdlet.ShouldProcess($projectConfigPath, "Update project nuget.config"))
    {
        $projectXml.Save($projectConfigPath)
        Write-Host "Project nuget.config updated successfully" -ForegroundColor Green
    }
}

function Configure-UserSources
{
    param(
        [string]$UserConfigPath,
        [object[]]$Sources
    )

    Ensure-Dir $UserConfigPath

    foreach ($source in $Sources)
    {
        $description = "user NuGet source '$( $source.NAME )'"
        $arguments = @(
            "update", "source", $source.NAME,
            "--source", $source.URL,
            "--configfile", $UserConfigPath,
            "--username", $Username,
            "--password", $Token,
            "--store-password-in-clear-text"
        )

        try
        {
            Invoke-DotnetNugetCommand -Description $description -Arguments $arguments | Out-Null
        }
        catch
        {
            Write-Host "Update failed for source $( $source.NAME ). Attempting to add instead..." -ForegroundColor Yellow
            $addArgs = @(
                "add", "source", $source.URL,
                "--name", $source.NAME,
                "--configfile", $UserConfigPath,
                "--username", $Username,
                "--password", $Token,
                "--store-password-in-clear-text"
            )
            Invoke-DotnetNugetCommand -Description $description -Arguments $addArgs | Out-Null
        }
    }
}

try
{
    if ($SyncProjectConfig.IsPresent)
    {
        Sync-ProjectNuGetConfig
    }

    $userConfigPath = Get-UserNuGetConfigPath
    Write-Host "`nConfiguring user NuGet credentials..." -ForegroundColor Cyan
    Write-Host "User config: $userConfigPath" -ForegroundColor Gray

    Configure-UserSources -UserConfigPath $userConfigPath -Sources $privateSources

    Write-Host "`n=== Configuration Summary ===" -ForegroundColor Magenta
    Write-Host ("User config : {0}" -f $userConfigPath) -ForegroundColor White
    Write-Host ("Project config : {0}" -f (Get-ProjectNuGetConfigPath)) -ForegroundColor White
    Write-Host ("Configured sources : {0}" -f ($privateSources.NAME -join ', ')) -ForegroundColor White

    if ($TestRestore.IsPresent)
    {
        $solutionFile = Join-Path $solutionPath "$solutionName.sln"
        if (-not (Test-Path $solutionFile))
        {
            throw "Solution not found: $solutionFile"
        }
        Write-Host "`nTesting restore with no cache..." -ForegroundColor Cyan
        $restoreArgs = @("restore", $solutionFile, "--no-cache", "--verbosity", "minimal")
        if ( $PSCmdlet.ShouldProcess($solutionFile, "dotnet restore --no-cache"))
        {
            & dotnet @restoreArgs
            if ($LASTEXITCODE -eq 0)
            {
                Write-Host "Test restore completed successfully" -ForegroundColor Green
            }
            else
            {
                Write-Host "Test restore failed with exit code: $LASTEXITCODE" -ForegroundColor Red
            }
        }
    }

    Write-Host "`nNuGet private source setup completed successfully." -ForegroundColor Green
}
catch
{
    Write-Host "`nError configuring NuGet sources: $_" -ForegroundColor Red
    Write-Host "Stack trace: $( $_.ScriptStackTrace )" -ForegroundColor Red
    exit 1
}
