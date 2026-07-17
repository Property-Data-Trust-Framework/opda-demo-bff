resource "aws_lambda_function" "app" {
  function_name = local.name_prefix
  role          = aws_iam_role.lambda.arn
  package_type  = "Image"
  image_uri     = "${aws_ecr_repository.app.repository_url}:${var.image_tag}"

  timeout     = 30
  memory_size = 256

  # No vpc_config — Lambda has direct internet access for outbound OPDA API calls.
  # The OPDA shared proxy NLB is publicly accessible, so no NAT gateway is needed.

  environment {
    variables = {
      SMOOVE_BASE_URL        = var.smoove_base_url
      SMOOVE_API_KEY_PATH    = aws_ssm_parameter.smoove_api_key.name
      SMOOVE_CALLBACK_URL    = "https://ext.smartpropdata.org.uk/webhook"
      OPDA_API_BASE_URL      = var.opda_api_base_url
      OPDA_CLIENT_CERT_PATH  = aws_ssm_parameter.opda_client_cert.name
      OPDA_CLIENT_KEY_PATH   = aws_ssm_parameter.opda_client_key.name
      OPDA_SIGNING_KEY_PATH  = aws_ssm_parameter.opda_signing_key.name
      OPDA_CLIENT_ID         = var.opda_client_id
      OPDA_TOKEN_ENDPOINT    = var.opda_token_endpoint
      DISCONNECTED_MODE      = var.disconnected_mode ? "true" : "false"
      PARTNER_TOKEN_ENDPOINT = var.partner_token_endpoint
      OPDA_SCOPE             = "land-registry"
      VMC_BASE_URL            = var.vmc_base_url
      VMC_SCOPE               = "land-registry"
      PDI_BASE_URL            = var.pdi_base_url
      PDI_SCOPE               = "property-pack"
      SPRIFT_BASE_URL         = var.sprift_base_url
      SPRIFT_SCOPE            = var.sprift_scope
      SPRIFT_API_KEY_PATH     = aws_ssm_parameter.sprift_api_key.name
      ARMALYTIX_CLIENT_REQUEST_ID = var.armalytix_client_request_id
      DynamoConfig__TableName     = aws_dynamodb_table.webhook_events.name
    }
  }

  depends_on = [aws_cloudwatch_log_group.app]

  tags = local.tags
}

resource "aws_lambda_permission" "api_gateway" {
  statement_id  = "AllowAPIGatewayInvoke"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.app.function_name
  principal     = "apigateway.amazonaws.com"
  source_arn    = "${aws_apigatewayv2_api.app.execution_arn}/*/*"
}
