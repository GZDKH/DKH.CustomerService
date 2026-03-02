#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Synchronize AGENTS.md files with CLAUDE.md across the GZDKH multi-repo workspace.

.DESCRIPTION
    Supports five modes:
    - check  : report missing/different AGENTS.md files
    - sync   : copy CLAUDE.md -> AGENTS.md where needed
    - commit : commit AGENTS.md changes in each git repository
    - push   : push AGENTS.md commits in each git repository
    - all    : sync + commit + push

.EXAMPLE
    pwsh scripts/tools/sync-agents.ps1 -Mode check

.EXAMPLE
    pwsh scripts/tools/sync-agents.ps1 -Mode sync

.EXAMPLE
    pwsh scripts/tools/sync-agents.ps1 -Mode all
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $false)]
    [ValidateSet("check", "sync", "commit", "push", "all")]
    [string]$Mode = "sync",

    [Parameter(Mandatory = $false)]
    [string]$CommitMessage = "chore(agents): sync AGENTS.md with CLAUDE.md",

    [Parameter(Mandatory = $false)]
    [switch]$NoVerify
)

$ErrorActionPreference = "Stop"

function Get-MonorepoRoot {
    return (Split-Path (Split-Path (Split-Path $PSScriptRoot -Parent) -Parent) -Parent)
}

function Test-FileContentEqual {
    param(
        [Parameter(Mandatory = $true)][string]$LeftPath,
        [Parameter(Mandatory = $true)][string]$RightPath
    )

    $leftHash = (Get-FileHash -Path $LeftPath -Algorithm SHA256).Hash
    $rightHash = (Get-FileHash -Path $RightPath -Algorithm SHA256).Hash
    return $leftHash -eq $rightHash
}

function Get-ClaudeFiles {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    return Get-ChildItem -Path $RootPath -Filter "CLAUDE.md" -File -Recurse |
        Where-Object { $_.FullName -notmatch '[/\\]node_modules[/\\]' } |
        Sort-Object FullName
}

function Get-AgentStatus {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $result = New-Object System.Collections.Generic.List[object]
    $claudeFiles = Get-ClaudeFiles -RootPath $RootPath

    foreach ($claude in $claudeFiles) {
        $projectDir = Split-Path $claude.FullName -Parent
        $agentsPath = Join-Path $projectDir "AGENTS.md"

        if (-not (Test-Path $agentsPath)) {
            $result.Add([PSCustomObject]@{
                ProjectDir = $projectDir
                ClaudePath = $claude.FullName
                AgentsPath = $agentsPath
                Status    = "MISS"
            })
            continue
        }

        if (Test-FileContentEqual -LeftPath $claude.FullName -RightPath $agentsPath) {
            $result.Add([PSCustomObject]@{
                ProjectDir = $projectDir
                ClaudePath = $claude.FullName
                AgentsPath = $agentsPath
                Status    = "OK"
            })
        }
        else {
            $result.Add([PSCustomObject]@{
                ProjectDir = $projectDir
                ClaudePath = $claude.FullName
                AgentsPath = $agentsPath
                Status    = "DIFF"
            })
        }
    }

    return $result
}

function Invoke-Check {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $statusRows = Get-AgentStatus -RootPath $RootPath

    foreach ($row in $statusRows | Where-Object { $_.Status -ne "OK" }) {
        $relativeDir = Resolve-Path -LiteralPath $row.ProjectDir -Relative
        Write-Host ("{0,-5} {1}" -f $row.Status, $relativeDir)
    }

    $total = $statusRows.Count
    $ok = ($statusRows | Where-Object { $_.Status -eq "OK" }).Count
    $miss = ($statusRows | Where-Object { $_.Status -eq "MISS" }).Count
    $diff = ($statusRows | Where-Object { $_.Status -eq "DIFF" }).Count

    Write-Host "----"
    Write-Host "TOTAL=$total OK=$ok MISS=$miss DIFF=$diff"

    if ($miss -gt 0 -or $diff -gt 0) {
        return 1
    }

    return 0
}

function Invoke-Sync {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $statusRows = Get-AgentStatus -RootPath $RootPath
    $toSync = $statusRows | Where-Object { $_.Status -in @("MISS", "DIFF") }

    foreach ($row in $toSync) {
        Copy-Item -Path $row.ClaudePath -Destination $row.AgentsPath -Force
        $relativeAgent = Resolve-Path -LiteralPath $row.AgentsPath -Relative
        Write-Host "SYNC  $relativeAgent"
    }

    Write-Host "----"
    Write-Host "SYNCED=$($toSync.Count)"
}

function Get-GitRepos {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    return Get-ChildItem -Path $RootPath -Directory -Recurse -Force |
        Where-Object { $_.Name -eq ".git" -and $_.FullName -notmatch '[/\\]node_modules[/\\]' } |
        ForEach-Object { Split-Path $_.FullName -Parent } |
        Sort-Object -Unique
}

function Invoke-Commit {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath,
        [Parameter(Mandatory = $true)][string]$Message,
        [Parameter(Mandatory = $true)][bool]$SkipVerify
    )

    $repos = Get-GitRepos -RootPath $RootPath
    $committed = 0

    foreach ($repo in $repos) {
        $statusLines = @(git -C $repo status --porcelain)
        $agentsLines = $statusLines | Where-Object { $_ -match 'AGENTS\.md$' }
        if ($agentsLines.Count -eq 0) {
            continue
        }

        Write-Host "COMMIT $repo"
        git -C $repo add --all -- ":(glob)**/AGENTS.md" | Out-Null

        $hasStaged = $true
        git -C $repo diff --cached --quiet
        if ($LASTEXITCODE -eq 0) {
            $hasStaged = $false
        }

        if (-not $hasStaged) {
            Write-Host "SKIP   no staged AGENTS changes"
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

    Write-Host "----"
    Write-Host "COMMITTED=$committed"
}

function Invoke-Push {
    param(
        [Parameter(Mandatory = $true)][string]$RootPath
    )

    $repos = Get-GitRepos -RootPath $RootPath
    $success = 0
    $failed = 0

    foreach ($repo in $repos) {
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

            Write-Host "PUSH   $repo"
            git -C $repo push
            if ($LASTEXITCODE -eq 0) {
                $success++
            }
            else {
                $failed++
            }
            continue
        }

        $branch = (git -C $repo rev-parse --abbrev-ref HEAD).Trim()
        $remote = (git -C $repo remote | Select-Object -First 1).Trim()
        if ([string]::IsNullOrWhiteSpace($remote)) {
            continue
        }

        Write-Host "PUSH   $repo (set-upstream)"
        git -C $repo push -u $remote $branch
        if ($LASTEXITCODE -eq 0) {
            $success++
        }
        else {
            $failed++
        }
    }

    Write-Host "----"
    Write-Host "PUSH_SUCCESS=$success PUSH_FAILED=$failed"

    if ($failed -gt 0) {
        return 1
    }

    return 0
}

$root = Get-MonorepoRoot
Write-Host "Monorepo root: $root"
Write-Host "Mode: $Mode"
Write-Host ""

switch ($Mode) {
    "check" {
        exit (Invoke-Check -RootPath $root)
    }
    "sync" {
        Invoke-Sync -RootPath $root
        exit 0
    }
    "commit" {
        Invoke-Commit -RootPath $root -Message $CommitMessage -SkipVerify:$NoVerify.IsPresent
        exit 0
    }
    "push" {
        exit (Invoke-Push -RootPath $root)
    }
    "all" {
        Invoke-Sync -RootPath $root
        Invoke-Commit -RootPath $root -Message $CommitMessage -SkipVerify:$NoVerify.IsPresent
        $checkCode = Invoke-Check -RootPath $root
        if ($checkCode -ne 0) {
            Write-Error "sync+commit completed, but check still reports MISS/DIFF."
        }
        exit (Invoke-Push -RootPath $root)
    }
}

