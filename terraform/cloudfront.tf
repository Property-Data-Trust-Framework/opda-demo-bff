# ── S3 bucket for SPA ────────────────────────────────────────────────────────

resource "aws_s3_bucket" "spa" {
  bucket = "${local.name_prefix}-spa"
  tags   = local.tags
}

resource "aws_s3_bucket_public_access_block" "spa" {
  bucket                  = aws_s3_bucket.spa.id
  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

resource "aws_cloudfront_origin_access_control" "spa" {
  name                              = "${local.name_prefix}-spa"
  origin_access_control_origin_type = "s3"
  signing_behavior                  = "always"
  signing_protocol                  = "sigv4"
}

resource "aws_s3_bucket_policy" "spa" {
  bucket = aws_s3_bucket.spa.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect    = "Allow"
      Principal = { Service = "cloudfront.amazonaws.com" }
      Action    = "s3:GetObject"
      Resource  = "${aws_s3_bucket.spa.arn}/*"
      Condition = {
        StringEquals = {
          "AWS:SourceArn" = aws_cloudfront_distribution.app.arn
        }
      }
    }]
  })
}

# ── CloudFront Function — strip /demo prefix before forwarding to S3 ─────────

resource "aws_cloudfront_function" "spa_rewrite" {
  name    = "${local.name_prefix}-spa-rewrite"
  runtime = "cloudfront-js-2.0"
  publish = true

  code = <<-JS
    function handler(event) {
      var request = event.request;
      var uri = request.uri;
      if (uri === '/demo' || uri === '/demo/') {
        request.uri = '/index.html';
      } else if (uri.startsWith('/demo/')) {
        request.uri = uri.substring('/demo'.length);
      }
      // SPA client-side routing: serve index.html for paths with no extension
      if (!request.uri.includes('.')) {
        request.uri = '/index.html';
      }
      return request;
    }
  JS
}

# ── CloudFront distribution ───────────────────────────────────────────────────

locals {
  s3_origin_id  = "spa-s3"
  apigw_origin_id = "bff-apigw"

  # HTTP API endpoint without the https:// scheme
  apigw_domain = replace(
    replace(aws_apigatewayv2_api.app.api_endpoint, "https://", ""),
    "http://", ""
  )
}

resource "aws_cloudfront_distribution" "app" {
  enabled             = true
  comment             = "${local.name_prefix}-demo"
  default_root_object = "index.html"
  aliases             = ["ext.smartpropdata.org.uk"]

  # ── Origin: SPA (S3) ───────────────────────────────────────────────────────
  origin {
    origin_id                = local.s3_origin_id
    domain_name              = aws_s3_bucket.spa.bucket_regional_domain_name
    origin_access_control_id = aws_cloudfront_origin_access_control.spa.id
  }

  # ── Origin: BFF Lambda (HTTP API Gateway) ──────────────────────────────────
  origin {
    origin_id   = local.apigw_origin_id
    domain_name = local.apigw_domain

    custom_origin_config {
      http_port              = 80
      https_port             = 443
      origin_protocol_policy = "https-only"
      origin_ssl_protocols   = ["TLSv1.2"]
    }
  }

  # ── Behaviour: /webhook → API Gateway (no caching) ─────────────────────────
  ordered_cache_behavior {
    path_pattern           = "/webhook"
    target_origin_id       = local.apigw_origin_id
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods         = ["GET", "HEAD"]
    cache_policy_id        = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad" # CachingDisabled
    origin_request_policy_id = "b689b0a8-53d0-40ab-baf2-68738e2966ac" # AllViewerExceptHostHeader
  }

  # ── Behaviour: /demo-api/* → API Gateway (no caching) ──────────────────────
  ordered_cache_behavior {
    path_pattern           = "/demo-api/*"
    target_origin_id       = local.apigw_origin_id
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["DELETE", "GET", "HEAD", "OPTIONS", "PATCH", "POST", "PUT"]
    cached_methods         = ["GET", "HEAD"]
    cache_policy_id        = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad" # CachingDisabled
    origin_request_policy_id = "b689b0a8-53d0-40ab-baf2-68738e2966ac" # AllViewerExceptHostHeader
  }

  # ── Behaviour: /demo/* → S3 (standard SPA caching, path rewrite) ───────────
  ordered_cache_behavior {
    path_pattern           = "/demo*"
    target_origin_id       = local.s3_origin_id
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD", "OPTIONS"]
    cached_methods         = ["GET", "HEAD"]
    cache_policy_id        = "658327ea-f89d-4fab-a63d-7e88639e58f6" # CachingOptimized

    function_association {
      event_type   = "viewer-request"
      function_arn = aws_cloudfront_function.spa_rewrite.arn
    }
  }

  # ── Default behaviour: root → redirect to /demo ────────────────────────────
  default_cache_behavior {
    target_origin_id       = local.s3_origin_id
    viewer_protocol_policy = "redirect-to-https"
    allowed_methods        = ["GET", "HEAD"]
    cached_methods         = ["GET", "HEAD"]
    cache_policy_id        = "4135ea2d-6df8-44a3-9df3-4b5a84be39ad" # CachingDisabled

    function_association {
      event_type   = "viewer-request"
      function_arn = aws_cloudfront_function.spa_rewrite.arn
    }
  }

  restrictions {
    geo_restriction {
      restriction_type = "none"
    }
  }

  viewer_certificate {
    acm_certificate_arn      = aws_acm_certificate_validation.ext.certificate_arn
    ssl_support_method       = "sni-only"
    minimum_protocol_version = "TLSv1.2_2021"
  }

  tags = local.tags
}
