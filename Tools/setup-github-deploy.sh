#!/usr/bin/env bash
#
# One-time setup so GitHub Actions can deploy to Cloud Run without a stored key.
#
# Run once, as a project owner (alikolik505@gmail.com owns this project, which
# is a different account from the GitHub login -- that split is intentional):
#
#     bash Tools/setup-github-deploy.sh
#
# What this creates:
#   * a deploy service account with the four roles Sidecar/deploy.sh needs
#   * a Workload Identity pool + OIDC provider that trusts GitHub's token issuer
#   * a binding letting ONLY this repo impersonate that service account
#
# Nothing secret is produced. GitHub mints a ~10-minute token per run; there is
# no JSON key to leak, rotate, or have gitleaks trip over.

set -euo pipefail

PROJECT="${PROJECT:-false-positive-504516}"
PROJECT_NUMBER="${PROJECT_NUMBER:-465469192069}"
REPO="${REPO:-stormragemc/FALSE-POSITIVE}"
SA_NAME="gh-deployer"
SA="${SA_NAME}@${PROJECT}.iam.gserviceaccount.com"
POOL="github"
PROVIDER="github"

echo "==> Enabling required APIs"
gcloud services enable \
  iamcredentials.googleapis.com \
  sts.googleapis.com \
  artifactregistry.googleapis.com \
  run.googleapis.com \
  secretmanager.googleapis.com \
  --project "${PROJECT}"

echo "==> Creating service account ${SA_NAME}"
gcloud iam service-accounts create "${SA_NAME}" \
  --project "${PROJECT}" \
  --display-name "GitHub Actions deployer" 2>/dev/null || echo "    already exists, continuing"

echo "==> Granting roles"
# artifactregistry.writer  -> docker push
# run.admin                -> gcloud run deploy
# iam.serviceAccountUser   -> permission to act as Cloud Run's runtime identity
# secretmanager.secretAccessor -> deploy.sh reads fp-client-key for its /turn probe
for ROLE in \
  roles/artifactregistry.writer \
  roles/run.admin \
  roles/iam.serviceAccountUser \
  roles/secretmanager.secretAccessor
do
  echo "    ${ROLE}"
  gcloud projects add-iam-policy-binding "${PROJECT}" \
    --member "serviceAccount:${SA}" \
    --role "${ROLE}" \
    --condition=None \
    --quiet > /dev/null
done

echo "==> Creating Workload Identity pool"
gcloud iam workload-identity-pools create "${POOL}" \
  --project "${PROJECT}" \
  --location global \
  --display-name "GitHub Actions" 2>/dev/null || echo "    already exists, continuing"

echo "==> Creating OIDC provider"
# The attribute-condition is the security boundary: without it, ANY repository
# on GitHub could exchange a token for these credentials.
gcloud iam workload-identity-pools providers create-oidc "${PROVIDER}" \
  --project "${PROJECT}" \
  --location global \
  --workload-identity-pool "${POOL}" \
  --display-name "GitHub OIDC" \
  --issuer-uri "https://token.actions.githubusercontent.com" \
  --attribute-mapping "google.subject=assertion.sub,attribute.repository=assertion.repository" \
  --attribute-condition "assertion.repository == '${REPO}'" 2>/dev/null \
  || echo "    already exists, continuing"

echo "==> Letting ${REPO} impersonate ${SA_NAME}"
gcloud iam service-accounts add-iam-policy-binding "${SA}" \
  --project "${PROJECT}" \
  --role roles/iam.workloadIdentityUser \
  --member "principalSet://iam.googleapis.com/projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${POOL}/attribute.repository/${REPO}" \
  --quiet > /dev/null

echo
echo "Done. The values already hardcoded in .github/workflows/ci.yml:"
echo "  workload_identity_provider: projects/${PROJECT_NUMBER}/locations/global/workloadIdentityPools/${POOL}/providers/${PROVIDER}"
echo "  service_account:            ${SA}"
echo
echo "Push a change under Sidecar/ (or run the CI workflow manually) to test it."
