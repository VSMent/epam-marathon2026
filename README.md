# EPAM Marathon 2026 — Restaurant (Green & Tasty)

Full-stack restaurant booking and **group pre-order** app for the EPAM IT Marathon: Angular frontend, .NET 8 serverless backend on AWS, Terraform infra, Reqnroll/Playwright AQA, and Manual QA artifacts.

Default branch: **`master`**. Live stack is meant to sit in **AWS `us-west-2`** (API Gateway + Lambdas + DynamoDB + Cognito + S3/CloudFront).

## Current features

| Area | What works |
| :--- | :--- |
| **Core restaurant** | Locations, menu/dishes, reservations, auth (Cognito), waiter/customer flows from the base marathon app |
| **Group pre-order (owner)** | Owner group-order screen: per-guest carts & totals, consolidated submit, live refresh (~15s), remove guest + confirm dialog |
| **Group pre-order (HW1 US1–US3)** | Bulk/upsell suggestions, alcohol filter & logistics prompt, empty-guest warnings + reminder UX |
| **Backend APIs** | Guest sessions, group reservation sessions list/submit/remove, seedable DynamoDB data, Postman collection |
| **Frontend** | Angular 18+ with Signals; HW6 Levels 1–3 + bonus done (mock API for L1–L2, real API for L3) |
| **Branding** | Ukrainian Steak & Wine hero / menu branding on main surfaces |

## Project state (homework tracker)

| HW | Focus | Status |
| :--- | :--- | :--- |
| **#1** | US1 / US2 / US3 group pre-order UX | Done (committed) |
| **#5** | Backend group pre-order (owner) | Done for committed levels; extra handlers/WIP may still be local |
| **#6** | Frontend owner group-order screen | Done — L1, L2, L3 + bonus |
| **#7** | Manual QA | Done — report, cases, bugs under [`manual-qa/`](./manual-qa/) (local until committed) |
| **#8** | Test automation (AQA) | Framework in repo; group pre-order scenarios / CI secrets still being wired |

**CI/CD:** GitHub Actions under [`.github/workflows/`](./.github/workflows/) — frontend PR checks + S3 deploy, Syndicate backend deploy, Terraform plan/apply, AQA quality gate, nightly/manual AQA tests. Workflows listen on `master` and `main`. Deploy/AQA need repo **Actions secrets & variables** (and AWS OIDC roles for deploy). See [`aqa/restaurant-aqa-framework/README.md`](./aqa/restaurant-aqa-framework/README.md) for AQA setup.

**Layout**

| Path | Role |
| :--- | :--- |
| `frontend/restaurant-frontend-app/` | Angular UI |
| `backend/restaurant-backend-app/` | .NET Lambdas + Syndicate |
| `infra/terraform/` | Shared AWS infra |
| `aqa/restaurant-aqa-framework/` | Reqnroll + TUnit + Playwright E2E |
| `manual-qa/` | HW7 analysis, test cases, bug reports |
| `docs/`, `postman/`, `data/`, `scripts/` | Contracts, Postman, seed data |

Progress notes also live in [`TODO.md`](./TODO.md).

---

# Deployment Guide

This repo uses three deployment tracks:

- Terraform for shared AWS infrastructure under `infra/terraform`
- AWS Syndicate for the backend application under `backend/restaurant-backend-app`
- Angular build artifacts synced to the frontend S3 bucket under `frontend/restaurant-frontend-app`

## 📚 API Testing, Database Seeding & Postman

Quick links for testing the live API and running homework scenarios:

| Resource | Path | Description |
| :--- | :--- | :--- |
| **Postman Guide** | [`postman/POSTMAN_GUIDE.md`](./postman/POSTMAN_GUIDE.md) | Step-by-step instructions for Postman, authentication, and live response examples |
| **Postman Collection** | [`postman/group-orders.postman_collection.json`](./postman/group-orders.postman_collection.json) | Importable Postman collection with auto-token extraction |
| **API Contracts** | [`docs/API_CONTRACTS.md`](./docs/API_CONTRACTS.md) | Full endpoint specs, DTOs, and error codes for HW5 / HW6 / HW8 |
| **Database Seed Data** | [`data/initial-seed-data.json`](./data/initial-seed-data.json) | Complete interconnected dataset for DynamoDB (HW1 US1/US2/US3, HW5) |
| **Seeder Script** | [`scripts/seed_all_tables.py`](./scripts/seed_all_tables.py) | Python script to populate all DynamoDB tables with seed data |

To populate DynamoDB tables at any time:
```powershell
python scripts/seed_all_tables.py
```

Quick terminal test with seeded data:
```powershell
# 1. Sign in
$token = (Invoke-RestMethod -Uri "https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/signin" -Method Post -Body '{"email":"demo_owner@test.com","password":"Password123!"}' -ContentType "application/json").idToken

# 2. Query seeded group pre-order sessions
Invoke-RestMethod -Uri "https://cs624wcc74.execute-api.us-west-2.amazonaws.com/develop/guest/groups/demo-group-1/sessions" -Headers @{"Authorization"="Bearer $token"} | ConvertTo-Json -Depth 5
```

---

## Prerequisites

Install the tools below on your machine:

- AWS CLI configured with credentials for the target AWS account (`aws configure`) (https://docs.aws.amazon.com/cli/latest/userguide/getting-started-prereqs.html)
- Terraform 1.10 or newer (https://developer.hashicorp.com/terraform/install)
- Python 3.10-3.11 (https://www.python.org/downloads/release/python-3110/)
- .NET 10 SDK (https://dotnet.microsoft.com/en-us/download/dotnet/10.0)
- AWS Syndicate (`pip install aws-syndicate`) (https://github.com/epam/aws-syndicate)
- Node.js 22.x with npm (https://nodejs.org/en/download/archive/v22.0.0)

## Deployment order

Use this order:

1. Deploy Terraform infrastructure
2. Deploy the backend using Syndicate
3. Deploy the frontend to s3

Terraform then provisions or updates the shared AWS foundation, including the frontend S3 bucket and CloudFront distribution. Syndicate packages and deploys the backend application stack. After that, build the Angular app with the correct backend URL and sync it to S3.

## 1. Deploy Terraform infrastructure

Terraform manages the shared infrastructure resources in `infra/terraform`.

Before deploying, verify terraform config files:
Replace variables with your **AWS account ID** and **Email**

- `infra/terraform/backend.tf` - replace the Terraform state bucket name
- `infra/terraform/variables.tf` - replace the default `account_id` and `alert_emails`


Before running `terraform init`, create the Terraform state bucket with AWS CLI if it does not already exist. 
Get your AWS account ID from the CLI first:

Windows PowerShell:

```powershell
$ACCOUNT_ID = aws sts get-caller-identity --query Account --output text
```

Linux or macOS:

```bash
ACCOUNT_ID=$(aws sts get-caller-identity --query Account --output text)
```

You can use AWS Console interface
AWS Management Console equivalent:
- Go to **S3** → **Create bucket** and use the bucket name **restaurant-terraform-state-$ACCOUNT_ID**
- Turn on **Versioning** in the bucket properties
- Leave **Block all public access** enabled
- Use **Default encryption** with SSE-S3 (AES256)

As alternative you can you AWS CLI
Replace the bucket name with the CLI variable:

Windows PowerShell:

```powershell
aws s3api create-bucket `
	--bucket restaurant-terraform-state-$ACCOUNT_ID `
	--region us-west-2 `
	--create-bucket-configuration LocationConstraint=us-west-2

aws s3api put-bucket-versioning `
	--bucket restaurant-terraform-state-$ACCOUNT_ID `
	--versioning-configuration Status=Enabled

aws s3api put-public-access-block `
		--bucket restaurant-terraform-state-$ACCOUNT_ID `
	--public-access-block-configuration `
		"BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"
```

Linux or macOS:

```bash
aws s3api create-bucket \
	--bucket restaurant-terraform-state-$ACCOUNT_ID \
	--region us-west-2 \
	--create-bucket-configuration LocationConstraint=us-west-2

aws s3api put-bucket-versioning \
	--bucket restaurant-terraform-state-$ACCOUNT_ID \
	--versioning-configuration Status=Enabled

aws s3api put-public-access-block \
		--bucket restaurant-terraform-state-$ACCOUNT_ID \
	--public-access-block-configuration \
		"BlockPublicAcls=true,IgnorePublicAcls=true,BlockPublicPolicy=true,RestrictPublicBuckets=true"
```

### After bucket creation
```bash
cd ../../infra/terraform
terraform init
terraform fmt -check -recursive
terraform validate
terraform plan
terraform apply -auto-approve
```

## 2. Deploy the backend with Syndicate

The backend app is deployed from `backend/restaurant-backend-app`.

Before deploying, verify the Syndicate config files:
edit syndicate.yml → set project_path to your local absolute path to backend/restaurant-backend-app
Replace bucket names with your AWS account ID
- `.syndicate-config-restaurant-backend-app/syndicate.yml`
- `.syndicate-config-restaurant-backend-app/syndicate_aliases.yml`

On Windows PowerShell, run:

```powershell
cd ../../backend/restaurant-backend-app
python3 -m venv syndicate_venv
.\syndicate_venv\Scripts\Activate.ps1
pip install aws-syndicate
$env:SDCT_CONF = (Resolve-Path ".\.syndicate-config-restaurant-backend-app").Path
syndicate create-deploy-target-bucket
syndicate build
syndicate deploy --deploy_name dev
```

On Linux or macOS, run:

```bash
cd ../../backend/restaurant-backend-app
python3 -m venv syndicate_venv
source syndicate_venv/bin/activate
export SDCT_CONF="$(pwd)/.syndicate-config-restaurant-backend-app"
syndicate create-deploy-target-bucket
syndicate build
syndicate deploy --deploy_name dev
```

For an incremental update instead of a full deploy, run the matching command for your shell.

```
syndicate update
```

## 3. Deploy the frontend

This section describes the manual deployment flow. It does not use GitHub Actions.

Terraform creates and configures everything needed for frontend hosting:

- the private frontend S3 bucket
- the bucket policy that allows CloudFront to read from it
- the CloudFront distribution that serves the site

You do not need to configure the bucket manually after `terraform apply`. The manual part is only building the frontend and uploading the files to S3.

### 3.1 Get the backend URL

Use the deployed API Gateway base URL as `BACKEND_URL`. Example:

```text
https://<api-id>.execute-api.us-west-2.amazonaws.com/develop
```

The production frontend build will fail unless `BACKEND_URL` is set.

To look it up in AWS:

```powershell
aws apigateway get-rest-apis --region us-west-2 --query "items[?name=='restaurant-backend-api'].{name:name,id:id}" --output table
```

Or set variables from AWS CLI:

Windows PowerShell:

```powershell
$API_ID = aws apigateway get-rest-apis --region us-west-2 --query "items[?name=='restaurant-backend-api'].id | [0]" --output text
$env:BACKEND_URL = "https://$API_ID.execute-api.us-west-2.amazonaws.com/develop"
```

Linux or macOS:

```bash
API_ID=$(aws apigateway get-rest-apis --region us-west-2 --query "items[?name=='restaurant-backend-api'].id | [0]" --output text)
export BACKEND_URL="https://$API_ID.execute-api.us-west-2.amazonaws.com/develop"
```

### 3.2 Build the production bundle

From the frontend app directory, install dependencies and build:

```powershell
cd ../../frontend/restaurant-frontend-app
npm ci
npm run build
```

The build process generates `src/environments/environment.production.ts` automatically from `BACKEND_URL` and writes the production bundle to `dist/restaurant-frontend-app/browser`.

### 3.3 Upload the bundle to S3

Sync the built files to the frontend bucket:

```powershell
aws s3 sync dist/restaurant-frontend-app/browser s3://restaurant-frontend-$ACCOUNT_ID/ --delete
```

The `--delete` flag removes files in S3 that no longer exist in the local build output.

### 3.4 Get the frontend URL

Get the CloudFront domain name from Terraform:

```powershell
cd ../../infra/terraform
terraform output cloudfront_distribution_domain_name
terraform output cloudfront_distribution_id
```

Use the returned domain name as the frontend host. Example:

```text
https://dxxxxxxxxxxxx.cloudfront.net
```

CloudFront does not provide a fixed public IP you should use. Use the distribution domain name instead.

## Clean up all resources

Use the reverse order of deployment:

1. Remove uploaded frontend files from the frontend S3 bucket
2. Clean up the backend resources deployed by Syndicate
3. Destroy the shared infrastructure managed by Terraform
4. Remove the Terraform state bucket
5. Remove the Syndicate artifacts bucket

### Remove frontend files from S3

The frontend bucket must be empty before Terraform can destroy it.

```powershell
aws s3 rm s3://restaurant-frontend-$ACCOUNT_ID/ --recursive
```

If you are not using the shared training account, replace the bucket name with your own account-specific frontend bucket.

### Clean up backend resources with Syndicate

From the backend project directory, use the same Syndicate configuration and run:

```powershell
cd ../../backend/restaurant-backend-app
$env:SDCT_CONF = (Resolve-Path ".\.syndicate-config-restaurant-backend-app").Path
syndicate clean
```

On Linux or macOS, use:

```bash
cd ../../backend/restaurant-backend-app
export SDCT_CONF="$(pwd)/.syndicate-config-restaurant-backend-app"
syndicate clean
```

This removes the backend resources that Syndicate deployed into the AWS account.

### Destroy Terraform-managed infrastructure

After the frontend bucket is empty and the backend resources are removed, destroy the shared infrastructure:

```powershell
cd ../../infra/terraform
terraform destroy -auto-approve
```

This removes the frontend S3 bucket, CloudFront distribution, and the rest of the infrastructure managed in `infra/terraform`.

### Clear versioned bucket

S3 bucket for storing terraform state have versioning configured. In order to delete it you must empty all versions created for s3 object.

- Go to **S3** select **restaurant-terraform-state-$ACCOUNT_ID** → **Empty Bucket**

### Remove the Terraform state bucket

The Terraform state bucket is configured in `infra/terraform/backend.tf` and is not deleted by `terraform destroy`. Empty it first and then delete it manually:

```powershell
aws s3api delete-bucket --bucket restaurant-terraform-state-$ACCOUNT_ID --region us-west-2
```

### Remove the Syndicate artifacts bucket

The Syndicate deploy target bucket is not managed by Terraform in this repo. If you also want to delete that bucket, empty it first and then remove it manually:

```powershell
aws s3 rm s3://marathon2026-restaurant-artifacts-$ACCOUNT_ID/ --recursive
aws s3api delete-bucket --bucket marathon2026-restaurant-artifacts-$ACCOUNT_ID --region us-west-2
```

Only do this if you are sure you no longer need the stored Syndicate build artifacts.