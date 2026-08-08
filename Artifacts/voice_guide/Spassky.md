# Officer Spassky — voice and script-driven delivery

**Status:** voice **shipped** 7 Aug 2026 (§4.1). Delivery registers (§4.3) designed, not yet
implemented. Supersedes `STORY_SCRIPT.md` §2's voice-cast note and `GAME_COMPLETION_PLAN.md` §7 B0
where they are now out of date (see §2).

---

## 1. Problem

Two defects in the officer's voice, one of casting and one of delivery.

**Casting.** `Sidecar/config.py:13` read `ELEVENLABS_VOICE_ID` from the environment with an empty
default, and the hosted account pointed it at Matilda — a female voice — while `Sidecar/llm.py:55`
writes dialogue for *Officer Spassky*, who reads male in `STORY_SCRIPT.md` §2. Because the default
was empty, the voice also could not be reproduced on a second machine without hand-copying an
undocumented ID into a `.env` file.

**Delivery.** `Sidecar/tts.py:55` called `text_to_speech.convert()` with **no `voice_settings`
argument at all**. Every line the officer speaks — the opening question, a press on a
contradiction, "That's enough from you. I've heard enough." — was synthesised with identical
delivery. `Assets/_Project/Prompts/phase_p2_recall.txt` already instructs the model to *"Change
your tone if the witness stonewalls versus if they're forthcoming"*, and nothing downstream could
act on it, because tone never leaves the text.

---

## 2. What is already true (corrections to existing docs)

Verified against the tree on 7 Aug 2026. Three standing notes are stale and are fixed as part of
this work.

| Document | Claim | Reality |
|---|---|---|
| `STORY_SCRIPT.md` §2 voice-cast note | "`Sidecar/llm.py` currently hardcodes *Detective Mara Voss*" | False since the Spassky rename. `llm.py:55` reads `You are Officer Spassky`. Only the **voice ID** was still female. |
| `GAME_COMPLETION_PLAN.md` §7 B0 | Fallback: "Keep Matilda; rename the character to a female officer" | Dead. See §2.2 — there is no rendered VO to preserve, so there is nothing to fall back *from*. |
| `Sidecar/tools/probe_tts.py:73` | Recommends a **female** voice "to match the female Mara Voss persona in `llm.py`" | Actively wrong. The probe steers whoever runs it to the wrong gender. |

`GAME_COMPLETION_PLAN.md` §7 **B1** (a `phase` form field on `/turn`) is **not needed** for this
work — see §4.2.

### 2.1 The cast table is a plan, not shipped audio

`Assets/_Project/Art/Audio/VO/README.md` casts Spassky as **Brian**, and an earlier draft of this
spec treated that as binding: change the live voice and the officer would sound like two different
men between cutscene and interrogation.

That constraint does not exist. On 7 Aug 2026:

```
git ls-tree -r origin/Game --name-only | grep -icE "Audio/VO/"   →  0
```

**Zero VO audio files are committed.** The README is a casting *intention* with nothing rendered
against it. Switching Spassky's voice therefore costs no re-render, and the README is updated to
the new casting rather than the new casting being constrained by the README.

This voids decision **D1** as originally written.

### 2.2 Probe evidence

`tools/probe_tts.py` run against the account on 7 Aug 2026: **24 of 24 visible voices are usable
over the API.** This is Branch A — there is no free-tier 402 gate on this account, and the Branch B
local-TTS fallback contemplated in the plan is not required. Voice Library voices convert directly
without an explicit add step.

### 2.3 Casting decision

Brief: *semi-deep, snarly, raspy, Russian, and angry in a contained way — not shouting.*

Nine Russian-accented male candidates were auditioned on the same line at identical settings.
Selected: **Maksim — "Raw, unpolished, deep"**, `6sXsAlJKKBf265ucBSRt`.

Sharing terms verified before committing to him, because a library voice with custom rates or a
short notice period is a dependency that can disappear:

| Field | Value |
|---|---|
| `rate` / `fiat_rate` | `1.0` / `None` — no custom per-character surcharge |
| `notice_period` | `0` |
| `live_moderation_enabled` | `False` |
| `is_added_by_user` | `True` — already in the account's own voice list |
| verified for | `eleven_flash_v2_5` and `eleven_multilingual_v2`, `accent='russian'`, `locale='en-RU'` |

---

## 3. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Live voice is **Maksim** (`6sXsAlJKKBf265ucBSRt`) | Matches the Russian / semi-deep / raspy brief. The Brian constraint that previously forced this choice is void — §2.1 |
| D1b | The voice ID is a **committed default in `config.py`**, not env-only | It is a public Voice Library ID, not a secret. An empty default meant the voice could not be reproduced on another machine. Env still overrides |
| D2 | Model is **`eleven_multilingual_v2`**, not `eleven_flash_v2_5` | Chosen by ear against flash in a blind A/B. It is materially the more Russian-sounding model. Costs latency — §4.6, accepted deliberately |
| D3 | Delivery is **derived by the sidecar** from reply text + phase | Deterministic, no LLM contract change, and nothing new can leak into TTS |
| D4 | Delivery includes a **post-TTS dB trim**, not only ElevenLabs settings | `stability`/`style` change expressiveness, not loudness. "Raises his voice" needs gain |
| D5 | Phase is **parsed from `scene_instruction`**, not a new form field | The phase prompts self-identify; a new field would need a Unity change |
| D6 | Spassky's anger is **performance only** — no story change | Considered and rejected: making him the victim's brother would have needed `STORY_SCRIPT.md` §1–2 and `COP_PERSONA` rewritten, and creates a conflict-of-interest premise. The rage is carried by the `LOW` register alone |

**Rejected:**

- **Having the LLM emit a delivery tag.** Breaks `llm.py`'s "write only spoken words" contract — a
  missed strip is read aloud verbatim.
- **Driving delivery from the player's `ProsodySignal`.** Ties the officer's voice to the player
  rather than to the script, and G6 forbids letting affect drive anything that reads as a verdict.
- **Post-hoc pitch shifting.** A −6 % resample lowers pitch and formants together and is free at
  runtime (numpy on PCM already in hand), which makes it the only route to "deeper" that
  `VoiceSettings` cannot reach. Auditioned and not selected; the chosen variant carries no shift,
  so `audio_utils` needs no `pitch_shift()` and the register table needs no back-compensated speed
  column.

---

## 4. Design

### 4.1 Voice — SHIPPED

`Sidecar/config.py`:

```python
ELEVENLABS_VOICE_ID = os.environ.get("ELEVENLABS_VOICE_ID", "6sXsAlJKKBf265ucBSRt")
```

`Sidecar/tts.py` pins the model and applies one baseline `VoiceSettings` to **both** the
`pcm_24000` call and the `mp3_44100_128` fallback, so the two paths stay audibly identical:

| Setting | Value |
|---|---|
| `model_id` | `eleven_multilingual_v2` |
| `stability` | `0.15` |
| `similarity_boost` | `1.00` |
| `style` | `0.85` |
| `speed` | `0.85` |

This is the `LOW` register of §4.3 applied uniformly — the exact configuration auditioned and
chosen. Until §4.3 ships, every line is delivered in it.

**Reproducing on another machine.** Only `ELEVENLABS_API_KEY` is required in `Sidecar/.env`; the
voice, model and settings are all in source. The API key is a secret and is never committed.

`similarity_boost` at `1.00` is not a default — it is the strongest accent carrier available and
was measured to be free (215 ms vs 248 ms at 0.75, i.e. inside noise). It also lengthens delivery
2.60 s → 3.20 s on the same text, because closer adherence to the source voice's pacing is itself
part of what the accent is.

`tools/probe_tts.py`'s recommendation logic changes to prefer **Maksim by ID**, falling back to any
male-labelled usable voice, and its comment block is rewritten for Spassky.

### 4.2 Phase detection

Both live phase prompts open with a stable self-identifying header —
`PHASE: P2_RECALL — "What do you remember?"` and `PHASE: P3_VERDICT — "Tell me why I should spare
your life."`. The sidecar already stores the active briefing per session in
`app.py:_scene_instructions`, so the phase is recoverable with:

```python
_PHASE_RE = re.compile(r"^\s*PHASE:\s*(P\d_[A-Z_]+)", re.IGNORECASE)
```

Absent, unparseable, or unrecognised → `None`, which selects the `FLAT` baseline. **No Unity
change and no `/turn` contract change.** B1 stays unimplemented.

This couples the sidecar to a text prefix owned by a Unity `TextAsset`. That is accepted: the
degradation is graceful (unknown phase → neutral delivery, never a failure), and it is covered by
a contract test in §5.

### 4.3 Delivery registers — NOT YET IMPLEMENTED

New module `Sidecar/delivery.py`. One frozen dataclass and one pure function — no I/O, no network,
no state.

```python
@dataclass(frozen=True)
class Delivery:
    name: str
    stability: float
    similarity_boost: float
    style: float
    speed: float
    gain_db: float

def choose(reply_text: str, scene_instruction: str) -> Delivery: ...
```

| Register | stability | similarity_boost | style | speed | gain_db |
|---|---|---|---|---|---|
| `FLAT` | 0.28 | 1.00 | 0.62 | 0.92 | 0.0 |
| `PRESS` | 0.20 | 1.00 | 0.78 | 0.90 | +1.0 |
| `RAISED` | 0.15 | 1.00 | 0.92 | 0.95 | +2.5 |
| `LOW` | 0.15 | 1.00 | 0.85 | 0.85 | −1.5 |

`LOW` is the shipped §4.1 baseline unchanged; the other three are perturbations around it.

**Stability is held in a narrow 0.15–0.28 band, and this is the load-bearing constraint.** It is
doing double duty: it is both the intonation lever *and* the accent carrier, because high stability
flattens delivery toward neutral English. An earlier draft of this table spread stability across
0.25–0.55, which would have sanded the Russian accent off `FLAT` — the register the officer spends
most of the game in. Style, speed and gain therefore carry most of the register difference.

Lower `stability` is ElevenLabs' knob for more variable, more emotional delivery; `style` is
exaggeration. `similarity_boost` is held constant across registers so only delivery varies, never
voice identity. `use_speaker_boost` is left at the API default.

**Selection, first match wins, evaluated in this order:**

1. `FLAT` — if `reply_text` is exactly `output_safety.FALLBACK_LINE`. The safety path must never
   get a dramatic read; a deflection delivered emphatically reads as a tell.
2. `RAISED` — if the reply contains `!`, or contains a token of two or more letters that is
   entirely uppercase.
3. `LOW` — if phase is `P3_VERDICT` or `P4_ENDING`, or the reply contains no `?` and runs to 18 or
   more words.
4. `PRESS` — if the reply ends in `?` **and** runs to 5 or fewer words.
5. `FLAT` — otherwise.

"Words" throughout means whitespace-separated tokens of the reply after `str.split()`.

Rule 2 sits above rule 3 so an exclamation inside `P3_VERDICT` still raises. `P1_TUTORIAL` never
reaches the backend (`PhaseDialogueController` handles it locally with pre-rendered VO), so it is
not given a register; if it ever did arrive, it falls through to `FLAT`.

**Rule 4 previously read `if phase is P2_RECALL, or the reply ends in ? and runs to 7 or fewer
words`.** That `or` made *every* line of the recall phase pressed, including the opener
`phase_p2_recall.txt` mandates verbatim — "So. What's the last thing you remember?" — which must
read as neutral. Narrowing the word count would not have fixed it; the phase clause had to go, so
`PRESS` is now text-driven only. At 7 words the opener correctly falls through to `FLAT`. This bug
was caught by running canon lines through the rules before implementing them, and §5 keeps that
check.

Every field is clamped into the API's accepted range on construction: `stability`,
`similarity_boost` and `style` to `0.0..1.0`, `speed` to `0.7..1.2`, `gain_db` to `−6.0..+6.0`.

### 4.4 Gain

New `audio_utils.apply_gain_db(pcm_bytes, gain_db) -> bytes`, applied **after**
`normalize_to_canonical` so it operates on the canonical format.

Peak-limited rather than clipped: convert to float32, multiply by `10 ** (gain_db / 20)`, and if
the resulting peak exceeds `0.97`, scale the whole buffer back down to sit exactly at `0.97`. A
loud line therefore receives less than the nominal boost rather than distorting. `gain_db == 0.0`
returns the input unchanged without a round-trip through float.

### 4.5 Wiring

`tts.synthesize(text)` becomes `tts.synthesize(text, delivery)`, passing the register's
`VoiceSettings` into both the `pcm_24000` call and the `mp3_44100_128` fallback.

`app.py` computes the register before the executor hand-off and applies the trim after
normalisation:

```python
delivery = delivery_mod.choose(reply_text, active_scene_instruction)
pcm, rate, channels, tts_ms = await _await_before_deadline(
    loop.run_in_executor(_vendor_pool, tts.synthesize, reply_text, delivery), deadline
)
norm_pcm, norm_rate = audio_utils.normalize_to_canonical(pcm, rate, channels)
norm_pcm = audio_utils.apply_gain_db(norm_pcm, delivery.gain_db)
```

`delivery.name` is added to the existing turn timing/debug log line so a playtest can be traced
back to a register. It is **not** added to the HTTP response: the client has no use for it, and
every field on that response is a field someone can surface in the UI, which G6 constrains.

The register table is also written into `Assets/_Project/Art/Audio/VO/README.md` so the
pre-rendered cutscene lines can be generated with matching settings.

### 4.6 Latency

`tts.py:21` already documented the trade — *"a 1-2s difference on the critical turn-latency path
versus the high-quality multilingual models."* Measured 7 Aug 2026 on Maksim, that comment is
correct:

| Model | synthesis p50 |
|---|---|
| `eleven_flash_v2_5` | ~215 ms |
| `eleven_multilingual_v2` | 950–2100 ms (scales with line length) |

Against the LLM's measured p50 of ~950 ms (`llm.py:33`), a live turn therefore costs **≈1.9–3.1 s**
against ≈1.2 s on flash. There is no hard failure: `TURN_DEADLINE_SECONDS` is 50.

**This is a deliberate quality-over-latency choice**, made by ear in an A/B against flash at
matched settings. What it costs downstream:

- At the P2:14 + P3:8 turn caps, a full playthrough gains **+20–35 s** of dead air.
- `GAME_COMPLETION_PLAN.md` Z5 already warns that the ≤5 min demo video overruns at ~3 s per turn.
  This lands on a budget that is already tight and should be planned into the route, not
  discovered during capture.

**Mitigation available and not yet taken:** the S1 intro — the longest line Spassky has and
multilingual's worst case — is **pre-rendered VO**, not live (`STORY_SCRIPT.md` line 141). Rendering
it offline costs zero at runtime. Only live P2/P3 turns pay the model's latency.

Within the flash model, delivery settings themselves were measured to cost nothing: 3 runs per
register on a 56-character line gave 214–246 ms across all four registers plus a `style=0.0`
control, entirely inside run-to-run noise (per-register min–max spread ≈ 100 ms). ElevenLabs
documents that non-zero `style` costs latency; on that model it did not. The register table is
therefore not a latency factor — only the model choice is.

`speed` was confirmed to take effect rather than being silently accepted: 0.85 → 3.72 s of audio,
1.00 → 3.30 s, 1.15 → 3.11 s for the same text.

---

## 5. Testing

New `Sidecar/tests/test_delivery.py`, all pure and offline:

- Each of the five selection rules fires on a representative line, in priority order.
- `FALLBACK_LINE` selects `FLAT` even when the phase is `P3_VERDICT` and even with a `!` appended.
- An exclamation in `P3_VERDICT` selects `RAISED`, not `LOW` (rule ordering).
- **Regression test for the rule-4 bug:** the verbatim mandated opener
  `"So. What's the last thing you remember?"` under `P2_RECALL` selects `FLAT`, not `PRESS`.
- **Canon-line table test:** a table of real lines lifted from `STORY_SCRIPT.md`, each annotated
  with its intended register, asserted end to end. This is what caught the rule-4 bug and is the
  cheapest guard against the next one.
- Absent, empty, malformed, and unrecognised `scene_instruction` all select the `FLAT` baseline
  and never raise.
- **Contract test:** the two shipped prompt files —
  `Assets/_Project/Prompts/phase_p2_recall.txt` and `phase_p3_verdict.txt` — each parse to their
  expected phase. This is what catches a Unity-side prompt edit silently disabling delivery.
- Every register's fields lie inside the API's accepted ranges.

New `Sidecar/tests/test_audio_gain.py` (there is no existing `audio_utils` test module) asserts:

- `apply_gain_db(pcm, 0.0)` returns the input bytes unchanged.
- A positive trim raises RMS.
- A near-full-scale buffer given `+2.5 dB` comes back with peak `≤ 0.97` and **no wrapped
  samples** — the specific failure a naive multiply produces.

`tools/probe_tts.py` gains a `--registers` mode reproducing the §4.6 table, so the measurement is
repeatable rather than a one-off number in this document.

---

## 6. Out of scope

- **Re-rendering the pre-rendered VO.** No VO is committed (§2.1), so there is nothing to
  re-render. This spec gives the settings table to generate them against when that work happens.
- **Streaming TTS.** Would cut multilingual's time-to-first-audio substantially, but the sidecar
  returns a complete buffer in one response and Unity plays it as one clip. Genuine mitigation for
  §4.6, and a cross-team architecture change rather than a settings change.
- **The `eleven_v3` audio-tag model** (`[whispers]`, `[shouts]`). More expressive, but not a
  low-latency model. Revisit for pre-rendered VO only, where latency is free.
- **Player-affect-driven delivery.** `ProsodySignal` carries `arousal`/`tension`/`trend` and could
  drive this. Deliberately not wired: G6 keeps affect from driving anything a player could read as
  a verdict, and the officer's voice is the most legible channel in the game.
- **Any change to `/turn`'s request or response contract.**

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| `eleven_multilingual_v2` pushes turn latency past what plays well, and the demo capture overruns | **Medium-high** | Measured and accepted (§4.6). Pre-rendering the intro reclaims the worst case; falling back is a one-line `_MODEL_ID` change |
| A Unity-side edit to a phase prompt's `PHASE:` header silently disables phase-based delivery | Medium | The §5 contract test parses the real shipped prompt files and fails on drift |
| A future selection rule regresses a mandated verbatim line, as rule 4 did | Medium | The §5 canon-line table test asserts real script lines, not synthetic ones |
| Register boundaries are tuned by ear, not measured, and may read as inconsistent | Low | Pure function with a table; retuning is a one-line change per register with tests unchanged |
| Raising `stability` for a future register quietly weakens the Russian accent | Low | §4.3 documents the 0.15–0.28 band and why; the range assertion in §5 does not catch this, so it is a review note |
| `+2.5 dB` interacts badly with Unity's own voice-volume slider on top of it | Low | Peak-limited at 0.97 with headroom, and the trim range is bounded to ±6 dB |
| Maksim is a Voice Library voice and could be withdrawn by its owner | Low | Terms checked (§2.3): `notice_period` 0, already added to the account. Swap is a one-line `config.py` default change |
