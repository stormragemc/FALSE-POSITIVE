# Test bench

A browser page for talking to the hosted backend by hand, so the affect channel
can be exercised before any of it is wired into Unity. Throwaway tooling — it is
not part of the game build and nothing in `Assets/` or `Sidecar/` depends on it.

```
python3 testbench/server.py
open http://localhost:8000
```

Hold the button (or hold space), talk, release. The page records at 16 kHz mono,
posts a WAV to `/turn`, plays the reply, and shows the whole prosody block:
class probabilities, arousal, tension, confidence, calibration state, and the
HuBERT instability/baseline numbers.

## Why there is a proxy

`server.py` is a ~150-line stdlib proxy sitting in front of Cloud Run. Three
reasons it exists rather than the page calling the backend directly:

1. The backend has no CORS middleware, and the auth middleware answers the
   preflight `OPTIONS` with `401` before CORS could apply. A browser on
   `localhost` cannot reach it.
2. Adding `CORSMiddleware` means editing `Sidecar/app.py`, which stream A owns
   exclusively.
3. The client key stays on your machine. The browser never receives it.

## Config

| Variable | Default |
| --- | --- |
| `FP_BACKEND_URL` | the deployed Cloud Run URL |
| `FP_CLIENT_KEY` | unset — falls back to `gcloud secrets versions access latest --secret=fp-client-key` |
| `FP_CLIENT_KEY_SECRET` | `fp-client-key` |
| `TESTBENCH_PORT` | `8000` |

You need `gcloud auth login` and access to the project, or `FP_CLIENT_KEY` set in
your shell. The key is never written to a file or logged.

## Reading the affect output

`confidence_in_signal` is capped at **0.75** on purpose — the checkpoint is
around 64% accurate out of domain, and the cap keeps the interrogator from
treating it as fact.

`calibration_state` starts at `calibrating` and
`reference_comparison_available` stays false for the first few turns. Deltas
against a baseline are meaningless until there is a baseline, so this is
expected on turn one, not a bug.

Synthetic audio (`say`, TTS clips) reads as **angry** with high confidence. That
is an artifact of flat machine speech, not a baseline — judge the numbers on
real voice only.

## What the detective was told

The class probabilities are the weakest thing on the page and the loudest. The
LLM never sees them — it sees a handful of prose sentences derived from
threshold gates in `prosody.py`, which the top panel shows verbatim. If a
reading swings wildly and that text does not change, the swing never reached
the detective. **Pacing triggers** shows each gate with its threshold, so
near-misses are visible rather than inferred.

That panel needs the server to run with `SIDECAR_DEBUG_AFFECT_CONTEXT=1`;
otherwise it reads *server echo off*. The echo is prompt text and the client
key is only a speed bump, so it is opt-in and stays out of the Unity DTO.

## Hesitation does not arrive through the label

The four IEMOCAP classes have no *nervous* or *hesitant* bucket, so hesitation
reaches the detective through `long_pause_count`, `frame_instability`,
`speech_rate_delta` and onset delay instead. Watch those, not the label.

Silence is not neutral padding. `ser.py` mean-pools over every frame, so dead
air is a vote: measured 6 Aug, 2s of room tone on each end of one clip moved
`neutral` from 0.078 to 0.005 and `happy` from 0.063 to 0.123, and diluted
`frame_instability` from 0.169 to 0.119 — a hesitant witness produces exactly
that shape. `ser.py` now trims leading and trailing silence before the encoder
(internal pauses are kept, and the pacing features still see untrimmed audio),
which cuts that swing to 0.025 and holds instability steady.
