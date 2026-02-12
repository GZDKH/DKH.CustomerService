<#
.SYNOPSIS
    .NET CLI operations module
.DESCRIPTION
    Provides wrapper functions for dotnet CLI commands:
    Clean, Restore, Build, Pack, Test, Format
#>

function Invoke-DotNetClean {
    <#
    .SYNOPSIS
        Clean build artifacts
    .PARAMETER ProjectPath
        Path to .csproj or .sln file
    .PARAMETER Configuration
        Build configuration (Debug/Release)
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectPath = ".",

        [Parameter(Mandatory=$false)]
        [string]$Configuration = "Release"
    )

    Write-Host "→ Cleaning $ProjectPath..." -ForegroundColor Gray

    $output = dotnet clean $ProjectPath -c $Configuration --verbosity quiet 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet clean failed with exit code $LASTEXITCODE`n$output"
    }

    Write-Host "  ✓ Clean completed" -ForegroundColor Green
}

function Invoke-DotNetRestore {
    <#
    .SYNOPSIS
        Restore NuGet packages
    .PARAMETER ProjectPath
        Path to .csproj or .sln file
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectPath = "."
    )

    Write-Host "→ Restoring packages for $ProjectPath..." -ForegroundColor Gray

    $output = dotnet restore $ProjectPath --verbosity quiet 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet restore failed with exit code $LASTEXITCODE`n$output"
    }

    Write-Host "  ✓ Restore completed" -ForegroundColor Green
}

function Invoke-DotNetBuild {
    <#
    .SYNOPSIS
        Build project
    .PARAMETER ProjectPath
        Path to .csproj or .sln file
    .PARAMETER Configuration
        Build configuration (Debug/Release)
    .PARAMETER NoRestore
        Skip restore during build
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectPath = ".",

        [Parameter(Mandatory=$false)]
        [string]$Configuration = "Release",

        [Parameter(Mandatory=$false)]
        [switch]$NoRestore
    )

    Write-Host "→ Building $ProjectPath ($Configuration)..." -ForegroundColor Gray

    $args = @("build", $ProjectPath, "-c", $Configuration, "--verbosity", "quiet")

    if ($NoRestore) {
        $args += "--no-restore"
    }

    $output = & dotnet $args 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet build failed with exit code $LASTEXITCODE`n$output"
    }

    Write-Host "  ✓ Build completed" -ForegroundColor Green
}

function Invoke-DotNetPack {
    <#
    .SYNOPSIS
        Create NuGet package
    .PARAMETER ProjectPath
        Path to .csproj file
    .PARAMETER OutputDir
        Output directory for .nupkg
    .PARAMETER Version
        Package version
    .PARAMETER Configuration
        Build configuration
    #>
    param(
        [Parameter(Mandatory=$true)]
        [string]$ProjectPath,

        [Parameter(Mandatory=$true)]
        [string]$OutputDir,

        [Parameter(Mandatory=$true)]
        [string]$Version,

        [Parameter(Mandatory=$false)]
        [string]$Configuration = "Release"
    )

    Write-Host "→ Packing $ProjectPath (v$Version)..." -ForegroundColor Gray

    # Ensure output directory exists
    if (-not (Test-Path $OutputDir)) {
        New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
    }

    $output = dotnet pack $ProjectPath `
        -c $Configuration `
        --no-build `
        --output $OutputDir `
        /p:PackageVersion=$Version `
        --verbosity quiet 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet pack failed with exit code $LASTEXITCODE`n$output"
    }

    Write-Host "  ✓ Pack completed: $OutputDir" -ForegroundColor Green
}

function Invoke-DotNetTest {
    <#
    .SYNOPSIS
        Run unit tests
    .PARAMETER ProjectPath
        Path to test .csproj or .sln
    .PARAMETER Configuration
        Build configuration
    .PARAMETER NoBuild
        Skip build before testing
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectPath = ".",

        [Parameter(Mandatory=$false)]
        [string]$Configuration = "Release",

        [Parameter(Mandatory=$false)]
        [switch]$NoBuild
    )

    Write-Host "→ Running tests in $ProjectPath..." -ForegroundColor Gray

    $args = @("test", $ProjectPath, "-c", $Configuration, "--verbosity", "quiet")

    if ($NoBuild) {
        $args += "--no-build"
    }

    $output = & dotnet $args 2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Error "dotnet test failed with exit code $LASTEXITCODE`n$output"
    }

    Write-Host "  ✓ Tests passed" -ForegroundColor Green
}

function Invoke-DotNetFormat {
    <#
    .SYNOPSIS
        Format code with dotnet format
    .PARAMETER ProjectPath
        Path to .csproj or .sln
    .PARAMETER Verify
        Check formatting without applying changes
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectPath = ".",

        [Parameter(Mandatory=$false)]
        [switch]$Verify
    )

    Write-Host "→ Formatting $ProjectPath..." -ForegroundColor Gray

    $args = @("format", $ProjectPath, "--verbosity", "quiet")

    if ($Verify) {
        $args += "--verify-no-changes"
    }

    $output = & dotnet $args 2>&1

    if ($LASTEXITCODE -ne 0) {
        if ($Verify) {
            Write-Error "Code formatting check failed. Run 'dotnet format' to fix."
        } else {
            Write-Error "dotnet format failed with exit code $LASTEXITCODE`n$output"
        }
    }

    if ($Verify) {
        Write-Host "  ✓ Format check passed" -ForegroundColor Green
    } else {
        Write-Host "  ✓ Format completed" -ForegroundColor Green
    }
}

Export-ModuleMember -Function @(
    'Invoke-DotNetClean',
    'Invoke-DotNetRestore',
    'Invoke-DotNetBuild',
    'Invoke-DotNetPack',
    'Invoke-DotNetTest',
    'Invoke-DotNetFormat'
)
