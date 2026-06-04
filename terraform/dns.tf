# ── ext.smartpropdata.org.uk hosted zone ─────────────────────────────────────

resource "aws_route53_zone" "ext" {
  name = "ext.smartpropdata.org.uk"
  tags = local.tags
}

# If the parent smartpropdata.org.uk zone ID is provided, automatically create
# the NS delegation record. Otherwise add the NS records to the parent zone manually
# (see the ns_records output).
resource "aws_route53_record" "ext_ns_delegation" {
  count   = var.parent_zone_id != "" ? 1 : 0
  zone_id = var.parent_zone_id
  name    = "ext.smartpropdata.org.uk"
  type    = "NS"
  ttl     = 300
  records = aws_route53_zone.ext.name_servers
}

# ── ACM certificate (must be in us-east-1 for CloudFront) ────────────────────

resource "aws_acm_certificate" "ext" {
  provider          = aws.us_east_1
  domain_name       = "ext.smartpropdata.org.uk"
  validation_method = "DNS"

  lifecycle {
    create_before_destroy = true
  }

  tags = local.tags
}

resource "aws_route53_record" "cert_validation" {
  for_each = {
    for dvo in aws_acm_certificate.ext.domain_validation_options : dvo.domain_name => {
      name   = dvo.resource_record_name
      type   = dvo.resource_record_type
      record = dvo.resource_record_value
    }
  }

  zone_id = aws_route53_zone.ext.zone_id
  name    = each.value.name
  type    = each.value.type
  ttl     = 60
  records = [each.value.record]
}

resource "aws_acm_certificate_validation" "ext" {
  provider                = aws.us_east_1
  certificate_arn         = aws_acm_certificate.ext.arn
  validation_record_fqdns = [for r in aws_route53_record.cert_validation : r.fqdn]
}

# ── DNS A record → CloudFront ─────────────────────────────────────────────────

resource "aws_route53_record" "apex" {
  zone_id = aws_route53_zone.ext.zone_id
  name    = "ext.smartpropdata.org.uk"
  type    = "A"

  alias {
    name                   = aws_cloudfront_distribution.app.domain_name
    zone_id                = aws_cloudfront_distribution.app.hosted_zone_id
    evaluate_target_health = false
  }
}
