using System.Collections.Generic;
using System.Linq;
using FalsePositive.Cutscene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Bakes and wires the M2 "out into the snow" cinematic: the arm reaches for
    /// the knob, the door swings, and the camera walks out, settles on a fixed
    /// pose looking left at Nick's body, travels to it and orbits behind it.
    ///
    /// Everything here is generated, not hand-placed, so the beat is byte-identical
    /// every time it is rebuilt and every time it plays. Three assets come out:
    ///
    ///   Animations/Door_SwingOpen.anim   the 100-degree swing, same start/end
    ///                                    rotations SwingDoorOpen() uses today
    ///   Animations/M2_CamPath.anim       the camera path, 30 fps, euler curves
    ///   Timelines/M2_DoorOpen.playable   three AnimationTracks over 13.4 s
    ///
    /// The arm clip itself (Reach_DoorKnob) is *not* built here — it is a humanoid
    /// muscle clip and all muscle authoring lives in CabinAnimationBuilder, so run
    /// "T03c - Build Cast Animation" before this.
    ///
    /// Run order: MemorySceneBuilderV2 -> dressing/wiring -> T04c (bounds) ->
    /// T03c (cast animation) -> T05 (this). Anything that rebuilds the morning
    /// scene from scratch drops the proxy rig and the bindings, so re-run this
    /// after it.
    ///
    /// The one thing that is easy to get wrong here and impossible to see: a
    /// PlayableDirector's generic bindings are keyed to the *TrackAsset object*,
    /// not the track name. CopAnimationBuilder.BuildTimeline deletes and recreates
    /// its track every run, which is fine there because that binding is set
    /// elsewhere afterwards — but doing it here would orphan all three bindings
    /// and the cutscene would play thirteen perfect seconds of nothing (see
    /// Assets/_Project/ASSETS_TODO.md section 1, where exactly this happened
    /// before). So tracks are found by name and reused, and every binding is
    /// re-set in the same run regardless. M2DoorOpenSequence.BindingsResolve
    /// fails loud at runtime if this ever regresses anyway.
    /// </summary>
    public static class M2DoorTimelineBuilder
    {
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";
        private const string AnimationFolder = "Assets/_Project/CabinNight/Animations";
        private const string TimelineFolder = "Assets/_Project/CabinNight/Timelines";
        private const string DoorClipPath = AnimationFolder + "/Door_SwingOpen.anim";
        private const string CameraClipPath = AnimationFolder + "/M2_CamPath.anim";
        private const string TimelinePath = TimelineFolder + "/M2_DoorOpen.playable";
        private const string ArmClipPath = AnimationFolder + "/" + CabinAnimationBuilder.StateReachDoorKnob + ".anim";

        private const string ProxyRootName = "M2_DoorCam";
        private const string ProxyPosName = "M2_DoorCam_Root";
        private const string ProxyPitchName = "M2_DoorCam_Pitch";
        private const string DoorName = "Prop_FrontDoor_Locked";
        private const string PlayerName = "Player (Male - First Person)";
        private const string SequencingName = "Sequencing";

        private const string TrackArm = "Arm";
        private const string TrackDoor = "Door";
        private const string TrackCamera = "Camera";

        // ---- timing ------------------------------------------------------
        // The arm clip is 1.95 s and is keyed reach @0.85, grip @1.25,
        // pull @1.75 (see CabinAnimationBuilder.BuildReachDoorKnob), so placing
        // it at 0.35 puts those beats at 1.20 / 1.60 / 2.10 on the timeline.
        // Every camera beat below is aligned to those three numbers.
        private const double ArmClipStart = 0.35d;
        private const double ArmClipDuration = 1.95d;

        private const float TSettle = 0.35f;   // stood on the mark, nothing moving yet
        private const float TReach = 1.20f;    // hand meets the knob
        private const float TGrip = 1.60f;     // grip closes
        private const float TPull = 2.30f;     // arm has hauled the door toward us
        private const float TStepStart = 2.60f;
        private const float TStepEnd = 4.50f;  // through the doorway, out on the snow
        private const float TTurnEnd = 5.90f;  // the left turn onto the body has landed
        private const float THoldEnd = 6.50f;  // end of the fixed hold
        private const float TEnd = 13.40f;     // end of travel + orbit, end of the timeline

        // Door starts moving between grip and pull — the arm is already loading it
        // before it reaches the fully-pulled pose — and keeps swinging after the
        // hand lets go, which is what a real pulled door does.
        private const double DoorClipStart = 1.85d;
        private const double DoorSwingDuration = 1.10d;

        private const float Fps = 30f;

        // ---- geometry ----------------------------------------------------
        /// <summary>Nick's torso, a little above the snow. Every rotation from
        /// the left turn onward is solved to point at this, which is what keeps
        /// the body centred through the travel *and* the whole orbit.</summary>
        private static readonly Vector3 AimPoint = new Vector3(2.3f, 0.25f, -6.3f);

        /// <summary>Player prefab's FirstPersonView localPosition.y.</summary>
        private const float EyeHeight = 1.64f;

        /// <summary>Where the player is snapped before frame one — see
        /// M2DoorOpenSequence.doorMark, which must match.</summary>
        private static readonly Vector3 DoorMark = new Vector3(-3.33f, 0f, -3.33f);

        /// <summary>Straight out through the 45-degree chamfered wall the front
        /// door sits in, i.e. along (-1, 0, -1).</summary>
        private const float ExitYaw = 225f;

        /// <summary>Exterior snow height — SnowTerrainBuilder.SnowSurfaceY, i.e.
        /// 5 cm under the cabin floor. Must track that constant: the camera root
        /// is the player's feet, so a mismatch is the camera walking through the
        /// snow or floating over it.</summary>
        private const float SnowY = -0.05f;

        private static readonly Vector3 DoorwayCentre = new Vector3(-3.75f, 0f, -3.75f);
        private static readonly Vector3 DoorwayStep = new Vector3(-4.30f, SnowY * 0.5f, -4.30f);
        private static readonly Vector3 DoorwayOutside = new Vector3(-4.60f, SnowY, -4.60f);
        private static readonly Vector3 ChamferCorner = new Vector3(-2.00f, SnowY, -5.80f);

        private const float OrbitRadius = 2.2f;

        /// <summary>Pitch at the knob. Reach_DoorKnob lands the hand ~1.0 m up and
        /// ~0.7 m ahead, so from an eye at 1.64 m the knob sits 42 degrees below
        /// the horizon. Still not a solved look-at — 42 would be a stare at the
        /// floor — but it can no longer be the 15-degree glance it was either:
        /// that was authored back when the arm was invisible and only the lean
        /// had to read, and with a 66-degree vertical FOV (+/-33) it left the
        /// hand sitting on the bottom edge of frame. At 28 the hand is 14 degrees
        /// below centre, about 44% of the way to the edge — low in frame, which
        /// is where a viewmodel belongs, but wholly on screen.</summary>
        private const float KnobPitch = 28f;

        /// <summary>Frame one, before the reach starts. Raised with KnobPitch so
        /// the settle-to-reach ramp stays a glance down rather than a 20-degree
        /// lurch in 0.85 s.</summary>
        private const float StartPitch = 14f;

        /// <summary>Where the eye has come back up to by the time the door is
        /// hauled open. The hand has folded back toward the chest by then and
        /// leaves frame at the bottom as the arm clip ends — that is the intent,
        /// not a miss: you look up and out as the door comes open.</summary>
        private const float PullPitch = 12f;

        /// <summary>How far the camera leans in as the hand goes out, and how
        /// far it rides back as the door is pulled. This used to be the *only*
        /// visible part of the reach — the player's body is ShadowsOnly
        /// (m_CastShadows: 3 on every renderer of the Body prefab instance), so
        /// the arm clip showed up in the shadow and never in frame. Editor.
        /// FirstPersonArmBuilder now carves a right-arm-only skin off the same
        /// skeleton and Cutscene.M2DoorOpenSequence switches it on for this
        /// sequence, so the reach is literally on screen; the lean stays because
        /// body motion is what sells the weight of the door.</summary>
        private const float LeanIn = 0.14f;
        private const float PullBack = 0.22f;

        // ---- the door ----------------------------------------------------
        // Both duplicated from CutsceneStage.SwingDoorOpen so the Timeline swing
        // is visually identical to the coroutine it replaces.
        private static readonly Quaternion DoorClosed = Quaternion.Euler(270f, 0f, 0f);
        private const float DoorOpenYawDegrees = 100f;

        [MenuItem("Tools/False Positive/Bootstrap/T05 - Build M2 Door Timeline")]
        public static void Build()
        {
            AnimationClip arm = AssetDatabase.LoadAssetAtPath<AnimationClip>(ArmClipPath);
            if (arm == null)
            {
                Debug.LogError($"[M2Timeline] {ArmClipPath} is missing. Run " +
                               "'Tools/False Positive/Bootstrap/T03c - Build Cast Animation' first — " +
                               "the reach is a humanoid muscle clip and lives in CabinAnimationBuilder.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MorningScenePath, OpenSceneMode.Single);

            GameObject door = GameObject.Find(DoorName);
            if (door == null)
            {
                Debug.LogError($"[M2Timeline] No '{DoorName}' in {MorningScenePath} — run MemorySceneBuilderV2 first.");
                return;
            }

            EnsureFolder(TimelineFolder);

            AnimationClip doorClip = BuildDoorClip();
            AnimationClip cameraClip = BuildCameraClip();
            TimelineAsset timeline = BuildTimeline(arm, doorClip, cameraClip, door.transform);

            StageScene(timeline, door);

            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[M2Timeline] Built {TimelinePath} ({TEnd:F2}s) and staged it into Memory_CabinMorning: " +
                      $"door swing {DoorClipStart:F2}-{DoorClipStart + DoorSwingDuration:F2}s, " +
                      $"arm {ArmClipStart:F2}-{ArmClipStart + ArmClipDuration:F2}s, " +
                      $"fixed hold {TTurnEnd:F2}-{THoldEnd:F2}s, orbit {TEnd - 6.9f:F2}-{TEnd:F2}s.");
        }

        // =====================================================================
        // Door clip
        // =====================================================================

        /// <summary>The 100-degree swing, baked in the door's *own* space as a
        /// delta from the closed pose, to be composed with a track offset equal
        /// to the door's scene transform.
        ///
        /// The obvious version — bake the absolute world rotation at path "" and
        /// give the track a zero offset — does not work, and fails silently. A
        /// curve on the bound Animator's own transform is read as root motion,
        /// which Unity re-bases on the clip's first frame: the track evaluates
        /// offset * q(t) * q(0)^-1, not offset * q(t). With a zero offset and a
        /// clip starting at Euler(270,0,0), that put the door at the world origin
        /// with identity rotation for the whole pre-swing hold and then swung it
        /// to a bare Euler(0,100,0). The T05 verifier measured exactly that:
        /// 90.00 degrees off the closed pose before the swing, 125.93 after,
        /// hinge parked at (0,0,0) throughout. AnimationClipSettings' "Based Upon:
        /// Original" flags do not override it either — ApplyTransformOffsets
        /// applies the track offset itself.
        ///
        /// So the clip is authored to *be* the delta. q_local(t) is the world
        /// yaw conjugated into closed-door space, which starts at identity by
        /// construction, and the track offset supplies the closed pose:
        ///
        ///   offset * q_local(t) * q_local(0)^-1
        ///     = closed * closed^-1 * Ry(theta) * closed
        ///     = Ry(theta) * closed          <- the world swing SwingDoorOpen() does
        ///
        /// Position is a constant zero for the same reason: the offset carries the
        /// hinge, and the root-motion delta p(t) - p(0) must therefore be zero.</summary>
        private static AnimationClip BuildDoorClip()
        {
            AnimationClip clip = LoadOrCreateClip(DoorClipPath);
            clip.ClearCurves();
            clip.frameRate = Fps;

            Quaternion closedInverse = Quaternion.Inverse(DoorClosed);

            int frames = Mathf.RoundToInt((float)DoorSwingDuration * Fps);
            var x = new AnimationCurve();
            var y = new AnimationCurve();
            var z = new AnimationCurve();
            var w = new AnimationCurve();

            for (int i = 0; i <= frames; i++)
            {
                float t = i / (float)frames;
                float time = i / Fps;

                // Smoothstep rather than linear: a door that starts and stops
                // dead is the giveaway that nothing physical is pulling it.
                float yaw = DoorOpenYawDegrees * Ease(t);
                Quaternion r = closedInverse * Quaternion.Euler(0f, yaw, 0f) * DoorClosed;
                x.AddKey(time, r.x);
                y.AddKey(time, r.y);
                z.AddKey(time, r.z);
                w.AddKey(time, r.w);
            }

            SmoothAll(x, y, z, w);

            SetCurve(clip, "", "m_LocalRotation.x", x);
            SetCurve(clip, "", "m_LocalRotation.y", y);
            SetCurve(clip, "", "m_LocalRotation.z", z);
            SetCurve(clip, "", "m_LocalRotation.w", w);

            SetCurve(clip, "", "m_LocalPosition.x", Constant(0f, (float)DoorSwingDuration));
            SetCurve(clip, "", "m_LocalPosition.y", Constant(0f, (float)DoorSwingDuration));
            SetCurve(clip, "", "m_LocalPosition.z", Constant(0f, (float)DoorSwingDuration));

            SetClipSettings(clip, false);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        // =====================================================================
        // Camera clip
        // =====================================================================

        /// <summary>The camera path, sampled at 30 fps onto the two child nodes
        /// of the proxy rig.
        ///
        /// Euler curves, not quaternion: the orbit sweeps a continuous 164 degrees
        /// and the exit-to-body turn another 121, and quaternion keys sign-flip
        /// partway through an arc that long. localEulerAnglesRaw specifically —
        /// it is the unclamped channel, so a yaw baked as 340 -> 380 stays a
        /// 40-degree turn instead of becoming a 320-degree spin the other way.
        /// Yaw is unwrapped across the whole clip below for exactly this reason.
        ///
        /// Nothing is written at path "", so the Animator's own transform is never
        /// touched and AnimationTrack's offset modes cannot affect the result.</summary>
        private static AnimationClip BuildCameraClip()
        {
            AnimationClip clip = LoadOrCreateClip(CameraClipPath);
            clip.ClearCurves();
            clip.frameRate = Fps;

            var px = new AnimationCurve();
            var py = new AnimationCurve();
            var pz = new AnimationCurve();
            var yaw = new AnimationCurve();
            var pitch = new AnimationCurve();

            int frames = Mathf.RoundToInt(TEnd * Fps);
            float unwrapped = ExitYaw;
            float previousRaw = ExitYaw;

            for (int i = 0; i <= frames; i++)
            {
                float time = i / Fps;
                Sample(time, out Vector3 root, out float rawYaw, out float pitchDegrees);

                // Accumulate the shortest signed step each frame rather than
                // taking the raw angle, so the curve is one continuous line
                // through the wrap point instead of a 360-degree cliff.
                if (i > 0) unwrapped += Mathf.DeltaAngle(previousRaw, rawYaw);
                previousRaw = rawYaw;

                px.AddKey(time, root.x);
                py.AddKey(time, root.y);
                pz.AddKey(time, root.z);
                yaw.AddKey(time, unwrapped);
                pitch.AddKey(time, pitchDegrees);
            }

            SmoothAll(px, py, pz, yaw, pitch);

            SetCurve(clip, ProxyPosName, "m_LocalPosition.x", px);
            SetCurve(clip, ProxyPosName, "m_LocalPosition.y", py);
            SetCurve(clip, ProxyPosName, "m_LocalPosition.z", pz);
            SetCurve(clip, ProxyPosName, "localEulerAnglesRaw.y", yaw);
            SetCurve(clip, ProxyPosName + "/" + ProxyPitchName, "localEulerAnglesRaw.x", pitch);

            SetClipSettings(clip, false);
            EditorUtility.SetDirty(clip);
            return clip;
        }

        /// <summary>The whole camera path as one function of time. Written as a
        /// sampler rather than a keyframe table so every value is derived from
        /// the geometry above — move the body and the entire back half re-solves
        /// itself.</summary>
        private static void Sample(float t, out Vector3 root, out float yaw, out float pitch)
        {
            Vector3 forward = new Vector3(Mathf.Sin(ExitYaw * Mathf.Deg2Rad), 0f, Mathf.Cos(ExitYaw * Mathf.Deg2Rad));
            Vector3 leaned = DoorMark + forward * LeanIn;
            Vector3 pulled = leaned - forward * PullBack;

            if (t <= TSettle)
            {
                // Stood on the mark. Held flat so the snap in Play() is never
                // visible as a jump on frame one.
                root = DoorMark;
                yaw = ExitYaw;
                pitch = StartPitch;
                return;
            }

            if (t <= TReach)
            {
                // Reaching: lean toward the knob and glance down at it.
                float u = Ease(Mathf.InverseLerp(TSettle, TReach, t));
                root = Vector3.Lerp(DoorMark, leaned, u);
                yaw = ExitYaw;
                pitch = Mathf.Lerp(StartPitch, KnobPitch, u);
                return;
            }

            if (t <= TGrip)
            {
                // Grip closing. Dead still — the pause is what sells the grab.
                root = leaned;
                yaw = ExitYaw;
                pitch = KnobPitch;
                return;
            }

            if (t <= TPull)
            {
                // Pulling: ride back with the door and lift the eye off the knob
                // and out through the opening gap.
                float u = Ease(Mathf.InverseLerp(TGrip, TPull, t));
                root = Vector3.Lerp(leaned, pulled, u);
                yaw = ExitYaw;
                pitch = Mathf.Lerp(KnobPitch, PullPitch, u);
                return;
            }

            if (t <= TStepStart)
            {
                root = pulled;
                yaw = ExitYaw;
                pitch = PullPitch;
                return;
            }

            if (t <= TStepEnd)
            {
                // Through the doorway and down onto the snow, still facing
                // straight out — the turn is deliberately a separate beat.
                float u = Ease(Mathf.InverseLerp(TStepStart, TStepEnd, t));
                root = WalkPolyline(new[] { pulled, DoorwayCentre, DoorwayStep, DoorwayOutside }, u);
                yaw = ExitYaw;
                pitch = Mathf.Lerp(PullPitch, 0f, u);
                return;
            }

            SolveLookAt(DoorwayOutside, out float outsideYaw, out float outsidePitch);

            if (t <= TTurnEnd)
            {
                // The left turn. DeltaAngle picks the short way round, which from
                // 225 to 104 is anticlockwise — a turn to the *left*, onto the body.
                float u = Ease(Mathf.InverseLerp(TStepEnd, TTurnEnd, t));
                root = DoorwayOutside;
                yaw = ExitYaw + Mathf.DeltaAngle(ExitYaw, outsideYaw) * u;
                pitch = Mathf.Lerp(0f, outsidePitch, u);
                return;
            }

            if (t <= THoldEnd)
            {
                // The fixed beat. Not a solved value that happens to be stable —
                // this pose is the same three numbers on every playthrough
                // because Play() snaps the player to the door mark first, so
                // everything upstream of here is identical too.
                root = DoorwayOutside;
                yaw = outsideYaw;
                pitch = outsidePitch;
                return;
            }

            // Travel and orbit as one arc-length-parameterised move under a
            // single ease. Easing them separately would stop the camera dead at
            // the moment it arrives, which reads as a mistake rather than a beat.
            float travel = Ease(Mathf.InverseLerp(THoldEnd, TEnd, t));
            root = WalkPolyline(TravelAndOrbitPath(), travel);
            SolveLookAt(root, out yaw, out pitch);
        }

        /// <summary>Doorway -> chamfer corner -> onto the orbit circle -> around
        /// the far side of the body. Built once as a dense polyline so the whole
        /// move can be walked by arc length and the camera holds a constant speed
        /// through the corner where the straight becomes the circle.</summary>
        private static Vector3[] TravelAndOrbitPath()
        {
            Vector2 body = new Vector2(AimPoint.x, AimPoint.z);

            // Enter the circle on the bearing we are already approaching from,
            // so the straight meets the arc tangentially instead of kinking.
            Vector2 approach = new Vector2(ChamferCorner.x, ChamferCorner.z) - body;
            float startAngle = Mathf.Atan2(approach.y, approach.x) * Mathf.Rad2Deg;

            // "Behind the body" = the far side from the door. Nick's head is
            // turned toward the cabin, so this genuinely ends up behind him.
            Vector2 awayFromDoor = body - new Vector2(DoorwayCentre.x, DoorwayCentre.z);
            float endAngle = Mathf.Atan2(awayFromDoor.y, awayFromDoor.x) * Mathf.Rad2Deg;

            // Anticlockwise, always. The sweep is ~164 degrees, close enough to a
            // half circle that "shortest arc" could flip direction on a small
            // change to any of these positions — and the clockwise half passes
            // straight through where Aaron, Ivy and Priya are standing.
            float sweep = Mathf.Repeat(endAngle - startAngle, 360f);

            var points = new List<Vector3> { DoorwayOutside, ChamferCorner };

            const float stepDegrees = 3f;
            int steps = Mathf.CeilToInt(sweep / stepDegrees);
            for (int i = 0; i <= steps; i++)
            {
                float angle = (startAngle + sweep * (i / (float)steps)) * Mathf.Deg2Rad;
                points.Add(new Vector3(
                    body.x + Mathf.Cos(angle) * OrbitRadius,
                    SnowY,
                    body.y + Mathf.Sin(angle) * OrbitRadius));
            }

            return points.ToArray();
        }

        /// <summary>Yaw and pitch that put AimPoint dead centre from a camera
        /// whose root is at <paramref name="root"/>.</summary>
        private static void SolveLookAt(Vector3 root, out float yaw, out float pitch)
        {
            Vector3 to = AimPoint - (root + Vector3.up * EyeHeight);
            float flat = new Vector2(to.x, to.z).magnitude;

            // Unity yaw is measured from +Z toward +X, and positive euler x is
            // nose-down, hence the sign flip on pitch.
            yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
            pitch = -Mathf.Atan2(to.y, flat) * Mathf.Rad2Deg;
        }

        /// <summary>Position at normalised distance <paramref name="u"/> along a
        /// polyline. By arc length, not by segment index, so a long straight and
        /// a short one are not traversed in the same amount of time.</summary>
        private static Vector3 WalkPolyline(Vector3[] points, float u)
        {
            if (points.Length == 1) return points[0];

            var lengths = new float[points.Length - 1];
            float total = 0f;
            for (int i = 0; i < lengths.Length; i++)
            {
                lengths[i] = Vector3.Distance(points[i], points[i + 1]);
                total += lengths[i];
            }

            if (total <= Mathf.Epsilon) return points[0];

            float target = Mathf.Clamp01(u) * total;
            for (int i = 0; i < lengths.Length; i++)
            {
                if (target <= lengths[i] || i == lengths.Length - 1)
                {
                    float local = lengths[i] <= Mathf.Epsilon ? 0f : Mathf.Clamp01(target / lengths[i]);
                    return Vector3.Lerp(points[i], points[i + 1], local);
                }

                target -= lengths[i];
            }

            return points[points.Length - 1];
        }

        private static float Ease(float u)
        {
            u = Mathf.Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        // =====================================================================
        // Timeline asset
        // =====================================================================

        private static TimelineAsset BuildTimeline(AnimationClip arm, AnimationClip door, AnimationClip camera,
            Transform doorTransform)
        {
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, TimelinePath);
            }

            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = TEnd;

            // Muscle-only clip with no root curves, on an Animator that carries
            // CabinCast.controller and is a child of a root this cinematic is
            // moving every frame. ApplySceneOffsets is the mode that means
            // "leave the object where it is" — the one thing this track must not
            // do is decide the player's Body belongs at the world origin.
            AddClip(timeline, TrackArm, arm, ArmClipStart, ArmClipDuration,
                TrackOffset.ApplySceneOffsets, easeIn: 0.15d, easeOut: 0.2d);

            // The one track whose offset is not zero. Door_SwingOpen is baked as a
            // delta from the closed pose (see BuildDoorClip), so the offset is what
            // supplies the hinge's actual place in the world — written explicitly
            // from the scene rather than left to ApplySceneOffsets, so the swing is
            // reproducible from the asset alone and a door left ajar in the scene
            // can never re-base it.
            AnimationTrack doorTrack = AddClip(timeline, TrackDoor, door, DoorClipStart, DoorSwingDuration,
                TrackOffset.ApplyTransformOffsets, easeIn: 0d, easeOut: 0d);
            doorTrack.position = doorTransform.localPosition;
            doorTrack.rotation = DoorClosed;

            AddClip(timeline, TrackCamera, camera, 0d, TEnd,
                TrackOffset.ApplyTransformOffsets, easeIn: 0d, easeOut: 0d);

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        /// <summary>Finds the track by name and reuses it, replacing only its
        /// clips. Never DeleteTrack + CreateTrack: that would orphan the
        /// PlayableDirector's binding for the track (see the class doc), and
        /// CreateTrack also uniquifies names, so the rebuilt "Camera" would come
        /// back as "Camera 1" and the name lookup would never match again.</summary>
        private static AnimationTrack AddClip(TimelineAsset timeline, string trackName, AnimationClip clip,
            double start, double duration, TrackOffset offset, double easeIn, double easeOut)
        {
            AnimationTrack track = timeline.GetOutputTracks()
                .OfType<AnimationTrack>()
                .FirstOrDefault(t => t.name == trackName);

            if (track == null) track = timeline.CreateTrack<AnimationTrack>(null, trackName);

            foreach (TimelineClip existing in track.GetClips().ToList())
            {
                timeline.DeleteClip(existing);
            }

            track.trackOffset = offset;
            track.position = Vector3.zero;
            track.rotation = Quaternion.identity;

            TimelineClip timelineClip = track.CreateClip(clip);
            timelineClip.start = start;
            timelineClip.duration = duration;
            timelineClip.easeInDuration = easeIn;
            timelineClip.easeOutDuration = easeOut;

            if (timelineClip.asset is AnimationPlayableAsset animation)
            {
                animation.loop = AnimationPlayableAsset.LoopMode.Off;
                animation.position = Vector3.zero;
                animation.rotation = Quaternion.identity;
            }

            return track;
        }

        // =====================================================================
        // Scene staging
        // =====================================================================

        private static void StageScene(TimelineAsset timeline, GameObject door)
        {
            // --- proxy rig ---
            // Three nodes on purpose. The Animator sits on a node nothing
            // animates, so the two nodes that *are* animated are plain children
            // and no offset mode can reinterpret their curves. And because the
            // Animator's node stays at the origin with identity rotation, the
            // baked local values are literally world values.
            GameObject proxy = FindOrCreate(ProxyRootName, null);
            proxy.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            proxy.transform.localScale = Vector3.one;

            GameObject proxyPos = FindOrCreate(ProxyPosName, proxy.transform);
            GameObject proxyPitch = FindOrCreate(ProxyPitchName, proxyPos.transform);

            Animator proxyAnimator = Ensure<Animator>(proxy);
            proxyAnimator.runtimeAnimatorController = null;
            proxyAnimator.applyRootMotion = false;
            // Off-screen and with no renderer of its own, so the default
            // culling mode would let Unity skip evaluating it entirely.
            proxyAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // --- door ---
            Animator doorAnimator = Ensure<Animator>(door);
            doorAnimator.runtimeAnimatorController = null;
            doorAnimator.applyRootMotion = false;
            doorAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            // --- director ---
            GameObject sequencing = FindOrCreate(SequencingName, null);
            PlayableDirector director = Ensure<PlayableDirector>(sequencing);
            director.playableAsset = timeline;
            director.playOnAwake = false;
            // Hold would leave the director running past the end and `stopped`
            // would never fire, so M2DoorOpenSequence would never hand control
            // back and the beat would soft-lock.
            director.extrapolationMode = DirectorWrapMode.None;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;

            // --- bindings ---
            Animator armAnimator = FindArmAnimator();

            Bind(director, timeline, TrackCamera, proxyAnimator);
            Bind(director, timeline, TrackDoor, doorAnimator);
            Bind(director, timeline, TrackArm, armAnimator);

            // --- runtime component ---
            M2DoorOpenSequence sequence = Ensure<M2DoorOpenSequence>(sequencing);
            var serialized = new SerializedObject(sequence);
            serialized.FindProperty("director").objectReferenceValue = director;
            serialized.FindProperty("proxyRoot").objectReferenceValue = proxyPos.transform;
            serialized.FindProperty("proxyPitch").objectReferenceValue = proxyPitch.transform;
            serialized.FindProperty("doorMark").vector3Value = DoorMark;
            serialized.FindProperty("doorMarkYaw").floatValue = ExitYaw;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(director);
            EditorUtility.SetDirty(sequence);
        }

        /// <summary>The Animator on the player's "Body" child — never the one a
        /// GetComponentInChildren from the root would return if anything ever
        /// added an Animator to the root itself, which is the trap this whole
        /// proxy design exists to avoid.</summary>
        private static Animator FindArmAnimator()
        {
            GameObject player = GameObject.Find(PlayerName);
            if (player == null)
            {
                Debug.LogWarning($"[M2Timeline] No '{PlayerName}' in the scene — the Arm track is left unbound " +
                                 "and M2DoorOpenSequence will skip the cinematic until it is.");
                return null;
            }

            Transform body = player.transform.Find("Body");
            if (body == null)
            {
                Debug.LogWarning("[M2Timeline] Player has no 'Body' child — the Arm track is left unbound.");
                return null;
            }

            Animator animator = body.GetComponent<Animator>();
            if (animator == null) Debug.LogWarning("[M2Timeline] Player 'Body' has no Animator — the Arm track is left unbound.");
            return animator;
        }

        private static void Bind(PlayableDirector director, TimelineAsset timeline, string trackName, Object target)
        {
            TrackAsset track = timeline.GetOutputTracks().FirstOrDefault(t => t.name == trackName);
            if (track == null)
            {
                Debug.LogError($"[M2Timeline] Track '{trackName}' vanished between build and bind.");
                return;
            }

            director.SetGenericBinding(track, target);
        }

        // =====================================================================
        // Small helpers
        // =====================================================================

        private static GameObject FindOrCreate(string name, Transform parent)
        {
            if (parent != null)
            {
                Transform child = parent.Find(name);
                if (child != null) return child.gameObject;
            }
            else
            {
                GameObject existing = GameObject.Find(name);
                if (existing != null) return existing;
            }

            var created = new GameObject(name);
            if (parent != null) created.transform.SetParent(parent, false);
            created.transform.localPosition = Vector3.zero;
            created.transform.localRotation = Quaternion.identity;
            created.transform.localScale = Vector3.one;
            return created;
        }

        private static T Ensure<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            return component != null ? component : go.AddComponent<T>();
        }

        private static AnimationClip LoadOrCreateClip(string path)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (clip != null) return clip;

            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
        {
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), property), curve);
        }

        private static AnimationCurve Constant(float value, float length)
        {
            var curve = new AnimationCurve();
            curve.AddKey(0f, value);
            curve.AddKey(length, value);
            return curve;
        }

        private static void SmoothAll(params AnimationCurve[] curves)
        {
            foreach (AnimationCurve curve in curves)
            {
                for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            }
        }

        /// <summary>Non-looping, and — the part that matters — root curves read as
        /// absolute values rather than as motion relative to frame zero.
        ///
        /// Unity's generic root motion is re-based on the clip's first frame by
        /// default ("Based Upon: Body Orientation"), so a root rotation curve
        /// evaluates to q(t) * q(0)^-1, not q(t). The door clip starts at
        /// Euler(270,0,0), so the default reading put the door at *identity*
        /// rotation and the world origin for the whole pre-swing hold, then swung
        /// it to a bare Euler(0,100,0) — measured at 90 and 125.93 degrees off the
        /// closed pose by the T05 verifier. keepOriginalOrientation /
        /// keepOriginalPositionY / keepOriginalPositionXZ are "Based Upon:
        /// Original", which hands the baked values through untouched.</summary>
        private static void SetClipSettings(AnimationClip clip, bool loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder)) return;

            int slash = folder.LastIndexOf('/');
            string parent = folder.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folder.Substring(slash + 1));
        }
    }
}
