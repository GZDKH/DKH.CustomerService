#!/usr/bin/env pwsh

<#
.SYNOPSIS
    Create and push git tag
.DESCRIPTION
    Creates an annotated git tag and pushes to remote
.PARAMETER Version
    Version for the tag (will be prefixed with 'v')
.PARAMETER Message
    Tag annotation message (optional)
.PARAMETER Push
    Push tag to remote (default: true)
.EXAMPLE
    ./.scripts/release/tag.ps1 -Version "1.2.0"
#>

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,

    [Parameter(Mandatory=$false)]
    [string]$Message,

    [Parameter(Mandatory=$false)]
    [bool]$Push = $true
)

$ErrorActionPreference = "Stop"

$tag = "v$Version"

if (-not $Message) {
    $Message = "Release $tag"
}

Write-Host "🏷️  Creating git tag..." -ForegroundColor Cyan
Write-Host "  → Tag: $tag" -ForegroundColor Gray
Write-Host "  → Message: $Message" -ForegroundColor Gray

# Check if tag already exists
$existingTag = git tag -l $tag 2>$null

if ($existingTag) {
    Write-Host "  ⚠ Tag $tag already exists" -ForegroundColor Yellow
    Write-Host ""
    return
}

# Create tag
git tag -a $tag -m $Message

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create tag $tag"
}

Write-Host "  ✓ Tag created locally" -ForegroundColor Green

if ($Push) {
    Write-Host "  → Pushing to remote..." -ForegroundColor Gray

    git push origin $tag

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to push tag $tag"
    }

    Write-Host "  ✓ Tag pushed to remote" -ForegroundColor Green
}

Write-Host ""
Write-Host "✓ Tag $tag created successfully" -ForegroundColor Green
