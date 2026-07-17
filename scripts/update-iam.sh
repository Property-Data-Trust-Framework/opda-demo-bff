#!/usr/bin/env bash
# Applies this repo's terraform/iam root (GitHub Actions OIDC role + policy).
# Location-independent — run from anywhere; paths derive from the script's own
# location (house pattern). Run after any IAM change (new SSM params, policy
# updates, trusted-repo identity changes) — CI cannot apply its own role changes.
set -euo pipefail
REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
REPO="$(basename "$REPO_DIR")"
BUCKET="ops-terraform-state-$(aws sts get-caller-identity --query Account --output text)"
terraform -chdir="$REPO_DIR/terraform/iam" init -reconfigure \
  -backend-config="bucket=$BUCKET" \
  -backend-config="region=eu-west-2" \
  -backend-config="key=${REPO}/iam/terraform.tfstate"
terraform -chdir="$REPO_DIR/terraform/iam" apply

# Receipt: the role CI must assume. First-time bootstrap: set it as the AWS_ROLE_ARN
# GitHub secret (env dev) — nothing sets it automatically on this path.
echo ""
echo "github_actions_role_arn: $(terraform -chdir="$REPO_DIR/terraform/iam" output -raw github_actions_role_arn)"
echo "(must match the repo's AWS_ROLE_ARN secret — gh secret set AWS_ROLE_ARN --env dev --repo <org>/$REPO --body <arn>)"
