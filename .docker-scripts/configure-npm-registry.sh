#!/bin/bash
set -e

# Configure NPM registry authentication for private @gzdkh packages in Docker
# Reads credentials from Docker BuildKit secrets and creates .npmrc
#
# Usage: ./configure-npm-registry.sh
#
# Expected Docker secrets (optional):
#   - gitlab_npm_token
#
# Environment variables (optional):
#   - NPM_REGISTRY_URL: Override the default GitLab NPM registry URL
#   - NPM_GROUP_ID: Override the default GitLab group ID (default: 2)
#
# Behavior:
#   - Reads token from /run/secrets/gitlab_npm_token (Docker BuildKit mount)
#   - Creates .npmrc with scoped registry config for @gzdkh packages
#   - Non-fatal: exits successfully even if token is missing
#
# Example Dockerfile usage:
#   COPY [".docker-scripts/configure-npm-registry.sh", "/tmp/"]
#
#   RUN --mount=type=secret,id=gitlab_npm_token \
#       bash /tmp/configure-npm-registry.sh && \
#       pnpm install --frozen-lockfile

# Read Docker secret (BuildKit mounts at /run/secrets/)
GITLAB_TOKEN=$(cat /run/secrets/gitlab_npm_token 2>/dev/null || echo "")

if [ -z "$GITLAB_TOKEN" ]; then
  echo "No NPM token found, using public registry only"
  exit 0
fi

NPM_REGISTRY="${NPM_REGISTRY_URL:-https://gitlab.thetea.app/api/v4/groups/${NPM_GROUP_ID:-2}/-/packages/npm/}"

cat > .npmrc <<EOF
@gzdkh:registry=${NPM_REGISTRY}
//${NPM_REGISTRY#https://}:_authToken=${GITLAB_TOKEN}
EOF

echo "NPM registry configured: ${NPM_REGISTRY}"
exit 0
