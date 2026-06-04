# HTTP API v2 — public, no mTLS, significantly cheaper than REST API.
resource "aws_apigatewayv2_api" "app" {
  name          = local.name_prefix
  protocol_type = "HTTP"
  tags          = local.tags
}

resource "aws_apigatewayv2_integration" "lambda" {
  api_id                 = aws_apigatewayv2_api.app.id
  integration_type       = "AWS_PROXY"
  integration_uri        = aws_lambda_function.app.invoke_arn
  payload_format_version = "2.0"
}

# POST /webhook — Smoove delivers signed JWT events here (no auth, verified by Lambda)
resource "aws_apigatewayv2_route" "webhook" {
  api_id    = aws_apigatewayv2_api.app.id
  route_key = "POST /webhook"
  target    = "integrations/${aws_apigatewayv2_integration.lambda.id}"
}

# ANY /demo-api/{proxy+} — BFF endpoints for the SPA
resource "aws_apigatewayv2_route" "bff" {
  api_id    = aws_apigatewayv2_api.app.id
  route_key = "ANY /demo-api/{proxy+}"
  target    = "integrations/${aws_apigatewayv2_integration.lambda.id}"
}

resource "aws_apigatewayv2_stage" "default" {
  api_id      = aws_apigatewayv2_api.app.id
  name        = "$default"
  auto_deploy = true

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.app.arn
    format = jsonencode({
      requestId      = "$context.requestId"
      ip             = "$context.identity.sourceIp"
      requestTime    = "$context.requestTime"
      httpMethod     = "$context.httpMethod"
      routeKey       = "$context.routeKey"
      status         = "$context.status"
      responseLength = "$context.responseLength"
    })
  }

  tags = local.tags
}
