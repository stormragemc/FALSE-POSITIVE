using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Timeline;

namespace FalsePositive.Editor
{
    /// <summary>
    /// SUPERSEDED, kept on disk unreferenced (matching this project's
    /// convention for the old T1 cop assets — nothing calls EnsureBuilt/
    /// Build any more, and Cutscene.CutsceneAnimationDirector no longer
    /// plays a Timeline clip at all). The talking body is now driven
    /// procedurally at runtime by Cop.CopTalkGestureAnimator, off uLipSync's
    /// own analyzed volume — see that class's doc for why: this class's
    /// Timeline output binding went null on every re-run of
    /// Editor.ProjectBootstrapBuilder.WireAnimationDirector (BuildTimeline
    /// deletes/recreates the track object each time, orphaning the scene
    /// binding), and even when bound it only ever covered
    /// CutsceneId.SpasskyAnswer — every live dialogue turn, the bulk of
    /// play, had a static body regardless.
    ///
    /// Original doc, still accurate for what this file itself does: bakes a
    /// "talking with hands" gesture clip for the cop rig — asymmetric
    /// arm/forearm/hand sway toward the table plus small head/chest life —
    /// and a one-track Timeline asset.
    ///
    /// Superseded design vs. the previous (jaw-bone-era) version of this
    /// file: that version wrote a single "Spine Front-Back" muscle at 0 for
    /// Cop_Idle and assumed muscle-0 reproduces the FBX's seated bind pose.
    /// Both assumptions were wrong — see ASSETS_TODO.md #2 for the resulting
    /// sink bug — and this rig (NewCop_rigged.fbx, an Avaturn T2 export) has
    /// no jaw bone at all, so there is no jaw curve to author here any more;
    /// mouth motion is entirely real-time blendshape-driven (see
    /// Editor.ProjectBootstrapBuilder.WireCopModel and
    /// Cutscene.CutsceneAnimationDirector). There is also no permanent
    /// Cop_Idle/AnimatorController any more — see ProjectBootstrapBuilder's
    /// WireAnimationDirector doc for why: Timeline's AnimationTrack binds
    /// directly to the Animator component with no controller needed, so
    /// nothing ever drives this rig outside the few seconds a cutscene
    /// plays.
    ///
    /// Every muscle curve is authored from a REAL measured base pose
    /// (MeasureBasePose, via HumanPoseHandler.GetHumanPose on the Cop
    /// GameObject actually in the currently-open scene) rather than assumed
    /// zero — matching Scripts/Editor/CabinAnimationBuilder's own rule
    /// (BaseMuscles always reads a real profile, never assumes 0) and
    /// specifically fixing the gap the sink bug exposed. This means
    /// EnsureBuilt/Build must run with Interrogation.unity open and the
    /// Cop's new rig already wired (Editor.ProjectBootstrapBuilder calls
    /// WireCopModel() immediately before this).
    ///
    /// Curve binding is still the CabinAnimationBuilder trick:
    ///
    ///  EditorCurveBinding.FloatCurve("", typeof(Animator), muscleName),
    ///
    /// muscleName taken verbatim from HumanTrait.MuscleName — humanMotion
    /// curves are the only form that survive humanoid retargeting (see
    /// CabinAnimationBuilder's class doc fact #1).
    ///
    /// While this clip plays, CopIdleAnimator must be disabled
    /// (CutsceneAnimationDirector does this) — it writes spine/neck/head in
    /// LateUpdate and would otherwise fight this clip's torso/head curves.
    ///
    /// NEW assets (first run) need their containing folder to exist as a
    /// Unity-recognised asset before AssetDatabase.CreateAsset runs, or it
    /// can hang the calling process on a folder-import prompt — BuildInternal
    /// creates both folders via raw System.IO calls and calls
    /// AssetDatabase.Refresh() first, matching CabinAnimationBuilder.
    /// </summary>
    public static class CopAnimationBuilder
    {
        private const string AnimRoot = "Assets/_Project/Art/Animations/Cop/";
        private const string TimelineRoot = "Assets/_Project/Art/Timelines/";
        private const string TimelinePath = TimelineRoot + "Cutscene_SpasskyAnswer.playable";

        public const string ClipTalk = "Cop_Talk";

        // A "talking with hands" gesture reads better slower than the old
        // jaw-only 1.2s loop — CutsceneAnimationDirector loops this for as
        // long as the cutscene's VO actually runs (via the Timeline clip's
        // AnimationPlayableAsset.loop below), not a fixed keyframed duration.
        private const float GestureClipLength = 2.4f;
        private const int CycleSamples = 13;

        [MenuItem("Tools/False Positive/Bootstrap/T03d - Build Cop Animation")]
        public static void Build() => EnsureBuilt(force: true);

        /// <summary>Builds only if the Timeline asset doesn't exist yet —
        /// mirrors CabinAnimationBuilder.EnsureBuilt's guard so a future
        /// caller (e.g. a scene builder) can call this defensively without
        /// re-baking on every run.</summary>
        public static void EnsureBuilt(bool force = false)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath) != null) return;
            BuildInternal();
        }

        private static void BuildInternal()
        {
            Directory.CreateDirectory(AnimRoot);
            Directory.CreateDirectory(TimelineRoot);
            AssetDatabase.Refresh();

            LogBodyMuscleNames();

            float[] baseMuscles = MeasureBasePose(out float bodyPositionY);
            Debug.Log($"[CopAnimationBuilder] Measured rest bodyPosition: y={bodyPositionY:F3} " +
                "(seat target ~0.45 — this clip carries no RootT/RootQ curves, so this is diagnostic " +
                "only; see CutsceneAnimationDirector for whether a driver-side offset ended up needed).");

            AnimationClip talk = BuildGestureClip(baseMuscles);
            BuildTimeline(talk);

            AssetDatabase.SaveAssets();
            Debug.Log("[CopAnimationBuilder] Cop_Talk clip + Cutscene_SpasskyAnswer.playable built.");
        }

        // ---- Muscle discovery / base pose ----

        private static void LogBodyMuscleNames()
        {
            Debug.Log("[CopAnimationBuilder] HumanTrait.MuscleName (body muscles, first 55): " +
                string.Join(", ", HumanTrait.MuscleName.Take(55)));
        }

        /// <summary>Reads the Cop's CURRENT rest HumanPose (the new rig's
        /// baked-seated bind pose, since nothing drives the Animator outside
        /// a cutscene) via HumanPoseHandler, the same mechanism Mecanim
        /// itself uses — round-tripping through it (rather than assuming
        /// muscle-0) is what guarantees writing these values back as
        /// constant curves reproduces this exact pose. Values can
        /// legitimately fall outside the nominal [-1,1] muscle range for a
        /// non-T-pose rest configuration; that is not a bug; see class
        /// doc.</summary>
        private static float[] MeasureBasePose(out float bodyPositionY)
        {
            GameObject cop = GameObject.Find("Cop");
            if (cop == null)
            {
                throw new InvalidOperationException(
                    "[CopAnimationBuilder] No 'Cop' GameObject in the open scene — cannot measure a base pose.");
            }

            Animator animator = cop.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                throw new InvalidOperationException(
                    "[CopAnimationBuilder] Cop has no valid Humanoid Animator to measure a base pose from — " +
                    "run ProjectBootstrapBuilder.WireCopModel first.");
            }

            HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
            HumanPose pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            bodyPositionY = pose.bodyPosition.y;

            float[] baseMuscles = new float[55];
            Array.Copy(pose.muscles, baseMuscles, 55);
            return baseMuscles;
        }

        // ---- Gesture clip ----

        private readonly struct Oscillator
        {
            public readonly string Muscle;
            public readonly float Amplitude;
            public readonly float Phase;
            public readonly float BaseBias;

            public Oscillator(string muscle, float amplitude, float phase, float baseBias = 0f)
            {
                Muscle = muscle;
                Amplitude = amplitude;
                Phase = phase;
                BaseBias = baseBias;
            }
        }

        /// <summary>Asymmetric "explaining with your hands" sway: both arms
        /// lift forward from the measured resting-at-sides base (BaseBias)
        /// and oscillate, but with different amplitude/phase per arm/joint
        /// so it doesn't read as a mirrored clap — a plain left/right mirror
        /// is the single biggest tell that a gesture clip is procedural
        /// rather than a performance. Same base period for every muscle
        /// (CycleClip-style oscillators only vary amplitude/phase, not
        /// frequency) — phase offsets alone are enough for organic-looking
        /// asymmetry, matching CabinAnimationBuilder's own idle clips.
        ///
        /// Sign/magnitude verified empirically against this specific rig via
        /// HumanPoseHandler.SetHumanPose experiments (not assumed): the
        /// seated bake's resting "Arm Front-Back" is already a strongly
        /// positive ~0.22 (arms tucked behind/at the sides), and NEGATIVE
        /// deltas are what swing the hand toward the table (world -Z, since
        /// Cop faces world -Z) — the opposite of CabinAnimationBuilder's
        /// walk-cycle sign convention, which starts from a T-pose-relative
        /// base, not this seated-and-baked one. -0.5..-1.0 delta is the
        /// sweet spot; beyond -1.0 the hand curls back the other way. Lands
        /// hands at roughly world Y=0.6, Z=1.25 at the bias midpoint — a
        /// bit short of the table's own Z~0.6 (that gap would need a much
        /// larger reach than a shoulder+elbow sway alone), but close/raised
        /// enough to read as gesturing near the table, not an exact IK
        /// placement. Adjust by eye against a camera capture, same as
        /// Cutscene.CutsceneStage's own "first pass, not tuned" offsets.</summary>
        private static Oscillator[] GestureOscillators() => new[]
        {
            new Oscillator("Left Arm Front-Back", 0.25f, 0.0f, baseBias: -0.65f),
            new Oscillator("Right Arm Front-Back", 0.20f, 1.3f, baseBias: -0.55f),
            new Oscillator("Left Arm Down-Up", 0.12f, 0.6f, baseBias: 0.32f),
            new Oscillator("Right Arm Down-Up", 0.15f, 2.0f, baseBias: 0.28f),
            new Oscillator("Left Forearm Stretch", 0.15f, 0.3f, baseBias: 0.20f),
            new Oscillator("Right Forearm Stretch", 0.18f, 1.7f, baseBias: 0.25f),
            new Oscillator("Left Hand Down-Up", 0.20f, 0.9f),
            new Oscillator("Right Hand Down-Up", 0.22f, 2.4f),
            new Oscillator("Head Nod Down-Up", 0.04f, 0.2f),
            new Oscillator("Head Tilt Left-Right", 0.025f, 1.6f),
            new Oscillator("Chest Front-Back", 0.02f, 0.9f),
        };

        private static AnimationClip BuildGestureClip(float[] baseMuscles)
        {
            Oscillator[] oscillators = GestureOscillators();
            AnimationClip clip = LoadOrCreateClip(ClipTalk);
            ClearAllCurves(clip);

            for (int m = 0; m < 55; m++)
            {
                string muscleName = HumanTrait.MuscleName[m];
                Oscillator? osc = FindOscillator(oscillators, muscleName);
                AnimationCurve curve;
                if (osc.HasValue)
                {
                    float baseValue = baseMuscles[m] + osc.Value.BaseBias;
                    Keyframe[] keys = new Keyframe[CycleSamples];
                    for (int s = 0; s < CycleSamples; s++)
                    {
                        float t = GestureClipLength * s / (CycleSamples - 1);
                        float angle = 2f * Mathf.PI * s / (CycleSamples - 1) + osc.Value.Phase;
                        keys[s] = new Keyframe(t, baseValue + osc.Value.Amplitude * Mathf.Sin(angle));
                    }
                    curve = new AnimationCurve(keys);
                    for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
                }
                else
                {
                    curve = AnimationCurve.Constant(0f, GestureClipLength, baseMuscles[m]);
                }
                WriteMuscle(clip, muscleName, curve);
            }

            SetClipSettings(clip, loop: true);
            return clip;
        }

        private static Oscillator? FindOscillator(Oscillator[] oscillators, string muscleName)
        {
            foreach (Oscillator o in oscillators)
            {
                if (string.Equals(o.Muscle, muscleName, StringComparison.Ordinal)) return o;
            }
            return null;
        }

        // ---- Shared low-level helpers (mirrors CabinAnimationBuilder) ----

        private static void WriteMuscle(AnimationClip clip, string muscleName, AnimationCurve curve)
        {
            int index = Array.FindIndex(HumanTrait.MuscleName,
                n => string.Equals(n, muscleName, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new InvalidOperationException($"[CopAnimationBuilder] Unknown muscle '{muscleName}'.");
            }
            if (index >= 55)
            {
                throw new InvalidOperationException(
                    $"[CopAnimationBuilder] '{muscleName}' is a finger muscle (index {index}); not supported here.");
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscleName), curve);
        }

        private static void ClearAllCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static void SetClipSettings(AnimationClip clip, bool loop)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = loop;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static AnimationClip LoadOrCreateClip(string name)
        {
            string path = AnimRoot + name + ".anim";
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null) return existing;

            AnimationClip clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
            return clip;
        }

        // ---- Timeline ----

        /// <summary>One AnimationTrack ("Cop"), one clip (Cop_Talk). Ease-in/
        /// out is NOT polish here: SpasskyAnswer plays with keepScreenLit
        /// true (Editor.CutsceneRecipeBuilder), so there is no fade to hide
        /// the cop snapping from CopIdleAnimator's current pose to the
        /// clip's frame 0 — Timeline blends the track against the
        /// underlying pose instead, on both entry and the
        /// CutsceneAnimationDirector-triggered exit.
        ///
        /// The gesture needs to keep repeating for however long
        /// CutsceneAnimationDirector keeps the PlayableDirector playing —
        /// the actual VO length, unknown at build time — not just this
        /// clip's own length. TimelineClip.preExtrapolationMode/
        /// postExtrapolationMode are read-only in this Timeline package
        /// version (auto-computed, not settable), so looping is done the
        /// other way Timeline supports it: the underlying
        /// AnimationPlayableAsset's own `loop` flag, with the TimelineClip's
        /// duration stretched well past any real spoken line so the loop
        /// has room to repeat. CutsceneAnimationDirector.HandleFinished
        /// calls director.Stop() regardless of how far into this duration
        /// playback actually got.
        ///
        /// The track binding (which Animator plays it) is a scene binding,
        /// not an asset binding — set once on the PlayableDirector by
        /// Editor.ProjectBootstrapBuilder.FixInterrogationScene, not here.</summary>
        private static void BuildTimeline(AnimationClip talk)
        {
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, TimelinePath);
            }

            // Idempotent: this asset only ever holds exactly one AnimationTrack
            // with one clip — drop and rebuild rather than trying to patch an
            // existing track/clip in place.
            foreach (TrackAsset existingTrack in timeline.GetOutputTracks().ToList())
            {
                timeline.DeleteTrack(existingTrack);
            }

            const double stretchedDuration = 60d; // generous upper bound for any spoken line

            AnimationTrack track = timeline.CreateTrack<AnimationTrack>(null, "Cop");
            TimelineClip clip = track.CreateClip(talk);
            clip.start = 0;
            clip.duration = stretchedDuration;
            clip.easeInDuration = 0.25;
            clip.easeOutDuration = 0.25;

            if (clip.asset is AnimationPlayableAsset animationAsset)
            {
                animationAsset.loop = AnimationPlayableAsset.LoopMode.On;
            }

            EditorUtility.SetDirty(timeline);
        }
    }
}
