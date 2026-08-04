# Unity client — build notes

> **Preserved verbatim from the `Unity` branch's root `README.md`** during the trunk merge
> (branch `ado/merge-unity-trunk`, 1 Aug 2026). Root `README.md` is now the submission-shaped
> README required by the brief; this file keeps Giorgi's client and pipeline detail intact.
> **Giorgi owns this content** — moved, not edited. If it should live somewhere else in `unity/`,
> that is his call.

> **⚠ Amended 4 Aug 2026 — the backend moved to Google Cloud Run.** Corrections below are marked
> inline and kept as narrow as possible, because this is Giorgi's text. What changed for the
> client: STT is Google Cloud Speech-to-Text, not local `faster-whisper`; Gemini is reached
> through Vertex AI and there is no `GEMINI_API_KEY`; `autoLaunchSidecar` is off, so Unity no
> longer starts a local process; and every request carries an `X-FP-Client-Key` header. The
> per-turn Unity flow — VAD, `UtteranceRecorder`, `OnTurnError`, lip sync — is unchanged.
> Setup for the backend is now [`Sidecar/README.md`](../Sidecar/README.md);
> [`ROADMAP.md` §9](ROADMAP.md#9-distribution-hosted-backend-migration-record) has the why.

---


A single-room, first-person interrogation game. You witnessed a crime; a
police officer questions you by voice. Stand up to walk around the room;
sit back down (E) to talk — the microphone is always live while seated, no
push-to-talk.

Full architecture and design rationale: `C:\Users\Giorg\.claude\plans\devise-a-great-and-gentle-wolf.md`.
This file is the practical "how do I run this" summary.

## Two halves

1. **Unity** (`Assets/_Project/`) — the game itself: player movement, the
   sit/stand camera system, microphone capture + voice activity detection,
   the cop's lip sync, and the HTTP client that talks to the sidecar.
2. **`Sidecar/`** — ~~a local Python process~~ **(4 Aug: a FastAPI service on Google
   Cloud Run)** that does everything external to Unity: speech-to-text, speech
   emotion recognition, the officer's LLM reply (Gemini 3.6 Flash), and the
   officer's TTS voice (ElevenLabs). Unity never holds a *vendor* API key —
   ~~both live only in `Sidecar/.env`~~ **they live in Secret Manager, injected
   into the container; Google is reached as the runtime service account.** It
   does ship one shared client key that gates the public URL, which is
   extractable from a build by design. See `Sidecar/README.md` for backend
   setup, deploy commands, and troubleshooting.

## Voice pipeline, precisely

One conversational turn, in order:

1. **Mic capture + voice activity detection** (Unity, `Audio/`) — the mic is
   always live while seated; `VoiceActivityDetector` decides speaking vs.
   silence off a calibrated RMS threshold and `UtteranceRecorder` buffers
   the utterance.
2. **Speech-to-text**: ~~`faster-whisper` (`small.en`), local, no API key~~
   **(4 Aug: Google Cloud Speech-to-Text v2, `short` recognizer, called from
   the backend)** — turns the utterance into a transcript.
3. **Speech emotion recognition**: **HuBERT** (`superb/hubert-base-superb-er`),
   in-process on the backend, no API key — runs *in parallel* with STT on the
   same audio buffer and returns one of 4 emotion labels + confidence, framed
   to the LLM as a soft impression, not a fact.
   **HuBERT is the emotion model here, not the speech-recognizer** — worth
   stating explicitly, since it's easy to assume "HuBERT" implies ASR. STT
   is Google's job; HuBERT's job is reading tone, not words.
4. **LLM reply**: Gemini 3.6 Flash, given the transcript + emotion reading,
   in character as the interrogating detective.
5. **TTS**: ElevenLabs synthesizes the reply as PCM audio.
6. **Playback + lip sync** (Unity) — the reply plays through `CopVoicePlayback`
   while `CopMouthController` drives whichever `ICopMouth` tier is active
   (currently `JawBoneCopMouth` — see "Cop character" below) off the
   playback's live amplitude.

## Cop character

`Assets/_Project/Art/cop.glb` is an **Avaturn T1** export. Per Avaturn's own
docs, T1 avatars *"cannot use face bones or blendshapes to animate the
face"* — confirmed directly on this file too: 0 morph targets, 0 animation
clips, no jaw bone, and a fully sealed mouth (no boundary edges at the lips
at all). None of the three `ICopMouth` tiers this project ships had anything
to work with out of the box.

Unity 6000.5 has no built-in glTF importer (and glTFast wasn't installed),
so `cop.glb` is staged through Blender instead: `Tools/blender/rig_cop.py`
(headless, `blender --background --python`) adds a `Jaw` bone with
geometry-measured placement and weighting, pushes the sealed lip-seam region
back with a dark interior material so a jaw-open reads as a shadowed recess
rather than stretched skin, bakes a seated pose (hips at the room's 0.45m
seat height) as the new rest pose, and exports
`Assets/_Project/Art/cop_rigged.fbx` — a normal FBX with Unity's usual
Humanoid `ModelImporter` path (the skeleton uses Mixamo-standard bone names
and auto-maps).

In the scene: `Cop` keeps its original `AudioSource`/`CopVoicePlayback`/
`CopMouthController` components; `cop_rigged` is a child instance of the
FBX. `CopMouthController.mouthImplementation` points at a `JawBoneCopMouth`
— amplitude-driven jaw rotation tracking the reply audio's volume, tuned to
a 10° max open angle. What's actually verified: 10° renders cleanly in
Blender with proper lighting; a much larger angle (22°) visibly tore the
sealed mesh in an earlier test. The true tear threshold in between was
never found (not worth chasing for a 10° runtime value), so don't read
"10°" as "right at the edge of safe" — it's an angle confirmed to look
right, with headroom above it that wasn't measured.

`JawBoneCopMouth.closedLocalEuler`/`openLocalEuler` are offsets from the
jaw bone's own rest rotation (captured at `Awake`), not absolute values —
this matters because an FBX-imported bone's rest local rotation is almost
never identity (this one sits at roughly `(56, 180, 180)` Euler, a Blender
bone-roll artifact carried through export). One thing this project could
**not** verify: which sign (`+10°` vs `-10°`) actually opens the jaw
on-screen in Unity. The Editor's player loop never ticks in this dev
environment (`Time.frameCount` stays at 1 through a real Play session —
confirmed via script, not assumed), and `SkinnedMeshRenderer` bone
deformation (both GPU rendering and `BakeMesh`) depends on that loop
running at least once, so no scripted render or bake reflects a live jaw
rotation change here. The Blender-side pose test used `-10°` in the same
rest-relative convention and rendered a clean opening, but Blender→FBX
axis conversion can flip a per-bone sign, so that isn't proof either way
for Unity. **First real Play session, watch the jaw**: if it rotates up
into the skull instead of down/open, negate `openLocalEuler`'s X value.
Body animation — breathing, idle head drift, an occasional glance,
a lean-forward "considering" beat while the sidecar is thinking — is
procedural (`Cop/CopIdleAnimator.cs`), because the GLB ships zero animation
clips and Mixamo retargeting needs an interactive login this pipeline
doesn't have.

**Upgrading to real visemes**: if the officer is re-exported from
avaturn.me as a **T2** avatar (mouth hole + ARKit blendshapes/visemes), the
tier this codebase was originally built around lights up with much less
Blender work — no jaw bone or mouth-dimple hack needed. `uLipSync` +
`uLipSyncBlendShape` components already sit on `Cop`, added but inert (no
profile, no phoneme table, not the active `mouthImplementation`) — see
`Assets/_Project/ASSETS_TODO.md` for the exact steps to wire them up once a
T2 file exists.

## Running it

1. One-time sidecar setup — see `Sidecar/README.md` §"One-time setup"
   (Python, ffmpeg, `.env` with your Gemini + ElevenLabs keys). If you hit
   a `402 payment_required` from ElevenLabs — see "ElevenLabs free-tier
   gotcha" below before assuming something's broken.
2. Open the project in Unity 6000.5.6f1 (or later 6000.5.x), open
   `Assets/_Project/Scenes/Interrogation.unity`, press Play.
3. `GameBootstrap` checks for the sidecar (`GET /health`) and auto-launches
   `Sidecar/run_sidecar.bat` if nothing answers — a manually-started sidecar
   (`Tools ▸ Interrogation ▸ Start Sidecar`, or running `run_sidecar.bat`
   yourself) is reused instead of a second copy being launched.
4. First run downloads local STT/SER models and can take a few minutes —
   the boot status text on screen says so; it isn't hung.

## ElevenLabs free-tier gotcha

ElevenLabs' free tier only grants API access to voices *you created* —
this includes the so-called "premade" voices (Adam, Rachel, Bill, …), not
just the paid Voice Library. Calling `/turn` with a voice you don't have
API rights to returns `402 payment_required` — this looked like a broken
pipeline at first but is a plan-tier restriction, not a bug.

Run `Sidecar/tools/probe_tts.py` (see that file's docstring) to find out
which voices your account can actually use over the API — it makes a real
`text_to_speech.convert()` call per candidate voice rather than just
listing what's visible, since visibility and API usability aren't the same
thing on this tier. Set `ELEVENLABS_VOICE_ID` in `Sidecar/.env` to whichever
voice the probe reports as usable, then restart the sidecar.

**Already run for you, on this account, right now**: `probe_tts.py` found
21 API-usable voices; `XrExE9yKIg1WjnnlVkGX` (Matilda, a female voice —
matching Detective Mara Voss, the officer persona in `llm.py`) is one of
them, and was independently proven to work end-to-end (LLM → TTS → 541KB
of real 24kHz PCM audio, no MP3 fallback needed) via
`Sidecar/tools/probe_full_turn.py`. This is the value to use — see Status
below.

## Status / what's still needed from you

- **`ELEVENLABS_VOICE_ID` in `Sidecar/.env`** — the currently-configured
  voice 402s on this account (see "ElevenLabs free-tier gotcha" above).
  Set it to:
  ```
  ELEVENLABS_VOICE_ID=XrExE9yKIg1WjnnlVkGX
  ```
  (Matilda — already verified usable and working end-to-end on this
  account, see above; run `python tools/probe_tts.py` yourself if you'd
  rather pick a different one or the account changes later.) Then restart
  the sidecar. This is the one thing standing between the current state
  and turns actually succeeding — everything downstream of it (Unity's
  side of the pipeline) is already wired and waiting.
- ~~**`GEMINI_API_KEY` / `ELEVENLABS_API_KEY`** in `Sidecar/.env`~~ **(4 Aug:
  `GCP_PROJECT`, `FP_CLIENT_KEY` and the two `ELEVENLABS_*` values; the Gemini
  key is gone, replaced by Vertex + ADC)** — without them the backend refuses
  to start (fails fast with a clear message naming the missing variable).
- **Art assets**: the room/table/chairs are still primitive placeholder
  geometry. The cop character is done — see "Cop character" above for what
  that took and `Assets/_Project/ASSETS_TODO.md` for room/furniture
  sourcing options (prices, URP compatibility).

**What's proven vs. what's wired but not yet exercised.** Being precise
about this matters more than it might seem, given how much of this got
verified through workarounds:
- **Proven**: the full backend chain (transcript exact-match on a real sample
  — then via Whisper, now via Google STT — HuBERT emotion label, Gemini
  in-character replies, ElevenLabs
  PCM audio) via the four `Sidecar/tools/probe_*.py` scripts; model import,
  Humanoid avatar validity, seated placement, and no stray geometry, via
  direct `Camera.Render()` captures in the Editor; one real Play-mode turn
  through Unity (a fresh `402`, recovered cleanly by `OnTurnError` without
  crashing the dialogue state machine).
- **Not exercised — compiled and wired, not observed**: the jaw actually
  moving with reply audio, `CopIdleAnimator`'s breathing/lean/glances, the
  filler-stop fix, and `CopVoicePlayback` reading `config.ttsEchoGateTailSeconds`.
  All of these need the Editor's Update loop to actually tick, and it
  didn't in the sessions used to build this — `Time.frameCount` stayed at 1
  through a full Play-mode session (confirmed via script: `realtimeSinceStartup`
  advances, `Time.frameCount`/`Time.time` don't), which also means
  `SkinnedMeshRenderer` deformation never recomputed from a scripted bone
  rotation, so even `SkinnedMeshRenderer.BakeMesh()` couldn't confirm the
  jaw's open-direction sign (see "Cop character" above). This is a
  same-cause gap, not several unrelated ones. You can confirm all of it in
  under a minute — press Play with the Editor window focused, once the
  voice ID above is set.

## Debugging

- **F1** in Play mode toggles a debug overlay: sidecar boot status, dialogue
  state, live VAD state + mic RMS, and the last turn's transcript, detected
  emotion, reply text, and per-stage timings (`stt_ms`/`ser_ms`/`llm_ms`/`tts_ms`).
- `Tools ▸ Interrogation ▸ Start Sidecar` in the Unity Editor menu for
  iterating on the Python side without restarting Play mode each time.
  **`Stop Sidecar` doesn't actually stop it** — the launcher spawns
  `cmd.exe` which spawns `python app.py`; killing the tracked process kills
  only the `cmd.exe` shell, not the Python grandchild. Close the sidecar's
  own console window instead, or Ctrl+C in it. (Mostly harmless day to day
  — a still-running sidecar is transparently reused, not duplicated — but
  worth knowing so the menu item doesn't look broken.)
- `Sidecar/README.md` §"Testing" — curl the backend directly before involving
  Unity at all; this is the fastest way to iterate on the STT/emotion/LLM/TTS
  pipeline itself. Remember the `x-fp-client-key` header, or you get a `401`.
- `Sidecar/tools/` — standalone probes that bypass the HTTP layer to
  isolate failures: `probe_tts.py` (which voices actually work over the
  API), `probe_llm.py` (is the Gemini model id still live on Vertex, with the
  real exception instead of the silent `FALLBACK_LINE` degrade), `probe_stt_ser.py`
  (feed a `.pcm` file through STT + HuBERT directly), `probe_full_turn.py`
  (exercise LLM→TTS→normalize end to end with a specific voice override,
  without needing `.env` changed first).

## Project layout

```
Assets/_Project/Scripts/
  Core/       bootstrap, sidecar process launcher, the shared config asset
  Player/     input routing, sit/stand state machine, both camera rigs
  Audio/      mic capture, voice activity detection, PCM helpers, cop playback
  Net/        the sidecar HTTP client and response DTOs
  Dialogue/   turn orchestration (capture -> sidecar -> playback -> lip sync)
  Cop/        the ICopMouth abstraction and its fidelity tiers, procedural idle animation
  UI/         screen fade, mic level meter, debug overlay
  Editor/     manual sidecar start/stop menu

Tools/blender/  headless Blender scripts that stage cop.glb into a
                jaw-riggable, seated FBX — see rig_cop.py's docstring

Sidecar/      FastAPI app — see Sidecar/README.md
  tools/      standalone diagnostic probes, see "Debugging" above
```
