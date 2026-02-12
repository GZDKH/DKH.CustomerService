<#
.SYNOPSIS
    NuGet package publishing module
.DESCRIPTION
    Provides functions to publish NuGet packages to:
    - GitLab Package Registry
    - GitHub Packages
    - NuGet.org
#>

function Publish-ToGitLab {
    <#
    .SYNOPSIS
        Publish NuGet packages to GitLab Package Registry
    .PARAMETER NupkgDir
        Directory containing .nupkg files
    .PARAMETER SourceUrl
        GitLab Package Registry URL (optional, reads from config)
    .PARAMETER Username
        GitLab username (optional, reads from config/env)
    .PARAMETER Token
        GitLab access token (optional, reads from config/env)
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$NupkgDir,

        [Parameter(Mandatory=$false)]
        [string]$SourceUrl,

        [Parameter(Mandatory=$false)]
        [string]$Username,

        [Parameter(Mandatory=$false)]
        [string]$Token
    )

    if (-not (Test-Path $NupkgDir)) {
        Write-Error "NuGet package directory not found: $NupkgDir"
    }

    # Try to load config from .scripts/config/gitlab.conf
    Import-Module "$PSScriptRoot/Project.psm1" -Force
    $projectRoot = Get-ProjectRoot
    $gitlabConfigPath = Join-Path $projectRoot ".scripts/config/gitlab.conf"

    if ((Test-Path $gitlabConfigPath) -and (-not $SourceUrl -or -not $Username -or -not $Token)) {
        Write-Host "→ Loading GitLab config from gitlab.conf..." -ForegroundColor Gray

        $config = @{}
        Get-Content $gitlabConfigPath | Where-Object { $_ -match '^\s*(\w+)="(.+)"' } | ForEach-Object {
            $config[$matches[1]] = $matches[2]
        }

        if (-not $SourceUrl) { $SourceUrl = $config['SourceUrl'] }
        if (-not $Username) { $Username = $config['Username'] }
        if (-not $Token) { $Token = $config['Token'] }
    }

    # Fallback to environment variables
    if (-not $SourceUrl) { $SourceUrl = $env:GITLAB_NUGET_SOURCE }
    if (-not $Username) { $Username = $env:GITLAB_NUGET_USERNAME }
    if (-not $Token) { $Token = $env:GITLAB_NUGET_TOKEN }

    # Validate credentials
    if (-not $SourceUrl) {
        Write-Error "GitLab source URL not provided. Set in gitlab.conf or GITLAB_NUGET_SOURCE env var."
    }
    if (-not $Username) {
        Write-Error "GitLab username not provided. Set in gitlab.conf or GITLAB_NUGET_USERNAME env var."
    }
    if (-not $Token) {
        Write-Error "GitLab token not provided. Set in gitlab.conf or GITLAB_NUGET_TOKEN env var."
    }

    # Find .nupkg files
    $packages = Get-ChildItem -Path $NupkgDir -Filter "*.nupkg" -File

    if ($packages.Count -eq 0) {
        Write-Error "No .nupkg files found in $NupkgDir"
    }

    Write-Host "→ Publishing $($packages.Count) package(s) to GitLab..." -ForegroundColor Gray

    foreach ($package in $packages) {
        Write-Host "  → $($package.Name)" -ForegroundColor Gray

        $output = dotnet nuget push $package.FullName `
            --source $SourceUrl `
            --api-key "${Username}:${Token}" `
            --skip-duplicate `
            2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish $($package.Name) to GitLab`n$output"
        }
    }

    Write-Host "  ✓ Published to GitLab Package Registry" -ForegroundColor Green
}

function Publish-ToGitHub {
    <#
    .SYNOPSIS
        Publish NuGet packages to GitHub Packages
    .PARAMETER NupkgDir
        Directory containing .nupkg files
    .PARAMETER SourceUrl
        GitHub Packages URL (optional, reads from env)
    .PARAMETER Token
        GitHub PAT (optional, reads from env)
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$NupkgDir,

        [Parameter(Mandatory=$false)]
        [string]$SourceUrl,

        [Parameter(Mandatory=$false)]
        [string]$Token
    )

    if (-not (Test-Path $NupkgDir)) {
        Write-Error "NuGet package directory not found: $NupkgDir"
    }

    # Fallback to environment variables
    if (-not $SourceUrl) { $SourceUrl = $env:GITHUB_NUGET_SOURCE }
    if (-not $Token) { $Token = $env:GITHUB_NUGET_TOKEN }

    # Validate credentials
    if (-not $SourceUrl) {
        Write-Error "GitHub source URL not provided. Set GITHUB_NUGET_SOURCE env var."
    }
    if (-not $Token) {
        Write-Error "GitHub token not provided. Set GITHUB_NUGET_TOKEN env var."
    }

    # Find .nupkg files
    $packages = Get-ChildItem -Path $NupkgDir -Filter "*.nupkg" -File

    if ($packages.Count -eq 0) {
        Write-Error "No .nupkg files found in $NupkgDir"
    }

    Write-Host "→ Publishing $($packages.Count) package(s) to GitHub..." -ForegroundColor Gray

    foreach ($package in $packages) {
        Write-Host "  → $($package.Name)" -ForegroundColor Gray

        $output = dotnet nuget push $package.FullName `
            --source $SourceUrl `
            --api-key $Token `
            --skip-duplicate `
            2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish $($package.Name) to GitHub`n$output"
        }
    }

    Write-Host "  ✓ Published to GitHub Packages" -ForegroundColor Green
}

function Publish-ToNuGet {
    <#
    .SYNOPSIS
        Publish NuGet packages to NuGet.org
    .PARAMETER NupkgDir
        Directory containing .nupkg files
    .PARAMETER ApiKey
        NuGet.org API key (optional, reads from env)
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$NupkgDir,

        [Parameter(Mandatory=$false)]
        [string]$ApiKey
    )

    if (-not (Test-Path $NupkgDir)) {
        Write-Error "NuGet package directory not found: $NupkgDir"
    }

    # Fallback to environment variable
    if (-not $ApiKey) { $ApiKey = $env:NUGET_API_KEY }

    # Validate credentials
    if (-not $ApiKey) {
        Write-Error "NuGet.org API key not provided. Set NUGET_API_KEY env var."
    }

    # Find .nupkg files
    $packages = Get-ChildItem -Path $NupkgDir -Filter "*.nupkg" -File

    if ($packages.Count -eq 0) {
        Write-Error "No .nupkg files found in $NupkgDir"
    }

    Write-Host "→ Publishing $($packages.Count) package(s) to NuGet.org..." -ForegroundColor Gray

    foreach ($package in $packages) {
        Write-Host "  → $($package.Name)" -ForegroundColor Gray

        $output = dotnet nuget push $package.FullName `
            --source https://api.nuget.org/v3/index.json `
            --api-key $ApiKey `
            --skip-duplicate `
            2>&1

        if ($LASTEXITCODE -ne 0) {
            Write-Error "Failed to publish $($package.Name) to NuGet.org`n$output"
        }
    }

    Write-Host "  ✓ Published to NuGet.org" -ForegroundColor Green
}

Export-ModuleMember -Function @(
    'Publish-ToGitLab',
    'Publish-ToGitHub',
    'Publish-ToNuGet'
)
