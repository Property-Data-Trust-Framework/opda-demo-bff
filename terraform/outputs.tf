output "cloudfront_domain" {
  value       = aws_cloudfront_distribution.app.domain_name
  description = "CloudFront distribution domain — set as CNAME target or use directly until DNS propagates"
}

output "demo_url" {
  value       = "https://ext.smartpropdata.org.uk/demo"
  description = "SPA URL once DNS propagates"
}

output "bff_url" {
  value       = "https://ext.smartpropdata.org.uk/demo-api"
  description = "BFF API base URL"
}

output "webhook_url" {
  value       = "https://ext.smartpropdata.org.uk/webhook"
  description = "Smoove webhook callback URL — register this as callbackUrl in subscriptions"
}

output "spa_bucket" {
  value       = aws_s3_bucket.spa.bucket
  description = "S3 bucket name — sync SPA build output here"
}

output "apigw_endpoint" {
  value       = aws_apigatewayv2_api.app.api_endpoint
  description = "HTTP API Gateway endpoint (direct, bypassing CloudFront — useful for debugging)"
}
