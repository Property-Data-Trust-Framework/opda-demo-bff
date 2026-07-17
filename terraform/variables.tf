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

variable "opda_signing_key" {
  type        = string
  description = "PEM RSA private key for private_key_jwt token requests to Raidiam"
  sensitive   = true
  default     = ""
}

variable "opda_client_id" {
  type    = string
  default = "https://rp.directory.pdtf.raidiam.io/openid_relying_party/70e31a1a-054d-4eb7-99eb-2ef4a78c1b0d"
}

# ── ViewMyChain ───────────────────────────────────────────────────────────────

variable "vmc_base_url" {
  type    = string
  default = "https://admin.demo3.viewmychain.com"
}

# ── Property Deals Insight ────────────────────────────────────────────────────

variable "pdi_base_url" {
  type    = string
  default = "https://sandbox-pdtf.propertydealsinsight.com"
}

# ── Sprift ────────────────────────────────────────────────────────────────────
# PDTF sandbox base URL and scope to be confirmed from Alan Hughes / Sprift call
# on 2026-06-11. Commercial API base (for reference): https://sprift.com/dashboard/api/v1
# Paths derived from commercial spec should hold; base URL and scope will differ.

variable "sprift_base_url" {
  type    = string
  default = "https://opdatest.sprift.com"
}

variable "sprift_scope" {
  type    = string
  default = "property-pack"
}

variable "sprift_api_key" {
  type      = string
  sensitive = true
}

variable "armalytix_client_request_id" {
  description = "Armalytix client request (correlation) id. Override per environment; not a secret."
  type        = string
  default     = "e574b05f-94fc-4249-be94-695bab8b268b"
}

variable "opda_token_endpoint" {
  type        = string
  description = "OAuth token endpoint the BFF mints client-credentials tokens from (Raidiam directory, or the auth stub's /token after decoupling — ADR-0012)."
  default     = "https://matls-auth.directory.pdtf.raidiam.io/token"
}

variable "disconnected_mode" {
  type        = bool
  description = "Fully self-contained sandbox mode: skip token minting and serve canned fixtures for unreachable upstreams (ADR-0012). Flip via the DISCONNECTED_MODE GitHub variable + redeploy."
  default     = false
}
