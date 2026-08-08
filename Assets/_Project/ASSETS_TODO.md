# Asset sourcing — what's left to do

The room/table/chairs are still primitive placeholder geometry. The cop
character is done, now at **T2** tier: `Assets/_Project/Art/NewCop.glb` (a
second Avaturn export, this one with real morph targets) has been rigged,
seated, and wired into the scene with real audio-driven blendshape lip
sync — see below for exactly what that involved. The original T1 export
(`cop.glb`/`cop_rigged.fbx`, jaw-bone hack) is kept on disk for reference/
rollback but is no longer referenced by the scene.

## 1. Cop character — status: done (T2 tier)

**`Unity's built-in glTF import` does not exist** — Unity 6000.5 has no
built-in glTF importer and glTFast wasn't installed (glTFast's importer
also has no Humanoid Avatar option, which is why it's not the answer even
once installed). `NewCop.glb` was staged via Blender instead
(`Tools/blender/rig_newcop.py`, driven live through the Blender MCP addon
this pass rather than the old `--background` CLI invocation — see that
script's own docstring) and exported as
`Assets/_Project/Art/NewCop_rigged.fbx`, which Unity imports normally with
a real `ModelImporter` (Animation Type: Humanoid — the skeleton uses
Mixamo-standard bone names and auto-maps cleanly).

**This is a T2 Avaturn export** — 54-joint skeleton, **73 real morph
targets on `Head_Mesh`**, including a full Oculus-viseme set (`viseme_sil`,
`viseme_PP/FF/TH/DD/kk/CH/SS/nn/RR/aa/E/I/O/U`) plus `jawOpen` and ARKit
brow/eye/cheek shapes. **There is no jaw bone anywhere in the rig** — unlike
the old T1 model, this one's mouth is 100% blendshape-driven, so none of
the old jaw-bone-surgery/mouth-dimple-carve steps (`add_jaw_bone`,
`weight_jaw`, `carve_mouth_dimple` in `rig_cop.py`) apply or were needed.

What `Tools/blender/rig_newcop.py` did (verified via side-view renders
showing the correct seated L-shape profile before committing to the bake,
not assumed from the bone math alone — a front view of a seated pose
foreshortens the thigh almost to nothing and can look deceptively like
"still standing"):
- Baked a seated pose (hips dropped from standing ~0.98m to the room's
  0.45m seat target, legs/arms/spine posed to match, same bend angles as
  the old T1 pass) as the new rest pose, for all 12 skinned meshes
  (`Body_Mesh`, `Head_Mesh`, `Eye_Mesh`, `EyeAO_Mesh`, `Eyelash_Mesh`,
  `Teeth_Mesh`, `Tongue_Mesh`, `avaturn_glasses_0/1`, `avaturn_hair_0`,
  `avaturn_shoes_0`, `avaturn_look_0`).
- **Real bug found and fixed, not present in the T1 pass:** several of
  those meshes carry real shape keys (73 on `Head_Mesh` alone). Shape key
  targets are absolute positions in the mesh's own bind space, independent
  of bone pose. The first version of `bake_seated_pose` rebaked only the
  Basis (`obj.data.vertices`) to the posed/seated shape and left every
  other shape key block in the original STANDING frame — so every exported
  blendshape's delta (`target - basis`) came out as a ~0.53m rigid
  translation of the whole head (the full seat-height drop), identical
  across all 4303 vertices for all 72 shapes on `Head_Mesh`. In-game this
  meant the head visibly teleported and detached from the body the instant
  any viseme activated during lip sync. Fixed by offsetting every shape key
  block (including Basis) by the same per-vertex displacement the write-back
  applies — exact, since the corruption was a pure translation, not the
  rotational skew the original docstring worried about. Verified via a
  blendshape-delta diagnostic (`GetBlendShapeFrameVertices`): before the
  fix, every shape's max delta and "nonzero vertex" count were identical
  across all 72 shapes (~0.0054, 4303/4303); after, deltas dropped by
  1-2 orders of magnitude and each shape only moves the vertices it
  actually should (e.g. `Head_Mesh`'s worst case 2198/4303, for the
  widest-reaching mouth/jaw shapes).

**A real sinking-into-the-floor bug was found and fixed along the way,
unrelated to the model swap itself**: giving the Cop's Animator a real
`AnimatorController` for the first time (a prior pass) exposed that Unity
ModelImporter humanoid prefabs default to `applyRootMotion: 1`, and a
muscle-only clip with no `RootT`/`RootQ` curves gets its implied per-frame
body delta applied to the GameObject transform every frame under root
motion — a continuous drift into the floor. Fixed two ways: (1)
`applyRootMotion = false` is now always set
(`Editor.ProjectBootstrapBuilder.WireCopModel`); (2) more fundamentally, **no
`RuntimeAnimatorController` — and, as of the fix below, no `PlayableDirector`
either — ever drives this Animator's muscles at all any more.** Both
`CopIdleAnimator` and `CopTalkGestureAnimator` (§2) work by writing bone
`Transform.localRotation` directly, bypassing Mecanim evaluation entirely,
which makes the whole rig immune to this class of bug regardless of how any
future clip is authored.

**Mouth/lip sync — real, audio-driven, done:** `Scripts/Cop/BlendShapeCopMouth.cs`
(previously written but unused) is now attached to `Cop` and wraps the
`uLipSync`/`uLipSyncBlendShape` components that were also previously
present but inert. `uLipSyncBlendShape.skinnedMeshRenderer` points at
`Head_Mesh`; its Phoneme→BlendShape table maps the bundled
`uLipSync-Profile-Sample-Male` profile's own phoneme set (`A`, `I`, `U`,
`E`, `O`, `-`, `S` — read directly off the profile asset) to this model's
`viseme_aa/I/U/E/O/sil/SS` shapes. The profile itself is copied into
`Assets/_Project/Config/uLipSyncProfile.asset` (not referenced straight out
of `Library/PackageCache`, which can be regenerated/moved) — swap for one
calibrated against the actual ElevenLabs voice tracks when there's time.
`CopMouthController.mouthImplementation` now points at `BlendShapeCopMouth`
instead of `JawBoneCopMouth` (the jaw-bone tier still exists in the
codebase, just unused — there's no jaw bone on this rig for it to drive).

`uLipSync` only analyzes an `AudioSource` living on its own GameObject by
default — the Cop's own `AudioSource`, so **live dialogue turns need zero
extra plumbing**. The one gap was `CutsceneId.SpasskyAnswer`'s VO, which
plays through `_Persistent`'s `CutsceneVoSource`, a different AudioSource
in a different scene: `CutsceneVoSource` now also carries a
`uLipSyncAudioSource` proxy component, `CutsceneDirector.VoSourceLipSync`
exposes it, and `CutsceneAnimationDirector` points the Cop's
`uLipSync.audioSourceProxy` at it for exactly the cutscene's duration,
clearing it back to `null` (self-source) on `Finished`.

One more non-obvious fix along the way: `UnityEventTools.AddPersistentListener`
(used to wire `uLipSync.onLipSyncUpdate` → `uLipSyncBlendShape.OnLipSyncUpdate`
as an always-on listener, since nothing calls `BlendShapeCopMouth.Begin()`
during a cutscene) always **appends** rather than deduping — the bootstrap
step clears existing persistent listeners on that event before adding one,
or every re-run of Bootstrap step 4 would silently accumulate a duplicate
listener.

## 2. Body idle/lean-forward animation

`cop.glb` ships zero animation clips and Mixamo retargeting needs an
interactive login this pipeline doesn't have. Handled procedurally instead:
`Assets/_Project/Scripts/Cop/CopIdleAnimator.cs` on the `Cop` GameObject —
breathing, idle head/neck drift, an occasional glance, a weight shift, and
a lean-forward "considering" beat tied to `DialogueManager.StateChanged`
(`Uploading` → lean in, covering the turn-latency window from plan section
0). It writes in `LateUpdate` specifically so it composes as an additive
offset rather than fighting a lower-priority animation layer.

**Update (second pass, current):** the talking body gesture is now fully
procedural — `Assets/_Project/Scripts/Cop/CopTalkGestureAnimator.cs`, on the
`Cop` GameObject, writes `Transform.localRotation` on the shoulder/upper-
arm/forearm/hand bones (plus a small `Spine1` accent layered additively on
top of `CopIdleAnimator`'s own breathing curve) directly in `LateUpdate`,
the same mechanism `CopIdleAnimator` already uses. Amplitude is driven by
`uLipSync.result.volume` (the same normalized 0-1 volume the mouth blend-
shapes already react to) through a fast-attack/slow-release envelope, so
the arms rise into a "talking with hands" sway while he's actually speaking
and settle back to the seated rest pose in silence — for **every** dialogue
turn, not just one cutscene. `Editor.ProjectBootstrapBuilder.WireCopModel`
wires the bones via `Animator.GetBoneTransform` and pins
`CopTalkGestureAnimator`'s script execution order to run right after
`CopIdleAnimator`'s (`MonoImporter.SetExecutionOrder`), since both write in
`LateUpdate` and the `Spine1` accent depends on reading `CopIdleAnimator`'s
value from the same frame.

**Superseded first pass, code kept on disk unreferenced:**
`Assets/_Project/Scripts/Editor/CopAnimationBuilder.cs` used to bake a
keyframed `Cop_Talk` clip (`Assets/_Project/Art/Animations/Cop/`) played by
a one-track Timeline asset
(`Assets/_Project/Art/Timelines/Cutscene_SpasskyAnswer.playable`) only
during `CutsceneId.SpasskyAnswer`, via a `PlayableDirector` on
`AnimationDirector` that `Scripts/Cutscene/CutsceneAnimationDirector.cs`
called `Play()`/`Stop()` on. Retired for two reasons found this pass: (1)
it only ever covered that one cutscene — every live dialogue turn, the
bulk of play, had a static body regardless; (2) its scene binding
(`PlayableDirector.SetGenericBinding`) went null on every re-run of the
bootstrap step, because `CopAnimationBuilder.BuildTimeline` deletes and
recreates the `TrackAsset` on every build, orphaning the binding — so even
during `SpasskyAnswer` it had silently stopped driving anything.
`CutsceneAnimationDirector`'s only remaining job is the cross-scene
uLipSync audio-proxy redirect described in §1 above; there is nothing left
to suppress `CopIdleAnimator`/`CopTalkGestureAnimator` for any more, so
both idle and the talk gesture now run straight through `SpasskyAnswer`
too, uninterrupted. `CopAnimationBuilder.EnsureBuilt` is no longer called
from anywhere.

## 2. Room, table, chairs

Pick one (prices/compatibility verified against the live Asset Store at
plan-writing time — re-check current price before buying):

| Asset | Price | URP out of the box? |
|---|---|---|
| **Interrogation Room - Interior and Props** (Asset Store id 144583, publisher Mixall) — recommended | $29.99 | Yes |
| Office and Police Station, Interrogation Room Pack (id 241928, Nick Abrams) | $11.99 | No — Built-in only |
| Low Poly Aesthetic Police Station Interior Props (id 315111, Geokim) | $9.99 | No — Built-in/HDRP |
| Synty POLYGON Police Station | varies | Yes |

**Whichever you pick, if it isn't already URP**: after importing, run
**Window → Rendering → Render Pipeline Converter → Built-in to URP** — this
is a required step for those three, not optional.

Once imported: replace `Room/Floor`, `Room/Wall_*`, `Room/Table`,
`Room/CopChairPlaceholder`, and `Room/PlayerChairPlaceholder` with the real
models, keeping their approximate positions (or update
`PlayerSeatAnchor`'s position/rotation and the `Cop` GameObject's
position/rotation to match wherever the real chairs end up — those two
transforms are what actually matter functionally; the placeholder cubes
themselves are cosmetic).

Only buy from `assetstore.unity.com` (including its free tier),
`syntystore.com`, itch.io CC0 packs, Sketchfab CC0, or Mixamo. Several
"free" reuploads of these exact paid packages turned up during research on
piracy-mirror sites — don't use those.

## 3. What does NOT need sourcing

- **uLipSync** — already installed via UPM git URL, MIT-licensed, no
  purchase needed.
- **STT / emotion detection** — run locally in the sidecar, no asset or key
  needed.
- Ready Player Me is **not** an option — its public avatar creator, APIs,
  and Unity SDK were all shut down/archived Jan–Feb 2026.
