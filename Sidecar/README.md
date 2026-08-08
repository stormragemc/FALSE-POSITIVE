# Interrogation backend

The FastAPI service that does everything the game's voice loop needs outside
Unity: speech-to-text, speech emotion recognition, the officer's LLM reply, and
the officer's TTS voice.

As of 6 Aug 2026 the Cloud Run migration is **deployed and verified end to end**
at `https://false-positive-backend-465469192069.us-central1.run.app`. A build
talks to that URL and needs no Python, model downloads, or player-supplied keys.
Unity wiring is still outstanding — see Task 8. See
[`../docs/ROADMAP.md` §9](../docs/ROADMAP.md#9-distribution-hosted-backend-migration-record)
for why, and [`../docs/PRIVACY.md`](../docs/PRIVACY.md) for what that means for
player audio.

Unity still never holds a vendor API key. It holds one shared client key that
gates this service (see [Auth](#auth)); the ElevenLabs key and the Google
credentials live in Secret Manager and are injected into the container.

See [`../docs/HUBERT_ORCHESTRATION_PLAN.md`](../docs/HUBERT_ORCHESTRATION_PLAN.md)
for the primary-source HuBERT research, model limits, reviewed architecture, and
reliability policy. This file is the practical setup, run, and deploy steps.

---

## Three ways to run this

| | You want to | You need |
|---|---|---|
| **1** | **Play the hosted game** | Available after Task 7 deploys and the Unity asset receives its URL/key. |
| **2** | **Run the backend locally in Docker** | Docker, `gcloud`, the ElevenLabs key. This is the demo fallback if Cloud Run is down or the room has no internet worth trusting. |
| **3** | **Develop on the backend** | Python 3.10–3.12, a venv, `gcloud`, `.env`. |

Modes 2 and 3 both need a Google Cloud project with Speech-to-Text and Vertex AI
enabled, because that is where STT and Gemini now live.

---

## 1. Play the hosted game (after Task 7)

Nothing will need installing on a player's machine. At this checkpoint,
`Assets/_Project/Config/InterrogationConfig.asset` serializes empty hosted URL
and client-key fields, and `autoLaunchSidecar` is off. Task 7 must deploy the
service; Task 8 then fills those two values and runs the Unity play-test.

To point the game at a *local* backend instead, clear `backendBaseUrl` in that
asset and it falls back to `sidecarHost`/`sidecarPort`.

---

## 2. Run the backend locally in Docker

This runs the same image that Cloud Run runs, so it is the honest local test.

```bash
gcloud auth application-default login   # once
cd Sidecar && docker build -t false-positive-backend:dev .
```

The HuBERT weights are baked into the image at build time, so the first build
takes a few minutes and every start after that is fast. Then:

```bash
docker run --rm -p 8080:8080 \
  -e GCP_PROJECT="$(gcloud config get-value project)" \
  -e GCP_LOCATION=global \
  -e FP_CLIENT_KEY=local-dev-key \
  -e ELEVENLABS_API_KEY="$ELEVENLABS_API_KEY" \
  -e ELEVENLABS_VOICE_ID="$ELEVENLABS_VOICE_ID" \
  -e GOOGLE_APPLICATION_CREDENTIALS=/adc/application_default_credentials.json \
  -v "$HOME/.config/gcloud:/adc:ro" \
  false-positive-backend:dev
```

Check it in a second terminal — these three are the gate, and a failure here is
a failure worth fixing before anything is deployed:

```bash
curl -s localhost:8080/health | python3 -m json.tool
# expect: status ok, models_loaded true, prosody available on cpu

curl -s -o /dev/null -w '%{http_code}\n' -X POST localhost:8080/turn -F session_id=probe
# expect: 401 — the auth middleware is doing its job

curl -s -X POST localhost:8080/turn \
  -H 'x-fp-client-key: local-dev-key' \
  -F session_id=probe | python3 -m json.tool
# expect: ok true, with reply_text and audio_b64 — the officer's opening line
```

---

## 3. Develop on the backend

1. Install **Python 3.10–3.12** and make sure it's on PATH. On this machine the
   interpreter is `python3` — `python` is not on PATH.
2. Install **ffmpeg** and make sure it's on PATH (used to decode MP3 if your
   ElevenLabs plan doesn't grant PCM output directly — see `tts.py`).
3. `gcloud auth application-default login`. Google STT and Vertex both
   authenticate through Application Default Credentials; **there is no
   `GEMINI_API_KEY` any more.**
4. Copy `.env.example` to `.env` and fill it in (see the table below).
5. `python3 -m venv .venv && .venv/bin/pip install -r requirements.txt`, then
   `.venv/bin/python -m uvicorn app:app --port 8080`.

On Windows, `run_sidecar.bat` still does step 5 in one go — it creates/reuses a
venv, installs `requirements.txt`, and serves on port `8080`. Leave the
console window open; Python tracebacks show up there. Unity no longer starts it
for you: `autoLaunchSidecar` is off by default now that the real backend is
hosted.

### Environment

| Variable | Required | Notes |
|---|---|---|
| `GCP_PROJECT` | yes | Bills Vertex AI (Gemini) and Speech-to-Text. No API key — ADC. |
| `GCP_LOCATION` | — | Defaults to `global`. Must be a region that serves the pinned Gemini model. |
| `FP_CLIENT_KEY` | yes | The shared key every request must present. Empty means *deny everything*, never *allow everything*. |
| `ELEVENLABS_API_KEY` | yes | From elevenlabs.io. Free tier is enough for light testing only. |
| `ELEVENLABS_VOICE_ID` | yes | See the voice note below — not just any stock voice. |
| `STT_MODEL` | — | Defaults to `long`. Pinned deliberately (roadmap S7); do not swap in a floating alias. `short` cleans disfluencies, which this pipeline needs — see the filler-word note below. |
| `STT_LANGUAGE` | — | Defaults to `en-US`. |
| `MAX_TURNS_PER_SESSION` | — | Defaults to 40. |
| `MAX_TURNS_PER_DAY` | — | Defaults to 2000. The budget ceiling for the whole service. |
| `SIDECAR_MAX_SESSIONS` | — | Bounds how many sessions the in-memory store holds. |
| `MAX_TURN_REQUEST_BYTES` | — | Defaults to 700000 and is capped below 1 MB; enforced before multipart parsing. |
| `SESSION_IDLE_TTL_SECONDS` | — | Defaults to 3600. Text and affect state expire after one idle hour. |
| `TURN_DEADLINE_SECONDS` | — | Defaults to 50 and is capped at 55, below Unity's 60-second timeout. |

Everything else is optional and documented in `.env.example`.

**The ElevenLabs voice is not free choice.** The free tier only grants API
access to voices you created yourself, which excludes the Voice Library *and*
the "premade" voices (Adam, Rachel, Bill, …) shown in most walkthroughs —
calling `/turn` with one of those returns `402 payment_required`. Run
`python3 tools/probe_tts.py` to find a voice ID your account can actually use
over the API; it makes a real `convert()` call per candidate rather than
trusting the visible voice list, since a voice being visible isn't the same as
it being usable. **Already probed on this account:** `XrExE9yKIg1WjnnlVkGX`
(Matilda) is one of 21 usable voices found and is verified end to end via
`tools/probe_full_turn.py` (real LLM → TTS → PCM audio, no fallback). It is also
a female voice, matching Detective Mara Voss, the officer persona in `llm.py`.

---

## Auth

Every endpoint except `/health` requires the header `x-fp-client-key`, matched
against `FP_CLIENT_KEY`. A missing or empty expected key **denies every
request** — it does not fall open to "no key configured, allow all". A missing
or wrong supplied key returns `401`.

This key is shipped inside the Unity build, so **it is not a secret from anyone
who downloads the game.** It is a bar against drive-by traffic hitting a public
URL, not a security boundary. The turn caps below constrain only one
uninterrupted process lifetime; they do not bound the public bill across
restarts. Do not paste the live value into chat logs or the deck.

## Cost ceiling

`limits.py` counts turns **on admission, not on success** — a client retrying a
failing turn still pays Google, Vertex, and ElevenLabs on every attempt. Over
either cap, `/turn` returns `429` with a reason of
`session_turn_limit_reached` or `daily_turn_budget_exhausted`, and Unity shows
the corresponding message.

These in-memory counters are a best-effort checkpoint guard, **not a durable
billing ceiling**: a restart resets them, and a downloadable client can mint
new session IDs. Before public deployment, Task 7 must add a provider-side hard
quota plus a durable per-device/client limiter. Pinning to one instance is
still required for conversation and prosody state; see the deploy note below.

The application abandons a turn after 50 seconds and never commits late state,
but a timed-out synchronous vendor call may continue in its worker thread. A
public launch still needs client-generated turn IDs with response replay and
provider SDK deadlines so a lost response cannot become a double-billed retry.

---

## Deploying

### Redeploying after a code change

The common case. Build, push, deploy:

```bash
PROJECT="$(gcloud config get-value project)"
IMAGE="us-central1-docker.pkg.dev/${PROJECT}/false-positive/backend:v1"

cd Sidecar && docker build --platform linux/amd64 -t "$IMAGE" . && docker push "$IMAGE"
gcloud run deploy false-positive-backend \
  --image "$IMAGE" --region us-central1 --max-instances 1 --concurrency 1
```

`--platform linux/amd64` matters on an Apple Silicon laptop — Cloud Run will not
run an arm64 image.

Then re-verify, exactly as after the first deploy:

```bash
URL="$(gcloud run services describe false-positive-backend --region us-central1 --format='value(status.url)')"
curl -s "$URL/health" | python3 -m json.tool
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$URL/turn" -F session_id=probe
curl -s -X POST "$URL/turn" -H "x-fp-client-key: $(gcloud secrets versions access latest --secret=fp-client-key)" -F session_id=probe | python3 -m json.tool
```

Expected: `status: ok` · `401` · `ok: true`.

**Those three never touch STT.** The authenticated probe sends no audio, so
`stt_ms` is `0` and prosody reports `opening_turn`. To exercise the whole chain,
post a real 16 kHz mono WAV — on macOS you can make one without a mic:

```bash
say -o probe.aiff "I was at home all evening. I never went near the warehouse."
afconvert -f WAVE -d LEI16@16000 -c 1 probe.aiff probe.wav

curl -s -X POST "$URL/turn" \
  -H "x-fp-client-key: $(gcloud secrets versions access latest --secret=fp-client-key)" \
  -F session_id=sttprobe -F sample_rate=16000 -F audio=@probe.wav | python3 -m json.tool
```

Expected: `stt_ms > 0` with a transcript matching the spoken line, and
`prosody.available: true`. Reference timings from 6 Aug — STT 544 ms, affect
519 ms, LLM 1243 ms, TTS 559 ms, **2351 ms total**. Anything past ~4 s is a
finding for the demo plan, not a curiosity.

Synthesized speech is fine for proving the wiring, but do not read its affect
output as a baseline — `say` audio classified as 79% `angry` on 6 Aug.

### First-time setup of a fresh project

Recorded so this is not archaeology the next time someone needs it.

```bash
# APIs
gcloud services enable \
  run.googleapis.com \
  artifactregistry.googleapis.com \
  speech.googleapis.com \
  aiplatform.googleapis.com \
  secretmanager.googleapis.com

# Budget alert FIRST, before anything can spend. $50 / $150 / $250 of $300.
gcloud billing budgets create \
  --billing-account="$(gcloud beta billing projects describe "$(gcloud config get-value project)" --format='value(billingAccountName)' | cut -d/ -f2)" \
  --display-name="false-positive" --budget-amount=300USD \
  --threshold-rule=percent=0.17 --threshold-rule=percent=0.5 --threshold-rule=percent=0.83

# Secrets
printf '%s' "$ELEVENLABS_API_KEY" | gcloud secrets create elevenlabs-api-key --data-file=-
printf '%s' "$ELEVENLABS_VOICE_ID" | gcloud secrets create elevenlabs-voice-id --data-file=-
python3 -c "import secrets; print(secrets.token_urlsafe(32))" | tr -d '\n' | gcloud secrets create fp-client-key --data-file=-

# Registry
gcloud artifacts repositories create false-positive --repository-format=docker --location=us-central1
gcloud auth configure-docker us-central1-docker.pkg.dev
```

Then build and push as above, and deploy with the full flag set:

```bash
gcloud run deploy false-positive-backend \
  --image "$IMAGE" \
  --region us-central1 \
  --allow-unauthenticated \
  --min-instances 1 \
  --max-instances 1 \
  --concurrency 1 \
  --cpu 2 \
  --memory 4Gi \
  --timeout 60 \
  --set-env-vars "GCP_PROJECT=${PROJECT},GCP_LOCATION=global,SIDECAR_MAX_SESSIONS=200" \
  --set-secrets "ELEVENLABS_API_KEY=elevenlabs-api-key:latest,ELEVENLABS_VOICE_ID=elevenlabs-voice-id:latest,FP_CLIENT_KEY=fp-client-key:latest"
```

**Grant the runtime service account its roles _before_ that deploy, not after.**
Derive it from the project number — the default compute service account exists
before any Cloud Run service does, so `gcloud run services describe` has nothing
to read on a first-time setup:

```bash
SA="$(gcloud projects describe "$PROJECT" --format='value(projectNumber)')-compute@developer.gserviceaccount.com"

# Secret Manager — per secret, so the account can read these three and nothing else
for s in elevenlabs-api-key elevenlabs-voice-id fp-client-key; do
  gcloud secrets add-iam-policy-binding "$s" \
    --member="serviceAccount:${SA}" --role=roles/secretmanager.secretAccessor
done

# Google STT and Vertex authenticate as this account — there is no API key
gcloud projects add-iam-policy-binding "$PROJECT" --member="serviceAccount:${SA}" --role=roles/speech.client
gcloud projects add-iam-policy-binding "$PROJECT" --member="serviceAccount:${SA}" --role=roles/aiplatform.user
```

> **Learned the hard way, 6 Aug.** Without `secretAccessor` the deploy fails with
> `Permission denied on secret: .../elevenlabs-api-key/versions/latest` and the
> revision never serves traffic. Granting it afterwards does **not** retry the
> failed revision — force a new one with
> `gcloud run services update false-positive-backend --region us-central1 --update-env-vars "GCP_LOCATION=global"`.

> **`--max-instances 1` is load-bearing, not a cost tweak.** Session history and
> each player's prosody baseline live in the process's memory. A second instance
> means half a player's turns land somewhere with no memory of the interrogation
> — the detective forgets what was said and the affect baseline resets mid-scene.
> If this ever needs to scale, the session store and the turn limiter move to a
> shared backend *in the same change*, not after it.
>
> `--allow-unauthenticated` is required because a game client cannot do IAM. The
> app's own middleware is the gate.
>
> **`--concurrency 1` is also deliberate for this checkpoint.** HuBERT is
> serialized through one CPU worker. Higher request concurrency would only
> queue full audio buffers and increase memory pressure until measured capacity
> supports a larger value.

**Service URL:** `https://false-positive-backend-465469192069.us-central1.run.app`
(project `false-positive-504516`, region `us-central1`, first deployed 6 Aug 2026)

> **⚠ The live service is at `--concurrency 160`, not 1.** The deploy command used
> on 6 Aug omitted the flag, so it took Cloud Run's default. `--min-instances` and
> `--max-instances` are correctly 1. See
> [`../docs/ROADMAP.md` §9](../docs/ROADMAP.md#9-distribution-hosted-backend-migration-record)
> — it is an open decision, not an oversight to paper over.

---

## Affect orchestration controls

The default HuBERT checkpoint predicts only neutral, happy, angry, and sad and
is not a fear or deception detector. The backend retains its full probability
distribution and combines it with uncertainty, pauses, energy/pitch variation,
hidden-state change, response onset, and an in-memory early-session reference.
Raw cosine distance remains debug data; actionable change is normalized to the
spread of that player's early reference turns. Only a successfully synthesized
turn is committed, preventing TTS retries from double-counting it. Low-quality
readings are visible in debug data but suppressed from Gemini, and witness text
is escaped into a separate trust block so it cannot imitate the sensor marker.
Only HuBERT is bounded, to the `HUBERT_MAX_SECONDS` prefix; STT and the
classical features both receive the complete accepted utterance. The windows
were identical until 8 Aug, when HuBERT was measured as the slowest stage of
the whole turn — 1934ms against STT's 1336ms on a 9.6s answer, growing with
input length. Its window shrank to 8s; the classical DSP kept the full buffer
because it costs ~37ms on a 20s one, so bounding it bought nothing and cost the
signal. A turn longer than the affect window is flagged
`affect_window_truncated`, meaning the emotion label was formed from the head of
the answer. Speech-rate comparison is unaffected: transcript and classical
features now cover the same full utterance, so the pacing clause survives on
exactly the long answers where it reads most.

All controls are optional and documented in `.env.example`. The quickest
rollback is `PROSODY_ENABLED=false`: STT, Gemini, and TTS continue normally
without loading HuBERT. `/health` reports whether affect is enabled and loaded,
the exact checkpoint, device, orchestration version, and a bounded load-error
category. A HuBERT load or inference failure never cancels a dialogue turn.
`HUBERT_DEVICE=auto` chooses CUDA when present and CPU otherwise; Cloud Run runs
it on CPU. MPS is an explicit opt-in because tested Apple builds can terminate
during model warm-up instead of raising a recoverable Python exception.
`PROSODY_MIN_CONFIDENCE` is capped at `0.75`, matching the maximum confidence
the conservative policy can emit.
An overridden `HUBERT_MODEL_ID`/`HUBERT_MODEL_REVISION` pair must expose exactly
the neutral, happy, angry, and sad labels; startup rejects incompatible
classifiers instead of silently zeroing Unity's fixed four-label debug DTO.
The container loads only its baked revision and requires safetensors weights.

### Why `STT_MODEL` defaults to `long`

Verbal hesitation has exactly one route to the interrogator, and it is the
transcript. The four IEMOCAP classes have no nervous bucket, so the label cannot
carry it. Neither can the pacing gates: `_count_long_pauses` counts *silent* gaps
of 0.40s or more, and a filler word is voiced — filling the gap is what it is
for. Measured 6 Aug on the live service, "…store, uh, around nine" produced
`long_pause_count` 0 and *"No long internal pause stood out"*, while the same
sentence with 1.0s of true silence produced `long_pause_count` 1 and *"There were
notable internal pauses"*. Filling a hesitation also *raises* speech ratio (0.764
against 0.579), so on the acoustic channel a halting witness reads as more fluent,
not less.

That makes the choice of recognizer load-bearing. `short` cleans disfluencies for
readability: it dropped the filler from "Um, I was at the store", and returned an
empty transcript for a halting 8.5s clip on three consecutive attempts — a length
sweep confirmed it handles a fluent 10.8s clip fine, so it is the disfluencies it
bails on, not the duration. `long` keeps them at the same latency. `chirp_2` is
the most verbatim of the three but does not exist at `global`, needs a regional
`api_endpoint` in `stt.py`, and roughly doubles STT latency.

One wrinkle when reading transcripts: Google normalizes the spelling. "um" stays
"um", but "uh" comes back as "ah". The disfluency survives; the exact token does
not.

---

## Testing

The unit suite uses generated signals and fakes the Google clients, so it needs
no API keys, no network access, and no model download. Run it from `Sidecar/`:

```bash
python3 -m pytest tests/ -q
```

To inspect a real audio file through STT and the richer HuBERT observation (this
does call Google and does load HuBERT):

```bash
python3 tools/probe_stt_ser.py tools/sample.pcm
```

`sample.pcm` must be raw 16-bit little-endian PCM, mono, 16kHz (no WAV header) —
`Sidecar/tools/sample.pcm` is exactly that, a short synthesized utterance ready
to use directly. To make a new one: record a short WAV and strip its 44-byte
header, or `ffmpeg -i sample.wav -f s16le -ar 16000 -ac 1 sample.pcm`.

Decode the returned `audio_b64` field back to a file and play it to confirm the
whole STT → emotion → LLM → TTS chain works end to end, and note the `*_ms`
timing fields — that number is what decides whether the pipeline's overall
latency is acceptable.

POST with no `audio` part (or an empty one) triggers the officer's scripted
opening line instead of running STT/SER — this is what Unity calls at scene
start.

## Endpoints

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/health` | no | Launch status plus nested HuBERT availability/model/device |
| `POST` | `/turn` | yes | Main pipeline; accepts optional `onset_delay_ms`, returns additive `prosody` |
| `POST` | `/session/reset` | yes | Clears conversation history and the prosody reference; paid-turn accounting is intentionally retained |

A reset also invalidates state commits from any turn that was already in flight,
so an old history/reference cannot reappear after the reset completes.

## Troubleshooting

- **Every request comes back `401`** — the client key doesn't match. Compare
  `backendClientKey` in the Unity config against
  `gcloud secrets versions access latest --secret=fp-client-key`. A `401` with
  no key configured on the server is also correct behaviour: auth fails closed.
- **`429` with `daily_turn_budget_exhausted`** — this process hit
  `MAX_TURNS_PER_DAY`. The checkpoint guard is working, but it resets with the
  process; inspect provider quota and billing controls before raising it.
- **"missing required environment variable" and the process exits immediately**
  — `.env` doesn't exist or is missing a key. Copy `.env.example` to `.env`.
  `GCP_PROJECT` is required now; `GEMINI_API_KEY` no longer exists.
- **STT returns empty text, or Vertex returns a permission error** — ADC isn't
  set up locally (`gcloud auth application-default login`), or the runtime
  service account is missing `roles/speech.client` / `roles/aiplatform.user`.
- **First `/turn` after a deploy is slow** — HuBERT is warming up. It is baked
  into the image, so this is load time, not download time, and only happens once
  per instance.
- **The detective forgot the last five minutes** — check the service is still at
  `--max-instances 1`. This is what that flag prevents.
- **A turn's `prosody.available` is false** — inspect that `/turn` response's
  `prosody.reliability_reason`: `prosody_disabled`, `hubert_load_failed`,
  `hubert_inference_failed`, and `feature_extraction_failed` name the degraded
  stage. For startup state, `/health` uses `prosody.error`: `loading`,
  `disabled`, empty when ready, or the bounded exception class from a failed
  load.
- **TTS request fails with `402 payment_required`** — the configured voice isn't
  API-usable on your plan (see the voice note above). Run
  `python3 tools/probe_tts.py` to find one that is; `tts.py` surfaces this as a
  clear one-line error naming the cause rather than a raw HTTP header dump.
- **TTS request fails some other way** — check the API key has quota remaining
  and hasn't expired. The free tier's ~10k characters/month is roughly 50
  detective lines.
- **`pydub`/ffmpeg errors** — ffmpeg isn't on PATH; this only matters if your
  ElevenLabs plan doesn't grant PCM output (see `tts.py`'s fallback path).
- **Windows install fails with an `OSError`/"No such file or directory"
  mentioning a very long path under `elevenlabs\...`** — Windows' default
  260-character `MAX_PATH` limit, tripped by that package's generated file names
  combined with this project's already-deep folder path. `run_sidecar.bat` works
  around this by creating its venv at `%SystemDrive%\fpsc_venv` instead of
  `Sidecar\.venv` — a short, fixed path outside the project tree. If you'd
  rather keep the venv local, enable "Win32 long paths" once
  (Settings → search "long paths" → toggle on, or
  `HKLM\SYSTEM\CurrentControlSet\Control\FileSystem\LongPathsEnabled = 1` via an
  elevated PowerShell, no reboot needed on current Windows builds) and change
  `VENV_DIR` in `run_sidecar.bat` back to `.venv`.
