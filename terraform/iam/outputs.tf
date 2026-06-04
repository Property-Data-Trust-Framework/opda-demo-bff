output "github_actions_role_arn" {
  description = "ARN of the GitHub Actions OIDC role — set this as AWS_ROLE_ARN in GitHub secrets"
  value       = aws_iam_role.github_actions.arn
}
