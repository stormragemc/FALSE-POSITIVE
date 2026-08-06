using System;
using System.Reflection;
using FalsePositive.Cutscene;
using FalsePositive.Interaction;
using FalsePositive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// B5/B7 (docs/GAME_COMPLETION_PLAN.md): adds InteractionRaycaster to each
    /// memory scene's player and wires M1NightController/M2MorningController
    /// to the props MemorySceneDressing already placed. Everything referenced
    /// here is same-scene — no cross-scene [SerializeField] is created.
    /// </summary>
    public static class MemorySceneWiring
    {
        private const string NightScenePath = "Assets/_Project/Scenes/Memory_CabinNight.unity";
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";

        [MenuItem("Tools/False Positive/Bootstrap/9 - Wire Memory Scene Beats")]
        public static void WireBoth()
        {
            WireNight();
            WireMorning();
        }

        [MenuItem("Tools/False Positive/Bootstrap/9a - Wire Memory_CabinNight")]
        public static void WireNight()
        {
            Scene scene = EditorSceneManager.OpenScene(NightScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player (Male - First Person)");
            Transform view = player.transform.Find("FirstPersonView");
            Camera camera = view.GetComponent<Camera>();
            PlayerInputRouter router = player.GetComponent<PlayerInputRouter>();
            if (router == null) router = player.AddComponent<PlayerInputRouter>();

            InteractionRaycaster raycaster = player.GetComponent<InteractionRaycaster>();
            if (raycaster == null) raycaster = player.AddComponent<InteractionRaycaster>();
            SetField(raycaster, "raycastCamera", camera);
            SetField(raycaster, "input", router);

            GameObject radioGo = GameObject.Find("Prop_Radio");
            RadioTuner radio = radioGo.GetComponent<RadioTuner>();

            GameObject controllerGo = GameObject.Find("M1NightController");
            if (controllerGo == null) controllerGo = new GameObject("M1NightController");
            // Just inside the Cabin_v2 door hinge (-3.379, 0, -4.121).
            controllerGo.transform.position = new Vector3(-3.0f, 1f, -3.8f);

            BoxCollider trigger = controllerGo.GetComponent<BoxCollider>();
            if (trigger == null) trigger = controllerGo.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = new Vector3(1.5f, 2f, 1.5f);

            M1NightController controller = controllerGo.GetComponent<M1NightController>();
            if (controller == null) controller = controllerGo.AddComponent<M1NightController>();
            SetField(controller, "radio", radio);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, NightScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MemorySceneWiring] Memory_CabinNight wired.");
        }

        [MenuItem("Tools/False Positive/Bootstrap/9b - Wire Memory_CabinMorning")]
        public static void WireMorning()
        {
            Scene scene = EditorSceneManager.OpenScene(MorningScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find("Player (Male - First Person)");
            Transform view = player.transform.Find("FirstPersonView");
            Camera camera = view.GetComponent<Camera>();
            PlayerInputRouter router = player.GetComponent<PlayerInputRouter>();
            if (router == null) router = player.AddComponent<PlayerInputRouter>();

            InteractionRaycaster raycaster = player.GetComponent<InteractionRaycaster>();
            if (raycaster == null) raycaster = player.AddComponent<InteractionRaycaster>();
            SetField(raycaster, "raycastCamera", camera);
            SetField(raycaster, "input", router);

            GameObject doorGo = GameObject.Find("Prop_FrontDoor_Locked");
            DoorInteractable door = doorGo.GetComponent<DoorInteractable>();

            GameObject controllerGo = GameObject.Find("M2MorningController");
            if (controllerGo == null) controllerGo = new GameObject("M2MorningController");

            M2MorningController controller = controllerGo.GetComponent<M2MorningController>();
            if (controller == null) controller = controllerGo.AddComponent<M2MorningController>();
            SetField(controller, "frontDoor", door);

            // CutsceneStage lives on "Sequencing" (MemorySceneBuilderV2) —
            // wire its lift-interlude SFX the same way frontDoor is wired
            // above. Missing SFX is non-fatal (LiftPrompt no-ops on a null
            // clip); AssetDatabase just leaves the field null.
            CutsceneStage stage = UnityEngine.Object.FindAnyObjectByType<CutsceneStage>();
            if (stage != null)
            {
                AudioClip liftClip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Project/Art/Audio/SFX/body_lift_effort.mp3");
                if (liftClip == null)
                {
                    Debug.LogWarning("[MemorySceneWiring] Assets/_Project/Art/Audio/SFX/body_lift_effort.mp3 " +
                        "not found — CutsceneStage.liftEffortClip left unset.");
                }
                SetField(stage, "liftEffortClip", liftClip);

                AudioClip ivyLine = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    "Assets/_Project/Art/Audio/VO/ivy_careful_lift.mp3");
                if (ivyLine == null)
                {
                    Debug.LogWarning("[MemorySceneWiring] Assets/_Project/Art/Audio/VO/ivy_careful_lift.mp3 " +
                        "not found — CutsceneStage.ivyLiftLineClip left unset.");
                }
                SetField(stage, "ivyLiftLineClip", ivyLine);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MorningScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MemorySceneWiring] Memory_CabinMorning wired.");
        }

        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                throw new InvalidOperationException($"[MemorySceneWiring] {target.GetType().Name} has no field '{fieldName}'.");
            }
            field.SetValue(target, value);
        }
    }
}
