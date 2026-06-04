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
