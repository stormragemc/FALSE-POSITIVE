#!/usr/bin/env bash
#
# Build, push and deploy the interrogation backend to Cloud Run.
#
# Usage:   ./Sidecar/deploy.sh            # deploy current HEAD
#          MIN_INSTANCES=0 ./deploy.sh    # allow scale-to-zero (see below)
#          ALLOW_DIRTY=1 ./deploy.sh      # deploy with uncommitted changes
#          REGION=asia-southeast1 ./deploy.sh   # deploy nearer the player
#
# Moving REGION: measured 8 Aug from Jakarta, round-trip to us-central1 was
# ~250ms, and because TCP slow-start needs several round trips to open the
# window for a 300KB upload and a 216KB reply, that RTT is paid many times —
# ~1.5s of the turn was transport, not work. A region close to the players is
# the single largest remaining win and needs no code change.
#
# It does mint a NEW service URL. After the first deploy to a new region:
#   1. read the URL this script prints at the end;
#   2. set it as `sidecarBaseUrl` on the InterrogationConfig asset in Unity;
#   3. re-run Sidecar/tools/measure_turn_latency.py --url <new-url> to confirm;
#   4. delete the old service once the new one is verified, or it keeps billing
#      a warm min-instances container:
#      gcloud run services delete false-positive-backend --region us-central1
# Artifact Registry deliberately does NOT move with it — the image is pulled
# once per cold start, which min-instances 1 makes rare, and a second registry
# is another thing to keep in sync.
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
# Pinned to where the repository actually exists, independent of REGION above.
AR_LOCATION="${AR_LOCATION:-us-central1}"
REPO="${AR_LOCATION}-docker.pkg.dev/${PROJECT}/false-positive/backend"

# min-instances 1 keeps one container warm. Cold starts on this image take
# longer than a request timeout (torch + HuBERT load), so scale-to-zero
# surfaces as an HTTP 500 on the first request after an idle gap. The cost is
# an always-on instance billed against the $300 credit — set MIN_INSTANCES=0
# between demos if that matters more than the first-request failure.
MIN_INSTANCES="${MIN_INSTANCES:-1}"

# Cloud Run throttles a warm instance's CPU to near zero between requests, so
# the first moments of a turn run while the container is still ramping back up.
# NO_CPU_THROTTLING=1 keeps the CPU allocated always, which removes that ramp —
# but it also switches the instance to instance-based billing, i.e. paying for
# an idle container around the clock instead of only while it works. Opt-in
# rather than default because that is a real bill against the $300 credit, and
# worth turning on for a demo day.
THROTTLE_FLAG="--cpu-throttling"
if [ "${NO_CPU_THROTTLING:-0}" = "1" ]; then
  THROTTLE_FLAG="--no-cpu-throttling"
fi

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
#
# --platform is load-bearing on a laptop and a no-op in CI, which is exactly why
# it was missing: the Actions runner is amd64, so the default was always right
# there. On Apple Silicon the default is arm64, and Cloud Run rejects it only at
# the very end, after the whole build and push are already paid for:
#   Container manifest type 'application/vnd.oci.image.index.v1+json'
#   must support amd64/linux
# --provenance=false drops the attestation manifest that nothing here reads and
# that makes the index harder to reason about. The cross-build runs under
# emulation and is slow; that is a laptop-only cost.
docker build --platform linux/amd64 --provenance=false -t "${IMAGE}" -t "${REPO}:v1" .
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
  "${THROTTLE_FLAG}" \
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
