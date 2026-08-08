using System;
using FalsePositive.CabinNight;
using FalsePositive.Cutscene;
using FalsePositive.Interaction;
using FalsePositive.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FalsePositive.Tests
{
    public sealed class CabinCastModelTests
    {
        private const string CharacterRoot = "Assets/_Project/Art/Characters/";
        private const string PrefabRoot = "Assets/_Project/CabinNight/Prefabs/";
        private const float TransformTolerance = 0.001f;

        private static readonly CastAsset[] Cast =
        {
            new CastAsset("Aaron", "Aaron_Teague"),
            new CastAsset("Ivy", "Ivy_Teague"),
            new CastAsset("Nick", "Nick_Vlahos"),
            new CastAsset("Priya", "Priya_Raman"),
        };

        [TestCaseSource(nameof(Cast))]
        public void StagedModel_IsCleanValidHumanoidWithUrpMaterials(CastAsset cast)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(cast.ModelPath);
            Assert.IsNotNull(model, $"Missing staged model: {cast.ModelPath}");

            Animator animator = model.GetComponentInChildren<Animator>(true);
            Assert.IsNotNull(animator, $"{cast.Name} has no Animator.");
            Assert.IsNotNull(animator.avatar, $"{cast.Name} has no Avatar.");
            Assert.IsTrue(animator.avatar.isValid, $"{cast.Name}'s Avatar is invalid.");
            Assert.IsTrue(animator.avatar.isHuman, $"{cast.Name} was not imported as Humanoid.");

            Assert.IsNull(FindTransform(model.transform, "Cube"));
            Assert.IsNull(FindTransform(model.transform, "Icosphere"));
            Assert.Zero(model.GetComponentsInChildren<Camera>(true).Length);
            Assert.Zero(model.GetComponentsInChildren<Light>(true).Length);

            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.GreaterOrEqual(renderers.Length, 10, $"{cast.Name} lost character meshes during staging.");

            int blendShapeCount = 0;
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Assert.IsNotNull(renderer.sharedMesh, $"{cast.Name}/{renderer.name} has no mesh.");
                blendShapeCount += renderer.sharedMesh.blendShapeCount;
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, $"{cast.Name}/{renderer.name} has a missing material.");
                    Assert.AreEqual("Universal Render Pipeline/Lit", material.shader.name,
                        $"{cast.Name}/{renderer.name}/{material.name} is not using URP/Lit.");
                    Assert.IsTrue(material.HasProperty("_BaseMap"));
                    Assert.IsNotNull(material.GetTexture("_BaseMap"),
                        $"{cast.Name}/{renderer.name}/{material.name} lost its source texture.");
                }
            }
            Assert.GreaterOrEqual(blendShapeCount, 180, $"{cast.Name} lost its facial blendshapes.");
        }

        [TestCaseSource(nameof(Cast))]
        public void CabinPrefab_UsesMatchingNamedModelAndRuntimeComponents(CastAsset cast)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(cast.PrefabPath);
            Assert.IsNotNull(prefab, $"Missing cabin prefab: {cast.PrefabPath}");

            GameObject instance = PrefabUtility.LoadPrefabContents(cast.PrefabPath);
            try
            {
                Transform body = instance.transform.Find("Body");
                Assert.IsNotNull(body, $"{cast.PrefabName} has no Body child.");
                Assert.AreEqual(cast.ModelPath, ResolveOutermostSourcePath(body.gameObject),
                    $"{cast.PrefabName}/Body does not ultimately source the matching FBX.");

                Assert.IsNotNull(instance.GetComponent<CapsuleCollider>());
                Assert.IsNotNull(instance.GetComponent<CabinAnimatorDriver>());
                Assert.IsNotNull(instance.GetComponent<CabinCharacterIdle>());
                Assert.IsNotNull(instance.GetComponent<ScriptedActor>());

                Animator animator = instance.GetComponentInChildren<Animator>(true);
                Assert.IsNotNull(animator);
                Assert.IsNotNull(animator.runtimeAnimatorController);
                Assert.IsTrue(animator.avatar.isHuman);
                Assert.IsFalse(animator.applyRootMotion);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(instance);
            }
        }

        [Test]
        public void MemoryScenes_KeepDocumentedCharacterStaging()
        {
            AssertScene(
                "Assets/_Project/Scenes/Memory_CabinNight.unity",
                new CharacterStaging("Nick Vlahos (Male)", false, new Vector3(2.3f, 0f, -6.3f), new Vector3(0f, 20f, 0f), 0.98f),
                new CharacterStaging("Aaron Teague (Male)", false, new Vector3(4.3f, 2.7f, 3.4f), new Vector3(0f, 226f, 0f), 1.02f),
                new CharacterStaging("Ivy Teague (Female)", false, new Vector3(4.6f, 2.7f, 3.0f), new Vector3(0f, 248f, 0f), 0.98f),
                new CharacterStaging("Priya Raman (Female)", true, new Vector3(0.75f, 0f, -0.5f), new Vector3(0f, 30f, 0f), 0.96f));

            AssertScene(
                "Assets/_Project/Scenes/Memory_CabinMorning.unity",
                new CharacterStaging("Nick Vlahos (Male)", true, new Vector3(2.3f, 0f, -6.3f), new Vector3(0f, 20f, 0f), 0.98f),
                new CharacterStaging("Aaron Teague (Male)", true, new Vector3(4.3f, 2.7f, 3.4f), new Vector3(0f, 226f, 0f), 1.02f),
                new CharacterStaging("Ivy Teague (Female)", true, new Vector3(4.6f, 2.7f, 3.0f), new Vector3(0f, 248f, 0f), 0.98f),
                new CharacterStaging("Priya Raman (Female)", true, new Vector3(2.3f, 0f, -4.4f), Vector3.zero, 0.96f));
        }

        [Test]
        public void MemoryScenes_PlayerRetainsInteractionRaycasterWiring()
        {
            const string playerPrefabPath = PrefabRoot + "Player_FirstPerson.prefab";
            GameObject playerPrefab = PrefabUtility.LoadPrefabContents(playerPrefabPath);
            try
            {
                AssertPlayerInteractionWiring(playerPrefab, playerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(playerPrefab);
            }

            foreach (string scenePath in new[]
            {
                "Assets/_Project/Scenes/Memory_CabinNight.unity",
                "Assets/_Project/Scenes/Memory_CabinMorning.unity",
            })
            {
                Scene scene = OpenPreviewSceneIgnoringKnownTerrainError(scenePath);
                try
                {
                    GameObject player = FindSceneObject(scene, "Player (Male - First Person)");
                    Assert.IsNotNull(player, $"{scenePath} has no player.");
                    AssertPlayerInteractionWiring(player, scenePath);
                }
                finally
                {
                    EditorSceneManager.ClosePreviewScene(scene);
                }
            }
        }

        private static void AssertPlayerInteractionWiring(GameObject player, string context)
        {
            PlayerInputRouter router = player.GetComponent<PlayerInputRouter>();
            InteractionRaycaster raycaster = player.GetComponent<InteractionRaycaster>();
            Camera camera = player.GetComponentInChildren<Camera>(true);
            Assert.IsNotNull(router, $"{context}'s player has no PlayerInputRouter.");
            Assert.IsNotNull(raycaster,
                $"{context}'s player lost InteractionRaycaster, so E presses have no interaction consumer.");
            Assert.IsNotNull(camera, $"{context}'s player has no first-person camera.");

            SerializedObject serializedRaycaster = new SerializedObject(raycaster);
            Assert.AreSame(camera,
                serializedRaycaster.FindProperty("raycastCamera").objectReferenceValue,
                $"{context}'s InteractionRaycaster is not wired to the first-person camera.");
            Assert.AreSame(router,
                serializedRaycaster.FindProperty("input").objectReferenceValue,
                $"{context}'s InteractionRaycaster is not wired to PlayerInputRouter.");
        }

        private static void AssertScene(string scenePath, params CharacterStaging[] expectedCharacters)
        {
            Scene scene = OpenPreviewSceneIgnoringKnownTerrainError(scenePath);
            try
            {
                Transform characters = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    if (root.name == "Characters") characters = root.transform;
                }

                Assert.IsNotNull(characters, $"{scenePath} has no Characters root.");
                foreach (CharacterStaging expected in expectedCharacters)
                {
                    Transform character = characters.Find(expected.Name);
                    Assert.IsNotNull(character, $"{scenePath} has no {expected.Name}.");
                    Assert.AreEqual(expected.Active, character.gameObject.activeSelf);
                    AssertVector(expected.Position, character.position, $"{scenePath}/{expected.Name} position");
                    Assert.Less(Quaternion.Angle(Quaternion.Euler(expected.EulerAngles), character.rotation), 0.01f,
                        $"{scenePath}/{expected.Name} rotation");
                    AssertVector(Vector3.one * expected.Scale, character.localScale, $"{scenePath}/{expected.Name} scale");
                }
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(scene);
            }
        }

        private static Scene OpenPreviewSceneIgnoringKnownTerrainError(string scenePath)
        {
            // This project currently logs an unrelated TerrainData deserialization
            // error the first time either memory scene is opened in an Editor
            // session. Keep that known baseline from failing this character-only
            // regression test; hierarchy and transform assertions still fail if
            // the scene itself does not load correctly.
            bool hasLogScope = true;
            bool previousIgnore = false;
            try
            {
                previousIgnore = LogAssert.ignoreFailingMessages;
            }
            catch (InvalidOperationException)
            {
                // Allows the same assertions to be invoked directly by Unity
                // MCP validation commands, which run outside Test Runner's
                // LogScope. The regular test-runner path still suppresses the
                // known TerrainData baseline below.
                hasLogScope = false;
            }
            try
            {
                if (hasLogScope) LogAssert.ignoreFailingMessages = true;
                return EditorSceneManager.OpenPreviewScene(scenePath);
            }
            finally
            {
                if (hasLogScope) LogAssert.ignoreFailingMessages = previousIgnore;
            }
        }

        private static GameObject FindSceneObject(Scene scene, string name)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
                {
                    if (string.Equals(transform.name, name, StringComparison.Ordinal)) return transform.gameObject;
                }
            }
            return null;
        }

        private static void AssertVector(Vector3 expected, Vector3 actual, string message)
        {
            Assert.Less(Vector3.Distance(expected, actual), TransformTolerance,
                $"{message}: expected {expected}, got {actual}");
        }

        private static Transform FindTransform(Transform root, string name)
        {
            foreach (Transform transform in root.GetComponentsInChildren<Transform>(true))
            {
                if (string.Equals(transform.name, name, StringComparison.Ordinal)) return transform;
            }
            return null;
        }

        private static string ResolveOutermostSourcePath(UnityEngine.Object instanceObject)
        {
            string lastAssetPath = string.Empty;
            UnityEngine.Object current = instanceObject;
            while (current != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(current);
                if (!string.IsNullOrEmpty(assetPath)) lastAssetPath = assetPath;
                current = PrefabUtility.GetCorrespondingObjectFromSource(current);
            }
            return lastAssetPath;
        }

        private sealed class CharacterStaging
        {
            public CharacterStaging(
                string name,
                bool active,
                Vector3 position,
                Vector3 eulerAngles,
                float scale)
            {
                Name = name;
                Active = active;
                Position = position;
                EulerAngles = eulerAngles;
                Scale = scale;
            }

            public string Name { get; }
            public bool Active { get; }
            public Vector3 Position { get; }
            public Vector3 EulerAngles { get; }
            public float Scale { get; }
        }

        public sealed class CastAsset
        {
            public CastAsset(string name, string prefabName)
            {
                Name = name;
                PrefabName = prefabName;
            }

            public string Name { get; }
            public string PrefabName { get; }
            public string ModelPath => CharacterRoot + Name + ".fbx";
            public string PrefabPath => PrefabRoot + PrefabName + ".prefab";

            public override string ToString() => Name;
        }
    }
}
