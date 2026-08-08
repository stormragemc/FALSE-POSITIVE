using UnityEngine;

namespace FalsePositive.CabinNight
{
    /// <summary>
    /// The HumanPose muscle tuning for each CabinIdleProfile, shared between
    /// Editor.CabinNightCharacterBuilder (bakes a rest pose at build time)
    /// and Cutscene.ScriptedActor (applies a pose at runtime for procedural
    /// cutscene staging — see the plan's Phase 4). Runtime code can't reach
    /// into Scripts/Editor/, so this lives in the runtime assembly and the
    /// builder delegates to it instead of duplicating the tuning.
    /// </summary>
    public static class CabinPoseLibrary
    {
        /// <summary>Resets to the shared base arm-down pose, then layers the profile's own muscles on top.</summary>
        public static void Apply(ref HumanPose pose, CabinIdleProfile profile)
        {
            System.Array.Clear(pose.muscles, 0, pose.muscles.Length);
            SetMuscle(ref pose, "Left Arm Down-Up", -0.72f);
            SetMuscle(ref pose, "Right Arm Down-Up", -0.72f);
            SetMuscle(ref pose, "Left Forearm Stretch", -0.18f);
            SetMuscle(ref pose, "Right Forearm Stretch", -0.18f);

            // Legs are set explicitly rather than left at the cleared 0. A muscle
            // value of 0 is the MIDPOINT of that muscle's range, not its rest
            // pose, and the knee range is asymmetric — leaving it at 0 stands
            // every character in a visible half-crouch, knees bent as if about to
            // hop. Positive Lower Leg Stretch extends the knee; Kneeling below
            // bends it with -0.58/-0.7, which is what fixes the sign. Kneeling
            // and Sleeping set their own leg muscles after this and so override
            // it.
            SetMuscle(ref pose, "Left Lower Leg Stretch", 0.6f);
            SetMuscle(ref pose, "Right Lower Leg Stretch", 0.6f);

            switch (profile)
            {
                case CabinIdleProfile.Confrontational:
                    SetMuscle(ref pose, "Spine Front-Back", -0.12f);
                    SetMuscle(ref pose, "Chest Front-Back", -0.08f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.48f);
                    SetMuscle(ref pose, "Left Arm Front-Back", -0.18f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.12f);
                    SetMuscle(ref pose, "Head Turn Left-Right", -0.08f);
                    break;
                case CabinIdleProfile.Controlled:
                    SetMuscle(ref pose, "Spine Front-Back", 0.05f);
                    SetMuscle(ref pose, "Head Nod Down-Up", 0.08f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.32f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.32f);
                    break;
                case CabinIdleProfile.Guarded:
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.52f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.52f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.18f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.18f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.58f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.58f);
                    SetMuscle(ref pose, "Spine Twist Left-Right", -0.08f);
                    SetMuscle(ref pose, "Head Turn Left-Right", -0.12f);
                    break;
                case CabinIdleProfile.Panicked:
                    SetMuscle(ref pose, "Spine Front-Back", 0.15f);
                    SetMuscle(ref pose, "Chest Front-Back", 0.1f);
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.28f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.28f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.55f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.4f);
                    SetMuscle(ref pose, "Head Nod Down-Up", 0.15f);
                    break;
                case CabinIdleProfile.Sleeping:
                    pose.bodyPosition += new Vector3(0f, -0.12f, 0f);
                    SetMuscle(ref pose, "Spine Front-Back", -0.42f);
                    SetMuscle(ref pose, "Chest Front-Back", -0.34f);
                    SetMuscle(ref pose, "Head Nod Down-Up", -0.35f);
                    SetMuscle(ref pose, "Neck Nod Down-Up", -0.22f);
                    SetMuscle(ref pose, "Left Upper Leg Front-Back", -0.62f);
                    SetMuscle(ref pose, "Right Upper Leg Front-Back", -0.42f);
                    SetMuscle(ref pose, "Left Lower Leg Stretch", -0.58f);
                    SetMuscle(ref pose, "Right Lower Leg Stretch", -0.7f);
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.15f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.32f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.7f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.52f);
                    break;
                case CabinIdleProfile.Walking:
                    // Static mid-stride silhouette — ScriptedActor.MoveTo layers
                    // a procedural leg/arm counter-swing offset on top of this
                    // base each frame while actually translating.
                    SetMuscle(ref pose, "Spine Front-Back", 0.06f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.15f);
                    SetMuscle(ref pose, "Right Arm Front-Back", -0.15f);
                    SetMuscle(ref pose, "Left Upper Leg Front-Back", 0.12f);
                    SetMuscle(ref pose, "Right Upper Leg Front-Back", -0.12f);
                    break;
                case CabinIdleProfile.Carrying:
                    // Both arms out and under a load (Nick's shoulders/legs
                    // during CS-13 "The carry"), spine braced back for weight.
                    SetMuscle(ref pose, "Spine Front-Back", -0.22f);
                    SetMuscle(ref pose, "Chest Front-Back", -0.15f);
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.35f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.35f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.42f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.42f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.65f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.65f);
                    SetMuscle(ref pose, "Head Nod Down-Up", -0.2f);
                    break;
                case CabinIdleProfile.Kneeling:
                    // Priya dialling on the sofa's edge (CS-14 "The sofa").
                    pose.bodyPosition += new Vector3(0f, -0.28f, 0f);
                    SetMuscle(ref pose, "Spine Front-Back", -0.28f);
                    SetMuscle(ref pose, "Left Upper Leg Front-Back", 0.55f);
                    SetMuscle(ref pose, "Right Upper Leg Front-Back", 0.55f);
                    SetMuscle(ref pose, "Left Lower Leg Stretch", -0.62f);
                    SetMuscle(ref pose, "Right Lower Leg Stretch", -0.62f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.3f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.6f);
                    SetMuscle(ref pose, "Head Nod Down-Up", 0.25f);
                    break;
                case CabinIdleProfile.Seated:
                case CabinIdleProfile.SeatedBack:
                case CabinIdleProfile.SeatedForward:
                    ApplySeated(ref pose, profile);
                    break;
            }
        }

        /// <summary>Shared seated pose for the P3 flashbacks (CS-16A "the good
        /// years" / CS-16B "when it went wrong") — the cast around SM_Table.
        /// The legs and hip drop are identical for all three variants; only the
        /// torso and arms change, so a lean can never accidentally desync the
        /// part that decides whether the character is actually ON the chair.
        ///
        /// "Upper Leg Front-Back" is NEGATIVE for hip flexion on this rig: its
        /// range is min -90 / max +50, so POSITIVE swings the thigh BACKWARD.
        /// An earlier pass used +0.72 by analogy with CabinAnimationBuilder's
        /// crouchOverrides and produced a kneel, not a sit — thighs folded
        /// behind, feet tucked up above the knee. Do not "fix" these signs to
        /// match the crouch's; the crouch is a different joint configuration.
        ///
        /// Leg values measured, not guessed (HumanPoseHandler on Nick_Vlahos in
        /// an isolated prefab scene). At -0.65/-0.05 with this drop: hips 0.542,
        /// knee 0.519 (thigh horizontal, ~3 degrees of natural downward slope),
        /// ankle 0.096 — which is where CutsceneStage.PlantFeet's SoleToAnkle
        /// 0.09 wants it — and the shin hangs vertical (knee 0.42 above the
        /// foot, foot directly under the knee). Hips 0.542 against SM_Chair_05's
        /// 0.45 m seat pad is the pelvis bone sitting ~9 cm above the cushion,
        /// which is anatomically correct: the sit bones, not the hip joint,
        /// rest on the seat.</summary>
        private static void ApplySeated(ref HumanPose pose, CabinIdleProfile profile)
        {
            pose.bodyPosition += new Vector3(0f, -0.31f, 0f);

            SetMuscle(ref pose, "Left Upper Leg Front-Back", -0.65f);
            SetMuscle(ref pose, "Right Upper Leg Front-Back", -0.65f);
            SetMuscle(ref pose, "Left Lower Leg Stretch", -0.05f);
            SetMuscle(ref pose, "Right Lower Leg Stretch", -0.05f);

            // Spine is deliberately IDENTICAL across the three variants, and the
            // torso lean is NOT expressed here — CabinCharacterIdle applies it
            // as a bone-space tilt instead. Measured reason: Unity's humanoid
            // solver holds pose.bodyRotation fixed, so bending the spine in
            // muscle space counter-rotates the HIPS rather than carrying the
            // head over. Driving "Spine/Chest Front-Back" across their full
            // -0.9..+0.9 range moved the bones a lot (Spine 37 deg, Chest 29)
            // but swung the hips-to-head vector only 6 deg — invisible — while
            // dragging the feet through 0.34 m of vertical, because the whole
            // body slid to keep bodyRotation satisfied. Keeping spine fixed is
            // what makes footY land at ~0.09 for all three variants, so
            // PlantFeet has nothing to correct and the leans cannot desync the
            // part that decides whether they are on the chair.
            SetMuscle(ref pose, "Spine Front-Back", -0.05f);

            switch (profile)
            {
                case CabinIdleProfile.SeatedBack:
                    // Arms deliberately IDENTICAL to the neutral Seated case,
                    // chin up as the only muscle difference — the recline
                    // itself is CabinCharacterIdle's bone tilt. An earlier pass
                    // gave this variant its own looser arm values and that
                    // alone moved the solved hips enough to drop the feet to
                    // y 0.052 against Seated's 0.096, i.e. arm mass feeds back
                    // into the body solve. Keeping the arms matched is what
                    // makes all three variants share one foot height.
                    SetMuscle(ref pose, "Head Nod Down-Up", 0.10f);
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.55f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.55f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.2f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.2f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.45f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.45f);
                    break;

                case CabinIdleProfile.SeatedForward:
                    // Hands down ON the table, chin dipped toward it.
                    //
                    // These are a measured optimum, not a guess, and the two
                    // arm muscles do not mean what their names suggest on this
                    // rig — isolating them one at a time showed "Arm
                    // Front-Back" mostly controls ELEVATION (sweeping it
                    // +0.6 -> -0.9 moved the hand from y 0.838 to 1.204 while
                    // barely changing reach) and "Forearm Stretch" POSITIVE is
                    // what extends the arm out. Down-Up -0.60 / Front-Back 0.15
                    // / Forearm 0.0 lands the hand at y 0.736 against a 0.75 m
                    // table top — resting on the surface — with the furthest
                    // forward reach available at that height, 0.249 m from the
                    // hip. Reach falls off in BOTH directions from here, so
                    // pushing any of these further pulls the hands back off the
                    // table rather than further onto it.
                    //
                    // 0.249 m is also why CutsceneStage.SeatedAtChair tucks the
                    // actor slightly toward the table — the chair centres sit
                    // 0.25-0.38 m out from the table edge, just past reach.
                    SetMuscle(ref pose, "Head Nod Down-Up", -0.08f);
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.60f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.60f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.15f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.15f);
                    SetMuscle(ref pose, "Left Forearm Stretch", 0f);
                    SetMuscle(ref pose, "Right Forearm Stretch", 0f);
                    break;

                default: // Seated — upright neutral, the base the leans vary from.
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.55f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.55f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.2f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.2f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.45f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.45f);
                    break;
            }
        }

        public static void SetMuscle(ref HumanPose pose, string muscleName, float value)
        {
            for (int index = 0; index < HumanTrait.MuscleName.Length; index++)
            {
                if (string.Equals(HumanTrait.MuscleName[index], muscleName, System.StringComparison.OrdinalIgnoreCase))
                {
                    pose.muscles[index] = value;
                    return;
                }
            }
        }
    }
}
