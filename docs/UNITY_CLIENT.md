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
   the utterance. Calibration (`MicCalibration`) samples 3 s of room tone
   (was 1 s) and takes the **minimum** RMS observed, not the mean — one
   cough/click during that window used to drag the mean up and desensitise
   the VAD for the whole session. Speaking starts at `2.0 x` that floor and
   ends once RMS drops back under `1.5x` (was `1.2x`, a thin hysteresis band
   that made speech end too eagerly on syllable gaps). The HUD meter
   (`MicIndicator`) goes green on `SpeakingStateChanged(true)` and back to
   grey only once the span resolves — either sent (`UtteranceRecorder.
   UtteranceCaptured`) or thrown away as too short (`UtteranceDiscarded`) —
   rather than a per-frame level-vs-threshold check, so it reflects whether
   an utterance is actually being captured instead of flickering on every
   transient.
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
   (currently `BlendShapeCopMouth`, real audio-driven visemes — see "Cop
   character" below) off the playback.

## Cop character

`Assets/_Project/Art/NewCop.glb` is the active model — an **Avaturn T2**
export (54-joint Mixamo-style skeleton, 73 real morph targets on
`Head_Mesh` including a full Oculus-viseme set, no jaw bone). Unity 6000.5
still has no built-in glTF importer (glTFast's importer also has no
Humanoid Avatar option), so it's staged through Blender the same way the
original model was: `Tools/blender/rig_newcop.py` bakes a seated rest pose
and exports `Assets/_Project/Art/NewCop_rigged.fbx`, a normal FBX using
Unity's usual Humanoid `ModelImporter` path. Unlike the original pass, this
one ran live through the Blender MCP addon rather than a headless
`--background` CLI call, and needed none of the jaw-bone/mouth-dimple
surgery the T1 model required — the mouth is entirely blendshape-driven.

The original model (`Assets/_Project/Art/cop.glb`/`cop_rigged.fbx`, an
**Avaturn T1** export — 0 morph targets, no jaw bone, sealed mouth
topology, needed a hand-added `Jaw` bone and a carved mouth-dimple to
animate at all) is kept on disk for reference but no longer referenced by
the scene. See `Assets/_Project/ASSETS_TODO.md` §1 for the full T1 build
history if you need it (jaw hinge placement, mouth-dimple carve, tear
thresholds).

**Mouth / lip sync — real, audio-driven.** `Cop/BlendShapeCopMouth.cs`
(written earlier, unused until this model existed) wraps `uLipSync` +
`uLipSyncBlendShape`, both on the `Cop` GameObject.
`uLipSyncBlendShape.skinnedMeshRenderer` points at `Head_Mesh`; its
Phoneme→BlendShape table maps `uLipSync-Profile-Sample-Male`'s phoneme set
(`A/I/U/E/O/-/S`) to this model's `viseme_aa/I/U/E/O/sil/SS` shapes. The
profile is copied into `Assets/_Project/Config/uLipSyncProfile.asset`
rather than referenced out of `Library/PackageCache` directly.
`CopMouthController.mouthImplementation` now points at `BlendShapeCopMouth`
(the `JawBoneCopMouth` tier is unused — no jaw bone on this rig to drive).

uLipSync only analyzes an `AudioSource` on its own GameObject by default —
the Cop's own — so **live dialogue turns need no extra wiring**. The one
gap is `CutsceneId.SpasskyAnswer`'s VO, which plays through `_Persistent`'s
`CutsceneVoSource`, a different scene's AudioSource: that GameObject now
also carries a `uLipSyncAudioSource` proxy component, exposed via
`CutsceneDirector.VoSourceLipSync`, and `CutsceneAnimationDirector` points
`uLipSync.audioSourceProxy` at it for the cutscene's duration only.

**Root-motion sink bug, found and fixed.** Assigning the Animator a real
`AnimatorController` for the first time (an earlier pass) exposed that
Unity humanoid prefabs default to `applyRootMotion: 1`; a muscle-only clip
with no `RootT`/`RootQ` curves then drives the GameObject's own transform
from the clip's implied per-frame delta — a continuous drift into the
floor. Fixed by always setting `applyRootMotion = false`, and more
fundamentally, as of the talking-body rewrite below, **nothing drives this
Animator's muscles at all any more** — no `RuntimeAnimatorController`, no
`PlayableDirector` either. `Cop/CopIdleAnimator.cs` and
`Cop/CopTalkGestureAnimator.cs` both work by writing bone
`Transform.localRotation` directly, bypassing Mecanim evaluation entirely.

**Head-detach bug, found and fixed.** After the T2 swap, the cop's head
would teleport ~0.5m and appear to detach from the body the instant any
mouth blendshape activated. Root cause: `Tools/blender/rig_newcop.py`'s
seated-pose bake rewrote the mesh Basis to the posed/seated shape but left
every other shape-key block (the 72 blendshape targets on `Head_Mesh`, plus
the equivalents on 5 other meshes) in the original *standing* frame — so
every exported blendshape's delta came out as a rigid translation by the
full seat-height drop (~0.53m), identical across every vertex, for every
shape. Fixed by offsetting every shape-key block (including Basis) by the
same per-vertex displacement the bake applies — exact, since the
corruption was a pure translation. See `Assets/_Project/ASSETS_TODO.md` §1
for the full diagnostic (before/after blendshape-delta numbers).

**Talking body — procedural, driven by uLipSync's own volume.**
`Cop/CopTalkGestureAnimator.cs` writes shoulder/upper-arm/forearm/hand
`Transform.localRotation` in `LateUpdate` (same mechanism as
`CopIdleAnimator`, plus a small `Spine1` accent layered on top of its
breathing curve — script execution order is pinned so
`CopTalkGestureAnimator` runs after `CopIdleAnimator` each frame). Gesture
amplitude comes from `uLipSync.result.volume` through an attack/release
envelope, so the arms rise into a talking sway while he's actually speaking
and settle back to rest in silence — for **every** dialogue turn, live or
cutscene, off one signal.

This replaced an earlier Timeline-based pass:
`Editor/CopAnimationBuilder.cs` used to bake a keyframed `Cop_Talk` clip
played by a one-track Timeline asset
(`Art/Timelines/Cutscene_SpasskyAnswer.playable`) only during
`CutsceneId.SpasskyAnswer`. Retired — it only ever covered that one
cutscene (every live dialogue turn had a static body), and its scene
binding went null on every bootstrap re-run since `BuildTimeline`
recreates the `TrackAsset` each time. `Cutscene/CutsceneAnimationDirector.cs`
is now scoped to only its cross-scene uLipSync audio-proxy redirect (see
above) — no `Play()`/`Stop()`, no `CopIdleAnimator` suppression, so idle and
the talk gesture both run straight through `SpasskyAnswer` uninterrupted.
`CopAnimationBuilder.cs` and its baked assets are left on disk unreferenced,
matching this project's convention for the superseded T1 cop assets.

## Cutscenes: screen cover and the drunk effect

`UI/ScreenFader.cs` is the only full-screen black cover in the game — a
`CanvasGroup` faded by `GameFlowDirector.TransitionRoutine` (every phase
change), `Flow/GameFlowDirector.MemoryInterludeRoutine` (P3's memory pair,
now fading the same way rather than the hard cut it used to be), and
`Cutscene/CutsceneDirector.PlayRoutine` per-recipe. A recipe's
`keepScreenLit` flag (authored in `Editor/CutsceneRecipeBuilder.cs` via
`Recipe(...)` vs `VisibleRecipe(...)`) decides whether that cutscene fades to
black or plays over the live scene:

- **Screen-lit** (no black): `Wake`, `SpasskyAnswer`, `StandFromChair`,
  `RadioClears`, `SomeoneLeft`, `CallForNick`, `PriyaScreams`,
  `TheyComeDown`, `OutIntoTheSnow`, `TheCarry`, `TheSofa`, `P3Photograph`,
  `P3AfterGoodYears`, `P3WhoDavid`, `GoodYears`, `WhenItWentWrong`.
- **Fades to black** — these *are* scene transitions, not cutscenes to be
  watched: `FuzzyToNight`, `FuzzyToInterrogation`, `FuzzyToMorning`,
  `FuzzyToVerdict`. Authored via `TransitionRecipe(...)`, which also sets
  `CutsceneRecipe.endsBlack` — see below.
- **Stays black by design** — a held black frame with one diegetic sound or
  an ending card, not something to watch: `FlashbackAaron/Ivy/Priya`,
  `EndingDavid/Aaron/Ivy/Priya`.

A `TransitionRecipe` fades to black, plays its beat, and stops there —
`endsBlack` skips `PlayRoutine`'s trailing `FadeFromBlack`. Every one of its
call sites (`PhaseDialogueController`, `M1NightController`,
`M2MorningController`) immediately follows with
`GameFlowDirector.AdvancePhase()`, whose own `TransitionRoutine` fades back
in once the new scene is active. Without `endsBlack`, the cutscene's own
fade-in and the transition's fade-out ran back to back with nothing new on
screen — a visible flash of the old scene between two blacks. `ScreenFader.
Fade` also now no-ops when it's already at the requested alpha (exposed via
`ScreenFader.Alpha`), so `TransitionRoutine`'s leading `FadeToBlack` costs
nothing when the screen is already black on arrival — the same fix collapses
the equivalent double-fade in `MemoryInterludeRoutine` for free. **If you add
a new recipe that hands off to a scene swap, use `TransitionRecipe`, not
`Recipe` — plain `Recipe` will reproduce the double-fade.**

Going screen-lit exposes whatever `Cutscene/CutsceneStage.cs` stages while
the beat plays (actors teleporting into position, doors swinging) — it used
to happen invisibly under the fade. `StandFromChair`, `SomeoneLeft`,
`PriyaScreams`, `TheyComeDown`, and the mid-cutscene room swap inside
`WhenItWentWrong` (previously hidden by a local 0.12s blink, now removed)
are the ones where this is visible. If a specific beat reads as a jump cut
rather than a transition, fix that beat's staging to happen off-camera
rather than re-adding a black cover.

**Drunk post-process** (`Rendering/DrunkColorPulseFeature.cs`) — a URP
`ScriptableRendererFeature` registered in `Assets/Settings/PC_Renderer.asset`,
implementing `RecordRenderGraph` (this project runs URP with RenderGraph
active; a pass that only implements the legacy `Execute(CommandBuffer)`
silently never runs). Composites a persistent previous-frame RTHandle for a
motion-trail smear plus a colour lerp that can pulse, ported from an
Asset-Store PPv2 package that could not run under URP at all (PPv2 and URP
don't coexist) — the original `Assets/DrunkColorPulse/` and
`Assets/Resources/DrunkColorPulse.shader` are deleted.
`Rendering/DrunkEffectController.cs` (one instance in `_Persistent`, sibling
to `CutsceneDirector`) is the only thing gameplay code should call —
`RampTo(intensity, seconds)` / `SetImmediate` / `Clear()`. It publishes a
static `CurrentSettings` struct the render pass reads fresh every frame; the
pass skips entirely when nothing is active, so it costs nothing outside a
cutscene using it. `Cutscene/DrunkCutsceneBinder.cs` is the only caller
today — it ramps the effect up on `CutsceneDirector.Started(CutsceneId.Wake)`
and back down on `Finished`, so it's active only across the player waking up
at the interrogation table.

The default colour/pulse/trail values, and the `maxOverlayIntensity` cap
(0.1) that `RampTo`'s 0..1 gameplay ramp scales into, are copied from the
vendor package's own shipped demo profile (recovered from the cached
`.unitypackage` in `AppData/Roaming/Unity/Asset Store-5.x/`, since the
original `Assets/DrunkColorPulse/Demo/` was deleted along with the rest of
the PPv2 package — see above). A first pass at this port drove
`_OverLayMaxIntensity` straight off the raw 0..1 ramp in an invented purple;
at full ramp that lerps the entire frame to solid colour rather than tinting
it, which is why it looked flatly pink/magenta rather than woozy.
`maxOverlayIntensity` exists specifically so the ramp's 0..1 stays inside the
vendor's actual 0..0.1 overlay range. These are all serialized fields on
`DrunkEffectController`, tunable per-need without a code change — just keep
`maxOverlayIntensity` well under 1 when doing so.

**Selling the wake-up beyond the post-process.** Three more pieces ramp
alongside the drunk post-process across `CutsceneId.Wake`, all driven off the
same `CutsceneDirector.Started`/`Finished` pair:

- **Camera sway** — `Player/DrunkCameraSway.cs`, on the `SeatedLook`
  GameObject in `Interrogation.unity` (hand-authored in-scene like the rest of
  the Player rig, not built by `ProjectBootstrapBuilder.cs` — add it the same
  way if a scene rebuild ever drops it). Runs in `LateUpdate`, multiplying a
  three-axis sine wobble (mismatched frequencies per axis so it never
  reads as a metronome) onto `playerCamera.localRotation` on top of whatever
  `SeatedCameraRig.Update()` wrote that frame. It lives on the Player rig
  rather than `_Persistent` because it needs that scene-local camera
  transform, which a persistent singleton can't hold a reference to.
- **A slow fade in from black** — `DrunkCutsceneBinder` drives `ScreenFader`
  directly (a quick snap to black, then a slow `FadeFromBlack`) rather than
  through `CutsceneRecipe.fadeOutSeconds`/`fadeInSeconds`. `Wake` is still
  `keepScreenLit` (so `CutsceneDirector.PlayRoutine` never touches the fader
  for it, and the drunk post-process stays visible per its original intent);
  the recipe's own fade fields would be a one-shot pre/post-cutscene fade,
  not the "reveal while the cutscene plays" effect wanted for eyes opening.
- **Slowed, echoed VO** — `DrunkCutsceneBinder` sets the shared
  `CutsceneVoSource` `AudioSource.pitch` down (`slowPitch`, default 0.75) and
  enables an `AudioEchoFilter` on it for the duration of `Wake`, then resets
  both on `Finished`. `AudioClip.length` doesn't change with pitch, so
  `CutsceneDirector.PlayBeat` divides each beat's hold by `voSource.pitch` —
  otherwise the slowed clip would get cut off mid-word when the next beat
  reassigns the shared source's `clip`. This is a no-op for every other
  cutscene, which never touches pitch.

**Verifying any of this changed rendered camera rotation:** don't trust a
value read back from a *different* coroutine/script a frame later — Update
(`SeatedCameraRig`) always runs before LateUpdate (`DrunkCameraSway`) in the
same frame, so an external read timed against `yield return null` can land in
the brief window after `SeatedCameraRig` has reset the rotation but before
`DrunkCameraSway` reapplies it, reading exactly zero every time despite the
sway genuinely working. `ScreenCapture.CaptureScreenshot` (the actual
rendered frame) is the reliable way to confirm — `Unity_Camera_Capture` with
no camera ID captures the *Scene* view, not the game camera, and is not a
substitute here.

**The same four pieces, ported to `CutsceneId.StandFromChair`** (M1_Night's own
wake-up, standing from the chair — `CutsceneStage.StandFromChair()`,
`Editor/CutsceneRecipeBuilder.cs`). Rather than generalizing `DrunkCutsceneBinder`
and `DrunkCameraSway` to handle multiple ids, each got a **second instance**,
since `Wake` and `StandFromChair` never play concurrently and can safely share
the same `DrunkEffectController`/`ScreenFader`/VO source underneath:

- `_Persistent`'s `CutsceneDirector` GameObject carries a second
  `DrunkCutsceneBinder (StandFromChair)`, wired to the same singletons as
  `Wake`'s but with slower timings (`rampInSeconds 1`, `rampOutSeconds 2`,
  `fadeInSeconds 4`) to match the longer beat.
- `DrunkCameraSway` is added directly to
  `Assets/_Project/CabinNight/Prefabs/Player_FirstPerson.prefab` (the prefab
  asset itself, in `Editor/CabinNightCharacterBuilder.cs`'s `ConvertToRouterRig`
  — not just the `Memory_CabinNight.unity` scene instance), reusing the exact
  same amplitude/frequency defaults as `Wake`'s. Adding a component to a
  prefab asset propagates to existing scene instances automatically — no
  "Apply to Prefab" step needed, unlike changing an existing serialized value.
- `CutsceneStage.StandFromChair()`'s stand-up animation is stretched from
  0.5s to 5s. Its recipe (`VisibleRecipe(CutsceneId.StandFromChair, ...)`) has
  only a sub-second `chair_creak` SFX beat, so a new `HoldBeat(seconds)` helper
  (wordless, clipless, just waits) pads the total beat time to match — without
  it, `Finished` (and the effects' ramp-out) fires while the camera is still
  mid-rise. The pad has to account for `voSource.pitch`: `chair_creak.mp3` is
  1.54s, played at 0.75x during this beat, so its actual hold is ~2.06s, not
  the raw clip length — re-measure both if the pitch or the SFX asset changes.

**A real `CutsceneRecipeBuilder` bug this surfaced:** `SerializedProperty`
array growth (`beatsProp.arraySize = N`) duplicates the *last* element's
serialized data into each newly created slot rather than zeroing it. Adding
`HoldBeat` as `StandFromChair`'s second beat meant the array grew from 1 to 2,
and the new slot silently inherited beat 0's `chair_creak` clip instead of
staying null — `WriteRecipe`'s old preserve-existing-clip check
(`if (existing voClip == null && new voClip != null)`) doesn't catch this
because the *duplicated* value isn't null. Fixed by only trusting that
preserve logic for beats that carry a `line` (dialogue beats, which
`Bootstrap/7 - Attach VO Clips` re-attaches from a table every run anyway);
wordless beats (`SfxBeat`/`HoldBeat`) now always take the value the code
computed, including clearing it back to null.

**Known pre-existing bug, not touched by the above:** `CutsceneStage.GoodYears()`'s
staging coroutine runs on its own hardcoded `WaitForSeconds` schedule
(~13s), independent of the `GoodYears` recipe's actual VO clip lengths.
When the recipe finishes first, `CutsceneDirector.Finished` fires and the
memory scene gets deactivated before the staging coroutine reaches
`ReturnBorrowed()`, leaving Nick/Aaron/Ivy active afterward (caught by
`Tools ▸ Debug ▸ Check M1 staging was restored`). Confirmed via `git stash`
A/B testing to reproduce identically on an unmodified checkout — it predates
this fade/screen-lit work and is not something to fix by adding cover back.
Needs `GoodYears()`'s staging waits re-timed against the real clip lengths.

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
- `Tools ▸ False Positive ▸ Debug ▸ Jump to P1_Tutorial / P2_Recall / P3_Verdict /
  P4_Ending` (`Editor/PhaseJumpDebugMenu.cs`) — play-mode-only menu items that
  drop straight into any interrogation phase, so testing P2/P3/P4 doesn't mean
  talking through every earlier phase first. Mints a session id via
  `StartNewPlaythrough` first if none exists yet (e.g. jumping right after
  pressing Play, without ever clicking through the main menu) — otherwise the
  jump binds a null session id and every backend turn fails. Memory flags and
  session score are empty on this path; use the F1 overlay's flag toggles if
  the officer's questions need them. Also auto-grants mic consent and starts
  VAD calibration for the debug jump (editor/play-mode only) — without this a
  jump straight into P1 leaves the mic never opened, so the "Who are you?
  Where am I?" prompt can never be satisfied by real speech and
  `CutsceneId.SpasskyAnswer` never fires. `Editor/P3MemoryPairDebugMenu.cs`
  keeps the P3-memory-pair-specific items (playing CS-16A/CS-16B in
  isolation, and asserting M1's borrowed cast was handed back).
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
