data "aws_caller_identity" "current" {}

locals {
  github_oidc_provider_arn = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:oidc-provider/token.actions.githubusercontent.com"
}

# ── GitHub Actions OIDC role ──────────────────────────────────────────────────
# One role per repo, shared across all environments (dev/staging/prod).
# Resource policies use ${var.name}-* wildcards to cover all environment-prefixed
# resources without needing to know the environment at role-creation time.
# Managed in its own Terraform root so that environment teardowns do not delete it.

resource "aws_iam_role" "github_actions" {
  name = "${var.name}-github-actions"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Action = "sts:AssumeRoleWithWebIdentity"
      Principal = {
        Federated = local.github_oidc_provider_arn
      }
      Condition = {
        StringLike = {
          "token.actions.githubusercontent.com:sub" = [
            "repo:${var.github_repo}:ref:refs/heads/main",
            "repo:${var.github_repo}:environment:dev",
            "repo:${var.github_repo}:environment:staging",
            "repo:${var.github_repo}:environment:prod",
          ]
        }
        StringEquals = {
          "token.actions.githubusercontent.com:aud" = "sts.amazonaws.com"
        }
      }
    }]
  })

  tags = {
    Project   = var.name
    ManagedBy = "terraform"
  }
}

resource "aws_iam_role_policy" "github_actions" {
  name = "deploy"
  role = aws_iam_role.github_actions.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      # ── ECR ────────────────────────────────────────────────────────────────
      # ECR repo is at var.name level (shared across environments).
      {
        Sid      = "ECRAuthToken"
        Effect   = "Allow"
        Action   = ["ecr:GetAuthorizationToken"]
        Resource = "*"
      },
      {
        Sid    = "ECRAppRepo"
        Effect = "Allow"
        Action = [
          "ecr:BatchCheckLayerAvailability", "ecr:BatchGetImage",
          "ecr:CompleteLayerUpload", "ecr:CreateRepository",
          "ecr:DeleteLifecyclePolicy", "ecr:DeleteRepository",
          "ecr:DescribeRepositories", "ecr:GetDownloadUrlForLayer",
          "ecr:GetLifecyclePolicy", "ecr:GetRepositoryPolicy",
          "ecr:InitiateLayerUpload", "ecr:ListTagsForResource",
          "ecr:PutImage", "ecr:PutLifecyclePolicy",
          "ecr:DeleteRepositoryPolicy", "ecr:SetRepositoryPolicy",
          "ecr:UploadLayerPart",
        ]
        Resource = "arn:aws:ecr:${var.aws_region}:${data.aws_caller_identity.current.account_id}:repository/${var.name}"
      },
      # ── Lambda ─────────────────────────────────────────────────────────────
      # Wildcard covers all environments: <name>-dev, <name>-dev-authorizer, etc.
      {
        Sid    = "Lambda"
        Effect = "Allow"
        Action = [
          "lambda:AddPermission", "lambda:CreateFunction",
          "lambda:DeleteFunction", "lambda:GetFunction",
          "lambda:GetFunctionCodeSigningConfig", "lambda:GetPolicy",
          "lambda:ListVersionsByFunction", "lambda:RemovePermission",
          "lambda:TagResource", "lambda:UntagResource",
          "lambda:UpdateFunctionCode", "lambda:UpdateFunctionConfiguration",
        ]
        Resource = "arn:aws:lambda:${var.aws_region}:${data.aws_caller_identity.current.account_id}:function:${var.name}-*"
      },
      # ── IAM ────────────────────────────────────────────────────────────────
      {
        Sid    = "IAMRoles"
        Effect = "Allow"
        Action = [
          "iam:AttachRolePolicy", "iam:CreateRole", "iam:DeleteRole",
          "iam:DeleteRolePolicy", "iam:DetachRolePolicy",
          "iam:GetRole", "iam:GetRolePolicy",
          "iam:ListAttachedRolePolicies", "iam:ListInstanceProfilesForRole",
          "iam:ListRolePolicies", "iam:PassRole", "iam:PutRolePolicy",
          "iam:TagRole", "iam:UntagRole", "iam:UpdateRole",
          "iam:CreateInstanceProfile", "iam:DeleteInstanceProfile",
          "iam:AddRoleToInstanceProfile", "iam:RemoveRoleFromInstanceProfile",
        ]
        Resource = [
          "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/${var.name}-*",
          "arn:aws:iam::${data.aws_caller_identity.current.account_id}:instance-profile/${var.name}-*",
        ]
      },
      # ── EC2 / VPC ──────────────────────────────────────────────────────────
      # VPC is shared (opda-ops/terraform/shared-vpc). Per-API roles only need
      # security group management, describe access, and Lambda ENI lifecycle.
      {
        Sid    = "EC2VpcAccess"
        Effect = "Allow"
        Action = [
          "ec2:AuthorizeSecurityGroupEgress",
          "ec2:AuthorizeSecurityGroupIngress",
          "ec2:CreateSecurityGroup",
          "ec2:CreateTags",
          "ec2:DeleteSecurityGroup",
          "ec2:RevokeSecurityGroupEgress",
          "ec2:RevokeSecurityGroupIngress",
          "ec2:DescribeAddresses",
          "ec2:DescribeAvailabilityZones",
          "ec2:DescribeInternetGateways",
          "ec2:DescribeNatGateways",
          "ec2:DescribeNetworkInterfaces",
          "ec2:DescribePrefixLists",
          "ec2:DescribeRouteTables",
          "ec2:DescribeSecurityGroups",
          "ec2:DescribeSubnets",
          "ec2:DescribeVpcEndpoints",
          "ec2:DescribeVpcs",
          "ec2:DescribeVpcAttribute",
          "ec2:DescribeAddressesAttribute",
          "ec2:CreateNetworkInterface",
          "ec2:DeleteNetworkInterface",
          "ec2:DescribeNetworkInterfaceAttribute",
          "ec2:ModifyNetworkInterfaceAttribute",
        ]
        Resource = "*"
      },
      # ── API Gateway ────────────────────────────────────────────────────────
      {
        Sid      = "APIGateway"
        Effect   = "Allow"
        Action   = ["apigateway:*"]
        Resource = "arn:aws:apigateway:${var.aws_region}::*"
      },
      # ── SSM — per-API parameters ───────────────────────────────────────────
      # Wildcard covers all environments: <name>-dev/*, <name>-staging/*, etc.
      {
        Sid    = "SSMParameters"
        Effect = "Allow"
        Action = [
          "ssm:PutParameter", "ssm:GetParameter", "ssm:GetParameters",
          "ssm:DeleteParameter", "ssm:ListTagsForResource", "ssm:AddTagsToResource",
        ]
        Resource = "arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter/${var.name}-*"
      },
      {
        Sid      = "SSMDescribe"
        Effect   = "Allow"
        Action   = ["ssm:DescribeParameters"]
        Resource = "*"
      },
      # ── SSM — shared VPC parameters (read-only) ───────────────────────────
      {
        Sid      = "SSMSharedVpc"
        Effect   = "Allow"
        Action   = ["ssm:GetParameter"]
        Resource = "arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter/opda/shared/*"
      },
      # ── SSM — shared proxy route registration ───────────────────────────────
      {
        Sid    = "SSMProxyRoute"
        Effect = "Allow"
        Action = [
          "ssm:PutParameter", "ssm:GetParameter", "ssm:GetParameters",
          "ssm:DeleteParameter", "ssm:ListTagsForResource", "ssm:AddTagsToResource",
        ]
        Resource = "arn:aws:ssm:${var.aws_region}:${data.aws_caller_identity.current.account_id}:parameter/opda/proxy/routes/${var.name}-*"
      },
      # ── CloudWatch Logs ────────────────────────────────────────────────────
      {
        Sid      = "LogsDescribe"
        Effect   = "Allow"
        Action   = ["logs:DescribeLogGroups"]
        Resource = "*"
      },
      {
        Sid    = "LogsManage"
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup", "logs:DeleteLogGroup",
          "logs:ListTagsForResource", "logs:ListTagsLogGroup",
          "logs:PutRetentionPolicy", "logs:TagLogGroup",
          "logs:TagResource", "logs:UntagResource",
        ]
        Resource = "arn:aws:logs:${var.aws_region}:${data.aws_caller_identity.current.account_id}:log-group:*${var.name}*"
      },
      # ── Terraform state ────────────────────────────────────────────────────
      {
        Sid      = "TerraformStateBucket"
        Effect   = "Allow"
        Action   = ["s3:ListBucket"]
        Resource = "arn:aws:s3:::ops-terraform-state-${data.aws_caller_identity.current.account_id}"
      },
      {
        Sid    = "TerraformStateObjects"
        Effect = "Allow"
        Action = ["s3:GetObject", "s3:PutObject", "s3:DeleteObject"]
        # Covers all environments and the iam/ state: <name>/dev/*, <name>/iam/*, etc.
        Resource = "arn:aws:s3:::ops-terraform-state-${data.aws_caller_identity.current.account_id}/${var.name}/*"
      },
      # ── DynamoDB (optional — uncomment if this API uses a DynamoDB data source) ─
      # Replace "my-table" with your table name suffix (e.g. "coalfields" gives
      # "<repo-name>-coalfields-<environment>"). The wildcard covers all environments.
      # {
      #   Sid    = "DynamoDBDescribe"
      #   Effect = "Allow"
      #   Action = [
      #     "dynamodb:DescribeTable",
      #     "dynamodb:DescribeContinuousBackups",
      #     "dynamodb:DescribeTimeToLive",
      #     "dynamodb:ListTagsOfResource",
      #   ]
      #   Resource = "arn:aws:dynamodb:${var.aws_region}:${data.aws_caller_identity.current.account_id}:table/${var.name}-my-table-*"
      # },
      # ── API docs site spec publish ─────────────────────────────────────────
      {
        Sid      = "ApiDocsSpecWrite"
        Effect   = "Allow"
        Action   = ["s3:PutObject"]
        Resource = "arn:aws:s3:::opda-api-docs-${data.aws_caller_identity.current.account_id}/specs/${var.name}.yaml"
      },
      {
        Sid    = "ApiDocsCacheInvalidation"
        Effect = "Allow"
        Action = ["cloudfront:CreateInvalidation", "cloudfront:ListDistributions"]
        Resource = "*"
      },
      # ── IAM self-management ────────────────────────────────────────────────
      {
        Sid    = "IAMSelfManagement"
        Effect = "Allow"
        Action = [
          "iam:GetRole", "iam:GetRolePolicy",
          "iam:ListRolePolicies", "iam:ListAttachedRolePolicies",
          "iam:ListInstanceProfilesForRole",
          "iam:PutRolePolicy", "iam:DeleteRolePolicy",
          "iam:UpdateRole", "iam:UpdateAssumeRolePolicy",
          "iam:TagRole", "iam:UntagRole",
        ]
        Resource = "arn:aws:iam::${data.aws_caller_identity.current.account_id}:role/${var.name}-github-actions"
      },
    ]
  })
}
