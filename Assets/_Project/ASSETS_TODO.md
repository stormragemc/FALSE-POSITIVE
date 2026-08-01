# Asset sourcing — what's left to do

The room/table/chairs are still primitive placeholder geometry. The cop
character is done: `Assets/_Project/Art/cop.glb` (an Avaturn export) has
been rigged, seated, and wired into the scene — see below for exactly what
that involved and what upgrading it further would take.

## 1. Cop character — status: done (T1 tier), upgradeable to T2

**`Unity's built-in glTF import` does not exist** — Unity 6000.5 has no
built-in glTF importer and glTFast wasn't installed. `cop.glb` was staged
via Blender instead (`Tools/blender/rig_cop.py`, headless,
`blender --background --python`) and exported as
`Assets/_Project/Art/cop_rigged.fbx`, which Unity imports normally with a
real `ModelImporter` (Animation Type: Humanoid — the skeleton uses
Mixamo-standard bone names and auto-maps).

**Why Blender was needed at all, not just "check blendshapes":** the
imported model is an Avaturn **T1** avatar. Per Avaturn's own docs, T1
*"cannot use face bones or blendshapes to animate the face"* — confirmed
directly on this file too: 0 morph targets, 0 animation clips, no jaw bone,
sealed mouth topology (no boundary edges at all near the lips). T2 is the
Avaturn tier with a mouth hole and ARKit blendshapes/visemes; this file
isn't one.

What the Blender pass did (all geometry-measured from the actual mesh, not
guessed — see `Tools/blender/_measure*.py`):
- Added a `Jaw` bone as a child of `Head`, hinge placed from real geometry
  (ear-band height, chin position), weighted to the lower face with a
  smooth falloff so only chin/lower-lip/lower-cheek verts move.
- Pushed the lip-seam region back ~1.8cm with a smooth falloff (a vertex
  translate, not a topology cut — the mesh has no mouth cavity, so this
  can only ever approximate an opening) and gave the recessed core a dark
  matte material, so a jaw-open rotation reads as a shadowed gap instead of
  visibly stretched skin.
- Baked a seated pose (hips dropped from standing ~0.90m to the room's
  0.45m seat height, legs/spine/arms posed to match) as the new rest pose,
  for all 6 skinned sub-meshes (body, hair, glasses ×2, shoes, clothing).
- Rendered jaw-closed/jaw-open checks before ever touching Unity — 10°
  renders cleanly with correct lighting; a much larger test angle (22°)
  visibly tore the sealed mesh. The exact threshold between them was never
  measured, so `JawBoneCopMouth.openLocalEuler` is tuned to the
  confirmed-clean 10°, with unmeasured headroom above it, not a value
  sitting right at a known edge.

In the scene, the `Cop` GameObject keeps its original `AudioSource`,
`CopVoicePlayback`, `CopMouthController` components (all of `DialogueManager`'s
references still point here); `cop_rigged` is a child instance of the FBX.
`CopMouthController.mouthImplementation` points at a `JawBoneCopMouth`
(amplitude-driven jaw rotation — no phonemes, no visemes, just open/closed
tracking the reply audio's RMS). `TextureSwapCopMouth`, the previous
placeholder assignment, was removed (every field was null on it anyway).

`JawBoneCopMouth` rotates the jaw bone *relative to its own rest rotation*
(captured at `Awake`), not to an absolute value — the FBX-imported Jaw
bone's rest local rotation isn't identity, it's roughly `(56, 180, 180)`
Euler (a Blender bone-roll artifact that survives export), so an earlier
absolute-overwrite version of this component would have snapped the jaw to
the wrong orientation on every frame. The *direction* (which sign opens vs.
closes) is a best guess, not confirmed live in Unity — see the comment on
`openLocalEuler` in the script, and the README's "Cop character" section,
for why and what to check on first real Play.

### Upgrading to real visemes (T2)

If you re-export the officer from avaturn.me as a **T2** avatar (mouth
hole + ARKit blendshapes), the codebase's originally-intended tier lights
up with much less Blender work — no jaw bone, no mouth-dimple hack needed:

1. Drop the new `.glb` in `Assets/_Project/Art/`, run it through the same
   `Tools/blender/rig_cop.py`-style GLB→FBX pass (skip the jaw/dimple/pose
   steps — the pose bake is still worth doing, morph targets survive it
   since they're mesh-level and it's the same "capture the deformed shape
   into the basis" step already used for the standing→seated pose).
2. On the `Cop` GameObject, the `uLipSync` and `uLipSyncBlendShape`
   components are already present but inert — assign `uLipSyncBlendShape`'s
   `Skinned Mesh Renderer` to the new mesh, and fill in the **Blend Shapes
   / Phoneme → BlendShape Table** (A/I/U/E/O/N/-) with the ARKit names it
   ships (`jawOpen`, `mouthFunnel`, `mouthClose`, `mouthPucker`,
   `mouthSmileLeft/Right`, `mouthStretchLeft/Right`).
3. Add a `BlendShapeCopMouth` component, assign its `Lip Sync`/`Blend Shape`
   fields to the two components above, and switch
   `CopMouthController.mouthImplementation` from `JawBoneCopMouth` to it.
4. Assign a **uLipSync Profile** (a bundled sample profile is a starting
   point; one calibrated against the actual ElevenLabs voice tracks
   better).

## 2. Body idle/lean-forward animation

`cop.glb` ships zero animation clips and Mixamo retargeting needs an
interactive login this pipeline doesn't have. Handled procedurally instead:
`Assets/_Project/Scripts/Cop/CopIdleAnimator.cs` on the `Cop` GameObject —
breathing, idle head/neck drift, an occasional glance, a weight shift, and
a lean-forward "considering" beat tied to `DialogueManager.StateChanged`
(`Uploading` → lean in, covering the turn-latency window from plan section
0). If real Mixamo/mocap clips get added later, an `Animator` can layer
underneath this script without conflict — it writes in `LateUpdate`
specifically so it composes as an additive offset rather than fighting a
lower-priority animation layer.

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
