using System;
using FalsePositive.Flow;
using FalsePositive.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// B3/B6 (docs/GAME_COMPLETION_PLAN.md): labelled primitive placeholders
    /// for every interactable and cutscene prop in the two memory scenes.
    /// Existing cabin furniture (table, door, fireplace, ...) is left as-is —
    /// this only adds the story-relevant props that don't exist yet. Coloured
    /// cube/cylinder + a floating TextMesh name label makes each one
    /// unambiguous to identify while playing, per the user's explicit choice
    /// over bare/unlabelled cubes.
    /// </summary>
    public static class MemorySceneDressing
    {
        private const string NightScenePath = "Assets/_Project/Scenes/Memory_CabinNight.unity";
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";

        [MenuItem("Tools/False Positive/Bootstrap/8 - Dress Memory Scenes")]
        public static void DressBothScenes()
        {
            DressNight();
            DressMorning();
        }

        [MenuItem("Tools/False Positive/Bootstrap/8a - Dress Memory_CabinNight")]
        public static void DressNight()
        {
            Scene scene = EditorSceneManager.OpenScene(NightScenePath, OpenSceneMode.Single);
            GameObject root = FindOrCreateRoot();

            // Table at (0.71, 0.04, -1.64) per the existing Kitchen Table.
            InspectPoint cups = AddProp<InspectPoint>(root, "Prop_FiveCups", new Vector3(0.71f, 0.42f, -1.64f),
                new Vector3(0.35f, 0.15f, 0.35f), new Color(0.85f, 0.8f, 0.7f), "5 Cups",
                "Look at the cups", MemoryFlagIds.SawFiveCups);
            AddProp<InspectPoint>(root, "Prop_Bottles", new Vector3(0.95f, 0.46f, -1.5f),
                new Vector3(0.12f, 0.3f, 0.12f), new Color(0.15f, 0.4f, 0.2f), "Bottles",
                "Look at the bottles", null);

            // Mantel at (0.65, 1.72, 2.05).
            RadioTuner radio = AddProp<RadioTuner>(root, "Prop_Radio", new Vector3(0.2f, 1.72f, 2.0f),
                new Vector3(0.3f, 0.2f, 0.15f), new Color(0.3f, 0.3f, 0.3f), "Radio",
                "Hold E to tune", null);
            AddProp<InspectPoint>(root, "Prop_MantelClock", new Vector3(1.05f, 1.75f, 2.0f),
                new Vector3(0.2f, 0.25f, 0.1f), new Color(0.5f, 0.4f, 0.25f), "Clock (00:52)",
                "Look at the clock", MemoryFlagIds.SawClock);

            // Chair by the door at (-3.76, 0.02, -1.35).
            AddProp<InspectPoint>(root, "Prop_CoatOnChair", new Vector3(-3.2f, 0.5f, -1.0f),
                new Vector3(0.4f, 0.5f, 0.15f), new Color(0.5f, 0.15f, 0.15f), "Nick's Coat",
                "Look at the coat", MemoryFlagIds.SawCoatSwap);

            // Landing/stairs — Ivy's built position (4.58, 1.72, -1.68) is upstairs.
            AddProp<InspectPoint>(root, "Prop_BlockedStairs", new Vector3(3.6f, 0.1f, -1.6f),
                new Vector3(0.6f, 0.2f, 0.6f), new Color(0.4f, 0.3f, 0.2f), "Stairs (blocked)",
                "Aaron and Ivy went up an hour ago.", null);

            // Front window — no separate window GameObject exists on the wall
            // containing the front door; placed on the same wall.
            AddProp<InspectPoint>(root, "Prop_FrontWindow", new Vector3(-3.76f, 1.4f, 1.2f),
                new Vector3(0.1f, 0.7f, 0.9f), new Color(0.15f, 0.2f, 0.35f), "Window (curtained)",
                "Look at the window", null);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, NightScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MemorySceneDressing] Memory_CabinNight dressed.");
        }

        [MenuItem("Tools/False Positive/Bootstrap/8b - Dress Memory_CabinMorning")]
        public static void DressMorning()
        {
            Scene scene = EditorSceneManager.OpenScene(MorningScenePath, OpenSceneMode.Single);
            GameObject root = FindOrCreateRoot();

            AddProp<InspectPoint>(root, "Prop_BrokenPane", new Vector3(-3.76f, 1.4f, 1.2f),
                new Vector3(0.1f, 0.7f, 0.9f), new Color(0.6f, 0.75f, 0.85f), "Broken Pane",
                "Look at the window", MemoryFlagIds.SawGlassInside);
            AddProp<InspectPoint>(root, "Prop_IntactGrille", new Vector3(-3.7f, 1.4f, 1.2f),
                new Vector3(0.05f, 0.7f, 0.9f), new Color(0.25f, 0.25f, 0.25f), "Grille (intact)",
                "Look at the grille", MemoryFlagIds.SawGrilleIntact);

            AddProp<InspectPoint>(root, "Prop_NickBody", new Vector3(-6.5f, 0.15f, 2.5f),
                new Vector3(1.8f, 0.3f, 0.6f), new Color(0.65f, 0.55f, 0.55f), "Nick (in the snow)",
                "Look at the body", MemoryFlagIds.SawBody);

            DoorInteractable door = AddProp<DoorInteractable>(root, "Prop_FrontDoor_Locked",
                new Vector3(-3.9f, 0.5f, -1.35f), new Vector3(0.15f, 1f, 0.6f),
                new Color(0.35f, 0.22f, 0.12f), "Front Door", "It's locked.", MemoryFlagIds.FoundDoorLocked);
            SerializedObject doorSo = new SerializedObject(door);
            doorSo.FindProperty("startsLocked").boolValue = true;
            doorSo.ApplyModifiedPropertiesWithoutUndo();

            KeyPickup key = AddProp<KeyPickup>(root, "Prop_DoorKey", new Vector3(-3.55f, 1.1f, -1.35f),
                new Vector3(0.08f, 0.08f, 0.02f), new Color(0.8f, 0.7f, 0.2f), "Key",
                "Take the key", MemoryFlagIds.FoundKeyInside);
            SerializedObject keySo = new SerializedObject(key);
            keySo.FindProperty("doorToUnlock").objectReferenceValue = door;
            keySo.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MorningScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MemorySceneDressing] Memory_CabinMorning dressed.");
        }

        private static GameObject FindOrCreateRoot()
        {
            GameObject root = GameObject.Find("StoryProps");
            if (root != null)
            {
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                {
                    UnityEngine.Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
                }
                return root;
            }
            return new GameObject("StoryProps");
        }

        private static T AddProp<T>(
            GameObject parent, string name, Vector3 position, Vector3 size, Color color,
            string labelText, string lookPrompt, string memoryFlag) where T : Interactable
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = position;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            visual.name = "Visual";
            visual.transform.SetParent(go.transform, false);
            visual.transform.localScale = size;
            ApplyMaterial(visual, color);

            GameObject labelGo = new GameObject("Label", typeof(TextMesh));
            labelGo.transform.SetParent(go.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, size.y * 0.5f + 0.2f, 0f);
            TextMesh label = labelGo.GetComponent<TextMesh>();
            label.text = labelText;
            label.characterSize = 0.12f;
            label.fontSize = 48;
            label.anchor = TextAnchor.LowerCenter;
            label.alignment = TextAlignment.Center;
            label.color = Color.yellow;

            T interactable = go.AddComponent<T>();
            SerializedObject so = new SerializedObject(interactable);
            so.FindProperty("lookPrompt").stringValue = lookPrompt;
            so.FindProperty("memoryFlag").stringValue = memoryFlag ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            return interactable;
        }

        private static void ApplyMaterial(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            renderer.sharedMaterial = material;
        }
    }
}
