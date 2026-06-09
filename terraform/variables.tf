variable "name" {
  type        = string
  description = "Resource name prefix — injected from the GitHub repo name by the pipeline"
}

variable "environment" {
  type    = string
  default = "dev"
}

variable "aws_region" {
  type    = string
  default = "eu-west-2"
}

variable "github_repo" {
  type        = string
  description = "GitHub repository in owner/repo format"
}

variable "image_tag" {
  type    = string
  default = "latest"
}

# ── Smoove integration ────────────────────────────────────────────────────────

variable "smoove_api_key" {
  type      = string
  sensitive = true
  default   = ""
}

variable "smoove_base_url" {
  type    = string
  default = "https://api.smve-staging.co.uk/opda"
}

# ── Outbound OPDA client certs (mTLS to the OPDA shared proxy) ───────────────

variable "opda_client_cert" {
  type        = string
  description = "PEM certificate for outbound mTLS calls to the OPDA shared proxy"
  sensitive   = true
  default     = ""
}

variable "opda_client_key" {
  type        = string
  description = "PEM private key for outbound mTLS calls to the OPDA shared proxy"
  sensitive   = true
  default     = ""
}

variable "opda_api_base_url" {
  type    = string
  default = "https://dev.api.smartpropdata.org.uk"
}
