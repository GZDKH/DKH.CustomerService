<#
.SYNOPSIS
    Git operations module
.DESCRIPTION
    Provides wrapper functions for git commands:
    Tag, Commit, Push
#>

function New-GitTag {
    <#
    .SYNOPSIS
        Create and push annotated git tag
    .PARAMETER Version
        Version for tag (e.g., "1.2.0")
    .PARAMETER Message
        Tag annotation message
    .PARAMETER Push
        Push tag to remote (default: true)
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Version,

        [Parameter(Mandatory=$false)]
        [string]$Message,

        [Parameter(Mandatory=$false)]
        [bool]$Push = $true
    )

    $tag = "v$Version"

    if (-not $Message) {
        $Message = "Release $tag"
    }

    Write-Host "→ Creating tag $tag..." -ForegroundColor Gray

    # Check if tag already exists
    $existingTag = git tag -l $tag 2>$null

    if ($existingTag) {
        Write-Warning "Tag $tag already exists"
        return
    }

    # Create annotated tag
    git tag -a $tag -m $Message

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create git tag $tag"
    }

    Write-Host "  ✓ Tag created: $tag" -ForegroundColor Green

    # Push tag to remote
    if ($Push) {
        Write-Host "→ Pushing tag to remote..." -ForegroundColor Gray

        git push origin $tag

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to push tag $tag to origin"
        }

        Write-Host "  ✓ Tag pushed to origin" -ForegroundColor Green
    }
}

function Invoke-GitCommit {
    <#
    .SYNOPSIS
        Create git commit
    .PARAMETER Message
        Commit message
    .PARAMETER Files
        Files to stage (default: all changes)
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$Message,

        [Parameter(Mandatory=$false)]
        [string[]]$Files = @(".")
    )

    Write-Host "→ Staging files..." -ForegroundColor Gray

    foreach ($file in $Files) {
        git add $file
    }

    Write-Host "→ Creating commit..." -ForegroundColor Gray

    git commit -m $Message

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create commit"
    }

    Write-Host "  ✓ Commit created" -ForegroundColor Green
}

function Invoke-GitPush {
    <#
    .SYNOPSIS
        Push commits to remote
    .PARAMETER Remote
        Remote name (default: origin)
    .PARAMETER Branch
        Branch to push (default: current branch)
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$Remote = "origin",

        [Parameter(Mandatory=$false)]
        [string]$Branch
    )

    Write-Host "→ Pushing to $Remote..." -ForegroundColor Gray

    if ($Branch) {
        git push $Remote $Branch
    } else {
        git push
    }

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to push to $Remote"
    }

    Write-Host "  ✓ Pushed to $Remote" -ForegroundColor Green
}

Export-ModuleMember -Function @(
    'New-GitTag',
    'Invoke-GitCommit',
    'Invoke-GitPush'
)
