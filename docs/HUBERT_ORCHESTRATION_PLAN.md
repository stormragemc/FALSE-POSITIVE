<!-- /autoplan restore point: /Users/anandatriharismaroso/.gstack/projects/stormragemc-FALSE-POSITIVE/main-autoplan-restore-20260803-040429.md -->
# HuBERT affect orchestration plan

**Status:** implemented and locally verified  
**Date:** 3 Aug 2026  
**Scope:** `Sidecar/` affect inference and session orchestration, plus the Unity HTTP/debug contract

## Problem

The vertical slice already runs `superb/hubert-base-superb-er` beside Whisper and sends one
argmax label plus its softmax score to Gemini. That proves the plumbing, but it does not deliver
the game's intended second channel:

- a single label discards uncertainty and near-ties;
- every utterance is interpreted in isolation, so the detective cannot notice change over time;
- there is no player-relative baseline;
- short, clipped, silent, or ambiguous audio still produces a confident-looking label;
- the LLM sees a generic emotion sentence rather than a bounded, game-specific affect signal;
- the HTTP/debug contract cannot explain why a reading was trusted or suppressed.

The model must remain an affect sensor, never a lie detector. It may change the detective's
pacing and choice of follow-up. It may not establish guilt, truthfulness, or intent.

## Research findings that constrain the design

1. HuBERT is a self-supervised speech representation model trained by masked prediction of
   clustered hidden units. Emotion recognition is a downstream fine-tune, not HuBERT's native
   claim. Source: [HuBERT paper](https://arxiv.org/abs/2106.07447).
2. `superb/hubert-base-superb-er` accepts 16 kHz speech and predicts four IEMOCAP classes:
   neutral, happy, angry, and sad. The model card reports 0.6359 accuracy for the Transformers
   port. Source: [base checkpoint model card](https://huggingface.co/superb/hubert-base-superb-er).
3. The large checkpoint reports 0.6762 accuracy, a small absolute gain for materially higher
   model size and CPU cost. The base checkpoint remains the demo default; the model ID becomes
   configurable for measurement. Source:
   [large checkpoint model card](https://huggingface.co/superb/hubert-large-superb-er).
4. IEMOCAP is an acted, multimodal corpus of roughly 12 hours from ten actors. This is a narrow
   training domain and a poor basis for universal claims about real players. Source:
   [USC IEMOCAP release page](https://sail.usc.edu/iemocap/iemocap_release.htm).

## Premises

1. The player-facing value is adaptation to vocal change, not displaying an emotion label.
2. Relative change within one session is more defensible than comparing absolute scores across
   players.
3. Uncertainty is first-class data. An unreliable reading must be explicitly suppressed.
4. HuBERT and Whisper remain parallel reads of the same 16 kHz buffer to protect turn latency.
5. The first three usable utterances form a lightweight early-session reference. This is neither
   a claim about the player's neutral state nor model training, and it is held in memory only.
6. The existing flat fields remain for compatibility while a versioned nested prosody payload
   adds the richer contract.
7. No audio, embeddings, or per-player baselines are persisted or sent to Gemini.

## Proposed architecture

```text
Unity VAD + onset timer
          |
          v
POST /turn: PCM + onset_delay_ms
          |
          +----------------------+-----------------------+
          |                                              |
          v                                              v
faster-whisper                               HuBERT + classical audio
transcript                                   full distribution
                                              hidden-state motion
                                              energy/pitch/pause quality
          |                                              |
          +----------------------+-----------------------+
                                 v
                      per-session ProsodyTracker
                      calibrated change + trend + confidence
                                 |
                      +----------+-----------+
                      |                      |
                      v                      v
              bounded LLM context      HTTP/debug payload
              reliable signal only     full observable signal
```

## Contracts

### Raw HuBERT observation

`Sidecar/ser.py` will expose a structured observation containing:

- display label and top probability;
- all four class probabilities;
- normalized entropy and top-two probability margin;
- mean hidden embedding for in-memory baseline comparison;
- frame-to-frame hidden-state instability from a configurable hidden layer;
- inference time and model ID.

The existing `classify()` tuple API remains as a compatibility wrapper for probes.

### Session `ProsodySignal`

`Sidecar/prosody.py` will combine HuBERT and deterministic audio features into a JSON-safe,
versioned signal:

- utterance duration and response onset delay;
- speech ratio, long pauses, pitch variability, and energy variability;
- HuBERT instability, raw distance from the session reference, and a dimensionless change score
  calibrated against that session's reference-vector spread;
- derived arousal and tension impressions;
- `confidence_in_signal`, reliability reason, calibration state, and flags;
- full four-class distribution for debugging.

The tracker is keyed by the existing session ID and cleared by `/session/reset`. A turn is first
previewed for the LLM and is committed to the reference/trend only after TTS has produced playable
audio, so a failed/retried turn cannot be counted twice. `calibration_state` reports session
readiness after that commit; `reference_comparison_available` reports whether this specific turn
had an already-established reference, so the reference-completing turn is ready but not compared
with itself. A process-wide reset epoch also prevents any in-flight pre-reset turn from
re-registering stale session state after `/session/reset`.

### LLM boundary

Gemini receives a short block only when `confidence_in_signal` clears the configured threshold.
Witness text and derived context are escaped into separate `WITNESS_TRANSCRIPT` and
`LOCAL_AFFECT_CONTEXT` blocks on the current turn and in replayed history. A witness imitating the
reserved marker cannot create sensor context. The block will:

- call the signal fallible and non-diagnostic;
- describe only the dominant class as an impression rather than promoting a low-probability
  runner-up;
- describe session-calibrated change, recent trend, notable onset delay, material speech-rate
  change, pauses, and elevated activation when available;
- authorize subtle pacing or follow-up changes only;
- prohibit claims about lying, guilt, truthfulness, or intent;
- prohibit mentioning the sensor or its numeric values to the player.

Below threshold, Gemini receives only that no reliable affect signal is available. Raw audio,
embeddings, and baseline vectors never cross the local sidecar boundary.

### HTTP and Unity

`POST /turn` accepts optional `onset_delay_ms`. The response keeps `emotion` and
`emotion_confidence` and adds nested `prosody`. Unity records time from listening re-arm to
utterance capture, sends it, mirrors the nested DTO, and shows the most useful fields in the F1
debug overlay. Opening turns use zero onset delay and no prosody signal.

## Reliability policy

A reading is suppressed or down-weighted for:

- audio shorter than 1.5 seconds;
- clipping or near-silence;
- high class entropy or a small top-two margin;
- missing/insufficient baseline when a relative claim is required;
- model failure.

Raw HuBERT cosine distance is retained for debugging. The actionable
`hubert_reference_change` is normalized to the spread of the first usable session turns, while
conservative absolute-distance floors prevent a very tight reference from magnifying ordinary
small perturbations into a change claim. These policy floors still require recorded-player
playtest calibration before they should be treated as model-quality claims.

The sidecar must still complete a turn when HuBERT fails. STT and dialogue continue with an
explicit unavailable prosody signal. Affect is an enhancement, not a single point of failure.

## Configuration

Document environment variables with safe defaults:

- `HUBERT_MODEL_ID=superb/hubert-base-superb-er`
- `HUBERT_DEVICE=auto`
- `HUBERT_HIDDEN_LAYER=9`
- `HUBERT_MAX_SECONDS=20`
- `PROSODY_ENABLED=true`
- `PROSODY_BASELINE_TURNS=3`
- `PROSODY_MIN_CONFIDENCE=0.40`

`PROSODY_MIN_CONFIDENCE` is clamped to `0.75`, which is also the deliberately conservative maximum
emitted signal confidence for this out-of-domain checkpoint.

`/health` reports the configured model ID and affect-orchestration version so the live demo can
prove what is loaded.

## Test plan

1. Pure audio feature tests with synthetic silence, tone, pauses, and clipping.
2. Tracker tests with synthetic HuBERT observations covering session-calibrated change,
   transactional preview/commit, rising tension, low confidence, and reset.
3. Prompt-context tests proving unreliable readings are suppressed, noisy runner-up labels are
   omitted, timing/rate/activation are bounded, and forbidden diagnostic claims never appear.
4. Prompt-boundary tests proving marker imitation is escaped in current and historical witness
   turns.
5. Failure tests for disabled/load-failed HuBERT, STT/TTS failure, startup rollback, and session
   eviction.
6. Exact C#/Python DTO field-and-type parity tests plus the existing tuple compatibility probe.

Model-quality claims require recorded human playtest audio and are not asserted by unit tests.
No real voice fixtures are committed.

## Failure modes and recovery

| Failure | User impact | Recovery |
|---|---|---|
| HuBERT load/inference fails | No affect adaptation | Continue transcript-only; mark signal unavailable |
| Audio too short/noisy/clipped | False-looking label | Suppress from LLM; expose reason in debug payload |
| First turns have no baseline | Relative claims unavailable | Mark calibrating; use only conservative class impression |
| TTS fails after analysis | Retry could double-count a turn | Preview state; commit only after playable audio exists |
| Witness imitates sensor marker | Transcript could pose as trusted context | Escape reserved phrase and use explicit trust delimiters |
| Session reset leaks baseline | New player compared to old player | Clear history and prosody tracker together |
| Large model makes turns slow | Noticeable interrogation pause | Default to base; expose model ID and timing; benchmark before changing |
| Prompt treats affect as guilt | Breaks the game's thesis | Bounded context plus tests banning diagnostic language |

## Not in scope

- training or fine-tuning a new checkpoint;
- claiming fear, stress, deception, guilt, or truth from the four IEMOCAP classes;
- persisting player audio, embeddings, baselines, or transcripts;
- replacing Whisper with HuBERT;
- changing the transport away from the working HTTP turn boundary;
- implementing consistency tracking, case data, endings, or the full structured detective tactic
  contract in this change.

## Acceptance criteria

- HuBERT and Whisper still execute concurrently.
- Every non-opening turn yields a structured, JSON-safe prosody signal or an explicit unavailable
  signal without failing the dialogue turn.
- Low-quality or uncertain readings do not reach Gemini as actionable affect.
- Session baseline and trends influence the LLM context only after enough usable turns.
- `/session/reset` clears both dialogue and affect state.
- Unity sends onset delay and can display signal confidence, tension, raw distance, calibrated
  reference change, and flags.
- All unit tests pass without downloading a model or requiring API keys.
- Documentation states the checkpoint's measured limits and the exact privacy boundary.

## Implementation order

1. Add deterministic audio features and unit tests.
2. Expand `ser.py` to emit a structured observation while preserving `classify()`.
3. Add the per-session prosody tracker and prompt formatter with tests.
4. Integrate the tracker into `app.py` with graceful degradation and reset behavior.
5. Update `llm.py`, HTTP response fields, Unity DTO/client/timing, health output, and debug overlay.
6. Update setup/research documentation and run unit, compile/static, and probe checks.

## Autoplan review

### Product review

- **Right problem:** yes. The useful game mechanic is reacting to within-session vocal change;
  improving a four-way label in isolation would not materially improve the interrogation.
- **Existing leverage:** retain the working parallel STT/SER pools, flat compatibility fields,
  session reset, LLM soft-signal framing, and Unity debug path.
- **Alternatives considered:** exposing only the four probabilities is the smallest change but
  leaves quality and temporal context unsolved; fine-tuning a new checkpoint requires licensed,
  representative voice data and validation that the project does not have. The selected middle
  path is a deterministic orchestration layer around the released base checkpoint.
- **Scope mode:** selective expansion. Add a feature flag, health status, and synthetic replay
  tests because they make rollout reversible and observable. Defer model training, stored voice
  fixtures, multimodal fusion, and gameplay outcome scoring.
- **Privacy:** audio and embeddings stay process-local and ephemeral. Gemini receives only a
  short derived text impression. The response exposes scalar debug values, not embeddings.

### Engineering review

The review changed the implementation in these places:

1. HuBERT load is optional and independently reported by `/health`; failure cannot abort sidecar
   startup when STT is usable.
2. STT remains required for a spoken turn, while HuBERT inference uses exception isolation and
   yields an explicit unavailable signal.
3. `session_id`, sample rate, onset delay, PCM alignment, and duration are validated or clamped.
   Session/reference registries are bounded to limit local memory abuse.
4. Empty/NaN hidden states, label-map differences, device selection, and reference-vector shape
   changes fail closed to no comparison or an unavailable signal rather than emitting fabricated
   values.
5. Hidden-state analysis is bounded by `HUBERT_MAX_SECONDS`; all inference uses
   `torch.inference_mode()` and the existing single-worker model pool. Classical affect features
   use the same bounded prefix, while STT retains the full accepted utterance. Over-window turns
   retain their full duration and an explicit truncation flag, but do not participate in
   speech-rate comparison.
6. The early-session centroid is called a **reference**, never a neutral baseline. Only usable
   turns enter it, and relative claims wait until it is ready.
7. The richer HTTP payload is additive; the original `emotion` and `emotion_confidence` fields
   remain intact for older Unity clients.

```text
startup: STT load fails ----------> sidecar not ready (existing required stage)
         HuBERT disabled/fails ---> ready, prosody.available=false

turn:    PCM -> [STT required] -------------------------------> dialogue
              [features + HuBERT optional] -> tracker/policy --^ (derived context only)

rollback: PROSODY_ENABLED=false -> skip HuBERT -> transcript-only dialogue
```

### Failure registry

| Boundary | Failure | Handling | Test |
|---|---|---|---|
| HTTP | invalid ID/rate/PCM/duration | structured 400, no state mutation | validation unit tests |
| Startup | HuBERT download/load/device error | health reports unavailable; STT still ready | isolated loader path |
| Startup | configured checkpoint has incompatible labels | reject affect model; report unavailable | label-contract load test |
| Inference | exception/NaN/empty states | unavailable signal; dialogue continues | injected observation failure |
| Features | classical extraction raises | zeroed unavailable signal; dialogue continues | injected feature failure |
| Reference | too few/poor turns or shape mismatch | calibrating/reset reference; no relative claim | tracker tests |
| Prompt | uncertain or diagnostic interpretation | suppression threshold and bounded sensor block | prompt policy tests |
| Prompt | witness imitates reserved marker/delimiter | escape witness text; separate trust blocks | prompt boundary test |
| TTS | synthesis/normalization fails after preview | do not commit reference or trend state | transactional turn test |
| Session | forgotten reset/unbounded/failed IDs | shared reset and success-only capped LRU commits | registry/turn tests |
| Session | reset races an in-flight turn | invalidate its history/reference commit with reset epoch | reset-race test |

### Developer-experience review

- The target operator is a Windows Unity contributor running a local Python sidecar. New controls
  therefore use environment variables and `/health`, not a second configuration system.
- Unit tests must run without API keys, network, or model downloads. From `Sidecar/`, run:
  `python -m unittest discover -s tests -v`.
- `Sidecar/README.md` will document first-download behavior, feature rollback, health fields,
  the additive response, and a pure orchestration probe. Error flags describe the failed stage and
  recovery instead of exposing an embedding or requiring a traceback to understand suppression.
- The stale machine-specific plan link in the sidecar README will be replaced with repository
  links.

### Decision audit

| Decision | Choice | Why |
|---|---|---|
| Model | configurable base checkpoint by default | best latency/quality fit for the demo |
| Interpretation | affect impression plus temporal change | supported more directly than any diagnostic claim |
| Calibration | first three usable turns as reference | useful personalization without persistence/training |
| Failure policy | transcript-only degradation | affect must not become a turn-level single point of failure |
| Contract | additive nested `prosody` | preserves current Unity/probe compatibility |
| Rollback | `PROSODY_ENABLED=false` | immediate operational escape hatch |

No unresolved product, architecture, or DX decision remains. The user explicitly authorized the
recommended implementation to continue unattended. Claude CLI was authenticated by the user and
used as a read-only outside voice; its confirmed findings drove the calibration, transactional
state, prompt-boundary, error-typing, loader-rollback, and stronger-contract changes above.

## Implementation evidence

- `Sidecar/features_classical.py`, `ser.py`, and `prosody.py` implement the planned measurement,
  structured HuBERT observation, bounded early-session reference, reliability policy, and prompt
  boundary.
- `Sidecar/app.py` retains concurrent STT/affect work, isolates recoverable HuBERT failure, bounds
  session/audio inputs, resets both state stores, and returns additive health/turn contracts.
- Unity now records actual speech-onset delay from VAD state, sends it, mirrors every nested JSON
  field, and exposes the usable signal in the F1 overlay.
- `python -m unittest discover -s tests -v` passes 47 offline tests from `Sidecar/`, including
  injected HuBERT/STT/TTS failures, startup rollback, prompt-marker imitation, transactional
  reference state, and exact Unity DTO type parity.
- A real checkpoint smoke on `Sidecar/tools/sample.pcm` (cached weights plus Hub metadata access)
  returned all four labels, an `angry` top score of `0.733184`, a 768-element embedding, and
  `111 ms` CPU inference. Automatic MPS selection was removed after its warm-up terminated the
  test process; `auto` now selects CUDA or the safe CPU fallback.
- Unity compilation could not run because no Unity Editor or generated Unity assemblies are
  installed on this machine. C# call sites and JSON fields were statically cross-checked instead.

## GSTACK REVIEW REPORT

- CEO review: **PASS** — selective expansion, no product-critical gap.
- Design review: **SKIPPED** — no new screen or interaction design; debug text only.
- Engineering review: **PASS** — failure isolation, bounds, compatibility, tests, and rollback specified.
- Developer experience review: **PASS** — setup, health, errors, and offline test path specified.
- Outside voice: **PASS WITH FINDINGS ADDRESSED** — authenticated Claude CLI reviewed the diff;
  confirmed issues were reproduced, fixed, and regression-tested.
- Implementation approval: **AUTO-APPROVED** by the user's explicit unattended-build request.

NO UNRESOLVED DECISIONS
