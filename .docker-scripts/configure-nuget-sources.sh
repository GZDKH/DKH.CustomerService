#!/bin/bash
set -e

# Wrapper script for configuring all NuGet package sources in Docker
# Reads credentials from Docker BuildKit secrets and calls modular scripts
#
# Usage: ./configure-nuget-sources.sh [nuget-config-path]
#
# Expected Docker secrets (optional):
#   - github_nuget_username
#   - github_nuget_token
#   - gitlab_nuget_username
#   - gitlab_nuget_token
#
# Behavior:
#   - Reads secrets from /run/secrets/ (Docker BuildKit mount)
#   - Falls back to global ~/.nuget/NuGet/NuGet.Config if secrets empty
#   - Non-fatal: exits successfully even if all credentials missing
#
# Example Dockerfile usage:
#   COPY [".docker-scripts/configure-nuget-sources.sh", "/tmp/"]
#   COPY [".docker-scripts/configure-gitlab-nuget.sh", "/tmp/"]
#   COPY [".docker-scripts/configure-github-nuget.sh", "/tmp/"]
#
#   RUN --mount=type=secret,id=github_nuget_username \
#       --mount=type=secret,id=github_nuget_token \
#       --mount=type=secret,id=gitlab_nuget_username \
#       --mount=type=secret,id=gitlab_nuget_token \
#       bash /tmp/configure-nuget-sources.sh nuget.config

NUGET_CONFIG="${1:-nuget.config}"
SCRIPT_DIR="/tmp"

echo "==> Configuring NuGet package sources..."
echo ""

# Read Docker secrets (BuildKit mounts at /run/secrets/)
GITHUB_USER=$(cat /run/secrets/github_nuget_username 2>/dev/null || echo "")
GITHUB_TOKEN=$(cat /run/secrets/github_nuget_token 2>/dev/null || echo "")
GITLAB_USER=$(cat /run/secrets/gitlab_nuget_username 2>/dev/null || echo "")
GITLAB_TOKEN=$(cat /run/secrets/gitlab_nuget_token 2>/dev/null || echo "")

# Check if nuget.config exists
if [ ! -f "$NUGET_CONFIG" ]; then
  echo "Error: $NUGET_CONFIG not found"
  exit 1
fi

# Configure GitLab Package Registry (primary)
echo "→ Configuring GitLab Package Registry..."
if bash "$SCRIPT_DIR/configure-gitlab-nuget.sh" \
    --user "$GITLAB_USER" \
    --token "$GITLAB_TOKEN" \
    --config "$NUGET_CONFIG"; then
  echo "  ✓ GitLab configured"
else
  echo "  ⊘ GitLab skipped (no credentials)"
fi
echo ""

# Configure GitHub Packages (legacy/optional)
echo "→ Configuring GitHub Packages..."
if bash "$SCRIPT_DIR/configure-github-nuget.sh" \
    --user "$GITHUB_USER" \
    --token "$GITHUB_TOKEN" \
    --config "$NUGET_CONFIG"; then
  echo "  ✓ GitHub configured"
else
  echo "  ⊘ GitHub skipped (no credentials)"
fi
echo ""

echo "==> NuGet authentication setup completed"
exit 0
