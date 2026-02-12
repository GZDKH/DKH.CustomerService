#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Sets the commit date of the last commit to a specified or auto-calculated date.

.DESCRIPTION
    Amends the last commit with a new date. Can auto-calculate the next available
    date after the last remote commit or accept a user-specified date.

.EXAMPLE
    pwsh set-commit-date.ps1
    # Prompts for date or auto-calculates next available time

.EXAMPLE
    pwsh set-commit-date.ps1
    # Enter 2024-03-15 when prompted to set specific date
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Find project root by looking for .git directory
function Find-GitRoot {
    param([string]$StartPath = $PSScriptRoot)

    $currentPath = $StartPath
    $maxDepth = 10
    $depth = 0

    while ($depth -lt $maxDepth) {
        if (Test-Path (Join-Path $currentPath ".git")) {
            return $currentPath
        }

        $parentPath = Split-Path -Parent $currentPath
        if ($parentPath -eq $currentPath) {
            break
        }
        $currentPath = $parentPath
        $depth++
    }

    throw ".git directory not found"
}

$repoRoot = Find-GitRoot

if (-not (Test-Path "$repoRoot\.git")) {
    Write-Host "ERROR: .git directory not found at: $repoRoot" -ForegroundColor Red
    exit 1
}

Write-Host "Found .git directory at: $repoRoot" -ForegroundColor Green
Set-Location $repoRoot

# Prompt user for input
Write-Host "Enter target date (YYYY-MM-DD) or press [Enter] to use next available time:" -ForegroundColor Yellow
$inputDate = Read-Host "Your date"

# Fetch latest changes from remote
Write-Host "Fetching latest changes from remote..." -ForegroundColor Gray
git fetch | Out-Null

# Get remote branch
$remoteBranch = git symbolic-ref --short HEAD 2>$null
if (-not $remoteBranch) {
    $remoteBranch = "main"
}
$remoteBranch = "origin/$remoteBranch"

# Check if remote branch exists
$remoteBranchExists = git branch -r | Select-String -Pattern "^\s*$remoteBranch$"
if (-not $remoteBranchExists) {
    Write-Host "Remote branch $remoteBranch not found. Checking for origin/main and origin/master..." -ForegroundColor Yellow

    if (git branch -r | Select-String -Pattern "^\s*origin/main$") {
        $remoteBranch = "origin/main"
    }
    elseif (git branch -r | Select-String -Pattern "^\s*origin/master$") {
        $remoteBranch = "origin/master"
    }
    else {
        Write-Host "ERROR: Could not find a default remote branch (origin/main or origin/master)." -ForegroundColor Red
        exit 1
    }
}

Write-Host "Using remote branch: $remoteBranch" -ForegroundColor Cyan

$lastCommitIso = git log $remoteBranch -1 --format="%aI"
if (-not $lastCommitIso) {
    Write-Host "ERROR: Failed to retrieve last remote commit date." -ForegroundColor Red
    exit 1
}

try {
    $lastCommitDate = [datetime]::Parse($lastCommitIso)
}
catch {
    Write-Host "ERROR: Failed to parse the last commit date: $lastCommitIso" -ForegroundColor Red
    exit 1
}

$now = Get-Date
Write-Host "Last remote commit date: $($lastCommitDate.ToString('yyyy-MM-dd HH:mm:ss'))" -ForegroundColor Cyan

# Parse date or use next available time
if ([string]::IsNullOrWhiteSpace($inputDate)) {
    # Automatic date determination
    if ($lastCommitDate.Date -eq $now.Date) {
        # Same day: add 5 minutes to the last commit time
        $nextTime = $lastCommitDate.AddMinutes(5)

        if ($nextTime -gt $now) {
            Write-Host "ERROR: Cannot create commit in the future. Last remote commit was at: $($lastCommitDate.ToString('HH:mm:ss'))" -ForegroundColor Red
            exit 1
        }

        Write-Host "Using next available time after last remote commit: $($nextTime.ToString('HH:mm:ss'))" -ForegroundColor Cyan
        $finalDateTime = $nextTime
    }
    else {
        # Use next day after last commit
        $TargetDate = $lastCommitDate.AddDays(1)

        if ($TargetDate.Date -gt $now.Date) {
            Write-Host "ERROR: Next date would be in the future: $($TargetDate.ToString('yyyy-MM-dd'))" -ForegroundColor Red
            exit 1
        }

        Write-Host "Using next date after last remote commit: $($TargetDate.ToString('yyyy-MM-dd'))" -ForegroundColor Cyan

        # For new day, use work hours
        $randomHour = Get-Random -Minimum 9 -Maximum 18
        $randomMinute = Get-Random -Minimum 0 -Maximum 60
        $randomSecond = Get-Random -Minimum 0 -Maximum 60

        $finalDateTime = Get-Date -Year $TargetDate.Year -Month $TargetDate.Month -Day $TargetDate.Day `
            -Hour $randomHour -Minute $randomMinute -Second $randomSecond
    }
}
else {
    try {
        $TargetDate = [datetime]::ParseExact($inputDate, 'yyyy-MM-dd', $null)

        if ($TargetDate.Date -gt $now.Date) {
            Write-Host "ERROR: Cannot use future date: $($TargetDate.ToString('yyyy-MM-dd'))" -ForegroundColor Red
            exit 1
        }

        $lastCommitDateOnly = $lastCommitDate.Date
        $targetDateOnly = $TargetDate.Date

        if ($targetDateOnly -lt $lastCommitDateOnly) {
            Write-Host "ERROR: The entered date ($($TargetDate.ToString('yyyy-MM-dd'))) must be after or same as the last remote commit: $($lastCommitDate.ToString('yyyy-MM-dd'))" -ForegroundColor Red
            exit 1
        }

        Write-Host "Parsed date: $($TargetDate.ToString('yyyy-MM-dd'))" -ForegroundColor Green

        if ($targetDateOnly -eq $lastCommitDateOnly) {
            # Same day: add 5 minutes to last commit
            $nextTime = $lastCommitDate.AddMinutes(5)

            if ($TargetDate.Date -eq $now.Date -and $nextTime -gt $now) {
                Write-Host "ERROR: Cannot create commit in the future. Last remote commit was at: $($lastCommitDate.ToString('HH:mm:ss'))" -ForegroundColor Red
                exit 1
            }

            $finalDateTime = $nextTime
        }
        else {
            # Different day: use work hours
            $randomHour = Get-Random -Minimum 9 -Maximum 18
            $randomMinute = Get-Random -Minimum 0 -Maximum 60
            $randomSecond = Get-Random -Minimum 0 -Maximum 60

            $finalDateTime = Get-Date -Year $TargetDate.Year -Month $TargetDate.Month -Day $TargetDate.Day `
                -Hour $randomHour -Minute $randomMinute -Second $randomSecond
        }
    }
    catch {
        Write-Host "ERROR: Invalid date format. Use YYYY-MM-DD. Error: $($_.Exception.Message)" -ForegroundColor Red
        exit 1
    }
}

# Apply and rewrite commit
$isoDate = $finalDateTime.ToString("yyyy-MM-ddTHH:mm:ss")
Write-Host "`nSetting commit datetime to: $isoDate" -ForegroundColor Cyan

$env:GIT_AUTHOR_DATE = "$isoDate"
$env:GIT_COMMITTER_DATE = "$isoDate"

git commit --amend --no-edit --date "$isoDate"

Write-Host "Commit date updated successfully." -ForegroundColor Green
