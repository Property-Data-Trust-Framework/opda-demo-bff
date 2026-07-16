#!/usr/bin/env bash
# Applies this repo's terraform/iam root (GitHub Actions OIDC role + policy).
# Run from the repo root after any IAM change (new SSM params, policy updates,
# trusted-repo identity changes) — CI cannot apply its own role changes.
set -euo pipefail
REPO=$(basename "$(git rev-parse --show-toplevel)")
BUCKET="ops-terraform-state-$(aws sts get-caller-identity --query Account --output text)"
terraform -chdir=terraform/iam init -reconfigure \
  -backend-config="bucket=$BUCKET" \
  -backend-config="region=eu-west-2" \
  -backend-config="key=${REPO}/iam/terraform.tfstate"
terraform -chdir=terraform/iam apply
