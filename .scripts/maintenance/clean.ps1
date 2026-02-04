#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Cleans bin and obj folders from the solution.
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

$solutionRoot = Get-DkhSolutionRoot

$foldersToDelete = @(Get-ChildItem -Path $solutionRoot -Include "bin", "obj" -Directory -Recurse -Force)

if ($foldersToDelete.Count -eq 0)
{
    Write-Host "No bin/obj folders found." -ForegroundColor Yellow
    exit
}

Write-Host "Folders found for deletion: $($foldersToDelete.Count)" -ForegroundColor Cyan
$foldersToDelete | ForEach-Object {
    Write-Host "  $($_.FullName)" -ForegroundColor DarkGray
}

$totalSize = 0
$successCount = 0

$foldersToDelete | ForEach-Object {
    try
    {
        $folder = $_
        $files = Get-ChildItem $_.FullName -Recurse -File -ErrorAction SilentlyContinue
        $size = if ($files)
        {
            ($files | Measure-Object -Property Length -Sum).Sum
        }
        else
        {
            0
        }
        $totalSize += $size

        Write-Host "Deleting: $($folder.FullName)" -ForegroundColor DarkYellow
        Remove-Item $folder.FullName -Recurse -Force -ErrorAction Stop
        $successCount++
    }
    catch
    {
        Write-Host "Deletion error $($folder.FullName): $_" -ForegroundColor Red
    }
}

Write-Host "`nCleanup complete!" -ForegroundColor Green
Write-Host "Successfully deleted folders: $successCount/$($foldersToDelete.Count)"
if ($totalSize -gt 0)
{
    Write-Host "Space cleared: $([Math]::Round($totalSize / 1MB, 2)) MB"
}
