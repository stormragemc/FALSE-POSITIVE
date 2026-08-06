using System;
using System.Reflection;
using FalsePositive.Core;
using FalsePositive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// T04 (docs/GAME_COMPLETION_PLAN.md): duplicates the teaser cabin scene into
    /// the two memory-phase scenes and swaps each player rig from the standalone
    /// CabinFirstPersonController (reads Mouse/Keyboard directly, cannot be
    /// input-gated during a cutscene) to the router-based rig shared with
    /// Interrogation. No cross-scene binder is needed here: FreeLookCameraRig's
    /// dependencies are same-scene objects and a shared InterrogationConfig
    /// asset, not another scene's MonoBehaviours.
    /// </summary>
    public static class MemorySceneBuilder
    {
        private const string SourceScenePath = "Assets/_Project/Scenes/NobodyWentOut_CabinNight.unity";
        private const string NightScenePath = "Assets/_Project/Scenes/Memory_CabinNight.unity";
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";
        private const string ConfigPath = "Assets/_Project/Config/InterrogationConfig.asset";

        [MenuItem("Tools/False Positive/Bootstrap/T04 - Build Memory Scenes")]
        public static void BuildMemoryScenes()
        {
            DuplicateAndRebuild(NightScenePath);
            DuplicateAndRebuild(MorningScenePath);
            Debug.Log("[MemorySceneBuilder] Memory_CabinNight.unity and Memory_CabinMorning.unity built.");
        }

        private static void DuplicateAndRebuild(string targetPath)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) == null)
            {
                if (!AssetDatabase.CopyAsset(SourceScenePath, targetPath))
                {
                    throw new InvalidOperationException($"[MemorySceneBuilder] Failed to copy {SourceScenePath} -> {targetPath}");
                }
                AssetDatabase.Refresh();
            }

            CabinNightCharacterBuilder.RebuildCastForScene(targetPath);

            Scene scene = EditorSceneManager.OpenScene(targetPath, OpenSceneMode.Single);
            ConvertPlayerToRouterRig(scene);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, targetPath);
            AssetDatabase.SaveAssets();
        }

        private static void ConvertPlayerToRouterRig(Scene scene)
        {
            GameObject player = GameObject.Find("Player (Male - First Person)");
            if (player == null)
            {
                throw new InvalidOperationException("[MemorySceneBuilder] Player object not found after cast rebuild.");
            }

            CabinFirstPersonController legacy = player.GetComponent<CabinFirstPersonController>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);

            Transform view = player.transform.Find("FirstPersonView");
            if (view == null)
            {
                throw new InvalidOperationException("[MemorySceneBuilder] FirstPersonView child not found on Player.");
            }

            InterrogationConfig config = AssetDatabase.LoadAssetAtPath<InterrogationConfig>(ConfigPath);

            PlayerInputRouter router = player.GetComponent<PlayerInputRouter>() ?? player.AddComponent<PlayerInputRouter>();
            FreeLookCameraRig rig = player.GetComponent<FreeLookCameraRig>() ?? player.AddComponent<FreeLookCameraRig>();
            SetField(rig, "input", router);
            SetField(rig, "playerCamera", view);
            SetField(rig, "config", config);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                throw new InvalidOperationException($"[MemorySceneBuilder] {target.GetType().Name} has no field '{fieldName}'.");
            }
            field.SetValue(target, value);
        }
    }
}
