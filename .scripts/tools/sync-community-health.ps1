#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Synchronize community health files across all GZDKH repositories.

.DESCRIPTION
    Distributes standard community health files (CODE_OF_CONDUCT, CONTRIBUTING,
    SECURITY, issue/PR templates) from DKH.Infrastructure/community-health/
    to all git repositories in the GZDKH workspace.

    Supports five modes:
    - check  : report missing/different files in each repository
    - sync   : copy community health files to all repositories
    - commit : commit changes in each git repository
    - push   : push commits in each git repository
    - all    : sync + commit + push

.PARAMETER Mode
    Operation mode: check, sync, commit, push, or all.

.PARAMETER ServicePath
    Path to a specific service to sync (relative to monorepo root, e.g. "services/DKH.CartService").
    When specified, only this repository is processed.

.PARAMETER All
    Sync all discovered git repositories in the GZDKH workspace.

.PARAMETER DryRun
    Preview changes without applying them (equivalent to -Mode check).

.PARAMETER CommitMessage
    Git commit message for the commit mode. Default: "docs: add community health files".

.PARAMETER NoVerify
    Skip git hooks when committing (--no-verify).

.EXAMPLE
    # Check status across all repos
    pwsh scripts/tools/sync-community-health.ps1 -Mode check

.EXAMPLE
    # Sync files to all repos
    pwsh scripts/tools/sync-community-health.ps1 -All

.EXAMPLE
    # Sync files to a single repo
    pwsh scripts/tools/sync-community-health.ps1 -ServicePath services/DKH.CartService

.EXAMPLE
    # Preview changes (dry run)
    pwsh scripts/tools/sync-community-health.ps1 -All -DryRun

.EXAMPLE
    # Full workflow: sync + commit + push
    pwsh scripts/tools/sync-community-health.ps1 -Mode all
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("check", "sync", "commit", "push", "all")]
    [string]$Mode = "sync",

    [Parameter(Mandatory = $false)]
    [string]$ServicePath,

    [Parameter(Mandatory = $false)]
    [switch]$All,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun,

    [Parameter(Mandatory = $false)]
    [string]$CommitMessage = "docs: add community health files",

    [Parameter(Mandatory = $false)]
    [switch]$NoVerify
)

$ErrorActionPreference = "Stop"

# --- Paths ---

function Get-InfraRoot {
    return (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent)
}

function Get-MonorepoRoot {
    return (Split-Path (Get-InfraRoot) -Parent)
}

function Get-SourceDir {
    return (Join-Path (Get-InfraRoot) "community-health")
}

# --- File discovery ---

function Get-SourceFiles {
    param(
        [Parameter(Mandatory = $true)][string]$SourceDir
    )

    return Get-ChildItem -Path $SourceDir -File -Recurse -Force |
        ForEach-Object {
            [PSCustomObject]@{
                FullPath     = $_.FullName
                RelativePath = $_.FullName.Substring($SourceDir.Length).TrimStart([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
            }
        } |
        Sort-Object RelativePath
}

function Get-GitRepos {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$InfraRoot
    )

    return Get-ChildItem -Path $RootPath -Directory -Recurse -Force |
        Where-Object {
            $_.Name -eq ".git" -and
            $_.FullName -notmatch '[/\\]node_modules[/\\]'
        } |
        ForEach-Object { Split-Path $_.FullName -Parent } |
        Where-Object { $_ -ne $InfraRoot } |
        Sort-Object -Unique
}

# --- Comparison ---

function Test-FileContentEqual {
    param(
        [Parameter(Mandatory = $true)][string]$LeftPath,
        [Parameter(Mandatory = $true)][string]$RightPath
    )

    $leftHash = (Get-FileHash -Path $LeftPath -Algorithm SHA256).Hash
    $rightHash = (Get-FileHash -Path $RightPath -Algorithm SHA256).Hash
    return $leftHash -eq $rightHash
}

function Get-RepoStatus {
    param(
        [Parameter(Mandatory = $true)][string]$RepoPath,
        [Parameter(Mandatory = $true)][object[]]$SourceFiles
    )

    $result = New-Object System.Collections.Generic.List[object]

    foreach ($src in $SourceFiles) {
        $targetPath = Join-Path $RepoPath $src.RelativePath

        if (-not (Test-Path $targetPath)) {
            $result.Add([PSCustomObject]@{
                RelativePath = $src.RelativePath
                SourcePath   = $src.FullPath
                TargetPath   = $targetPath
                Status       = "MISS"
            })
            continue
        }

        if (Test-FileContentEqual -LeftPath $src.FullPath -RightPath $targetPath) {
            $result.Add([PSCustomObject]@{
                RelativePath = $src.RelativePath
                SourcePath   = $src.FullPath
                TargetPath   = $targetPath
                Status       = "OK"
            })
        }
        else {
            $result.Add([PSCustomObject]@{
                RelativePath = $src.RelativePath
                SourcePath   = $src.FullPath
                TargetPath   = $targetPath
                Status       = "DIFF"
            })
        }
    }

    return $result
}

# --- Actions ---

function Invoke-Check {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$InfraRoot,
        [Parameter(Mandatory = $true)][object[]]$SourceFiles
    )

    $repos = Get-GitRepos -RootPath $RootPath -InfraRoot $InfraRoot
    $totalOk = 0
    $totalMiss = 0
    $totalDiff = 0
    $repoCount = 0

    foreach ($repo in $repos) {
        $repoName = Split-Path $repo -Leaf
        $status = Get-RepoStatus -RepoPath $repo -SourceFiles $SourceFiles
        $repoCount++

        $ok = ($status | Where-Object { $_.Status -eq "OK" }).Count
        $miss = ($status | Where-Object { $_.Status -eq "MISS" }).Count
        $diff = ($status | Where-Object { $_.Status -eq "DIFF" }).Count

        $totalOk += $ok
        $totalMiss += $miss
        $totalDiff += $diff

        if ($miss -gt 0 -or $diff -gt 0) {
            Write-Host ""
            Write-Host "[$repoName] MISS=$miss DIFF=$diff OK=$ok" -ForegroundColor Yellow
            foreach ($row in $status | Where-Object { $_.Status -ne "OK" }) {
                Write-Host "  $($row.Status)  $($row.RelativePath)" -ForegroundColor $(if ($row.Status -eq "MISS") { "Red" } else { "Cyan" })
            }
        }
        else {
            Write-Host "[$repoName] OK=$ok" -ForegroundColor Green
        }
    }

    Write-Host ""
    Write-Host "----"
    Write-Host "REPOS=$repoCount FILES_OK=$totalOk FILES_MISS=$totalMiss FILES_DIFF=$totalDiff"

    if ($totalMiss -gt 0 -or $totalDiff -gt 0) {
        return 1
    }

    return 0
}

function Invoke-Sync {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$InfraRoot,
        [Parameter(Mandatory = $true)][object[]]$SourceFiles
    )

    $repos = Get-GitRepos -RootPath $RootPath -InfraRoot $InfraRoot
    $totalSynced = 0
    $reposSynced = 0

    foreach ($repo in $repos) {
        $repoName = Split-Path $repo -Leaf
        $status = Get-RepoStatus -RepoPath $repo -SourceFiles $SourceFiles
        $toSync = $status | Where-Object { $_.Status -in @("MISS", "DIFF") }

        if ($toSync.Count -eq 0) {
            continue
        }

        Write-Host ""
        Write-Host "[$repoName] syncing $($toSync.Count) file(s)..." -ForegroundColor Cyan

        foreach ($row in $toSync) {
            $targetDir = Split-Path $row.TargetPath -Parent
            if (-not (Test-Path $targetDir)) {
                New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
            }

            Copy-Item -Path $row.SourcePath -Destination $row.TargetPath -Force
            Write-Host "  SYNC  $($row.RelativePath)"
            $totalSynced++
        }

        $reposSynced++
    }

    Write-Host ""
    Write-Host "----"
    Write-Host "REPOS_SYNCED=$reposSynced FILES_SYNCED=$totalSynced"
}

function Invoke-Commit {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$InfraRoot,
        [Parameter(Mandatory = $true)][string]$Message,
        [Parameter(Mandatory = $true)][bool]$SkipVerify
    )

    $repos = Get-GitRepos -RootPath $RootPath -InfraRoot $InfraRoot
    $committed = 0

    # Files to stage (relative paths from community-health source)
    $filesToStage = @(
        "LICENSE",
        "CODE_OF_CONDUCT.md",
        "CODE_OF_CONDUCT.ru.md",
        "CONTRIBUTING.md",
        "CONTRIBUTING.ru.md",
        "SECURITY.md",
        "SECURITY.ru.md",
        ".github/ISSUE_TEMPLATE/bug_report.yml",
        ".github/ISSUE_TEMPLATE/feature_request.yml",
        ".github/ISSUE_TEMPLATE/config.yml",
        ".github/PULL_REQUEST_TEMPLATE.md"
    )

    foreach ($repo in $repos) {
        $repoName = Split-Path $repo -Leaf

        # Check if any community health files have changes
        $statusLines = @(git -C $repo status --porcelain)
        $hasChanges = $false
        foreach ($file in $filesToStage) {
            $matching = $statusLines | Where-Object { $_ -match [regex]::Escape($file) }
            if ($matching) {
                $hasChanges = $true
                break
            }
        }

        if (-not $hasChanges) {
            continue
        }

        Write-Host "COMMIT [$repoName]" -ForegroundColor Cyan

        # Stage only community health files
        foreach ($file in $filesToStage) {
            $filePath = Join-Path $repo $file
            if (Test-Path $filePath) {
                git -C $repo add $file 2>$null
            }
        }

        # Verify there are staged changes
        git -C $repo diff --cached --quiet
        if ($LASTEXITCODE -eq 0) {
            Write-Host "  SKIP  no staged changes" -ForegroundColor Yellow
            continue
        }

        if ($SkipVerify) {
            git -C $repo commit --no-verify -m $Message | Out-Null
        }
        else {
            git -C $repo commit -m $Message | Out-Null
        }

        $committed++
    }

    Write-Host ""
    Write-Host "----"
    Write-Host "COMMITTED=$committed"
}

function Invoke-Push {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$InfraRoot
    )

    $repos = Get-GitRepos -RootPath $RootPath -InfraRoot $InfraRoot
    $success = 0
    $failed = 0

    foreach ($repo in $repos) {
        $repoName = Split-Path $repo -Leaf

        $hasUpstream = $true
        git -C $repo rev-parse --abbrev-ref --symbolic-full-name "@{u}" *> $null
        if ($LASTEXITCODE -ne 0) {
            $hasUpstream = $false
        }

        if ($hasUpstream) {
            $ahead = [int](git -C $repo rev-list --count "@{u}..HEAD")
            if ($ahead -le 0) {
                continue
            }

            Write-Host "PUSH   [$repoName]" -ForegroundColor Cyan
            git -C $repo push
            if ($LASTEXITCODE -eq 0) {
                $success++
            }
            else {
                Write-Host "  FAIL" -ForegroundColor Red
                $failed++
            }
            continue
        }

        $branch = (git -C $repo rev-parse --abbrev-ref HEAD).Trim()
        $remote = (git -C $repo remote | Select-Object -First 1).Trim()
        if ([string]::IsNullOrWhiteSpace($remote)) {
            continue
        }

        Write-Host "PUSH   [$repoName] (set-upstream)" -ForegroundColor Cyan
        git -C $repo push -u $remote $branch
        if ($LASTEXITCODE -eq 0) {
            $success++
        }
        else {
            Write-Host "  FAIL" -ForegroundColor Red
            $failed++
        }
    }

    Write-Host ""
    Write-Host "----"
    Write-Host "PUSH_SUCCESS=$success PUSH_FAILED=$failed"

    if ($failed -gt 0) {
        return 1
    }

    return 0
}

# --- Main ---

$infraRoot = Get-InfraRoot
$root = Get-MonorepoRoot
$sourceDir = Get-SourceDir

# Handle -DryRun override
if ($DryRun) {
    $Mode = "check"
}

# Validate parameter combination
if (-not $All -and -not $ServicePath -and $Mode -ne "check") {
    # Default: if neither -All nor -ServicePath specified, behave as -All
    $All = $true
}

Write-Host "Community Health Sync"
Write-Host "  Monorepo root: $root"
Write-Host "  Source dir:    $sourceDir"
Write-Host "  Mode:          $Mode"
if ($ServicePath) {
    Write-Host "  Target:        $ServicePath"
}
elseif ($All) {
    Write-Host "  Target:        all repositories"
}
if ($DryRun) {
    Write-Host ""
    Write-Host "  DRY RUN - No changes will be made" -ForegroundColor Yellow
}
Write-Host ""

# Verify source directory exists
if (-not (Test-Path $sourceDir)) {
    Write-Error "Source directory not found: $sourceDir"
    exit 1
}

$sourceFiles = Get-SourceFiles -SourceDir $sourceDir
Write-Host "Source files: $($sourceFiles.Count)"
foreach ($f in $sourceFiles) {
    Write-Host "  $($f.RelativePath)"
}
Write-Host ""

# When -ServicePath is provided, override Get-GitRepos to return only that repo
if ($ServicePath) {
    $targetPath = Join-Path $root $ServicePath
    if (-not (Test-Path $targetPath)) {
        Write-Error "Service path not found: $targetPath"
        exit 1
    }
    $gitDir = Join-Path $targetPath ".git"
    if (-not (Test-Path $gitDir)) {
        Write-Error "Not a git repository: $targetPath"
        exit 1
    }

    # Override the Get-GitRepos function to return only the target repo
    function Get-GitRepos {
        param(
            [Parameter(Mandatory = $true)][string]$RootPath,
            [Parameter(Mandatory = $false)][string]$InfraRoot
        )
        return @($targetPath)
    }
}

switch ($Mode) {
    "check" {
        exit (Invoke-Check -RootPath $root -InfraRoot $infraRoot -SourceFiles $sourceFiles)
    }
    "sync" {
        Invoke-Sync -RootPath $root -InfraRoot $infraRoot -SourceFiles $sourceFiles
        exit 0
    }
    "commit" {
        Invoke-Commit -RootPath $root -InfraRoot $infraRoot -Message $CommitMessage -SkipVerify:$NoVerify.IsPresent
        exit 0
    }
    "push" {
        exit (Invoke-Push -RootPath $root -InfraRoot $infraRoot)
    }
    "all" {
        Invoke-Sync -RootPath $root -InfraRoot $infraRoot -SourceFiles $sourceFiles
        Invoke-Commit -RootPath $root -InfraRoot $infraRoot -Message $CommitMessage -SkipVerify:$NoVerify.IsPresent
        $checkCode = Invoke-Check -RootPath $root -InfraRoot $infraRoot -SourceFiles $sourceFiles
        if ($checkCode -ne 0) {
            Write-Error "sync+commit completed, but check still reports MISS/DIFF."
        }
        exit (Invoke-Push -RootPath $root -InfraRoot $infraRoot)
    }
}
