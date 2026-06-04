variable "name" {
  type        = string
  description = "Repository/resource name prefix (e.g. opda-uprn-validator)"

  validation {
    condition     = length(var.name) > 0
    error_message = "var.name must not be empty. Values are committed in terraform.tfvars in this directory."
  }
}

variable "aws_region" {
  type    = string
  default = "eu-west-2"
}

variable "github_repo" {
  type        = string
  description = "GitHub repository in owner/repo format (e.g. Property-Data-Trust-Framework/opda-uprn-validator)"

  validation {
    condition     = can(regex("^[^/[:space:]]+/[^/[:space:]]+$", var.github_repo))
    error_message = "var.github_repo must be in 'owner/repo' format and non-empty. Got: '${var.github_repo}'."
  }
}

