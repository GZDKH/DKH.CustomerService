#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates all DKH.Platform.* package versions in Directory.Packages.props across all services.

.DESCRIPTION
    Finds all Directory.Packages.props files in the monorepo and updates
    DKH.Platform.* package versions to a specified version.

.PARAMETER Version
    The version to set for all DKH.Platform packages (e.g., "1.0.0")

.PARAMETER DryRun
    Preview changes without modifying files

.EXAMPLE
    pwsh update-platform-versions.ps1 -Version "1.0.0"
    # Updates all services to Platform 1.0.0

.EXAMPLE
    pwsh update-platform-versions.ps1 -Version "1.0.0" -DryRun
    # Preview what would be changed
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [switch]$DryRun
)

$ErrorActionPreference = "Stop"

# Find monorepo root
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

Write-Host ""
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Update DKH.Platform Versions" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Monorepo: $monorepoRoot" -ForegroundColor Gray
Write-Host "Version:  $Version" -ForegroundColor Gray
if ($DryRun) {
    Write-Host "Mode:     DRY RUN" -ForegroundColor Yellow
}
Write-Host ""

# Find all Directory.Packages.props files
$packagesFiles = Get-ChildItem -Path $monorepoRoot -Recurse -Filter "Directory.Packages.props" |
    Where-Object { $_.FullName -notmatch "\\node_modules\\" -and $_.FullName -notmatch "\\.git\\" }

Write-Host "Found $($packagesFiles.Count) Directory.Packages.props files" -ForegroundColor Green
Write-Host ""

$totalUpdated = 0
$totalFiles = 0

foreach ($file in $packagesFiles) {
    $relativePath = $file.FullName.Replace($monorepoRoot, "").TrimStart([IO.Path]::DirectorySeparatorChar)

    Write-Host "Processing: $relativePath" -ForegroundColor Cyan

    $content = Get-Content $file.FullName -Raw
    $originalContent = $content

    # Update all DKH.Platform.* package versions using regex
    # Pattern: <PackageVersion Include="DKH.Platform.*" Version="x.x.x" />
    $pattern = '(<PackageVersion\s+Include="DKH\.Platform\.[^"]+"\s+Version=")[^"]+"'
    $replacement = "`${1}$Version`""

    $newContent = $content -replace $pattern, $replacement

    # Also update base DKH.Platform package (without sub-packages)
    $basePattern = '(<PackageVersion\s+Include="DKH\.Platform"\s+Version=")[^"]+"'
    $newContent = $newContent -replace $basePattern, $replacement

    if ($newContent -ne $originalContent) {
        # Count how many packages were updated
        $matches = [regex]::Matches($originalContent, $pattern)
        $baseMatches = [regex]::Matches($originalContent, $basePattern)
        $updateCount = $matches.Count + $baseMatches.Count

        Write-Host "  Found $updateCount DKH.Platform packages to update" -ForegroundColor Yellow

        if ($DryRun) {
            Write-Host "  [DRY RUN] Would update $updateCount packages to version $Version" -ForegroundColor Gray
        } else {
            Set-Content -Path $file.FullName -Value $newContent -NoNewline
            Write-Host "  ✓ Updated $updateCount packages to version $Version" -ForegroundColor Green
        }

        $totalUpdated += $updateCount
        $totalFiles++
    } else {
        Write-Host "  No DKH.Platform packages found" -ForegroundColor Gray
    }

    Write-Host ""
}

# Summary
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host "Summary" -ForegroundColor Cyan
Write-Host "════════════════════════════════════════" -ForegroundColor Cyan
Write-Host ""
Write-Host "Files processed:      $($packagesFiles.Count)" -ForegroundColor White
Write-Host "Files updated:        $totalFiles" -ForegroundColor Green
Write-Host "Packages updated:     $totalUpdated" -ForegroundColor Green
Write-Host ""

if ($DryRun) {
    Write-Host "This was a dry run. Run without -DryRun to apply changes." -ForegroundColor Yellow
} else {
    Write-Host "✓ All versions updated to $Version" -ForegroundColor Green
}
Write-Host ""
