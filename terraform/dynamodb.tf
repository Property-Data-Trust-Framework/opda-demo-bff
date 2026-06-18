resource "aws_dynamodb_table" "webhook_events" {
  name         = "${local.name_prefix}-webhook-events"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "transactionDid"
  range_key    = "event"

  attribute {
    name = "transactionDid"
    type = "S"
  }

  attribute {
    name = "event"
    type = "S"
  }

  ttl {
    attribute_name = "ttl"
    enabled        = true
  }

  tags = local.tags
}
