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
