#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publish Contracts packages to GitLab Package Registry
.DESCRIPTION
    Scans all *.Contracts projects, reads their local version.json,
    and publishes according to the configuration.
.PARAMETER DryRun
    Preview changes without actually publishing or deleting packages
.PARAMETER ServicePath
    Optional path to a specific service to publish only its Contracts package
#>

param(
    [switch]$DryRun,
    [string]$ServicePath
)

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
$gitlabConfigFile = Join-Path $monorepoRoot "DKH.Infrastructure/scripts/config/gitlab.conf"

if (-not (Test-Path $gitlabConfigFile)) {
    Write-Error "GitLab config not found: $gitlabConfigFile"
}

# Load GitLab config
$config = @{}
Get-Content $gitlabConfigFile | Where-Object { $_ -match '^\s*(\w+)="(.+)"' } | ForEach-Object {
    $matches[1] | Out-Null
    $config[$matches[1]] = $matches[2]
}

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Publish Contracts Packages to GitLab" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""

if ($DryRun) {
    Write-Host "⚠ DRY RUN MODE - No changes will be made" -ForegroundColor Yellow
    Write-Host ""
}

# Find all *.Contracts projects
if ($ServicePath) {
    $contractsProjects = Get-ChildItem -Path $ServicePath -Recurse -Filter "*.Contracts.csproj" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }
} else {
    $contractsProjects = Get-ChildItem -Path $monorepoRoot -Recurse -Filter "*.Contracts.csproj" -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match "(services|gateways)" -and $_.FullName -notlike "*\obj\*" -and $_.FullName -notlike "*\bin\*" }
}

Write-Host "Found $($contractsProjects.Count) Contracts projects" -ForegroundColor Gray
Write-Host ""

$nupkgDir = Join-Path $monorepoRoot "nupkgs-contracts"

# Clean and create output directory
if (Test-Path $nupkgDir) {
    Remove-Item -Path $nupkgDir -Recurse -Force
}
New-Item -ItemType Directory -Path $nupkgDir -Force | Out-Null

$packCount = 0
$publishCount = 0
$deleteCount = 0
$skipCount = 0

foreach ($projectFile in $contractsProjects) {
    $projectDir = $projectFile.Directory.FullName
    $contractName = $projectFile.BaseName -replace '\.csproj$', ''
    $versionFile = Join-Path $projectDir "version.json"

    Write-Host "📦 $contractName" -ForegroundColor Cyan

    # Check if version file exists
    if (-not (Test-Path $versionFile)) {
        Write-Host "   ⚠ No version.json found, skipping" -ForegroundColor Yellow
        Write-Host ""
        $skipCount++
        continue
    }

    # Read version configuration
    try {
        $versionConfig = Get-Content $versionFile -Raw | ConvertFrom-Json
    } catch {
        Write-Host "   ✗ Failed to read version.json: $_" -ForegroundColor Red
        Write-Host ""
        continue
    }

    $version = $versionConfig.version
    $shouldPublish = $versionConfig.publish
    $shouldDelete = $versionConfig.delete
    $forceRepublish = $versionConfig.forceRepublish

    Write-Host "   Version: $version | Publish: $shouldPublish | Delete: $shouldDelete | Force: $forceRepublish" -ForegroundColor Gray

    # Handle delete flag
    if ($shouldDelete) {
        Write-Host "   ⚠ Marked for deletion" -ForegroundColor Yellow

        if (-not $DryRun) {
            $apiUrl = "https://gitlab.com/api/v4/projects/79414535/packages?per_page=100"
            $headers = @{ "PRIVATE-TOKEN" = $config['GITLAB_TOKEN'] }

            try {
                $packages = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method Get
                $targetPackage = $packages | Where-Object { $_.name -eq $contractName -and $_.version -eq $version }

                if ($targetPackage) {
                    $deleteUrl = "https://gitlab.com/api/v4/projects/79414535/packages/$($targetPackage.id)"
                    Invoke-RestMethod -Uri $deleteUrl -Headers $headers -Method Delete | Out-Null
                    Write-Host "   ✓ Deleted from GitLab" -ForegroundColor Green
                    $deleteCount++
                } else {
                    Write-Host "   ℹ Package not found in GitLab" -ForegroundColor Gray
                }
            } catch {
                Write-Host "   ✗ Failed to delete: $_" -ForegroundColor Red
            }
        } else {
            Write-Host "   [DRY RUN] Would delete package" -ForegroundColor Gray
            $deleteCount++
        }

        Write-Host ""
        continue
    }

    # Skip if publish is false
    if (-not $shouldPublish) {
        Write-Host "   ⊘ Skipped (publish: false)" -ForegroundColor Gray
        Write-Host ""
        $skipCount++
        continue
    }

    # Delete existing packages before republish to avoid duplicates
    if ($forceRepublish) {
        Write-Host "   🗑 Deleting existing packages (forceRepublish)..." -ForegroundColor Gray

        $apiUrl = "https://gitlab.com/api/v4/projects/79414535/packages?per_page=100&package_name=$contractName"
        $headers = @{ "PRIVATE-TOKEN" = $config['GITLAB_TOKEN'] }

        try {
            $packages = Invoke-RestMethod -Uri $apiUrl -Headers $headers -Method Get
            $targetPackages = $packages | Where-Object { $_.name -eq $contractName -and $_.version -eq $version }

            if ($targetPackages) {
                foreach ($pkg in $targetPackages) {
                    if (-not $DryRun) {
                        $deleteUrl = "https://gitlab.com/api/v4/projects/79414535/packages/$($pkg.id)"
                        Invoke-RestMethod -Uri $deleteUrl -Headers $headers -Method Delete | Out-Null
                        Write-Host "   ✓ Deleted package ID $($pkg.id)" -ForegroundColor Green
                        $deleteCount++
                    } else {
                        Write-Host "   [DRY RUN] Would delete package ID $($pkg.id)" -ForegroundColor Gray
                        $deleteCount++
                    }
                }
            } else {
                Write-Host "   ℹ No existing packages to delete" -ForegroundColor Gray
            }
        } catch {
            Write-Host "   ⚠ Failed to query/delete existing packages: $_" -ForegroundColor Yellow
        }
    }

    # Pack the project
    Write-Host "   📦 Packing..." -ForegroundColor Gray

    if (-not $DryRun) {
        $packOutput = dotnet pack $projectFile.FullName -c Release `
            -o $nupkgDir /p:PackageVersion=$version --verbosity quiet 2>&1

        if ($LASTEXITCODE -eq 0) {
            Write-Host "   ✓ Packed successfully" -ForegroundColor Green
            $packCount++

            # Publish to GitLab
            $nupkgFile = Join-Path $nupkgDir "$contractName.$version.nupkg"

            if (Test-Path $nupkgFile) {
                Write-Host "   🚀 Publishing to GitLab..." -ForegroundColor Gray

                $pushArgs = @(
                    "nuget", "push", $nupkgFile,
                    "--source", $config['GITLAB_SOURCE_URL'],
                    "--api-key", $config['GITLAB_TOKEN']
                )

                if (-not $forceRepublish) {
                    $pushArgs += "--skip-duplicate"
                }

                $output = & dotnet $pushArgs 2>&1

                if ($LASTEXITCODE -eq 0) {
                    Write-Host "   ✓ Published successfully" -ForegroundColor Green
                    $publishCount++
                } elseif ($LASTEXITCODE -eq 1 -and $output -match "(409|already exists|duplicate)") {
                    Write-Host "   ℹ Already exists (skipped)" -ForegroundColor Yellow
                    $publishCount++
                } else {
                    Write-Host "   ✗ Failed to publish: $output" -ForegroundColor Red
                }
            } else {
                Write-Host "   ✗ Package file not found: $nupkgFile" -ForegroundColor Red
            }
        } else {
            Write-Host "   ✗ Failed to pack: $packOutput" -ForegroundColor Red
        }
    } else {
        Write-Host "   [DRY RUN] Would pack and publish" -ForegroundColor Gray
        $packCount++
        $publishCount++
    }

    Write-Host ""
}

Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host "Summary" -ForegroundColor Green
Write-Host "════════════════════════════════════════" -ForegroundColor Green
Write-Host ""
Write-Host "Packed:     $packCount packages" -ForegroundColor Gray
Write-Host "Published:  $publishCount packages" -ForegroundColor Green
Write-Host "Deleted:    $deleteCount packages" -ForegroundColor Yellow
Write-Host "Skipped:    $skipCount packages" -ForegroundColor Gray
Write-Host ""

if (-not $DryRun -and ($packCount -gt 0 -or $deleteCount -gt 0)) {
    Write-Host "✓ Contracts processing completed!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Registry: https://gitlab.com/gzdkh/dkh-packages/-/packages" -ForegroundColor Gray
    Write-Host ""
} elseif ($DryRun) {
    Write-Host "ℹ Dry run completed - no changes were made" -ForegroundColor Yellow
    Write-Host ""
} else {
    Write-Host "ℹ No contracts to process" -ForegroundColor Gray
    Write-Host ""
}
