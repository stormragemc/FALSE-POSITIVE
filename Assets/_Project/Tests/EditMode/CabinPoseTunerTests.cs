using System.Reflection;
using FalsePositive.CabinNight;
using FalsePositive.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FalsePositive.Tests
{
    /// <summary>
    /// Guards the two failure modes that would otherwise be silent:
    ///  - CabinPoseLibrary.SetMuscle does nothing on a name it doesn't
    ///    recognize (a linear scan with no failure path — see the bottom of
    ///    that file), so a typo'd muscle string in CabinPoseTunerWindow is a
    ///    slider that visibly moves but writes nothing, with no error
    ///    anywhere.
    ///  - CabinPoseOverride and CabinCharacterIdle both write the skeleton in
    ///    LateUpdate with no ordering enforced by Unity between two
    ///    default-order scripts. If CabinPoseOverride ever stopped running
    ///    before CabinCharacterIdle, its SetHumanPose would silently wipe the
    ///    waist-lean/breathing bone tilt every frame.
    /// </summary>
    public sealed class CabinPoseTunerTests
    {
        [Test]
        public void ArmSuffixes_ProduceValidHumanTraitMuscleNames_ForBothSides()
        {
            foreach (string suffix in CabinPoseTunerWindow.ArmSuffixes)
            {
                AssertKnownMuscle("Left " + suffix);
                AssertKnownMuscle("Right " + suffix);
            }
        }

        [Test]
        public void HeadNeckMuscles_ProduceValidHumanTraitMuscleNames()
        {
            foreach (string name in CabinPoseTunerWindow.HeadNeckMuscles) AssertKnownMuscle(name);
        }

        [Test]
        public void SpineChestMuscles_ProduceValidHumanTraitMuscleNames()
        {
            foreach (string name in CabinPoseTunerWindow.SpineChestMuscles) AssertKnownMuscle(name);
        }

        [Test]
        public void CabinPoseOverride_RunsBeforeCabinCharacterIdle()
        {
            int overrideOrder = ExecutionOrderOf(typeof(CabinPoseOverride));
            int idleOrder = ExecutionOrderOf(typeof(CabinCharacterIdle));
            Assert.Less(overrideOrder, idleOrder,
                "CabinPoseOverride must execute before CabinCharacterIdle, or its SetHumanPose " +
                "would silently overwrite the waist-lean/breathing bone tilt CabinCharacterIdle just applied.");
        }

        [Test]
        public void CabinPoseOverride_WithOverridesDisabled_LeavesLibraryPoseUnchanged()
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/CabinNight/Prefabs/Priya_Raman.prefab");
            Assert.IsNotNull(prefab, "Priya_Raman.prefab not found — run Bootstrap step 5 first.");

            GameObject inst = Object.Instantiate(prefab);
            try
            {
                Animator animator = inst.GetComponentInChildren<Animator>();
                CabinPoseOverride poseOverride = animator.gameObject.AddComponent<CabinPoseOverride>();
                poseOverride.Muscles.Add(new CabinPoseOverride.MuscleOverride
                {
                    muscleName = "Left Arm Down-Up",
                    value = 0.9f, // would be obviously wrong if this leaked through
                });
                // enableOverrides defaults false — never set true here.
                // AddComponent already triggered Awake (Unity calls it
                // immediately on AddComponent, even outside Play mode), so
                // _handler is already wired — no manual Awake call here.

                using HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
                HumanPose before = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
                CabinPoseLibrary.Apply(ref before, CabinIdleProfile.SeatedForward);
                handler.SetHumanPose(ref before);

                InvokePrivate(poseOverride, "LateUpdate");

                HumanPose after = new HumanPose();
                handler.GetHumanPose(ref after);

                int leftArmDownUp = System.Array.IndexOf(HumanTrait.MuscleName, "Left Arm Down-Up");
                Assert.AreEqual(before.muscles[leftArmDownUp], after.muscles[leftArmDownUp], 0.001f,
                    "A disabled CabinPoseOverride must not change the pose at all.");
            }
            finally
            {
                Object.DestroyImmediate(inst);
            }
        }

        private static void AssertKnownMuscle(string muscleName)
        {
            bool found = false;
            foreach (string known in HumanTrait.MuscleName)
            {
                if (string.Equals(known, muscleName, System.StringComparison.OrdinalIgnoreCase))
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, $"'{muscleName}' is not a HumanTrait.MuscleName — CabinPoseLibrary.SetMuscle " +
                "would silently ignore it.");
        }

        private static int ExecutionOrderOf(System.Type componentType)
        {
            DefaultExecutionOrder attribute = (DefaultExecutionOrder)System.Attribute.GetCustomAttribute(
                componentType, typeof(DefaultExecutionOrder));
            return attribute?.order ?? 0;
        }

        private static void InvokePrivate(Object component, string methodName)
        {
            MethodInfo method = component.GetType().GetMethod(
                methodName, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(method, $"{component.GetType().Name} has no {methodName} to invoke.");
            method.Invoke(component, null);
        }
    }
}
