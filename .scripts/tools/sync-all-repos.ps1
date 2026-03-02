#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Synchronize all GZDKH repositories with their remote origins.

.DESCRIPTION
    Fetches all git repositories in the GZDKH workspace and ensures they are
    up to date with their remote branches. Handles three scenarios:

    - Behind remote: performs git pull --rebase
    - Diverged (force push): resets local branch to match remote
    - Up to date: no action needed

    Supports three modes:
    - check : fetch and report status only (no changes)
    - sync  : pull behind repos, reset diverged repos (default)
    - force : reset ALL repos to match remote (ignores local commits)

.EXAMPLE
    pwsh scripts/tools/sync-all-repos.ps1

.EXAMPLE
    pwsh scripts/tools/sync-all-repos.ps1 -Mode check

.EXAMPLE
    pwsh scripts/tools/sync-all-repos.ps1 -Mode force
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("check", "sync", "force")]
    [string]$Mode = "sync",

    [Parameter(Mandatory = $false)]
    [int]$ThrottleLimit = 5
)

$ErrorActionPreference = "Stop"

# --- Paths ---

function Get-InfraRoot {
    return (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
}

function Get-MonorepoRoot {
    return (Split-Path (Get-InfraRoot) -Parent)
}

# --- Repository discovery ---

function Get-GitRepos {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $searchDirs = @(
        $RootPath,
        (Join-Path $RootPath "services"),
        (Join-Path $RootPath "gateways"),
        (Join-Path $RootPath "libraries"),
        (Join-Path $RootPath "ui"),
        (Join-Path $RootPath "workers"),
        (Join-Path $RootPath "infrastructure")
    )

    $repos = @()

    foreach ($searchDir in $searchDirs) {
        if (-not (Test-Path $searchDir)) { continue }

        Get-ChildItem -Path $searchDir -Directory | ForEach-Object {
            $gitDir = Join-Path $_.FullName ".git"
            if (Test-Path $gitDir) {
                $repos += [PSCustomObject]@{
                    Name = $_.Name
                    Path = $_.FullName
                }
            }
        }
    }

    return $repos | Sort-Object Name
}

# --- Output helpers ---

function Write-Info {
    param([string]$Message)
    Write-Host "[i] $Message" -ForegroundColor Cyan
}

function Write-Ok {
    param([string]$Message)
    Write-Host "[OK] $Message" -ForegroundColor Green
}

function Write-Warn {
    param([string]$Message)
    Write-Host "[!] $Message" -ForegroundColor Yellow
}

function Write-Err {
    param([string]$Message)
    Write-Host "[X] $Message" -ForegroundColor Red
}

# --- Main logic ---

$rootPath = Get-MonorepoRoot
$repos = Get-GitRepos -RootPath $rootPath

if ($repos.Count -eq 0) {
    Write-Err "No git repositories found in $rootPath"
    exit 1
}

Write-Info "Found $($repos.Count) repositories in $rootPath"
Write-Info "Mode: $Mode"
Write-Host ""

# Fetch and analyze all repos in parallel
$modeCapture = $Mode
$results = $repos | ForEach-Object -Parallel {
    $repo = $_
    $mode = $using:modeCapture

    $result = @{
        Name    = $repo.Name
        Path    = $repo.Path
        Status  = "unknown"
        Action  = "none"
        Message = ""
        Ahead   = 0
        Behind  = 0
        Dirty   = $false
    }

    try {
        Set-Location $repo.Path

        # Get current branch
        $branch = git rev-parse --abbrev-ref HEAD 2>$null
        if (-not $branch -or $LASTEXITCODE -ne 0) {
            $result.Status = "error"
            $result.Message = "Cannot determine branch"
            return $result
        }

        # Fetch remote
        git fetch origin $branch --quiet 2>$null
        if ($LASTEXITCODE -ne 0) {
            $result.Status = "error"
            $result.Message = "Fetch failed"
            return $result
        }

        # Check for uncommitted changes
        $dirty = (git status --porcelain 2>$null | Measure-Object).Count -gt 0
        $result.Dirty = $dirty

        # Compare local vs remote
        $localHash = git rev-parse HEAD 2>$null
        $remoteHash = git rev-parse "origin/$branch" 2>$null

        if ($localHash -eq $remoteHash) {
            $result.Status = "up_to_date"
            $result.Message = "Up to date"
            return $result
        }

        $ahead = [int](git rev-list --count "origin/$branch..HEAD" 2>$null)
        $behind = [int](git rev-list --count "HEAD..origin/$branch" 2>$null)
        $result.Ahead = $ahead
        $result.Behind = $behind

        # Classify status
        if ($ahead -gt 0 -and $behind -gt 0) {
            $result.Status = "diverged"
            $result.Message = "Diverged (ahead=$ahead behind=$behind)"
        }
        elseif ($behind -gt 0) {
            $result.Status = "behind"
            $result.Message = "Behind by $behind commits"
        }
        elseif ($ahead -gt 0) {
            $result.Status = "ahead"
            $result.Message = "Ahead by $ahead commits"
        }

        # Apply action based on mode
        if ($mode -eq "check") {
            return $result
        }

        if ($mode -eq "force") {
            # Force mode: reset everything to remote
            if ($dirty) {
                $result.Action = "skipped"
                $result.Message += " [SKIPPED: uncommitted changes]"
                return $result
            }

            $output = git reset --hard "origin/$branch" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $result.Action = "reset"
                $result.Message += " -> Reset to origin/$branch"
            }
            else {
                $result.Action = "failed"
                $result.Message += " -> Reset failed: $output"
            }
            return $result
        }

        # Sync mode: smart handling per status
        if ($result.Status -eq "diverged") {
            if ($dirty) {
                $result.Action = "skipped"
                $result.Message += " [SKIPPED: uncommitted changes]"
                return $result
            }

            # Diverged with equal ahead/behind = force push on remote
            # Reset to match remote
            $output = git reset --hard "origin/$branch" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $result.Action = "reset"
                $result.Message += " -> Reset to origin/$branch"
            }
            else {
                $result.Action = "failed"
                $result.Message += " -> Reset failed: $output"
            }
        }
        elseif ($result.Status -eq "behind") {
            if ($dirty) {
                $result.Action = "skipped"
                $result.Message += " [SKIPPED: uncommitted changes]"
                return $result
            }

            $output = git rebase "origin/$branch" 2>&1
            if ($LASTEXITCODE -eq 0) {
                $result.Action = "pulled"
                $result.Message += " -> Rebased"
            }
            else {
                # Rebase failed, try reset
                git rebase --abort 2>$null
                $output = git reset --hard "origin/$branch" 2>&1
                if ($LASTEXITCODE -eq 0) {
                    $result.Action = "reset"
                    $result.Message += " -> Rebase failed, reset to origin/$branch"
                }
                else {
                    $result.Action = "failed"
                    $result.Message += " -> Rebase and reset failed"
                }
            }
        }
        elseif ($result.Status -eq "ahead") {
            # Local commits not on remote - don't touch
            $result.Action = "skipped"
            $result.Message += " [local commits, needs push]"
        }
    }
    catch {
        $result.Status = "error"
        $result.Message = "Exception: $_"
    }

    return $result
} -ThrottleLimit $ThrottleLimit

# --- Display results ---

Write-Host ("{0,-35} {1,-12} {2}" -f "REPOSITORY", "STATUS", "DETAILS") -ForegroundColor White
Write-Host ("-" * 80)

$upToDate = @()
$synced = @()
$skipped = @()
$failed = @()
$errors = @()

foreach ($r in $results | Sort-Object { $_.Name }) {
    $color = switch ($r.Status) {
        "up_to_date" { "Green" }
        "behind"     { if ($r.Action -eq "pulled" -or $r.Action -eq "reset") { "Green" } else { "Yellow" } }
        "diverged"   { if ($r.Action -eq "reset") { "Green" } else { "Yellow" } }
        "ahead"      { "Yellow" }
        "error"      { "Red" }
        default      { "DarkGray" }
    }

    $statusDisplay = switch ($r.Status) {
        "up_to_date" { "UP TO DATE" }
        "behind"     { "BEHIND" }
        "diverged"   { "DIVERGED" }
        "ahead"      { "AHEAD" }
        "error"      { "ERROR" }
        default      { $r.Status.ToUpper() }
    }

    $actionSuffix = switch ($r.Action) {
        "reset"   { " -> reset" }
        "pulled"  { " -> rebased" }
        "skipped" { " (skipped)" }
        "failed"  { " FAILED" }
        default   { "" }
    }

    $line = "{0,-35} {1,-12} {2}{3}" -f $r.Name, $statusDisplay, $r.Message, $actionSuffix
    Write-Host $line -ForegroundColor $color

    # Categorize
    switch ($r.Status) {
        "up_to_date" { $upToDate += $r }
        default {
            switch ($r.Action) {
                "reset"   { $synced += $r }
                "pulled"  { $synced += $r }
                "skipped" { $skipped += $r }
                "failed"  { $failed += $r }
                "none"    { if ($r.Status -eq "error") { $errors += $r } else { $skipped += $r } }
            }
        }
    }
}

Write-Host ("-" * 80)

# Summary
Write-Host ""
$total = $results.Count
Write-Host "Total: $total repositories" -ForegroundColor White
if ($upToDate.Count -gt 0) { Write-Ok "$($upToDate.Count) up to date" }
if ($synced.Count -gt 0) { Write-Ok "$($synced.Count) synchronized" }
if ($skipped.Count -gt 0) { Write-Warn "$($skipped.Count) skipped" }
if ($failed.Count -gt 0) { Write-Err "$($failed.Count) failed" }
if ($errors.Count -gt 0) { Write-Err "$($errors.Count) errors" }

# Exit code
if ($failed.Count -gt 0 -or $errors.Count -gt 0) {
    exit 1
}
exit 0
