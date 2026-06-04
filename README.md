# OPDA Demo BFF

Backend-for-frontend Lambda + Smoove webhook receiver for the OPDA demo ecosystem.

- Public HTTP API Gateway (no shared mTLS proxy)
- VPC-less Lambda (outbound mTLS to OPDA APIs over public internet)
- CloudFront + S3 for the SPA frontend
