# Officer Spassky — male voice and script-driven delivery

**Status:** design, approved 7 Aug 2026. Implements `STORY_SCRIPT.md` §2's voice-cast note and
`GAME_COMPLETION_PLAN.md` §7 B0, and supersedes both where they are now out of date (see §2).

---

## 1. Problem

Two defects in the officer's voice, one of casting and one of delivery.

**Casting.** `Sidecar/config.py:13` reads `ELEVENLABS_VOICE_ID` from the environment, and the
hosted account still points it at Matilda — a female voice — while `Sidecar/llm.py:55` writes
dialogue for *Officer Spassky*, who reads male in `STORY_SCRIPT.md` §2. The pre-rendered cutscene
VO is already cast as **Brian** (`Assets/_Project/Art/Audio/VO/README.md`), so today the officer
would change voice and gender between his cutscene lines and his live interrogation lines.

**Delivery.** `Sidecar/tts.py:55` calls `text_to_speech.convert()` with **no `voice_settings`
argument at all**. Every line the officer speaks — the opening question, a press on a
contradiction, "That's enough from you. I've heard enough." — is synthesised with identical
delivery. `Assets/_Project/Prompts/phase_p2_recall.txt` already instructs the model to *"Change
your tone if the witness stonewalls versus if they're forthcoming"*, and nothing downstream can
act on it, because tone never leaves the text.

---

## 2. What is already true (corrections to existing docs)

Verified against the tree on 7 Aug 2026. Three standing notes are stale and are fixed as part of
this work.

| Document | Claim | Reality |
|---|---|---|
| `STORY_SCRIPT.md` §2 voice-cast note | "`Sidecar/llm.py` currently hardcodes *Detective Mara Voss*" | False since the Spassky rename. `llm.py:55` reads `You are Officer Spassky`. Only the **voice ID** is still female. |
| `GAME_COMPLETION_PLAN.md` §7 B0 | Fallback: "Keep Matilda; rename the character to a female officer" | Dead. The cutscene VO is already Brian; renaming the character would require re-rendering every pre-rendered clip. |
| `Sidecar/tools/probe_tts.py:73` | Recommends a **female** voice "to match the female Mara Voss persona in `llm.py`" | Actively wrong now. The probe steers whoever runs it to the wrong gender. |

`GAME_COMPLETION_PLAN.md` §7 **B1** (a `phase` form field on `/turn`) is also **not needed** for
this work — see §4.2.

### 2.1 Probe evidence

`tools/probe_tts.py` run against the account on 7 Aug 2026: **24 of 24 visible voices are usable
over the API.** This is Branch A — there is no free-tier 402 gate on this account, and the Branch B
local-TTS fallback contemplated in the plan is not required.

Every voice in the `VO/README.md` cast is API-usable:

| Character | Voice | ID |
|---|---|---|
| **Officer Spassky** | Brian — Deep, Resonant and Comforting | `nPczCjzI2devNBz1zQrb` |
| Radio | Daniel — Steady Broadcaster | `onwK4e9ZLuTAKqWW03F9` |
| Priya | Jessica | `cgSgspJ2msm6clMCkdW9` |
| Ivy | Lily | `pFZP5JQG7iQjIQuC4Bku` |
| Aaron | Eric | `cjVigY5qzO86Huf0OWal` |
| "David" wake calls | River | `SAz9YHcvj6GT2YYXdXww` |

---

## 3. Decisions

| # | Decision | Rationale |
|---|---|---|
| D1 | Live voice is **Brian**, not "some usable male voice" | Must match the pre-rendered cutscene VO or Spassky changes voice mid-game |
| D2 | Delivery is **derived by the sidecar** from reply text + phase | Deterministic, no LLM contract change, and nothing new can leak into TTS |
| D3 | Delivery includes a **post-TTS dB trim**, not only ElevenLabs settings | `stability`/`style` change expressiveness, not loudness. "Raises his voice" needs gain |
| D4 | Phase is **parsed from `scene_instruction`**, not a new form field | The phase prompts self-identify; a new field would need a Unity change |

**Rejected:** having the LLM emit a delivery tag (breaks `llm.py`'s "write only spoken words"
contract — a missed strip is read aloud verbatim); driving delivery from the player's
`ProsodySignal` (ties the officer's voice to the player rather than to the script, and G6 forbids
letting affect drive anything that reads as a verdict).

---

## 4. Design

### 4.1 Voice

A deployment change, not a code change: set `ELEVENLABS_VOICE_ID=nPczCjzI2devNBz1zQrb` on the
hosted account and in local `.env` files. No source edit — `config.py` already reads it from env.

`tools/probe_tts.py`'s recommendation logic changes to prefer **Brian by ID**, falling back to any
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

### 4.3 Delivery registers

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
| `FLAT` | 0.55 | 0.75 | 0.15 | 1.00 | 0.0 |
| `PRESS` | 0.40 | 0.75 | 0.30 | 1.02 | +1.0 |
| `RAISED` | 0.25 | 0.75 | 0.45 | 1.06 | +2.5 |
| `LOW` | 0.45 | 0.75 | 0.35 | 0.94 | −2.0 |

Lower `stability` is ElevenLabs' knob for more variable, more emotional delivery; `style` is
exaggeration. `similarity_boost` is held constant across registers so only delivery varies, never
voice identity. `use_speaker_boost` is left at the API default.

**Selection, first match wins, evaluated in this order:**

1. `FLAT` — if `reply_text` is exactly `output_safety.FALLBACK_LINE`. The safety path must never
   get a dramatic read; a deflection delivered emphatically reads as a tell.
2. `RAISED` — if the reply contains `!`, or contains a token of two or more letters that is
   entirely uppercase.
3. `LOW` — if phase is `P3_VERDICT`, or the reply contains no `?` and runs to 18 or more words.
4. `PRESS` — if phase is `P2_RECALL`, or the reply ends in `?` and runs to 7 or fewer words.

"Words" throughout means whitespace-separated tokens of the reply after `str.split()`.
5. `FLAT` — otherwise.

Rule 2 sits above rule 3 so an exclamation inside `P3_VERDICT` still raises. `P1_TUTORIAL` never
reaches the backend (`PhaseDialogueController` handles it locally with pre-rendered VO), so it is
not given a register; if it ever did arrive, it falls through to `FLAT`.

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

`tts.synthesize(text)` becomes `tts.synthesize(text, delivery)`, passing a
`VoiceSettings(stability=…, similarity_boost=…, style=…, speed=…)` into **both** the `pcm_24000`
call and the `mp3_44100_128` fallback, so the two paths stay audibly identical.

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

Measured 7 Aug 2026 against `eleven_flash_v2_5` on Brian, 3 runs per register, 56-character line:

| Register | p50 | Δ vs baseline |
|---|---|---|
| baseline (no `voice_settings`) | 227 ms | — |
| `FLAT` | 246 ms | +19 ms |
| `PRESS` | 227 ms | −0 ms |
| `RAISED` | 214 ms | −13 ms |
| `LOW` | 217 ms | −10 ms |
| control (`style=0.0`) | 218 ms | −9 ms |

ElevenLabs documents that non-zero `style` costs latency; **on this model it does not measurably**.
All registers sit inside run-to-run noise (per-register min–max spread ≈ 100 ms), and the
`style=0.0` control is no faster than `style=0.45`. Against the LLM's measured p50 of ~950 ms
(`llm.py:33`), TTS delivery settings are not a latency factor and the table ships unmodified.

`speed` was confirmed to take effect on this model rather than being silently accepted:
0.85 → 3.72 s of audio, 1.00 → 3.30 s, 1.15 → 3.11 s for the same text.

---

## 5. Testing

New `Sidecar/tests/test_delivery.py`, all pure and offline:

- Each of the five selection rules fires on a representative line, in priority order.
- `FALLBACK_LINE` selects `FLAT` even when the phase is `P3_VERDICT` and even with a `!` appended.
- An exclamation in `P3_VERDICT` selects `RAISED`, not `LOW` (rule ordering).
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

- **Re-rendering the pre-rendered VO.** Those clips are generated outside this repo via
  `/elevenlabs-dialog`. This spec gives them the settings table to match; it does not regenerate
  the 18 clips.
- **The `eleven_v3` audio-tag model** (`[whispers]`, `[shouts]`). More expressive, but it is not a
  low-latency model and `llm.py:33` documents how little headroom the turn budget has. Revisit for
  pre-rendered VO only, where latency is free.
- **Player-affect-driven delivery.** `ProsodySignal` carries `arousal`/`tension`/`trend` and could
  drive this. Deliberately not wired: G6 keeps affect from driving anything a player could read as
  a verdict, and the officer's voice is the most legible channel in the game.
- **Any change to `/turn`'s request or response contract.**

---

## 7. Risks

| Risk | Severity | Mitigation |
|---|---|---|
| A Unity-side edit to a phase prompt's `PHASE:` header silently disables phase-based delivery | Medium | The §5 contract test parses the real shipped prompt files and fails on drift |
| Register boundaries are tuned by ear, not measured, and may read as inconsistent | Low | Pure function with a table; retuning is a one-line change per register with tests unchanged |
| `+2.5 dB` interacts badly with Unity's own voice-volume slider on top of it | Low | Peak-limited at 0.97 with headroom, and the trim range is bounded to ±6 dB |
| ElevenLabs changes `stability`/`style` semantics in a future model | Low | Registers live in one table in one module; `_MODEL_ID` is already pinned in `tts.py` |
