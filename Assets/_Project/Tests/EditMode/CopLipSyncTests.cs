using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ULS = uLipSync;

namespace FalsePositive.Tests
{
    /// <summary>
    /// Regression coverage for the officer's lip sync (docs/graceful-juggling-parrot
    /// plan, Step 2). Nothing overrides the visemes today only because the Cop's
    /// Animator happens to carry no RuntimeAnimatorController — this test makes that
    /// an asserted invariant instead of an accident, and catches the one thing that
    /// would silently break the mouth without touching a line of code: a re-import
    /// reordering NewCop_rigged.fbx's blend shapes out from under the hardcoded
    /// phoneme->index table.
    /// </summary>
    public sealed class CopLipSyncTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Interrogation.unity";

        private static readonly (string Phoneme, string Viseme)[] ExpectedVisemes =
        {
            ("A", "viseme_aa"),
            ("I", "viseme_I"),
            ("U", "viseme_U"),
            ("E", "viseme_E"),
            ("O", "viseme_O"),
            ("-", "viseme_sil"),
            ("S", "viseme_SS"),
        };

        [Test]
        public void Cop_AnimatorHasNoController_SoNothingOverridesTheVisemes()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                GameObject cop = FindInScene(scene, "Cop");
                Assert.IsNotNull(cop, $"{ScenePath} has no Cop.");

                Animator animator = cop.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator, "Cop has no Animator.");
                Assert.IsNull(animator.runtimeAnimatorController,
                    "Cop's Animator has a RuntimeAnimatorController assigned — its muscle " +
                    "evaluation would now run every frame and can drive blend shapes referenced " +
                    "by any clip, silently fighting uLipSync's own weights. See " +
                    "Editor.ProjectBootstrapBuilder.WireAnimationDirector's class doc for why this " +
                    "must stay unassigned.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void HeadBlendShape_UpdatesInLateUpdate_AfterAnyAnimatorEvaluation()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                GameObject cop = FindInScene(scene, "Cop");
                ULS.uLipSyncBlendShape headTable = cop.GetComponent<ULS.uLipSyncBlendShape>();
                Assert.IsNotNull(headTable, "Cop has no uLipSyncBlendShape for Head_Mesh.");
                Assert.AreEqual(ULS.UpdateMethod.LateUpdate, headTable.updateMethod,
                    "Head_Mesh's uLipSyncBlendShape must apply in LateUpdate so its weights are " +
                    "the last write of the frame.");
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [TestCase("Head_Mesh")]
        [TestCase("Teeth_Mesh")]
        [TestCase("Tongue_Mesh")]
        public void PhonemeTable_IndicesResolveToExpectedVisemeNames(string meshName)
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                GameObject cop = FindInScene(scene, "Cop");
                SkinnedMeshRenderer renderer = null;
                foreach (SkinnedMeshRenderer smr in cop.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (smr.gameObject.name == meshName) { renderer = smr; break; }
                }
                Assert.IsNotNull(renderer, $"Cop has no {meshName} renderer.");

                // The table isn't necessarily on the renderer's own GameObject —
                // Head_Mesh's lives on the Cop root by original design, while
                // Teeth_Mesh/Tongue_Mesh's live on their own mesh GameObjects.
                // Match by which renderer the table is bound to, not co-location.
                ULS.uLipSyncBlendShape table = null;
                foreach (ULS.uLipSyncBlendShape candidate in cop.GetComponentsInChildren<ULS.uLipSyncBlendShape>(true))
                {
                    if (candidate.skinnedMeshRenderer == renderer) { table = candidate; break; }
                }
                Assert.IsNotNull(table, $"{meshName} has no uLipSyncBlendShape bound to it — its mouth " +
                    "would stay frozen even while the head moves.");

                foreach ((string phoneme, string expectedViseme) in ExpectedVisemes)
                {
                    ULS.uLipSyncBlendShape.BlendShapeInfo entry = table.GetBlendShapeInfo(phoneme);
                    Assert.IsNotNull(entry, $"{meshName} is missing a table entry for phoneme '{phoneme}'.");
                    Assert.GreaterOrEqual(entry.index, 0,
                        $"{meshName}'s '{phoneme}' entry never resolved a blend shape index.");

                    string actualName = renderer.sharedMesh.GetBlendShapeName(entry.index);
                    Assert.AreEqual(expectedViseme, actualName,
                        $"{meshName}'s '{phoneme}' entry points at blend shape index {entry.index} " +
                        $"(\"{actualName}\"), not \"{expectedViseme}\" — a re-import reordered the " +
                        "shape keys out from under the hardcoded table.");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        [Test]
        public void JawOpen_IsOwnedByBlendShapeCopMouthAlone_NotByAnyPhonemeTable()
        {
            Scene scene = EditorSceneManager.OpenPreviewScene(ScenePath);
            try
            {
                GameObject cop = FindInScene(scene, "Cop");
                foreach (ULS.uLipSyncBlendShape table in cop.GetComponentsInChildren<ULS.uLipSyncBlendShape>(true))
                {
                    Assert.IsNull(table.GetBlendShapeInfo("jawOpen"),
                        $"{table.skinnedMeshRenderer?.gameObject.name}'s uLipSyncBlendShape table has a " +
                        "'jawOpen' entry — that table clears-then-writes its own entries every " +
                        "LateUpdate and will fight BlendShapeCopMouth's separate jaw envelope. " +
                        "jawOpen must be written by exactly one place.");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static GameObject FindInScene(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (root.name == name) return root;
                Transform found = root.transform.Find(name);
                if (found != null) return found.gameObject;
            }
            return null;
        }
    }
}
