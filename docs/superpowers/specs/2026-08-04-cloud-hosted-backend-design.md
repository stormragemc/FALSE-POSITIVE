# Cloud-hosted backend — design

**Date:** 4 Aug 2026
**Status:** approved, not yet implemented
**Supersedes:** the "local Python sidecar" half of the 1 Aug stack decision in
[`CONCEPT.md`](../../CONCEPT.md). The Unity-client half stands.

---

## Why

The game is going on itch.io. Today's backend is a Python process the player has to
install, feed multi-GB model downloads, and supply two of their own API keys to. That is
not a thing you can publish. The backend moves to Google Cloud Run so the shipped build is
just a Unity binary that talks to a URL.

**Decided by the team, 4 Aug:** full migration now rather than after 9 Aug. The cost is
roughly two of the five remaining days, taken out of the consistency tracker (A6) and
`DetectiveAction` — the features the pitch is actually about. Recorded here so the trade
stays visible; it is not a reason to revisit.

**Player audio now leaves the player's machine.** Explicit team decision, 4 Aug. This
inverts the privacy position recorded on 1 Aug and creates documentation work tracked in
§7.

## Decisions taken

| # | Decision | Chosen |
|---|---|---|
| 1 | Session state | **Pinned single instance** — `min-instances=1, max-instances=1`, state stays in RAM |
| 2 | STT | **Google Cloud Speech-to-Text**, replacing local `faster-whisper` |
| 3 | TTS | **ElevenLabs stays** — quality of the detective's voice is load-bearing |
| 4 | LLM billing | **Vertex AI**, replacing the AI Studio key, so GCP credits actually pay |
| 5 | Host | **Cloud Run**, one region, one service |

Budget: **$300 GCP credits**. ElevenLabs is *not* covered by it — see §6.

---

## 1. Target architecture

```
itch.io ──► Unity build
                │  HTTPS POST /turn   (PCM16 upload + X-FP-Client-Key)
                ▼
        Cloud Run  false-positive-backend
        min=1  max=1   2 vCPU / 4 GiB
        ┌────────────────────────────────────┐
        │ FastAPI — app.py, unchanged shape  │
        │   ├─ stt.py    → Google Cloud STT  │  network
        │   ├─ ser.py    → HuBERT (torch)    │  in-process, CPU
        │   ├─ llm.py    → Gemini via Vertex │  network
        │   └─ tts.py    → ElevenLabs        │  network, external vendor
        └────────────────────────────────────┘
              │                    │
       Secret Manager        Cloud Logging
       (ElevenLabs key,      + budget alert
        client key)
```

Gemini and STT authenticate as the service account via ADC — **no API key for either**.
The only secrets left are the ElevenLabs key and the client shared secret.

**The same image runs locally.** `docker run` on a laptop gives a byte-identical backend,
so the 9 Aug demo keeps a zero-network fallback at no extra engineering cost. This is the
single most valuable property of the design and must not be broken for convenience.

There are therefore **three** ways to run the backend, and they must not be confused:

| Mode | How | Who uses it |
|---|---|---|
| Cloud | Cloud Run URL in `backendBaseUrl` | itch.io players, judges |
| Local container | `docker run`, point `backendBaseUrl` at `localhost` | **demo fallback if the room's network is bad** |
| Local process | `SidecarProcessLauncher` starts `python app.py` | sidecar developers only |

Only the first two use the shipped configuration. The third stays because it is the fastest
inner loop for anyone editing `Sidecar/`; it is not a fallback and is not documented for
players.

## 2. Session state — why pinning works

`app.py:42-47` holds two things in process memory:

- `_sessions` — an `OrderedDict` of conversation history, LRU-evicted at
  `SIDECAR_MAX_SESSIONS` (32)
- `_prosody_registry` — per-session HuBERT reference centroid and tension trend

Default Cloud Run runs N ephemeral instances with separate memory, so consecutive turns
from one player can land on different containers. The detective would forget the
interrogation mid-scene, and the prosody baseline would reset — which kills the affect
system specifically, because it measures *change from how the player sounded at the start*.

Pinning to exactly one instance makes the existing in-memory code correct as written. No
code change.

**Accepted limits:**

| Limit | Consequence |
|---|---|
| One instance | Turns queue behind the single HuBERT worker (`_ser_pool`, `max_workers=1`) |
| Redeploy drops RAM | Every live session gets a blank detective. Deploy between playtests, never during |
| 32-session LRU cap | Player 33 evicts player 1's history. Raise `SIDECAR_MAX_SESSIONS` to 200 |

**Concurrency is better than first estimated.** `_stt_pool` (`app.py:37`) is also
`max_workers=1`, and today Whisper is the slowest CPU stage. Moving STT to Google removes
that stage from the CPU entirely — it becomes an awaited network call. Only HuBERT
(~111 ms measured) stays single-worker, so the pinned instance's practical ceiling is set
by a 111 ms critical section rather than a multi-second one.

**Escape hatch, designed in but not built:** history and prosody state move behind a
`SessionStore` interface with one in-memory implementation. A Firestore implementation is
then a drop-in when concurrency demands it, rather than surgery on `app.py`. Serializing
`ProsodyTracker` requires the reference centroid (float32[768]) to round-trip — out of
scope now, noted so the interface is shaped to allow it.

## 3. STT: faster-whisper → Google Cloud Speech-to-Text

`stt.py` is rewritten behind its existing contract. `transcribe()` keeps returning
`(text, elapsed_ms)`, so `app.py` changes only at the call site.

- **API:** Speech-to-Text v2, synchronous `recognize` (utterances are ≤20 s — streaming
  buys nothing and complicates the turn boundary).
- **Input:** `LINEAR16`, 16 kHz, mono — exactly the bytes Unity already uploads.
  `app.py:256` currently converts to float32 for Whisper; the PCM16 bytes go to Google
  untouched, and the float32 conversion stays because HuBERT still needs it.
- **Model:** the short-form recognizer; select the specific model at implementation and
  pin it (roadmap S7 — never let a model id float).
- **Removed:** `faster-whisper` from `requirements.txt`; the `stt.load()` warm-up in
  `lifespan`; `_stt_pool`.
- **Call shape:** `stt.transcribe` becomes async and is awaited directly in the
  `asyncio.gather` at `app.py:266` instead of going through an executor.

**Failure path:** a Google STT error must degrade exactly as a Whisper failure does today
— the turn fails with a 500 and Unity replays a canned line. Do not let a transcription
outage become an unhandled exception.

## 4. LLM: AI Studio key → Vertex AI

`llm.py:88` is `genai.Client(api_key=config.GEMINI_API_KEY)` — an AI Studio client that
bills a personal key, not the team's credits.

```python
_client = genai.Client(
    vertexai=True,
    project=config.GCP_PROJECT,
    location=config.GCP_LOCATION,
)
```

`google-genai` supports both backends behind one client, so `generate_reply` is untouched.
`GEMINI_API_KEY` leaves `config.py` and `.env.example`. Confirm `gemini-3.6-flash` is
available in the chosen region before committing to it, and pin the model revision.

This is a security improvement as well as a billing one: one fewer long-lived secret.

## 5. Security — now blocking, not backlog

A public endpoint with two paid vendors behind it is a billing incident waiting to happen.
These roadmap items are promoted to prerequisites for the first deploy.

**S4 — authentication.** Cloud Run must be `--allow-unauthenticated` (a game client cannot
do IAM), so the app enforces its own check: FastAPI middleware requiring
`X-FP-Client-Key`, compared with `hmac.compare_digest`, on `/turn` and `/session/reset`.
`/health` stays open for uptime checks.

> The key ships inside the Unity build and is therefore extractable by anyone determined.
> This is a speed bump against drive-by traffic, not a wall. Real protection is S6 and rate
> limiting. Document it honestly rather than implying the endpoint is secured.

**`/debug/last_turn` is removed.** `app.py:213` returns the last player's transcript to any
caller. Acceptable on localhost; a data leak on a public URL. Delete the endpoint and the
`_last_turn_debug` global, or gate both behind an env flag defaulting to off.

**S6 — cost ceiling.** Three layers:
1. Per-session turn cap (~40 turns) — one stuck client cannot loop forever.
2. Global daily turn counter in memory, returning 429 past the cap. Correct on a pinned
   instance precisely because there is only one.
3. A GCP **budget alert at $50 / $150 / $250** of the $300, set on day one.

**Rate limiting.** Per-IP token bucket in middleware. Cloud Armor is the better answer and
is out of scope for this week.

**Binding.** `app.py` defaults to `127.0.0.1` and Cloud Run requires `0.0.0.0` on the
injected `$PORT`. Change the default via env, and delete the docstring claim at
`app.py:5-7` that the process "must not be LAN-reachable" — it is about to be
internet-reachable, and a stale comment that contradicts the deployment is worse than none.

**Secrets.** ElevenLabs key and client key move to Secret Manager, mounted as env vars.
`config.validate()` keeps failing fast at startup. `.env` remains the local-dev path.

## 6. Container

- **Base:** `python:3.12-slim`.
- **torch:** CPU-only wheel via `--index-url https://download.pytorch.org/whl/cpu`. The
  default wheel pulls ~2.5 GB of CUDA that a Cloud Run CPU instance can never use.
- **ffmpeg:** required. `tts.py:36` decodes ElevenLabs MP3 through pydub on the fallback
  path, and pydub shells out to ffmpeg. Miss this and TTS works until the day the PCM
  endpoint 402s, then fails in production only.
- **HuBERT weights baked in.** A build step downloads `superb/hubert-base-superb-er` into
  `HF_HOME` inside the image. Runtime downloads on a scale-to-zero platform are how you get
  a 90-second first turn.
- **Entrypoint:** `uvicorn app:app --host 0.0.0.0 --port $PORT`. The `__main__` block and
  the `--parent-pid` watchdog (`app.py:366`) are local-dev only and stay for that purpose.
- **Registry:** Artifact Registry. Deploy by `gcloud run deploy` for now; CI is not worth
  the day this week.

### Cost, against $300

| Item | Rough monthly | Notes |
|---|---|---|
| Cloud Run, pinned always-on | tens of dollars | Idle CPU is billed at a reduced rate; **price it properly before deploying** |
| Google STT | ~$0.10 per 5-min session | ~$30 for 300 sessions |
| Gemini Flash via Vertex | small | Flash is cheap at this token volume |
| **ElevenLabs** | **not on GCP credits** | See below |

**Turn the service off after judging.** An always-on pinned instance with no budget alert
is the standard way to wake up to an empty balance.

**ElevenLabs is the real ceiling.** The free tier is ~10k characters/month; a detective
line is ~200 characters, so roughly **50 lines per month across all players** — about two
playthroughs. Fine for the 9 Aug demo. It does not survive a public itch.io launch.

Team chose to keep ElevenLabs for voice quality. The mitigation is that `tts.synthesize()`
is already a single function with a stable `(pcm, rate, channels, ms)` contract, so
swapping in Google Cloud TTS — which *is* covered by the credits — is a one-file change if
the wall is hit. **No work now; documented so the option stays cheap.**

## 7. Unity client

Small, because `InterrogationConfig.cs:46` is the only place the backend's location is
known and `InterrogationSidecarClient` references it twice (lines 31 and 82).

- **`InterrogationConfig`** gains `backendBaseUrl` (a full `https://…` string). `sidecarHost`
  and `sidecarPort` stay for local dev; `SidecarBaseUrl` returns `backendBaseUrl` when it is
  non-empty and falls back to `http://{host}:{port}` otherwise. One property, both modes.
- **`autoLaunchSidecar` defaults to `false`.** `SidecarProcessLauncher.cs` is kept, not
  deleted — see the two local paths below.
- **Auth header** — `req.SetRequestHeader("X-FP-Client-Key", …)` on both requests in
  `InterrogationSidecarClient`.
- **Error copy** — `app.py`'s client-facing string "Voice service unreachable. Is the
  sidecar running?" (`InterrogationSidecarClient.cs:92`) becomes a network-failure message.
  Roadmap fault **F6** changes meaning from *launch failed* to *backend unreachable*, and
  needs a retry rather than a dead end.
- **Timeout** — 60 s stays; it now covers network latency instead of first-run model loads.

WebGL is explicitly **out of scope** for 9 Aug. `Microphone` is unavailable in WebGL and
needs JS interop; that is a separate piece of work. Desktop build on itch.io first.

## 8. Documentation that becomes false

The honesty rule in [`CONCEPT.md`](../../CONCEPT.md) makes these blocking for submission,
not cleanup. Each of these currently asserts something the deployed system contradicts:

| File | Claim to fix |
|---|---|
| `CONCEPT.md` | The 1 Aug STT row: *"Player audio never leaves the machine — this is stronger than the original privacy position."* Now false. |
| `Sidecar/config.py:3-4` | *"Both API keys live only here … never sent to or stored by the Unity client."* Still true of the keys; rewrite around the service account and the new client key. |
| `Sidecar/app.py:5-7` | *"Binds to 127.0.0.1 only … must not be LAN-reachable."* |
| `README.md` | Setup instructions assume a local Python install. |
| `Sidecar/README.md` | Same. |
| `docs/ROADMAP.md` §9 | Currently frames local-backend as a decision with three options. It is decided; rewrite as the migration record. |
| **`PRIVACY.md`** (new) | What audio is sent, to whom, retained how long, and that no recording is stored server-side. Required by concept non-negotiable: *"Voice data handling is stated plainly to the player and in the README."* |

The deck must not claim on-device privacy. This also touches the **AI security S8** row.

## 9. Testing

The existing 47 tests must stay green. Specifically:

- `features_classical` tests import no models and must remain runnable offline — do not let
  a Google client import creep into that path.
- **New:** `stt` adapter test against a faked Google response, asserting the
  `(text, elapsed_ms)` contract and that an API error propagates as a handled turn failure.
- **New:** middleware test — missing key → 401, wrong key → 401, correct key → passes;
  `/health` reachable without a key.
- **New:** cost-cap test — turn N+1 past the session cap returns 429.
- **Manual:** deploy, then run a full interrogation against the Cloud Run URL and record
  end-to-end turn latency. Compare against the local baseline. If the network round trip
  pushes a turn past ~4 s, that is a finding for the demo plan, not a surprise on stage.

## 10. Order of work

1. `SessionStore` interface + in-memory implementation (keeps §2's escape hatch open)
2. `stt.py` → Google Cloud STT; drop `faster-whisper`
3. `llm.py` → Vertex client; drop `GEMINI_API_KEY`
4. Auth middleware; remove `/debug/last_turn`; cost caps
5. Dockerfile; verify the image runs locally end-to-end **before** any cloud step
6. Artifact Registry + Cloud Run deploy; Secret Manager; budget alert
7. Unity: `backendBaseUrl`, auth header, `autoLaunchSidecar = false`, F6 copy
8. Docs: `PRIVACY.md`, README, `CONCEPT.md`, `ROADMAP.md` §9

Step 5 is the checkpoint. If the container does not run a full turn on a laptop, nothing
after it is worth attempting.

## Out of scope

WebGL build · Firestore session store · Cloud Armor · CI/CD pipeline · autoscaling ·
multi-region · swapping TTS vendors · anything in the roadmap's critical path (A5, A6,
`DetectiveAction`, notebook panel) — untouched by this work and still the priority once it
lands.
