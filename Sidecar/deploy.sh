#!/usr/bin/env bash
#
# Build, push and deploy the interrogation backend to Cloud Run.
#
# Usage:   ./Sidecar/deploy.sh            # deploy current HEAD
#          MIN_INSTANCES=0 ./deploy.sh    # allow scale-to-zero (see below)
#          ALLOW_DIRTY=1 ./deploy.sh      # deploy with uncommitted changes
#
# Why this exists: the service was deployed for two days against the mutable
# tag ":v1", so "what is actually running?" was unanswerable — and the running
# container turned out to predate the Cabin_v2 story rewrite by 4.5 hours,
# still interrogating players about Halden's Convenience Store. Every image
# this script pushes carries an immutable git-SHA tag so that can't recur.

set -euo pipefail

PROJECT="${PROJECT:-false-positive-504516}"
REGION="${REGION:-us-central1}"
SERVICE="${SERVICE:-false-positive-backend}"
REPO="us-central1-docker.pkg.dev/${PROJECT}/false-positive/backend"

# min-instances 1 keeps one container warm. Cold starts on this image take
# longer than a request timeout (torch + HuBERT load), so scale-to-zero
# surfaces as an HTTP 500 on the first request after an idle gap. The cost is
# an always-on instance billed against the $300 credit — set MIN_INSTANCES=0
# between demos if that matters more than the first-request failure.
MIN_INSTANCES="${MIN_INSTANCES:-1}"

cd "$(dirname "$0")"

# --- Work out an honest tag -------------------------------------------------
SHA="$(git rev-parse --short HEAD)"
if [ -n "$(git status --porcelain -- . ../Assets ../Packages 2>/dev/null)" ]; then
  if [ "${ALLOW_DIRTY:-0}" != "1" ]; then
    echo "ERROR: uncommitted changes. Commit them, or re-run with ALLOW_DIRTY=1." >&2
    git status --short -- . ../Assets ../Packages >&2
    exit 1
  fi
  SHA="${SHA}-dirty"
fi

IMAGE="${REPO}:${SHA}"
echo "==> Deploying ${SHA}"

# --- Build and push ---------------------------------------------------------
# Both tags point at the same digest: the SHA tag is the durable answer to
# "what is running", ":v1" stays only so older docs/commands keep working.
docker build -t "${IMAGE}" -t "${REPO}:v1" .
docker push "${IMAGE}"
docker push "${REPO}:v1"

# --- Deploy -----------------------------------------------------------------
# Deployed by digest, not tag, so this revision can never be silently
# repointed by a later ":v1" push.
DIGEST="$(docker inspect --format='{{index .RepoDigests 0}}' "${IMAGE}" 2>/dev/null || echo "${IMAGE}")"

gcloud run deploy "${SERVICE}" \
  --image "${DIGEST}" \
  --region "${REGION}" \
  --allow-unauthenticated \
  --min-instances "${MIN_INSTANCES}" \
  --max-instances 1 \
  --concurrency 1 \
  --cpu 2 \
  --memory 4Gi \
  --timeout 60 \
  --labels "git-sha=${SHA//[^a-z0-9_-]/-}" \
  --set-env-vars "GCP_PROJECT=${PROJECT},GCP_LOCATION=global,SIDECAR_MAX_SESSIONS=200" \
  --set-secrets "ELEVENLABS_API_KEY=elevenlabs-api-key:latest,ELEVENLABS_VOICE_ID=elevenlabs-voice-id:latest,FP_CLIENT_KEY=fp-client-key:latest"

# --- Verify -----------------------------------------------------------------
# A deploy that returns 200 on /health only proves the process booted. The
# check that actually matters is whether the persona in the running container
# is the current one, which is exactly what went unnoticed before.
URL="$(gcloud run services describe "${SERVICE}" --region "${REGION}" --format='value(status.url)')"
KEY="$(gcloud secrets versions access latest --secret=fp-client-key)"

echo "==> health"
curl -fsS -m 60 -o /dev/null -w '    HTTP %{http_code} in %{time_total}s\n' "${URL}/health"

echo "==> turn (checking the live persona)"
REPLY="$(curl -fsS -m 90 -X POST "${URL}/turn" \
  -H "x-fp-client-key: ${KEY}" \
  -F session_id="deploy-probe-${SHA}" \
  | python -c 'import json,sys; print(json.load(sys.stdin).get("reply_text",""))')"

echo "    ${REPLY}"
if printf '%s' "${REPLY}" | grep -qi "halden\|convenience store\|mara voss"; then
  echo "ERROR: the deployed container is still running the pre-Cabin_v2 persona." >&2
  exit 1
fi

echo "==> deployed ${SHA} -> ${URL}"
