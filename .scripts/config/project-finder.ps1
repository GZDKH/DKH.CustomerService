# Project Root Finder Module
# Module for automatically finding project root directory

function Find-ProjectRoot
{
    param([string]$StartPath = $PSScriptRoot)

    $currentPath = $StartPath
    $maxDepth = 10  # Prevent infinite loops
    $depth = 0

    while ($depth -lt $maxDepth)
    {
        # Check for project markers in order of priority
        $markers = @(
            "*.sln", # Solution files (highest priority)
            "Directory.Build.props", # MSBuild directory props
            "global.json", # .NET global.json
            ".git", # Git repository
            ".scripts", # Our scripts folder
            "docker-compose.yml"        # Docker compose
        )

        foreach ($marker in $markers)
        {
            $found = Get-ChildItem -Path $currentPath -Name $marker -ErrorAction SilentlyContinue
            if ($found)
            {
                Write-Host "Found project root at: $currentPath" -ForegroundColor Green
                Write-Host "  Detected by marker: $( $found[0] )" -ForegroundColor Gray
                return $currentPath
            }
        }

        # Move up one directory
        $parentPath = Split-Path -Parent $currentPath
        if ($parentPath -eq $currentPath)
        {
            # Reached the root of the drive
            break
        }
        $currentPath = $parentPath
        $depth++
    }

    Write-Host "Could not find project root. Markers searched: $( $markers -join ', ' )" -ForegroundColor Red
    Write-Host "   Started search from: $StartPath" -ForegroundColor Gray
    throw "Project root not found"
}

# Function to load project configuration
function Initialize-ProjectConfig
{
    param([string]$StartPath = $PSScriptRoot)

    try
    {
        $projectRoot = Find-ProjectRoot -StartPath $StartPath
        $configPath = Join-Path $projectRoot ".scripts\config.ps1"

        if (-not (Test-Path $configPath))
        {
            throw "Configuration file not found at: $configPath"
        }

        Write-Host "Found configuration at: $configPath" -ForegroundColor Cyan

        # Return both project root path and config path
        return @{
            ProjectRoot = $projectRoot
            ConfigPath = $configPath
        }
    }
    catch
    {
        Write-Host "Failed to initialize project configuration: $_" -ForegroundColor Red
        throw
    }
}
