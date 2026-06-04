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
      OPDA_API_BASE_URL      = var.opda_api_base_url
      OPDA_CLIENT_CERT_PATH  = aws_ssm_parameter.opda_client_cert.name
      OPDA_CLIENT_KEY_PATH   = aws_ssm_parameter.opda_client_key.name
      DynamoConfig__TableName = aws_dynamodb_table.webhook_events.name
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
