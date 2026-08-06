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
    /// B3/B6 (docs/GAME_COMPLETION_PLAN.md): places every interactable and
    /// cutscene prop in the two memory scenes on the Cabin_v2 shell (see
    /// CabinV2Builder/MemorySceneBuilderV2). Existing shell furniture (table,
    /// door, fireplace, window grille, ...) is left as-is — this only adds
    /// the story-relevant props that don't exist yet, using a real low-poly
    /// model from Art/Props/ when one exists for the prop, or a coloured
    /// placeholder cube otherwise. No floating name label anymore — that was
    /// removed per the user's explicit request; UI.InteractionPromptUI's
    /// on-screen "[E] &lt;prompt&gt;" line plus a looked-at highlight are the
    /// replacement identification.
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

            // Coordinates below are the Cabin_v2 shell's own layout (plan
            // Phase 2b), not the old Assets\Cabin pack's — SM_Table top is
            // at (-3.0, 0.75, 2.3), the mantel runs along the +Z fireplace
            // wall, the coat hanger is BO_CoatHanger at (-1.4, 0, -4.5).
            InspectPoint cups = AddProp<InspectPoint>(root, "Prop_FiveCups", new Vector3(-3.0f, 0.78f, 2.15f),
                new Vector3(0.35f, 0.15f, 0.35f), new Color(0.85f, 0.8f, 0.7f), "5 Cups",
                "Look at the cups", MemoryFlagIds.SawFiveCups);
            AddProp<InspectPoint>(root, "Prop_Bottles", new Vector3(-2.7f, 0.82f, 2.4f),
                new Vector3(0.12f, 0.3f, 0.12f), new Color(0.15f, 0.4f, 0.2f), "Bottles",
                "Look at the bottles", null);

            // Mantel — fireplace runs the +Z wall (x in [-0.9,0.9], z~4.3).
            RadioTuner radio = AddProp<RadioTuner>(root, "Prop_Radio", new Vector3(-0.35f, 1.35f, 3.45f),
                new Vector3(0.3f, 0.2f, 0.15f), new Color(0.3f, 0.3f, 0.3f), "Radio",
                "Tune the radio", null);
            WireRadioAudio(radio);

            InspectPoint clock = AddProp<InspectPoint>(root, "Prop_MantelClock", new Vector3(0.35f, 1.45f, 3.45f),
                new Vector3(0.2f, 0.25f, 0.1f), new Color(0.5f, 0.4f, 0.25f), "Clock (00:52)",
                "Look at the clock", MemoryFlagIds.SawClock);
            AddAmbientLoop(clock.gameObject, "clock_tick_loop", volume: 0.35f);

            // On the coat hanger, BO_CoatHanger at (-1.4, 0, -4.5), ~1.79 m tall.
            AddProp<InspectPoint>(root, "Prop_CoatOnChair", new Vector3(-1.4f, 1.45f, -4.5f),
                new Vector3(0.4f, 0.5f, 0.15f), new Color(0.5f, 0.15f, 0.15f), "Nick's Coat",
                "Look at the coat", MemoryFlagIds.SawCoatSwap);

            // Bottom of the +X wall stair run (SM_Cabin_Stairs x in [3.9,5.0]).
            AddProp<InspectPoint>(root, "Prop_BlockedStairs", new Vector3(3.9f, 0.2f, -1.2f),
                new Vector3(0.6f, 0.2f, 0.6f), new Color(0.4f, 0.3f, 0.2f), "Stairs (blocked)",
                "Aaron and Ivy went up an hour ago.", null);

            // In front of BO_WindowGrille on the -Z wall (grille at z~-5.05).
            AddProp<InspectPoint>(root, "Prop_FrontWindow", new Vector3(2.3f, 1.6f, -4.95f),
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

            // Window opening on the -Z wall (BO_WindowGrille at z~-5.05).
            InspectPoint brokenPane = AddProp<InspectPoint>(root, "Prop_BrokenPane", new Vector3(2.3f, 1.6f, -5.0f),
                new Vector3(0.1f, 0.7f, 0.9f), new Color(0.6f, 0.75f, 0.85f), "Broken Pane",
                "Look at the window", MemoryFlagIds.SawGlassInside);
            SetClip(brokenPane, "inspectClip", "glass_crunch");

            // The intact grille is Cabin_v2's own BO_WindowGrille — no new
            // geometry needed, just an inspect trigger on the existing mesh.
            GameObject cabin = GameObject.Find("Cabin_v2");
            Transform grilleTransform = cabin != null ? cabin.transform.Find("BO_WindowGrille") : null;
            if (grilleTransform != null)
            {
                InspectPoint grilleInspect = grilleTransform.gameObject.GetComponent<InspectPoint>();
                if (grilleInspect == null) grilleInspect = grilleTransform.gameObject.AddComponent<InspectPoint>();
                SerializedObject grilleSo = new SerializedObject(grilleInspect);
                grilleSo.FindProperty("lookPrompt").stringValue = "Look at the grille";
                grilleSo.FindProperty("memoryFlag").stringValue = MemoryFlagIds.SawGrilleIntact;
                grilleSo.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[MemorySceneDressing] BO_WindowGrille not found on Cabin_v2 — grille inspect point skipped.");
            }

            // Outside, past the window, visible through the broken pane.
            AddProp<InspectPoint>(root, "Prop_NickBody", new Vector3(2.3f, 0.1f, -6.3f),
                new Vector3(1.8f, 0.3f, 0.6f), new Color(0.65f, 0.55f, 0.55f), "Nick (in the snow)",
                "Look at the body", MemoryFlagIds.SawBody);

            // The door itself is the real Door_v2 instance placed by
            // MemorySceneBuilderV2 (named "Prop_FrontDoor_Locked" so
            // downstream lookups need no changes) — not a placeholder cube.
            GameObject doorGo = GameObject.Find("Prop_FrontDoor_Locked");
            DoorInteractable door = doorGo != null ? doorGo.GetComponent<DoorInteractable>() : null;
            if (door != null)
            {
                SerializedObject doorSo = new SerializedObject(door);
                doorSo.FindProperty("startsLocked").boolValue = true;
                doorSo.FindProperty("lookPrompt").stringValue = "It's locked.";
                doorSo.FindProperty("memoryFlag").stringValue = MemoryFlagIds.FoundDoorLocked;
                doorSo.ApplyModifiedPropertiesWithoutUndo();

                AudioSource doorAudio = door.gameObject.GetComponent<AudioSource>();
                if (doorAudio == null) doorAudio = door.gameObject.AddComponent<AudioSource>();
                doorAudio.playOnAwake = false;
                doorSo.Update();
                doorSo.FindProperty("audioSource").objectReferenceValue = doorAudio;
                doorSo.ApplyModifiedPropertiesWithoutUndo();
                SetClip(door, "openClip", "door_creak_open");
            }
            else
            {
                Debug.LogError("[MemorySceneDressing] Prop_FrontDoor_Locked (Door_v2 instance) not found — run MemorySceneBuilderV2 first.");
            }

            // Hook immediately left of the door frame (hinge at x=-3.379,
            // z=-4.121; "left of the frame" facing the door from inside).
            KeyPickup key = AddProp<KeyPickup>(root, "Prop_DoorKey", new Vector3(-2.75f, 1.5f, -4.65f),
                new Vector3(0.08f, 0.08f, 0.02f), new Color(0.8f, 0.7f, 0.2f), "Key",
                "Take the key", MemoryFlagIds.FoundKeyInside);
            if (door != null)
            {
                SerializedObject keySo = new SerializedObject(key);
                keySo.FindProperty("doorToUnlock").objectReferenceValue = door;
                keySo.ApplyModifiedPropertiesWithoutUndo();
            }
            SetClip(key, "pickupClip", "key_pickup");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MorningScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MemorySceneDressing] Memory_CabinMorning dressed.");
        }

        private const string SfxRoot = "Assets/_Project/Art/Audio/SFX/";

        /// <summary>Loop AudioSource, playOnAwake, on the given GameObject — clock tick, and (from MemorySceneBuilderV2) fire crackle / interior wind.</summary>
        private static void AddAmbientLoop(GameObject go, string clipName, float volume = 1f)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + clipName + ".mp3");
            if (clip == null)
            {
                Debug.LogWarning($"[MemorySceneDressing] {SfxRoot}{clipName}.mp3 not found — skipping ambient loop on {go.name}.");
                return;
            }
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = volume;
            source.spatialBlend = 1f; // 3D — fades with distance from the prop
            go.AddComponent<FalsePositive.Audio.LoopOnEnable>();
        }

        /// <summary>Sets a serialized AudioClip field by name, loading from Art/Audio/SFX by filename.</summary>
        private static void SetClip(Interactable interactable, string fieldName, string clipName)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + clipName + ".mp3");
            if (clip == null)
            {
                Debug.LogWarning($"[MemorySceneDressing] {SfxRoot}{clipName}.mp3 not found — {interactable.name}.{fieldName} left unset.");
                return;
            }
            SerializedObject so = new SerializedObject(interactable);
            so.FindProperty(fieldName).objectReferenceValue = clip;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void WireRadioAudio(RadioTuner radio)
        {
            AudioSource staticSource = radio.gameObject.AddComponent<AudioSource>();
            staticSource.loop = true;
            staticSource.playOnAwake = false; // RadioTuner.Awake() starts it explicitly
            staticSource.volume = 0.5f;
            staticSource.spatialBlend = 1f;

            AudioSource sfxSource = radio.gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
            sfxSource.spatialBlend = 1f;

            SerializedObject so = new SerializedObject(radio);
            so.FindProperty("staticLoopSource").objectReferenceValue = staticSource;
            so.FindProperty("sfxSource").objectReferenceValue = sfxSource;
            so.ApplyModifiedPropertiesWithoutUndo();

            SetClip(radio, "tuningSweepClip", "radio_tuning_sweep");
            SetClip(radio, "lockOnClip", "radio_lock_on");

            AudioClip staticClip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + "radio_static_loop.mp3");
            if (staticClip != null) staticSource.clip = staticClip;
            else Debug.LogWarning($"[MemorySceneDressing] {SfxRoot}radio_static_loop.mp3 not found — radio static loop left unset.");
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

        private const string ModelRoot = "Assets/_Project/Art/Props/";

        private static T AddProp<T>(
            GameObject parent, string name, Vector3 position, Vector3 size, Color color,
            string labelText, string lookPrompt, string memoryFlag) where T : Interactable
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.position = position;

            // Real low-poly model when one exists for this prop (built via
            // Blender MCP, see docs/GAME_COMPLETION_PLAN.md's model list) —
            // falls back to the labelled placeholder cube so a missing FBX
            // never produces an invisible/unpickable prop.
            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelRoot + name + ".fbx");
            GameObject visual;
            Bounds localBounds;
            if (modelPrefab != null)
            {
                visual = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab, go.transform);
                visual.name = "Visual";
                visual.transform.localPosition = Vector3.zero;
                visual.transform.localRotation = Quaternion.identity;
                ApplyMaterialRecursive(visual, color);
                localBounds = ComputeLocalBounds(visual, go.transform);
            }
            else
            {
                Debug.LogWarning($"[MemorySceneDressing] No model at {ModelRoot}{name}.fbx — " +
                    "falling back to a placeholder cube.");
                visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visual.name = "Visual";
                visual.transform.SetParent(go.transform, false);
                visual.transform.localScale = size;
                ApplyMaterialRecursive(visual, color);
                localBounds = new Bounds(Vector3.zero, size);
            }

            BoxCollider collider = go.AddComponent<BoxCollider>();
            collider.center = localBounds.center;
            collider.size = Vector3.Max(localBounds.size, new Vector3(0.05f, 0.05f, 0.05f));

            // Floating TextMesh name labels used to live here — the game's
            // only on-screen identification for an interactable, per
            // InteractionRaycaster's old doc comment. Removed per the user's
            // explicit request ("remove all those placeholder assets that
            // have text above"); UI.InteractionPromptUI now shows a
            // centre-screen "[E] <prompt>" line driven by
            // InteractionRaycaster.Current instead (Phase 3 of the Cabin_v2
            // pass), and a MaterialPropertyBlock emissive tint on the looked-
            // at renderer keeps props findable without a label. `labelText`
            // is kept as a parameter for now (used in a couple of call sites'
            // comments/debugging) but no longer produces on-screen text.
            T interactable = go.AddComponent<T>();
            SerializedObject so = new SerializedObject(interactable);
            so.FindProperty("lookPrompt").stringValue = lookPrompt;
            so.FindProperty("memoryFlag").stringValue = memoryFlag ?? string.Empty;
            so.ApplyModifiedPropertiesWithoutUndo();

            return interactable;
        }

        private static Bounds ComputeLocalBounds(GameObject visual, Transform relativeTo)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.1f);

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = relativeTo.InverseTransformPoint(worldBounds.center);
            Vector3 localSize = relativeTo.InverseTransformVector(worldBounds.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            return new Bounds(localCenter, localSize);
        }

        private static void ApplyMaterialRecursive(GameObject go, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            // UI.InteractionPromptUI drives a looked-at highlight purely via
            // MaterialPropertyBlock (no per-instance material), which can
            // only override an existing shader keyword's value, not enable
            // the keyword itself — so _EMISSION must be on here, at the
            // shared material, for the highlight to render at all.
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            foreach (Renderer renderer in go.GetComponentsInChildren<Renderer>())
            {
                Material[] shared = new Material[renderer.sharedMaterials.Length == 0 ? 1 : renderer.sharedMaterials.Length];
                for (int i = 0; i < shared.Length; i++) shared[i] = material;
                renderer.sharedMaterials = shared;
            }
        }
    }
}
