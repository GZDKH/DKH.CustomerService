<#
.SYNOPSIS
    Project structure and path resolution helpers
.DESCRIPTION
    Provides functions to navigate project structure, identify project types,
    and resolve common paths (Contracts, solution, etc.)
#>

function Get-ProjectRoot {
    <#
    .SYNOPSIS
        Get the root directory of the current project
    .DESCRIPTION
        Resolves project root from .scripts/ location or explicit path
    .PARAMETER ScriptsRoot
        Path to .scripts/ directory (default: $PSScriptRoot/../)
    .EXAMPLE
        Get-ProjectRoot
        # Returns /Users/.../DKH.CartService
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ScriptsRoot = (Split-Path $PSScriptRoot -Parent)
    )

    # .scripts/ is child of project root
    $projectRoot = Split-Path $ScriptsRoot -Parent

    if (-not (Test-Path $projectRoot)) {
        throw "Project root not found: $projectRoot"
    }

    return $projectRoot
}

function Get-ServiceName {
    <#
    .SYNOPSIS
        Get service name from project root
    .EXAMPLE
        Get-ServiceName
        # Returns "DKH.CartService"
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectRoot = (Get-ProjectRoot)
    )

    return Split-Path $ProjectRoot -Leaf
}

function Get-ContractsProject {
    <#
    .SYNOPSIS
        Get path to Contracts project (.csproj)
    .DESCRIPTION
        Returns path to {ServiceName}.Contracts/{ServiceName}.Contracts.csproj
    .PARAMETER ProjectRoot
        Project root directory
    .EXAMPLE
        Get-ContractsProject
        # Returns DKH.CartService.Contracts/DKH.CartService.Contracts.csproj
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectRoot = (Get-ProjectRoot)
    )

    $serviceName = Get-ServiceName -ProjectRoot $ProjectRoot
    $contractsProject = "$serviceName.Contracts"
    $contractsPath = "$contractsProject/$contractsProject.csproj"
    $fullPath = Join-Path $ProjectRoot $contractsPath

    if (-not (Test-Path $fullPath)) {
        throw "Contracts project not found: $fullPath"
    }

    return $contractsPath
}

function Get-ProjectCategory {
    <#
    .SYNOPSIS
        Determine project category (service, gateway, library, worker)
    .DESCRIPTION
        Reads projects.json from Infrastructure and determines category
    .PARAMETER ProjectRoot
        Project root directory
    .EXAMPLE
        Get-ProjectCategory
        # Returns "services"
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectRoot = (Get-ProjectRoot)
    )

    # Find Infrastructure scripts
    $monoRepoRoot = Split-Path (Split-Path $ProjectRoot -Parent) -Parent
    $projectsJson = Join-Path $monoRepoRoot "DKH.Infrastructure/scripts/config/projects.json"

    if (-not (Test-Path $projectsJson)) {
        Write-Warning "projects.json not found at $projectsJson"
        return "unknown"
    }

    $config = Get-Content $projectsJson | ConvertFrom-Json
    $relativePath = $ProjectRoot -replace [regex]::Escape("$monoRepoRoot/"), ""

    foreach ($category in $config.categories.PSObject.Properties) {
        if ($category.Value.projects -contains $relativePath) {
            return $category.Name
        }
    }

    return "unknown"
}

function Test-HasContracts {
    <#
    .SYNOPSIS
        Check if project has Contracts package
    .EXAMPLE
        Test-HasContracts
        # Returns $true or $false
    #>
    param(
        [Parameter(Mandatory=$false)]
        [string]$ProjectRoot = (Get-ProjectRoot)
    )

    try {
        Get-ContractsProject -ProjectRoot $ProjectRoot | Out-Null
        return $true
    }
    catch {
        return $false
    }
}

Export-ModuleMember -Function @(
    'Get-ProjectRoot',
    'Get-ServiceName',
    'Get-ContractsProject',
    'Get-ProjectCategory',
    'Test-HasContracts'
)
