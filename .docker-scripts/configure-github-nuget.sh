#!/bin/bash
set -e

# Configure GitHub Packages authentication for NuGet
# Usage: ./configure-github-nuget.sh [--user <username>] [--token <token>] [--config <path>]
#
# Options:
#   --user <username>    GitHub username
#   --token <token>      GitHub PAT (scope: read:packages)
#   --config <path>      Path to nuget.config (default: nuget.config)
#
# Behavior:
#   1. Use --user/--token if provided
#   2. If empty, try to read from global ~/.nuget/NuGet/NuGet.Config
#   3. Exit 0 (success) if no credentials found (non-fatal)
#
# Example:
#   ./configure-github-nuget.sh --user "myuser" --token "ghp_xxx" --config nuget.config

GITHUB_USER=""
GITHUB_TOKEN=""
NUGET_CONFIG="nuget.config"

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --user)
      GITHUB_USER="$2"
      shift 2
      ;;
    --token)
      GITHUB_TOKEN="$2"
      shift 2
      ;;
    --config)
      NUGET_CONFIG="$2"
      shift 2
      ;;
    *)
      echo "Unknown option: $1" >&2
      exit 1
      ;;
  esac
done

# Function to get credentials from global nuget.config
get_global_credentials() {
    local config_file=""

    # Determine global config path
    if [ -n "$HOME" ]; then
        if [ -f "$HOME/.nuget/NuGet/NuGet.Config" ]; then
            config_file="$HOME/.nuget/NuGet/NuGet.Config"
        elif [ -f "$HOME/.config/NuGet/NuGet.Config" ]; then
            config_file="$HOME/.config/NuGet/NuGet.Config"
        fi
    fi

    if [ -z "$config_file" ] || [ ! -f "$config_file" ]; then
        return 1
    fi

    # Extract username and password for github-dotnet-gzdkh source
    local username=$(grep -A 10 "<github-dotnet-gzdkh>" "$config_file" 2>/dev/null | \
        grep -o 'Username="[^"]*"' | sed 's/Username="\(.*\)"/\1/' | head -1)
    local password=$(grep -A 10 "<github-dotnet-gzdkh>" "$config_file" 2>/dev/null | \
        grep -o 'ClearTextPassword="[^"]*"' | sed 's/ClearTextPassword="\(.*\)"/\1/' | head -1)

    if [ -n "$username" ] && [ -n "$password" ]; then
        GITHUB_USER="$username"
        GITHUB_TOKEN="$password"
        return 0
    fi

    return 1
}

# Try to get credentials from global config if not provided
if [ -z "$GITHUB_USER" ] || [ -z "$GITHUB_TOKEN" ]; then
    get_global_credentials 2>/dev/null || true
fi

# Exit successfully if no credentials (non-fatal)
if [ -z "$GITHUB_USER" ] || [ -z "$GITHUB_TOKEN" ]; then
    exit 0
fi

# Check if config file exists
if [ ! -f "$NUGET_CONFIG" ]; then
    echo "Error: $NUGET_CONFIG not found" >&2
    exit 1
fi

# Configure GitHub Packages
dotnet nuget update source github-dotnet-gzdkh \
    --username "$GITHUB_USER" \
    --password "$GITHUB_TOKEN" \
    --store-password-in-clear-text \
    --configfile "$NUGET_CONFIG" 2>/dev/null || \
dotnet nuget add source https://nuget.pkg.github.com/GZDKH/index.json \
    --name github-dotnet-gzdkh \
    --username "$GITHUB_USER" \
    --password "$GITHUB_TOKEN" \
    --store-password-in-clear-text \
    --configfile "$NUGET_CONFIG" 2>/dev/null

exit 0
