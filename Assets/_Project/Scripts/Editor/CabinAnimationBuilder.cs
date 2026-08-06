using System;
using System.Collections.Generic;
using System.IO;
using FalsePositive.CabinNight;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Bakes real humanoid AnimationClips + one AnimatorController from the
    /// muscle vocabulary already tuned in CabinPoseLibrary — the project had
    /// zero .anim assets before this; every cast member was a code-driven
    /// HumanPose statue. See Tests/EditMode/HumanoidClipAuthoringTests.cs for
    /// the two facts this design depends on, confirmed empirically before
    /// this class was written (not documented anywhere by Unity):
    ///
    ///  1. EditorCurveBinding.FloatCurve("", typeof(Animator), muscleName),
    ///     with muscleName taken verbatim from HumanTrait.MuscleName, is a
    ///     valid humanoid curve binding for all 55 body muscles — no name
    ///     translation needed — and writing those curves alone flips
    ///     AnimationClip.humanMotion to true with no explicit flag call
    ///     (there is no setter for it).
    ///  2. RootT/RootQ curves do NOT reproduce HumanPose.bodyPosition/
    ///     bodyRotation. CabinPoseLibrary's Kneeling (-0.28) and Sleeping
    ///     (-0.12) profiles rely on a bodyPosition Y offset that a
    ///     muscle-only clip cannot carry. This builder authors ZERO root
    ///     curves — CabinAnimatorDriver applies that offset itself as a
    ///     direct Body.localPosition adjustment for those two states.
    ///
    /// Zero root motion by design (see also applyRootMotion = false in
    /// CabinNightCharacterBuilder.ApplyPose): the cast prefabs are built at
    /// non-uniform scale (0.96-1.02, CabinNightCharacterBuilder.BuildCastInScene)
    /// and root translation curves are not scale-safe, while muscle values
    /// are avatar-height-normalized and are.
    ///
    /// Idempotent — safe to re-run from Bootstrap or MemorySceneBuilderV2
    /// without churning asset GUIDs: existing clips/controller are loaded
    /// and their curves cleared/rewritten in place, never deleted and
    /// recreated. NEW assets (the first run) still need their containing
    /// folder to exist as a Unity-recognised asset BEFORE this runs, or
    /// AssetDatabase.CreateAsset can hang the calling process on a folder-
    /// import prompt — EnsureBuilt always creates the folder via a raw
    /// System.IO call and calls AssetDatabase.Refresh() first.
    /// </summary>
    public static class CabinAnimationBuilder
    {
        private const string AnimRoot = "Assets/_Project/CabinNight/Animations/";
        private const string ControllerPath = AnimRoot + "CabinCast.controller";

        // State/clip names — CabinAnimatorDriver.StateName maps CabinIdleProfile
        // to these verbatim, so a rename here must be mirrored there.
        public const string StateIdleConfrontational = "Idle_Confrontational";
        public const string StateIdleControlled = "Idle_Controlled";
        public const string StateIdleGuarded = "Idle_Guarded";
        public const string StateIdlePanicked = "Idle_Panicked";
        public const string StateIdleSleeping = "Idle_Sleeping";
        public const string StateIdleWalking = "Idle_Walking";
        public const string StatePoseCarrying = "Pose_Carrying";
        public const string StatePoseKneeling = "Pose_Kneeling";
        public const string StateWalk = "Walk";
        public const string StateWalkCarry = "Walk_Carry";
        public const string StateLiftCrouch = "Lift_Crouch";

        /// <summary>The Y offset CabinAnimatorDriver must apply on top of these two
        /// states, mirroring CabinPoseLibrary.Apply's pose.bodyPosition += calls
        /// for Kneeling/Sleeping — see class doc fact #2 for why a clip can't
        /// carry this itself.</summary>
        public static float BodyYOffsetFor(CabinIdleProfile profile)
        {
            switch (profile)
            {
                case CabinIdleProfile.Kneeling: return -0.28f;
                case CabinIdleProfile.Sleeping: return -0.12f;
                default: return 0f;
            }
        }

        [MenuItem("Tools/False Positive/Bootstrap/T03c - Build Cast Animation")]
        public static void Build() => EnsureBuilt(force: true);

        /// <summary>Builds only if CabinCast.controller doesn't exist yet — the
        /// guard MemorySceneBuilderV2 calls before every cast build so clip
        /// ordering can't be forgotten, without re-baking on every scene
        /// rebuild.</summary>
        public static void EnsureBuilt(bool force = false)
        {
            if (!force && AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) != null) return;
            BuildInternal();
        }

        private static void BuildInternal()
        {
            Directory.CreateDirectory(AnimRoot);
            AssetDatabase.Refresh();

            const float spineBreath = 0.015f;
            const float spineTwist = 0.012f;

            AnimationClip idleConfrontational = CycleClip(StateIdleConfrontational, CabinIdleProfile.Confrontational, 4.0f,
                new[] { Spine(spineBreath, 0f), Twist(spineTwist, 0.6f) });
            AnimationClip idleControlled = CycleClip(StateIdleControlled, CabinIdleProfile.Controlled, 4.6f,
                new[] { Spine(spineBreath, 0.3f), Twist(spineTwist, 1.1f) });
            AnimationClip idleGuarded = CycleClip(StateIdleGuarded, CabinIdleProfile.Guarded, 4.2f,
                new[] { Spine(spineBreath, 0.9f), Twist(spineTwist, 1.7f) });
            AnimationClip idlePanicked = CycleClip(StateIdlePanicked, CabinIdleProfile.Panicked, 2.6f,
                new[] { Spine(spineBreath * 1.6f, 1.4f), Twist(spineTwist * 1.6f, 2.2f) });
            AnimationClip idleSleeping = CycleClip(StateIdleSleeping, CabinIdleProfile.Sleeping, 6.0f,
                new[] { Spine(spineBreath * 0.5f, 0.2f) });
            AnimationClip idleWalking = CycleClip(StateIdleWalking, CabinIdleProfile.Walking, 4.0f,
                new[] { Spine(spineBreath, 2.0f) });

            AnimationClip poseCarrying = HoldClip(StatePoseCarrying, CabinIdleProfile.Carrying, 4.0f);
            AnimationClip poseKneeling = HoldClip(StatePoseKneeling, CabinIdleProfile.Kneeling, 5.0f);

            // Walk/Walk_Carry oscillator amplitudes and centres port
            // ScriptedActor.MoveTo's old per-frame swing math verbatim (see
            // that file's git history) so the baked cycle reproduces exactly
            // the motion the procedural version had, now as real keyframes.
            // strideRate 5.5 -> one full sine period is 2*PI/5.5 = 1.142s;
            // 3.2 -> 1.963s — the clip lengths below match those periods
            // exactly so CrossFade loops seamlessly at the speed MoveTo used.
            AnimationClip walk = CycleClip(StateWalk, CabinIdleProfile.Walking, 1.142f, new[]
            {
                new Oscillator("Left Upper Leg Front-Back", 14f / 30f, 0f, baseOverride: 0.12f),
                new Oscillator("Right Upper Leg Front-Back", 14f / 30f, Mathf.PI, baseOverride: -0.12f),
                new Oscillator("Left Arm Front-Back", 14f / 20f, Mathf.PI, baseOverride: 0f),
                new Oscillator("Right Arm Front-Back", 14f / 20f, 0f, baseOverride: 0f),
            });
            AnimationClip walkCarry = CycleClip(StateWalkCarry, CabinIdleProfile.Carrying, 1.963f, new[]
            {
                new Oscillator("Left Upper Leg Front-Back", 6f / 30f, 0f, baseOverride: 0f),
                new Oscillator("Right Upper Leg Front-Back", 6f / 30f, Mathf.PI, baseOverride: 0f),
            });

            // Controlled -> a declared crouch key -> Carrying, one-shot.
            // The two lifters (player + Aaron) both stoop, take the weight,
            // and rise into the Carrying pose (Scripts/Cutscene/CutsceneStage.cs,
            // the gameplay-interlude lift beat).
            Dictionary<string, float> crouchOverrides = new Dictionary<string, float>
            {
                ["Left Upper Leg Front-Back"] = 0.75f,
                ["Right Upper Leg Front-Back"] = 0.75f,
                ["Left Lower Leg Stretch"] = -0.75f,
                ["Right Lower Leg Stretch"] = -0.75f,
                ["Spine Front-Back"] = -0.30f,
                ["Left Arm Front-Back"] = 0.35f,
                ["Right Arm Front-Back"] = 0.35f,
                ["Left Arm Down-Up"] = -0.45f,
                ["Right Arm Down-Up"] = -0.45f,
            };
            AnimationClip liftCrouch = BlendClip(StateLiftCrouch, CabinIdleProfile.Controlled, crouchOverrides,
                CabinIdleProfile.Carrying, 1.6f);

            BuildController(
                idleConfrontational, idleControlled, idleGuarded, idlePanicked, idleSleeping, idleWalking,
                poseCarrying, poseKneeling, walk, walkCarry, liftCrouch);

            AssetDatabase.SaveAssets();
            Debug.Log("[CabinAnimationBuilder] Cast animation clips + CabinCast.controller built.");
        }

        // ---- Oscillator-driven idle/walk clips ----

        private readonly struct Oscillator
        {
            public readonly string Muscle;
            public readonly float Amplitude;
            public readonly float Phase;
            public readonly float? BaseOverride;

            public Oscillator(string muscle, float amplitude, float phase, float? baseOverride = null)
            {
                Muscle = muscle;
                Amplitude = amplitude;
                Phase = phase;
                BaseOverride = baseOverride;
            }
        }

        private static Oscillator Spine(float amplitude, float phase) => new Oscillator("Spine Left-Right", amplitude, phase);
        private static Oscillator Twist(float amplitude, float phase) => new Oscillator("Spine Twist Left-Right", amplitude, phase);

        private const int CycleSamples = 13;

        private static AnimationClip CycleClip(string name, CabinIdleProfile profile, float length, Oscillator[] oscillators)
        {
            float[] baseMuscles = BaseMuscles(profile);
            AnimationClip clip = LoadOrCreateClip(name);
            ClearAllCurves(clip);

            for (int m = 0; m < 55; m++)
            {
                string muscleName = HumanTrait.MuscleName[m];
                Oscillator? osc = FindOscillator(oscillators, muscleName);
                AnimationCurve curve;
                if (osc.HasValue)
                {
                    float baseValue = osc.Value.BaseOverride ?? baseMuscles[m];
                    Keyframe[] keys = new Keyframe[CycleSamples];
                    for (int s = 0; s < CycleSamples; s++)
                    {
                        float t = length * s / (CycleSamples - 1);
                        float angle = 2f * Mathf.PI * s / (CycleSamples - 1) + osc.Value.Phase;
                        keys[s] = new Keyframe(t, baseValue + osc.Value.Amplitude * Mathf.Sin(angle));
                    }
                    curve = new AnimationCurve(keys);
                    for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
                }
                else
                {
                    curve = AnimationCurve.Constant(0f, length, baseMuscles[m]);
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

        // ---- Static pose clips ----

        private static AnimationClip HoldClip(string name, CabinIdleProfile profile, float length)
        {
            float[] muscles = BaseMuscles(profile);
            AnimationClip clip = LoadOrCreateClip(name);
            ClearAllCurves(clip);

            for (int m = 0; m < 55; m++)
            {
                AnimationCurve curve = AnimationCurve.Constant(0f, length, muscles[m]);
                WriteMuscle(clip, HumanTrait.MuscleName[m], curve);
            }

            SetClipSettings(clip, loop: true);
            return clip;
        }

        // ---- Blend clips (one-shot pose transitions) ----

        private static AnimationClip BlendClip(string name, CabinIdleProfile from, Dictionary<string, float> midOverrides,
            CabinIdleProfile to, float length)
        {
            float[] fromMuscles = BaseMuscles(from);
            float[] toMuscles = BaseMuscles(to);
            AnimationClip clip = LoadOrCreateClip(name);
            ClearAllCurves(clip);

            float mid = length * 0.5f;
            for (int m = 0; m < 55; m++)
            {
                string muscleName = HumanTrait.MuscleName[m];
                float midValue = midOverrides.TryGetValue(muscleName, out float overrideValue)
                    ? overrideValue
                    : Mathf.Lerp(fromMuscles[m], toMuscles[m], 0.5f);

                Keyframe[] keys =
                {
                    new Keyframe(0f, fromMuscles[m]),
                    new Keyframe(mid, midValue),
                    new Keyframe(length, toMuscles[m]),
                };
                AnimationCurve curve = new AnimationCurve(keys);
                for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
                WriteMuscle(clip, muscleName, curve);
            }

            SetClipSettings(clip, loop: false);
            return clip;
        }

        // ---- Shared low-level helpers ----

        /// <summary>A HumanPose's muscle array, independent of any specific avatar
        /// instance — muscle values are a fixed semantic vocabulary (Mecanim's
        /// whole retargeting premise), so one clip set plays correctly on every
        /// cast member's avatar regardless of gender/skeleton. Avoids
        /// instantiating a prefab just to read CabinPoseLibrary's tuning.</summary>
        private static float[] BaseMuscles(CabinIdleProfile profile)
        {
            HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            CabinPoseLibrary.Apply(ref pose, profile);
            return pose.muscles;
        }

        /// <summary>Binds a body-muscle curve by name. Guards against the one place
        /// clip bindings diverge from HumanTrait.MuscleName: finger muscles
        /// (index >= 55) bind as "LeftHand.Index.1 Stretched", not the
        /// HumanTrait.MuscleName form — see HumanoidClipAuthoringTests. No
        /// CabinPoseLibrary profile uses fingers, so this should never fire;
        /// failing loud beats writing a curve Unity silently ignores.</summary>
        private static void WriteMuscle(AnimationClip clip, string muscleName, AnimationCurve curve)
        {
            int index = Array.FindIndex(HumanTrait.MuscleName,
                n => string.Equals(n, muscleName, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new InvalidOperationException($"[CabinAnimationBuilder] Unknown muscle '{muscleName}'.");
            if (index >= 55)
            {
                throw new InvalidOperationException(
                    $"[CabinAnimationBuilder] '{muscleName}' is a finger muscle (index {index}); clip bindings need " +
                    "the 'LeftHand.Index.1 Stretched' form, not HumanTrait.MuscleName. Not supported here.");
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

        /// <summary>All three bake-into-pose flags are set so no channel escapes
        /// as root motion — see class doc fact #2 and CabinNightCharacterBuilder's
        /// applyRootMotion = false, which this pairs with.</summary>
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

        // ---- Controller ----

        /// <summary>One layer, one state per clip, no transitions and no
        /// parameters — every state change is driven imperatively via
        /// CabinAnimatorDriver.PlayState(name, fade) -&gt; CrossFadeInFixedTime,
        /// matching how CutsceneStage already drives everything else.</summary>
        private static void BuildController(params AnimationClip[] clips)
        {
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            }

            while (controller.layers.Length > 1)
            {
                controller.RemoveLayer(controller.layers.Length - 1);
            }
            if (controller.layers.Length == 0)
            {
                controller.AddLayer("Base");
            }

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                stateMachine.RemoveState(child.state);
            }

            foreach (AnimationClip clip in clips)
            {
                AnimatorState state = stateMachine.AddState(clip.name);
                state.motion = clip;
            }
        }
    }
}
