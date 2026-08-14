resource "aws_ssm_parameter" "smoove_api_key" {
  name      = "/${local.name_prefix}/smoove_api_key"
  type      = "SecureString"
  value     = var.smoove_api_key
  overwrite = true
  tags      = local.tags
}

resource "aws_ssm_parameter" "opda_client_cert" {
  name      = "/${local.name_prefix}/opda_client_cert"
  type      = "String"
  value     = var.opda_client_cert
  overwrite = true
  tags      = local.tags
}

resource "aws_ssm_parameter" "opda_client_key" {
  name      = "/${local.name_prefix}/opda_client_key"
  type      = "SecureString"
  value     = var.opda_client_key
  overwrite = true
  tags      = local.tags
}

resource "aws_ssm_parameter" "opda_signing_key" {
  name      = "/${local.name_prefix}/opda_signing_key"
  type      = "SecureString"
  value     = var.opda_signing_key
  overwrite = true
  tags      = local.tags
}

resource "aws_ssm_parameter" "sprift_api_key" {
  name      = "/${local.name_prefix}/sprift_api_key"
  type      = "SecureString"
  value     = var.sprift_api_key
  overwrite = true
  tags      = local.tags
}

# JSON map of trusted VC issuer (the `iss` HTTPS URL) -> PEM public key,
# read by StaticIssuerKeyResolver (ADR-0013). Same shape as the ADR-0012 auth
# stub's client registry: one JSON parameter, no real trust framework. Empty
# by default — every presentation fails signature verification until an
# operator registers at least one issuer.
resource "aws_ssm_parameter" "wallet_trusted_issuers" {
  name      = "/${local.name_prefix}/wallet_trusted_issuers"
  type      = "String"
  value     = var.wallet_trusted_issuers
  overwrite = true
  tags      = local.tags
}
