#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Configure Docker daemon DNS servers
.DESCRIPTION
    Adds custom DNS servers (Google 8.8.8.8 + Cloudflare 1.1.1.1) to Docker Desktop
    daemon.json. This fixes DNS resolution failures during docker build (dotnet restore,
    npm install, etc.) caused by ISP/router DNS issues.

    Requires Docker Desktop restart to take effect.
.PARAMETER DnsPrimary
    Primary DNS server (default: 8.8.8.8)
.PARAMETER DnsSecondary
    Secondary DNS server (default: 1.1.1.1)
.PARAMETER Remove
    Remove custom DNS configuration from daemon.json
.PARAMETER Force
    Skip confirmation prompt
.EXAMPLE
    # Add default DNS servers (Google + Cloudflare)
    pwsh -File scripts/setup/configure-docker-dns.ps1

    # Use custom DNS servers
    pwsh -File scripts/setup/configure-docker-dns.ps1 -DnsPrimary 9.9.9.9 -DnsSecondary 149.112.112.112

    # Remove custom DNS
    pwsh -File scripts/setup/configure-docker-dns.ps1 -Remove
#>

[CmdletBinding()]
param(
    [string]$DnsPrimary = "8.8.8.8",
    [string]$DnsSecondary = "1.1.1.1",
    [switch]$Remove,
    [switch]$Force
)

$ErrorActionPreference = "Stop"

# Determine daemon.json path based on OS
if ($IsWindows -or $env:OS -eq "Windows_NT") {
    $daemonJsonPath = Join-Path $env:USERPROFILE ".docker" "daemon.json"
} else {
    $daemonJsonPath = Join-Path $env:HOME ".docker" "daemon.json"
}

Write-Host "`n[i] Docker DNS Configuration" -ForegroundColor Cyan
Write-Host "============================`n" -ForegroundColor Cyan
Write-Host "  daemon.json: $daemonJsonPath" -ForegroundColor Gray

# Read existing config or create empty object
$config = @{}
if (Test-Path $daemonJsonPath) {
    try {
        $content = Get-Content $daemonJsonPath -Raw
        if ($content -and $content.Trim()) {
            $config = $content | ConvertFrom-Json -AsHashtable
        }
    } catch {
        Write-Host "  [!] Failed to parse existing daemon.json: $_" -ForegroundColor Yellow
        Write-Host "  [i] Will create new configuration" -ForegroundColor Gray
        $config = @{}
    }
} else {
    Write-Host "  [i] daemon.json not found, will create" -ForegroundColor Gray
}

# Show current DNS
if ($config.ContainsKey("dns")) {
    Write-Host "  Current DNS: $($config["dns"] -join ", ")" -ForegroundColor Yellow
} else {
    Write-Host "  Current DNS: (not configured — using Docker default)" -ForegroundColor Gray
}

if ($Remove) {
    # Remove DNS configuration
    if ($config.ContainsKey("dns")) {
        $config.Remove("dns")
        Write-Host "`n  [i] Will remove custom DNS configuration" -ForegroundColor Yellow
    } else {
        Write-Host "`n  [OK] No custom DNS to remove" -ForegroundColor Green
        return
    }
} else {
    # Set DNS configuration
    $dnsServers = @($DnsPrimary, $DnsSecondary)
    Write-Host "`n  New DNS: $($dnsServers -join ", ")" -ForegroundColor Green

    # Check if already configured with same values
    if ($config.ContainsKey("dns")) {
        $existing = $config["dns"]
        if ($existing.Count -eq $dnsServers.Count -and
            $existing[0] -eq $dnsServers[0] -and
            $existing[1] -eq $dnsServers[1]) {
            Write-Host "`n  [OK] DNS already configured with these values" -ForegroundColor Green
            return
        }
    }

    $config["dns"] = $dnsServers
}

# Confirm
if (-not $Force) {
    Write-Host ""
    $confirm = Read-Host "  Apply changes? (y/N)"
    if ($confirm -notin @("y", "Y", "yes")) {
        Write-Host "  Cancelled." -ForegroundColor Gray
        return
    }
}

# Ensure directory exists
$dir = Split-Path $daemonJsonPath -Parent
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}

# Write daemon.json
$json = $config | ConvertTo-Json -Depth 10
Set-Content -Path $daemonJsonPath -Value $json -Encoding UTF8
Write-Host "`n  [OK] daemon.json updated" -ForegroundColor Green

# Show result
Write-Host "`n  Updated config:" -ForegroundColor Gray
Write-Host "  $json" -ForegroundColor Gray

Write-Host "`n  [!] Docker Desktop restart required for changes to take effect." -ForegroundColor Yellow
Write-Host "      Restart via: Docker Desktop tray icon -> Restart" -ForegroundColor Yellow
Write-Host ""
