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

# Wallet OpenID4VP presentation state (ADR-0013). Keyed by `state`, not
# transactionDid — a wallet vp_token doesn't carry our transaction id, only the
# state we handed out at request time does. Short TTL: a presentation that
# hasn't completed within an hour is abandoned, not resumed.
resource "aws_dynamodb_table" "wallet_presentations" {
  name         = "${local.name_prefix}-wallet-presentations"
  billing_mode = "PAY_PER_REQUEST"
  hash_key     = "state"

  attribute {
    name = "state"
    type = "S"
  }

  ttl {
    attribute_name = "ttl"
    enabled        = true
  }

  tags = local.tags
}
