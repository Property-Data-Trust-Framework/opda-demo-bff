# `bucket`, `region`, and `key` are supplied at init time so the account ID is
# not committed. This root has its own state separate from the environment stack.
#
# Local init:
#   BUCKET="ops-terraform-state-$(aws sts get-caller-identity --query Account --output text)"
#   terraform init \
#     -backend-config="bucket=$BUCKET" \
#     -backend-config="region=eu-west-2" \
#     -backend-config="key=<repo-name>/iam/terraform.tfstate"

terraform {
  backend "s3" {
    use_lockfile = true
  }
}
