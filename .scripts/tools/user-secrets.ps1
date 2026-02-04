#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Manage .NET user secrets for GitHub NuGet credentials.

.DESCRIPTION
    Combined script for getting, setting, and loading user secrets.
    Replaces get-user-secrets.ps1, set-user-secrets.ps1, and load-user-secrets.ps1.

.PARAMETER Action
    Action to perform: Get, Set, or Load.

.PARAMETER ItemName
    User secrets section prefix (default: GITHUB_NUGET).

.PARAMETER Username
    GitHub username (for Set action).

.PARAMETER Token
    GitHub PAT token (for Set action).

.EXAMPLE
    ./user-secrets.ps1 -Action Get
    ./user-secrets.ps1 -Action Set -Username "myuser" -Token "ghp_xxx"
    ./user-secrets.ps1 -Action Load
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [ValidateSet("Get", "Set", "Load")]
    [string]$Action,

    [string]$ItemName = "GITHUB_NUGET",
    [string]$Username,
    [string]$Token
)

$ErrorActionPreference = "Stop"

function Get-UserSecrets
{
    param([string]$Prefix)

    $secretsList = dotnet user-secrets list 2>$null
    if ($LASTEXITCODE -ne 0)
    {
        return $null
    }

    $u = $secretsList | Where-Object { $_ -match "${Prefix}:USERNAME" } | ForEach-Object {
        $_.Split('=')[1].Trim()
    } | Select-Object -First 1

    $t = $secretsList | Where-Object { $_ -match "${Prefix}:TOKEN" } | ForEach-Object {
        $_.Split('=')[1].Trim()
    } | Select-Object -First 1

    return @{
        Username = $u
        Token    = $t
    }
}

switch ($Action)
{
    "Get" {
        $secrets = Get-UserSecrets -Prefix $ItemName
        if (-not $secrets -or -not $secrets.Username -or -not $secrets.Token)
        {
            Write-Host "No secrets found for $ItemName. Run with -Action Set first." -ForegroundColor Yellow
            exit 1
        }
        Write-Host "GITHUB_NUGET_USERNAME=$($secrets.Username)"
        Write-Host "GITHUB_NUGET_TOKEN=$($secrets.Token)"
    }

    "Set" {
        if (-not $Username)
        {
            $Username = Read-Host "Enter GITHUB_NUGET_USERNAME"
        }
        if (-not $Token)
        {
            $Token = Read-Host "Enter GITHUB_NUGET_TOKEN (PAT)"
        }

        Write-Host "Initializing user secrets for project..." -ForegroundColor Cyan
        dotnet user-secrets init -q

        Write-Host "Saving secrets to user profile..." -ForegroundColor Cyan
        dotnet user-secrets set "${ItemName}:USERNAME" "$Username" | Out-Null
        dotnet user-secrets set "${ItemName}:TOKEN" "$Token" | Out-Null

        Write-Host "Done. Stored in .NET user secrets (per-user, not in git)." -ForegroundColor Green
    }

    "Load" {
        $secrets = Get-UserSecrets -Prefix $ItemName
        if (-not $secrets -or -not $secrets.Username -or -not $secrets.Token)
        {
            Write-Host "No secrets found for $ItemName. Run with -Action Set first." -ForegroundColor Yellow
            exit 1
        }

        $env:GITHUB_NUGET_USERNAME = $secrets.Username
        $env:GITHUB_NUGET_TOKEN = $secrets.Token

        Write-Host "Loaded GITHUB_NUGET_USERNAME/TOKEN into current session." -ForegroundColor Green
    }
}
