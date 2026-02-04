# clean-packages.ps1
# Removes old NuGet package versions from GitHub Packages with optional .env credentials.

[CmdletBinding()]
param (
    [Parameter(Mandatory = $true)]
    [string]$GitHubOrganization,

    [int]$KeepLastVersions = 1,
    [switch]$WhatIf,
    [switch]$ListOnly,

    [string]$Token
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$SCRIPT_DIR = Split-Path -Parent $MyInvocation.MyCommand.Path
$SCRIPTS_ROOT = Split-Path -Parent $SCRIPT_DIR
$CONFIG_MODULE = Join-Path $SCRIPTS_ROOT "modules\Dkh.Configuration.psm1"

if (-not (Test-Path $CONFIG_MODULE))
{
    Write-Host "Configuration module not found at $CONFIG_MODULE" -ForegroundColor Red
    exit 1
}

Import-Module $CONFIG_MODULE -Force
$projectContext = Get-DkhProjectContext

if (-not $Token)
{
    $Token = $projectContext.Github.Token
    if ($Token)
    {
        Write-Host "Using GitHub PAT from .env" -ForegroundColor Cyan
    }
}

if (-not $Token)
{
    Write-Host "GitHub token is required. Provide -Token or populate GITHUB_NUGET_TOKEN in .env." -ForegroundColor Red
    exit 1
}

function Test-GitHubCli
{
    try
    {
        $null = Get-Command gh -ErrorAction Stop
        return $true
    }
    catch
    {
        return $false
    }
}

function Test-GitHubAuth
{
    try
    {
        gh auth status -h github.com | Out-Null
        return ($LASTEXITCODE -eq 0)
    }
    catch
    {
        return $false
    }
}

function Invoke-GitHubApi
{
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [ValidateSet("GET", "DELETE")]
        [string]$Method = "GET",

        [switch]$Paginate
    )

    $commonArgs = @("api", $Path, "--header", "Accept: application/vnd.github+json", "--header", "Authorization: token $Token")
    if ($Method -eq "DELETE")
    {
        $commonArgs += @("--method", "DELETE")
    }
    if ($Paginate)
    {
        $commonArgs += "--paginate"
    }

    $output = & gh @commonArgs
    if ($LASTEXITCODE -ne 0)
    {
        throw "GitHub API call failed for path $Path"
    }

    if ( [string]::IsNullOrWhiteSpace($output))
    {
        return @()
    }

    $json = $output | ConvertFrom-Json
    return $json
}

function Get-AllPackages
{
    param (
        [string]$Organization
    )

    Write-Host "Fetching packages for organization $Organization..." -ForegroundColor Cyan
    $path = "/orgs/$Organization/packages?package_type=nuget"
    $packages = Invoke-GitHubApi -Path $path -Paginate
    if (-not $packages)
    {
        return @()
    }

    return $packages | ForEach-Object {
        [PSCustomObject]@{
            Name = $_.name
            Id = $_.id
            Url = $_.html_url
            Type = $_.package_type
            Created = $_.created_at
            UpdatedAt = $_.updated_at
        }
    }
}

function Get-PackageVersions
{
    param (
        [string]$Organization,
        [string]$PackageName
    )

    Write-Host "  Retrieving versions for $PackageName..." -ForegroundColor DarkCyan
    $path = "orgs/$Organization/packages/nuget/$PackageName/versions"
    $versions = Invoke-GitHubApi -Path $path -Paginate
    if (-not $versions)
    {
        return @()
    }

    return $versions | ForEach-Object {
        [PSCustomObject]@{
            Id = $_.id
            Version = $_.name
            Created = $_.created_at
            UpdatedAt = $_.updated_at
            Url = $_.html_url
        }
    } | Sort-Object -Property Created -Descending
}

function Remove-PackageVersion
{
    param (
        [string]$Organization,
        [string]$PackageName,
        [string]$VersionId,
        [switch]$WhatIf
    )

    if ($WhatIf)
    {
        Write-Host "  WhatIf: would delete version id $VersionId" -ForegroundColor Yellow
        return $true
    }

    $path = "orgs/$Organization/packages/nuget/$PackageName/versions/$VersionId"
    Invoke-GitHubApi -Path $path -Method DELETE | Out-Null
    return $true
}

function Clean-Package
{
    param (
        [string]$Organization,
        [string]$PackageName,
        [int]$KeepLastVersions,
        [switch]$WhatIf
    )

    $versions = Get-PackageVersions -Organization $Organization -PackageName $PackageName
    if ($versions.Count -eq 0)
    {
        Write-Host "  No versions found for $PackageName." -ForegroundColor Yellow
        return
    }

    Write-Host ("  Found {0} versions for {1}" -f $versions.Count, $PackageName) -ForegroundColor Gray
    $versionsToRemove = $versions | Select-Object -Skip $KeepLastVersions

    if ($versionsToRemove.Count -eq 0)
    {
        Write-Host "  Nothing to remove (keeping last $KeepLastVersions)." -ForegroundColor Green
        return
    }

    foreach ($version in $versionsToRemove)
    {
        Write-Host ("  Removing {0} (created {1})" -f $version.Version, $version.Created) -ForegroundColor Cyan
        $success = Remove-PackageVersion -Organization $Organization -PackageName $PackageName -VersionId $version.Id -WhatIf:$WhatIf
        if (-not $success)
        {
            Write-Host ("    Failed to remove {0}" -f $version.Version) -ForegroundColor Red
        }
    }
}

if (-not (Test-GitHubCli))
{
    Write-Host "GitHub CLI (gh) is not installed. Install it from https://cli.github.com/." -ForegroundColor Red
    exit 1
}

if (-not (Test-GitHubAuth))
{
    Write-Host "gh CLI is not authenticated. Run 'gh auth login' first." -ForegroundColor Red
    exit 1
}

$packages = Get-AllPackages -Organization $GitHubOrganization
if (-not $packages -or $packages.Count -eq 0)
{
    Write-Host "No NuGet packages found in organization $GitHubOrganization." -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host ("Found {0} package(s):" -f $packages.Count) -ForegroundColor Cyan
$index = 1
foreach ($package in $packages)
{
    Write-Host ("  {0}. {1}" -f $index, ($package.Name ?? "[unnamed]")) -ForegroundColor White
    $index++
}

if ($ListOnly)
{
    Write-Host "`nListOnly flag specified. Exiting without deleting versions." -ForegroundColor Yellow
    exit 0
}

foreach ($package in $packages)
{
    if (-not [string]::IsNullOrWhiteSpace($package.Name))
    {
        Write-Host "`n========================================" -ForegroundColor Cyan
        Write-Host ("Processing package: {0}" -f $package.Name) -ForegroundColor Cyan
        Write-Host "========================================" -ForegroundColor Cyan
        Clean-Package -Organization $GitHubOrganization -PackageName $package.Name -KeepLastVersions $KeepLastVersions -WhatIf:$WhatIf
    }
}

Write-Host "`nCompleted package cleanup." -ForegroundColor Green
