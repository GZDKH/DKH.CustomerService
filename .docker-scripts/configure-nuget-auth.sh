#!/bin/bash
set -e

# Configure NuGet authentication for private package sources
# Usage: ./configure-nuget-auth.sh [options]
#
# Options:
#   --github-user <username>    GitHub username
#   --github-token <token>      GitHub PAT
#   --gitlab-user <username>    GitLab username
#   --gitlab-token <token>      GitLab PAT
#   --config <path>             Path to nuget.config (default: nuget.config)
#   --use-global                Try to use credentials from global nuget.config
#
# Behavior:
#   - If credentials are provided via options, use them
#   - If credentials are empty and --use-global (default), try global nuget.config
#   - Falls back gracefully if global config not found
#
# Example:
#   ./configure-nuget-auth.sh \
#     --github-user myuser --github-token ghp_xxx \
#     --gitlab-user myuser --gitlab-token glpat-xxx

GITHUB_USER=""
GITHUB_TOKEN=""
GITLAB_USER=""
GITLAB_TOKEN=""
NUGET_CONFIG="nuget.config"
USE_GLOBAL=true

# Function to get credentials from global nuget.config
get_global_credentials() {
    local source_name=$1
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

    # Extract username and password using grep and sed
    local username=$(grep -A 10 "<$source_name>" "$config_file" 2>/dev/null | grep -o 'Username="[^"]*"' | sed 's/Username="\(.*\)"/\1/')
    local password=$(grep -A 10 "<$source_name>" "$config_file" 2>/dev/null | grep -o 'ClearTextPassword="[^"]*"' | sed 's/ClearTextPassword="\(.*\)"/\1/')

    if [ -n "$username" ] && [ -n "$password" ]; then
        echo "$username:$password"
        return 0
    fi

    return 1
}

GITHUB_USER=""
GITHUB_TOKEN=""
GITLAB_USER=""
GITLAB_TOKEN=""
NUGET_CONFIG="nuget.config"
USE_GLOBAL=true

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --github-user)
      GITHUB_USER="$2"
      shift 2
      ;;
    --github-token)
      GITHUB_TOKEN="$2"
      shift 2
      ;;
    --gitlab-user)
      GITLAB_USER="$2"
      shift 2
      ;;
    --gitlab-token)
      GITLAB_TOKEN="$2"
      shift 2
      ;;
    --config)
      NUGET_CONFIG="$2"
      shift 2
      ;;
    --use-global)
      USE_GLOBAL=true
      shift
      ;;
    --no-global)
      USE_GLOBAL=false
      shift
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

# Check if config file exists
if [ ! -f "$NUGET_CONFIG" ]; then
  echo "Error: nuget.config not found at $NUGET_CONFIG"
  exit 1
fi

echo "Configuring NuGet authentication..."

# Try to get credentials from global config if not provided and --use-global is set
if [ "$USE_GLOBAL" = true ]; then
  # GitHub credentials
  if [ -z "$GITHUB_USER" ] || [ -z "$GITHUB_TOKEN" ]; then
    echo "  → Checking global config for GitHub credentials..."
    GLOBAL_GITHUB=$(get_global_credentials "github-dotnet-gzdkh" 2>/dev/null)
    if [ $? -eq 0 ] && [ -n "$GLOBAL_GITHUB" ]; then
      GITHUB_USER=$(echo "$GLOBAL_GITHUB" | cut -d: -f1)
      GITHUB_TOKEN=$(echo "$GLOBAL_GITHUB" | cut -d: -f2-)
      echo "     ✓ Found GitHub credentials in global config"
    else
      echo "     ⊘ No GitHub credentials in global config"
    fi
  fi

  # GitLab credentials
  if [ -z "$GITLAB_USER" ] || [ -z "$GITLAB_TOKEN" ]; then
    echo "  → Checking global config for GitLab credentials..."
    GLOBAL_GITLAB=$(get_global_credentials "gitlab-gzdkh-group" 2>/dev/null)
    if [ $? -eq 0 ] && [ -n "$GLOBAL_GITLAB" ]; then
      GITLAB_USER=$(echo "$GLOBAL_GITLAB" | cut -d: -f1)
      GITLAB_TOKEN=$(echo "$GLOBAL_GITLAB" | cut -d: -f2-)
      echo "     ✓ Found GitLab credentials in global config"
    else
      echo "     ⊘ No GitLab credentials in global config"
    fi
  fi
fi

# Configure GitHub Packages if credentials provided
if [ -n "$GITHUB_USER" ] && [ -n "$GITHUB_TOKEN" ]; then
  echo "  → Configuring GitHub Packages (github-dotnet-gzdkh)"
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
    --configfile "$NUGET_CONFIG"

  if [ $? -eq 0 ]; then
    echo "     ✓ GitHub authentication configured"
  else
    echo "     ✗ Failed to configure GitHub authentication"
    exit 1
  fi
fi

# Configure GitLab Package Registry if credentials provided
if [ -n "$GITLAB_USER" ] && [ -n "$GITLAB_TOKEN" ]; then
  echo "  → Configuring GitLab Package Registry (gitlab-gzdkh-group)"
  dotnet nuget update source gitlab-gzdkh-group \
    --username "$GITLAB_USER" \
    --password "$GITLAB_TOKEN" \
    --store-password-in-clear-text \
    --configfile "$NUGET_CONFIG" 2>/dev/null || \
  dotnet nuget add source "${GITLAB_NUGET_SOURCE_URL:-https://gitlab.thetea.app/api/v4/groups/2/-/packages/nuget/index.json}" \
    --name gitlab-gzdkh-group \
    --username "$GITLAB_USER" \
    --password "$GITLAB_TOKEN" \
    --store-password-in-clear-text \
    --configfile "$NUGET_CONFIG"

  if [ $? -eq 0 ]; then
    echo "     ✓ GitLab authentication configured"
  else
    echo "     ✗ Failed to configure GitLab authentication"
    exit 1
  fi
fi

# Verify configuration
if [ -z "$GITHUB_USER" ] && [ -z "$GITLAB_USER" ]; then
  echo "Warning: No credentials provided, nuget.config not modified"
  exit 0
fi

echo "✓ NuGet authentication configured successfully"
