#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Update nuget.config files to use GitLab Package Registry
#>

$ErrorActionPreference = "Stop"

function Get-MonorepoRoot {
    $currentPath = $PSScriptRoot
    $maxDepth = 5
    $depth = 0

    while ($depth -lt $maxDepth) {
        if (Test-Path (Join-Path $currentPath "DKH.Infrastructure")) {
            return $currentPath
        }
        $parentPath = Split-Path -Parent $currentPath
        if ($parentPath -eq $currentPath) {
            break
        }
        $currentPath = $parentPath
        $depth++
    }

    throw "Could not find monorepo root"
}

$monorepoRoot = Get-MonorepoRoot
$gitlabUrl = "https://gitlab.com/api/v4/projects/79414535/packages/nuget/index.json"
$gitlabUsername = "itprodavets"
$gitlabToken = "glpat-4aDAFA2Y3HIZgfv-br8sF286MQp1OjJwa240Cw.01.1203qajna"

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Update NuGet Sources to GitLab" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

# Find all nuget.config files in services, gateways, workers
$nugetConfigs = Get-ChildItem -Path $monorepoRoot -Recurse -Filter "nuget.config" |
    Where-Object { $_.FullName -match "(services|gateways|workers)" }

Write-Host "Found $($nugetConfigs.Count) nuget.config files" -ForegroundColor Gray
Write-Host ""

$updatedCount = 0

foreach ($config in $nugetConfigs) {
    $relativePath = $config.FullName.Replace("$monorepoRoot/", "")
    Write-Host "Processing: $relativePath" -ForegroundColor Yellow

    [xml]$xml = Get-Content $config.FullName

    # Find packageSources node
    $packageSources = $xml.configuration.packageSources

    if ($null -eq $packageSources) {
        Write-Host "  ⚠ No packageSources found, skipping" -ForegroundColor Yellow
        continue
    }

    # Check if GitLab source already exists
    $gitlabSource = $packageSources.add | Where-Object { $_.key -eq "gitlab-gzdkh" }

    if ($gitlabSource) {
        Write-Host "  ✓ GitLab source already configured" -ForegroundColor Green
        continue
    }

    # Remove GitHub source if exists
    $githubSource = $packageSources.add | Where-Object { $_.key -eq "github-dotnet-gzdkh" }
    if ($githubSource) {
        $packageSources.RemoveChild($githubSource) | Out-Null
        Write-Host "  - Removed GitHub source" -ForegroundColor Gray
    }

    # Add GitLab source
    $gitlabNode = $xml.CreateElement("add")
    $gitlabNode.SetAttribute("key", "gitlab-gzdkh")
    $gitlabNode.SetAttribute("value", $gitlabUrl)
    $gitlabNode.SetAttribute("protocolVersion", "3")
    $packageSources.AppendChild($gitlabNode) | Out-Null

    # Add or update packageSourceCredentials
    $credentials = $xml.configuration.packageSourceCredentials
    if ($null -eq $credentials) {
        $credentials = $xml.CreateElement("packageSourceCredentials")
        $xml.configuration.AppendChild($credentials) | Out-Null
    }

    # Remove existing gitlab-gzdkh credentials if any
    $existingCreds = $credentials.SelectSingleNode("gitlab-gzdkh")
    if ($existingCreds) {
        $credentials.RemoveChild($existingCreds) | Out-Null
    }

    # Add credentials
    $gitlabCreds = $xml.CreateElement("gitlab-gzdkh")

    $usernameNode = $xml.CreateElement("add")
    $usernameNode.SetAttribute("key", "Username")
    $usernameNode.SetAttribute("value", $gitlabUsername)
    $gitlabCreds.AppendChild($usernameNode) | Out-Null

    $passwordNode = $xml.CreateElement("add")
    $passwordNode.SetAttribute("key", "ClearTextPassword")
    $passwordNode.SetAttribute("value", $gitlabToken)
    $gitlabCreds.AppendChild($passwordNode) | Out-Null

    $credentials.AppendChild($gitlabCreds) | Out-Null

    # Save
    $xml.Save($config.FullName)
    Write-Host "  ✓ Updated to GitLab source" -ForegroundColor Green
    $updatedCount++
}

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host "Summary" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "Files processed: $($nugetConfigs.Count)" -ForegroundColor Gray
Write-Host "Files updated:   $updatedCount" -ForegroundColor Green
Write-Host ""
