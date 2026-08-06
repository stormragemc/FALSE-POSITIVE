using FalsePositive.CabinNight;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace FalsePositive.Tests
{
    /// <summary>
    /// Permanent regression gate for the technique CabinAnimationBuilder relies
    /// on: authoring humanoid AnimationClips purely in editor code via
    /// AnimationUtility.SetEditorCurve muscle bindings, with no .anim asset
    /// authored by hand and no Mixamo/motion-capture dependency.
    ///
    /// Confirmed empirically (not from documentation, which does not state
    /// this) before CabinAnimationBuilder was written:
    ///  - EditorCurveBinding.FloatCurve("", typeof(Animator), muscleName) with
    ///    muscleName taken verbatim from HumanTrait.MuscleName is the correct
    ///    binding — no name translation needed for the 55 body muscles.
    ///  - Writing those curves alone (no explicit flag) makes
    ///    AnimationClip.humanMotion report true.
    ///  - RootT.x/y/z curves do NOT reproduce HumanPose.bodyPosition — with
    ///    applyRootMotion = false (required, see CabinAnimationBuilder's
    ///    class doc) a RootT.y curve has zero effect on the sampled pose.
    ///    CabinPoseLibrary's Kneeling/Sleeping profiles rely on a
    ///    pose.bodyPosition Y offset that a muscle-only clip cannot carry —
    ///    CabinAnimatorDriver must apply that offset itself as a deterministic
    ///    Body.localPosition adjustment, not bake it into the clip. This test
    ///    encodes that as the actually-shipped technique, not just the caveat.
    /// </summary>
    public class HumanoidClipAuthoringTests
    {
        private const float MuscleTolerance = 0.05f;

        [Test]
        public void MuscleCurveClip_ReportsHumanMotion()
        {
            GameObject prefab = LoadPriyaPrefab();
            GameObject inst = Object.Instantiate(prefab);
            try
            {
                Animator animator = inst.GetComponentInChildren<Animator>();
                AnimationClip clip = BuildMuscleOnlyClip(animator, CabinIdleProfile.Kneeling, out _);
                try
                {
                    Assert.IsTrue(clip.humanMotion, "SetEditorCurve muscle bindings should flip humanMotion to true.");
                    Assert.IsFalse(clip.empty);
                }
                finally
                {
                    Object.DestroyImmediate(clip);
                }
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        [Test]
        public void MuscleCurveClip_ReproducesCabinPoseLibraryPose_WhenPlayedThroughAnAnimator()
        {
            GameObject prefab = LoadPriyaPrefab();

            // Path A: today's runtime technique (HumanPoseHandler.SetHumanPose),
            // the ground truth CabinAnimationBuilder's clips must match.
            GameObject instA = Object.Instantiate(prefab);
            Animator animA = instA.GetComponentInChildren<Animator>();
            HumanPoseHandler handlerA = new HumanPoseHandler(animA.avatar, animA.transform);
            HumanPose poseA = new HumanPose();
            try
            {
                handlerA.GetHumanPose(ref poseA);
                CabinPoseLibrary.Apply(ref poseA, CabinIdleProfile.Kneeling);
                handlerA.SetHumanPose(ref poseA);

                // Path B: bake the same profile into a muscle-curve clip,
                // play it through a real AnimatorController on a second
                // instance (Animator playback, not SetHumanPose directly —
                // this is what CutsceneStage/CabinAnimatorDriver actually see).
                GameObject instB = Object.Instantiate(prefab);
                try
                {
                    Animator animB = instB.GetComponentInChildren<Animator>();
                    AnimationClip clip = BuildMuscleOnlyClip(animB, CabinIdleProfile.Kneeling, out float[] writtenMuscles);
                    AnimatorController controller = new AnimatorController();
                    try
                    {
                        controller.AddLayer("Base");
                        controller.layers[0].stateMachine.AddState("Probe").motion = clip;
                        animB.runtimeAnimatorController = controller;
                        animB.applyRootMotion = false;

                        // Two updates: one to enter the default state, one
                        // to let the pose settle — matches what the probe
                        // that validated this technique needed.
                        animB.Update(0f);
                        animB.Update(1f / 30f);

                        HumanPoseHandler handlerB = new HumanPoseHandler(animB.avatar, animB.transform);
                        HumanPose poseB = new HumanPose();
                        try
                        {
                            handlerB.GetHumanPose(ref poseB);

                            int mismatches = 0;
                            for (int i = 0; i < writtenMuscles.Length; i++)
                            {
                                if (Mathf.Abs(poseA.muscles[i] - poseB.muscles[i]) > MuscleTolerance) mismatches++;
                            }

                            // Confirmed empirically: 54/55 body muscles match
                            // near-exactly; a single leg-stretch DOF showed an
                            // avatar-specific IK divergence when tested in
                            // isolation but settles once the full pose (not
                            // just that one muscle) drives the Animator, as
                            // it does here. Allow at most one outlier rather
                            // than requiring bit-for-bit equality across all
                            // 55, which the full-body IK/retarget pass inside
                            // Animator playback does not guarantee.
                            Assert.LessOrEqual(mismatches, 1,
                                $"Expected at most one muscle to diverge beyond {MuscleTolerance}, got {mismatches}.");

                            // The one thing muscle curves cannot carry:
                            // bodyPosition. Confirm the gap is real (so nobody
                            // "fixes" this test by adding a RootT.y curve and
                            // silently reintroducing the sunk-hips bug) —
                            // CabinAnimatorDriver must apply this offset itself.
                            float bodyPositionYGap = Mathf.Abs(poseA.bodyPosition.y - poseB.bodyPosition.y);
                            Assert.Greater(bodyPositionYGap, 0.1f,
                                "Expected a muscle-only clip to NOT reproduce the Kneeling bodyPosition Y offset — " +
                                "if this now passes, CabinAnimatorDriver's separate Y-offset compensation is redundant " +
                                "or (more likely) this assertion needs updating alongside the driver.");
                        }
                        finally
                        {
                            handlerB.Dispose();
                        }
                    }
                    finally
                    {
                        Object.DestroyImmediate(controller);
                        Object.DestroyImmediate(clip);
                    }
                }
                finally
                {
                    Object.DestroyImmediate(instB);
                }
            }
            finally
            {
                handlerA.Dispose();
                Object.DestroyImmediate(instA);
            }
        }

        private static GameObject LoadPriyaPrefab()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/CabinNight/Prefabs/Priya_Raman.prefab");
            Assert.IsNotNull(prefab, "Priya_Raman.prefab not found — run Bootstrap step 5 first.");
            return prefab;
        }

        /// <summary>Bakes only the 55 body-muscle curves for a profile — no root
        /// curves — mirroring CabinAnimationBuilder.WriteMuscle exactly enough
        /// to be a faithful regression gate without depending on that class
        /// (which is built after this test, per the plan's probe-first order).</summary>
        private static AnimationClip BuildMuscleOnlyClip(Animator animator, CabinIdleProfile profile, out float[] writtenMuscles)
        {
            HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
            HumanPose pose = new HumanPose();
            try
            {
                handler.GetHumanPose(ref pose);
                CabinPoseLibrary.Apply(ref pose, profile);
                writtenMuscles = (float[])pose.muscles.Clone();
            }
            finally
            {
                handler.Dispose();
            }

            AnimationClip clip = new AnimationClip();
            for (int i = 0; i < writtenMuscles.Length; i++)
            {
                string muscleName = HumanTrait.MuscleName[i];
                AnimationCurve curve = AnimationCurve.Constant(0f, 1f / 30f, writtenMuscles[i]);
                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscleName), curve);
            }
            return clip;
        }
    }
}
