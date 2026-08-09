# Posing the cabin cast — arms, hands, head, and waist

You want to move a seated character — hands on the table, head turned, torso
leaning further — and have that stick, including in Play mode. Rotating a bone
in the Scene view snaps back the instant you hit Play. This doc explains why,
and how to do it so it actually sticks.

Covers the cabin cast (`Memory_CabinNight` / `Memory_CabinMorning` — Aaron,
Ivy, Nick, Priya). The interrogation cop is a **different system entirely**
(see the bottom of this doc).

**Two different mechanisms, because the waist doesn't behave like the rest:**

| | Arms, wrists, head, neck, spine, chest | Waist lean |
|---|---|---|
| Stored as | muscle floats | degrees, direct bone tilt |
| Lives in | `CabinPoseLibrary.cs` | `CabinCharacterIdle.cs` |
| Why | this is normal humanoid muscle space | muscles **can't** bend this rig's waist — see below |

## Why dragging a bone doesn't work

Arms, hands, head, neck, spine and chest are not stored as bone rotations
anywhere. They're stored as floating-point muscle values per pose:

```
CabinPoseLibrary.cs          6 numbers per pose define the arms
        |  bake — Tools > False Positive > Bootstrap > T03c
        v
Pose_SeatedForward.anim      those numbers, as muscle curves
        |  Animator plays this every frame
        v
CabinCast.controller         on the cast member's "Body" child
        |
        v
Unity's humanoid solver      rewrites EVERY arm/hand bone, every frame,
                              from those muscle curves
```

The humanoid (Mecanim) solver rewrites every one of those bones every frame
the Animator runs. Any rotation you type into a bone's Transform is gone by
the next frame.

The waist is different, and worse: muscles don't work there **at all** on
this rig. `CabinPoseLibrary.ApplySeated`'s doc comment has the measurement —
sweeping `Spine/Chest Front-Back` across its full ±0.9 range moved the
hips-to-head vector only 6°, while dragging the feet through 0.34 m of
vertical, because Unity's solver holds `pose.bodyRotation` fixed and
counter-rotates the hips instead of leaning the torso forward. The lean that
actually works is `CabinCharacterIdle` writing a direct degree-space tilt onto
the `Spine1` bone every `LateUpdate`, layered on top of whatever the Animator
just wrote — that's `leanDegrees` in that file (`SeatedForward` → 16°,
`SeatedBack` → -18°), plus the same script's breathing and head-drift.

So "moving the cast" means **changing the muscle numbers, or the lean
degrees** — never rotating a bone directly. The skill is finding the numbers
you want by looking at the result, instead of guessing and re-baking to
check.

## The six numbers, today

`Assets/_Project/Scripts/CabinNight/CabinPoseLibrary.cs`, `ApplySeated`, the
`SeatedForward` case:

| Muscle | Value | What it actually does on this rig |
|---|---|---|
| `Left/Right Arm Down-Up` | `-0.60` | Shoulder swing — the main up/down |
| `Left/Right Arm Front-Back` | `0.15` | Mostly elevation, **not** front/back |
| `Left/Right Forearm Stretch` | `0` | Positive **extends** the arm outward |

The middle row is counter-intuitive on purpose — it was measured by isolating
one muscle at a time (see the comment above `ApplySeated` in that file). The
current values land the hand at y 0.736 against a 0.75 m table top, 0.249 m
forward of the hip; reach falls off in both directions from there.

Every muscle clamps to −1..+1. Names are matched against
`HumanTrait.MuscleName`, so the string has to be exact. `SeatedBack` and plain
`Seated` live in the same method; standing profiles are in `Apply` above it.

## The tool: Cabin Pose Tuner

`Tools > False Positive > Cabin Pose Tuner` (`CabinPoseTunerWindow.cs`). Select
a cast member in the Hierarchy (the root, e.g. `Nick Vlahos (Male)`, or its
`Body` child — either works) and open the window. It only works outside Play
mode — in Play mode `CabinAnimatorDriver` has already cross-faded the Animator
into `CabinCast.controller`, which re-solves every frame and would fight every
drag.

Sliders are grouped **Arms**, **Head + Neck**, **Spine + Chest**, and
**Waist + idle motion**. Drag any of them — the Scene view updates
immediately, seated, at the real table, because the window drives the
skeleton through the same `HumanPoseHandler` path
`CabinNightCharacterBuilder.ApplyPose` uses for the edit-time prefab preview.
`Preview runtime idle` (on by default) also layers the waist lean on top, at
rest phase (breathing/drift contribute zero) — turn it off to see the pure
muscle pose, the way the Animation window below would show it, but leave it on
for anything involving the head or torso, since without it you're tuning
against a pose that omits a 16° lean that's actually there at runtime.

The `Spine + Chest` group carries its own warning in the window: those
muscles are real DOFs but barely bend the torso forward on this rig (see
above) — they mostly twist and side-bend. Use `Waist lean (degrees)` for a
forward/back lean, not this group.

### Profile mode vs. Character mode

The **Mode** dropdown at the top picks who a change affects:

- **Profile mode** edits `CabinPoseLibrary`'s shared defaults. All four seated
  flashback characters use `SeatedForward` (`CutsceneStage.cs`), so a change
  here moves Nick, Aaron, Ivy, and Priya together. `Mirror to right arm`
  copies the left arm slider onto the matching right muscle (a plain copy, not
  a sign flip — every existing profile keeps left/right arm values identical).
  **Copy SetMuscle + idle lines** puts the changed muscles on the clipboard as
  `SetMuscle(ref pose, "…", …f);`, plus a read-out of the waist/breath/drift
  constants (those live in two separate `switch` statements in
  `CabinCharacterIdle.cs`, one arm per `CabinIdleProfile` case, so there's no
  single line to paste — the read-out tells you what to type into the matching
  case). **Re-bake clips (T03c)** re-runs `CabinAnimationBuilder.Build()` so
  the `.anim` files reflect what you pasted. Needs a paste + re-bake to stick.

- **Character mode** writes straight into the *selected* character's
  `CabinPoseOverride` and `CabinCharacterIdle` components via
  `SerializedObject` — no code edit, no re-bake, and it only moves that one
  character. This is how Nick slouches further than Aaron on the same
  `SeatedForward` profile. Adds `CabinPoseOverride` to the character the first
  time you tune them if it isn't there yet (defaults to doing nothing until
  you touch a slider). `Enable per-character overrides` gates both components
  at once; leave it off to preview without committing. Values persist on the
  scene object immediately as you drag — reopening the tuner on an
  already-tuned character shows their real current values, not the profile
  default. **Reset to profile default** clears back to what Profile mode would
  show.

Ctrl+Z undoes slider edits (registered per change), but not the profile/target
switch itself — switching profile or mode reloads a fresh baseline, which is
always a safe fallback if you want to start over.

## Doing it without the tool

If you'd rather work directly:

**Animation window** — select the cast member's `Body` child,
`Window > Animation > Animation` (Ctrl+6), pick `Pose_SeatedForward` from the
clip dropdown. The muscle curves show up as plain float properties (e.g.
`Left Arm Down-Up`) you can keyframe directly. Change both keyframes (start
and end) for a static pose, or it'll drift over the clip's length. Remember:
the `.anim` is generated — re-running T03c overwrites it from
`CabinPoseLibrary`, so mirror any change back into the source file. The waist
lean won't show up here at all — it's not a clip curve, it's
`CabinCharacterIdle` writing `Spine1` directly at runtime, so this window can
only preview the pure pre-lean muscle pose.

**Avatar muscle preview** — select `Assets/_Project/Art/Characters/Nick.fbx`
(or Aaron/Ivy/Priya) → Inspector → Rig tab → Configure... → Muscles &
Settings tab → expand Left/Right Arm. Sliders match the `CabinPoseLibrary`
names exactly and preview live, but on a standing T-pose-ish figure with no
chair or table — you get arm shape, not the hand's position in the world.

## Things that will bite you

- **The wrist is free and unused.** `Left/Right Hand Down-Up` and
  `Hand In-Out` are real curves in every baked clip (the bake writes all 55
  body muscles), but `CabinPoseLibrary` never sets them, so they're 0 and the
  next bake resets them. Add `SetMuscle` lines for them if you want a wrist
  angle to stick.
- **Fingers are not reachable this way.**
  `CabinAnimationBuilder.WriteMuscle` throws on any muscle index ≥ 55 —
  finger curves need the `"LeftHand.Index.1 Stretched"` binding form, not
  `HumanTrait.MuscleName`, and nothing in this project authors that. Don't add
  a finger `SetMuscle` call to `CabinPoseLibrary`; it will hard-fail the bake.
- **Don't give `SeatedBack` its own arm values.** An earlier pass did, and the
  extra arm mass shifted the solved hips enough to drop the feet from y 0.096
  to y 0.052 (see the comment in `ApplySeated`). All three seated variants
  share one set of arm numbers on purpose — vary the torso/head instead.
- **A muscle at 0 is the midpoint of its range, not rest.** Leaving legs at 0
  produces a visible half-crouch — that's why `CabinPoseLibrary.Apply` sets
  leg muscles explicitly before layering a profile on top.
- **`CabinPoseOverride` and `CabinCharacterIdle` must run in a specific
  order.** `CabinPoseOverride` writes muscles via `HumanPoseHandler`;
  `CabinCharacterIdle` writes Spine1/Neck/Head bone rotations directly. If the
  override ran *after* the idle script, its `SetHumanPose` would silently wipe
  the waist lean and breathing every frame. `CabinPoseOverride` carries
  `[DefaultExecutionOrder(-50)]` to guarantee it runs first — don't remove
  that attribute, and don't add a competing execution-order override to
  `CabinCharacterIdle` that could put it back in front.
- The cop in `Interrogation.unity` is unrelated: `CopTalkGestureAnimator.cs`
  and `CopIdleAnimator.cs` write bone rotations directly in `LateUpdate` —
  no Animator, no clips, none of the above applies to him.

## Beyond muscles: fingers and exact hand targets

Muscles reach shoulder, elbow, wrist, neck and spine, and the waist lean
reaches the torso. None of that can pose fingers, and none of it can express
"put the hand exactly *here* in world space" — you steer joint angles (and,
for the waist, a lean angle) and read off where things land.

There's no IK in this project (no `com.unity.animation.rigging`, no
`OnAnimatorIK` anywhere). The pattern already in the repo for going further is
a `LateUpdate` script that writes bone rotations directly, running after the
Animator so it wins — see `Assets/_Project/Scripts/Cop/CopTalkGestureAnimator.cs`.
That's a different mode of working (no clips, no Animator at all for the
object it controls) and isn't covered here.
