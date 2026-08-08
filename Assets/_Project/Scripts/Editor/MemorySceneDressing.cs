using System;
using FalsePositive.CabinNight;
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
            // Both meshes are authored (Blender MCP, ArtSource/Props_Drinks.blend) with
            // their base at local z=0, so y=0.75 (SM_Table top) seats them ON the table —
            // the old 0.78/0.82 sank them ~8cm through it. glass:true swaps the flat
            // placeholder colour for an actual transparent URP material.
            InspectPoint cups = AddProp<InspectPoint>(root, "Prop_FiveCups", new Vector3(-3.0f, 0.75f, 2.15f),
                new Vector3(0.35f, 0.15f, 0.35f), new Color(0.85f, 0.8f, 0.7f), "5 Cups",
                "Look at the cups", MemoryFlagIds.SawFiveCups, glass: true);
            AddProp<InspectPoint>(root, "Prop_Bottles", new Vector3(-2.7f, 0.75f, 2.4f),
                new Vector3(0.12f, 0.3f, 0.12f), new Color(0.15f, 0.4f, 0.2f), "Bottles",
                "Look at the bottles", null, glass: true);

            // ON the mantel shelf. Found by casting rays straight DOWN onto the
            // fireplace rather than trusting SM_Fireplace_Stone's bounding box,
            // which is misleading here: the box spans z [3.50, 5.00] but the
            // shelf surface only begins at z 3.85 — every ray in front of that
            // falls past it to the hearth at y 0.05. The shelf top is y 1.380,
            // and SM_Fireplace_Brick (the chimney breast) rises from z 4.00, so
            // the usable strip is just z [3.85, 4.00].
            //
            // The old values had both props hanging in mid-air in front of the
            // fireplace AND sunk into it: at z 3.45 they were 0.40 forward of
            // any surface, with the radio's base at y 1.17 — 0.21 below the
            // shelf it was supposed to stand on. Each y below is derived from
            // that prop's own measured model bounds so its BASE lands exactly
            // on 1.380 (the radio's base sits 0.185 under its origin, the
            // clock's 0.125). The clock fits the 0.15 m strip outright; the
            // radio is 0.21 deep so it is biased forward to overhang the front
            // lip slightly rather than clip through the brickwork behind.
            RadioTuner radio = AddProp<RadioTuner>(root, "Prop_Radio", new Vector3(-0.35f, 1.565f, 3.91f),
                new Vector3(0.3f, 0.2f, 0.15f), new Color(0.3f, 0.3f, 0.3f), "Radio",
                "Tune the radio", null);
            WireRadioAudio(radio);

            InspectPoint clock = AddProp<InspectPoint>(root, "Prop_MantelClock", new Vector3(0.35f, 1.505f, 3.93f),
                new Vector3(0.2f, 0.25f, 0.1f), new Color(0.5f, 0.4f, 0.25f), "Clock (00:52)",
                "Look at the clock", MemoryFlagIds.SawClock);
            // 0.35 was loud enough to sit on top of dialogue from across the
            // room. A mantel clock should only be audible near the fireplace.
            AddAmbientLoop(clock.gameObject, "clock_tick_loop", volume: 0.10f);

            // On the front peg of BO_CoatHanger (pole at (-1.4, 0, -4.5),
            // ~1.79 m tall). Its own BoxCollider is a solid 0.56x0.56x1.79 m
            // box centred on that axis (z in [-4.78,-4.22]) — a prop placed
            // ON the axis, like this used to be, sits fully enclosed inside
            // it, so every raycast toward the prop hits the hanger first and
            // it becomes invisible AND unpickable (GetComponentInParent
            // finds no Interactable on the hanger). Offset along +Z, clear
            // of that box, to read as hung on the front peg instead.
            AddProp<InspectPoint>(root, "Prop_CoatOnChair", new Vector3(-1.4f, 1.45f, -4.10f),
                new Vector3(0.4f, 0.5f, 0.15f), new Color(0.5f, 0.15f, 0.15f), "Nick's Coat",
                "Look at the coat", MemoryFlagIds.SawCoatSwap);

            // The M1 front door — same Door_v2 instance MemorySceneBuilderV2
            // places as "Prop_FrontDoor_Locked", but DressNight never
            // configured it, so it ran on the prefab's own defaults:
            // lookPrompt empty, startsLocked false, no AudioSource. Result:
            // InteractionPromptUI.UpdateHighlight still emissive-tints it on
            // look (no prompt gates that), and E fires MarkComplete()+Opened
            // in total silence — it read as a broken, glowing, do-nothing
            // door. M1's door is meant to stay a trigger-volume-only beat
            // (see M1NightController) — locking it and giving the locked
            // branch feedback, same as DressMorning already does for M2,
            // fixes both without changing what the door DOES here.
            GameObject nightDoorGo = GameObject.Find("Prop_FrontDoor_Locked");
            DoorInteractable nightDoor = nightDoorGo != null ? nightDoorGo.GetComponent<DoorInteractable>() : null;
            if (nightDoor != null)
            {
                SerializedObject nightDoorSo = new SerializedObject(nightDoor);
                nightDoorSo.FindProperty("startsLocked").boolValue = true;
                nightDoorSo.FindProperty("lookPrompt").stringValue = "It's locked.";
                nightDoorSo.FindProperty("memoryFlag").stringValue = string.Empty;
                nightDoorSo.ApplyModifiedPropertiesWithoutUndo();

                AudioSource nightDoorAudio = nightDoor.gameObject.GetComponent<AudioSource>();
                if (nightDoorAudio == null) nightDoorAudio = nightDoor.gameObject.AddComponent<AudioSource>();
                nightDoorAudio.playOnAwake = false;
                nightDoorSo.Update();
                nightDoorSo.FindProperty("audioSource").objectReferenceValue = nightDoorAudio;
                nightDoorSo.ApplyModifiedPropertiesWithoutUndo();
                SetClip(nightDoor, "lockedClip", "door_locked_rattle");
            }
            else
            {
                Debug.LogWarning("[MemorySceneDressing] Prop_FrontDoor_Locked not found in Memory_CabinNight — door feedback skipped.");
            }

            // Bottom of the +X wall stair run (SM_Cabin_Stairs x in [3.9,5.0]).
            AddProp<InspectPoint>(root, "Prop_BlockedStairs", new Vector3(3.9f, 0.2f, -1.2f),
                new Vector3(0.6f, 0.2f, 0.6f), new Color(0.4f, 0.3f, 0.2f), "Stairs (blocked)",
                "Aaron and Ivy went up an hour ago.", null);

            // In front of BO_WindowGrille on the -Z wall (grille at z~-5.05).
            AddProp<InspectPoint>(root, "Prop_FrontWindow", new Vector3(2.3f, 1.6f, -4.95f),
                new Vector3(0.1f, 0.7f, 0.9f), new Color(0.15f, 0.2f, 0.35f), "Window (curtained)",
                "Look at the window", null);

            AddStairBlocker();

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
                // No base-class memoryFlag here on purpose: DoorInteractable
                // now writes MemoryFlagIds.FoundDoorLocked itself, directly,
                // the first time the player tries the locked door — writing
                // it here would instead fire MarkComplete's memoryFlag on
                // *open* (the base Interactable.MarkComplete path), which is
                // the wrong moment and backwards from what the flag name says.
                doorSo.FindProperty("memoryFlag").stringValue = string.Empty;
                doorSo.ApplyModifiedPropertiesWithoutUndo();

                AudioSource doorAudio = door.gameObject.GetComponent<AudioSource>();
                if (doorAudio == null) doorAudio = door.gameObject.AddComponent<AudioSource>();
                doorAudio.playOnAwake = false;
                doorSo.Update();
                doorSo.FindProperty("audioSource").objectReferenceValue = doorAudio;
                doorSo.ApplyModifiedPropertiesWithoutUndo();
                SetClip(door, "openClip", "door_creak_open");
                SetClip(door, "lockedClip", "door_locked_rattle");
                SetClip(door, "unlockClip", "door_unlock_click");
            }
            else
            {
                Debug.LogError("[MemorySceneDressing] Prop_FrontDoor_Locked (Door_v2 instance) not found — run MemorySceneBuilderV2 first.");
            }

            // On the front peg of BO_CoatHanger, same spot DressNight hangs
            // Nick's coat (matches the story beat — key hung on the inside
            // hook, STORY_SCRIPT.md §2). Used to float at (-2.75, 1.5, -4.65),
            // ~1.4m from the actual hanger and effectively unfindable; then
            // moved onto the hanger's own axis (-1.4, 1.45, -4.5), which is
            // WORSE — the hanger's BoxCollider is a solid 0.56x0.56x1.79 m
            // box centred on that exact axis (z in [-4.78,-4.22]), so the key
            // was fully enclosed inside it: every raycast hit the hanger
            // first, GetComponentInParent found no Interactable there, and
            // the key was unpickable from any direction. Offset along +Z,
            // clear of that box, same as Prop_CoatOnChair in DressNight.
            KeyPickup key = AddProp<KeyPickup>(root, "Prop_DoorKey", new Vector3(-1.4f, 1.45f, -4.10f),
                new Vector3(0.08f, 0.08f, 0.02f), new Color(0.8f, 0.7f, 0.2f), "Key",
                "Take the key", MemoryFlagIds.FoundKeyInside);
            if (door != null)
            {
                SerializedObject keySo = new SerializedObject(key);
                keySo.FindProperty("doorToUnlock").objectReferenceValue = door;
                keySo.ApplyModifiedPropertiesWithoutUndo();
            }
            SetClip(key, "pickupClip", "key_pickup");

            // Morning has no Prop_BlockedStairs, but it has the same unbuilt
            // second floor, so the same stranding bug. Blocking it costs
            // nothing here: M2MorningController's opening beat is
            // PriyaScreams -> TheyComeDown, which walks Aaron and Ivy down to
            // the player, so the player never needs to climb. The descent
            // itself is unaffected — CutsceneStage.MoveActor lerps actor
            // Transforms directly rather than driving a CharacterController,
            // so cast members pass through this volume.
            AddStairBlocker();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MorningScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MemorySceneDressing] Memory_CabinMorning dressed.");
        }

        private const string SfxRoot = "Assets/_Project/Art/Audio/SFX/";

        /// <summary>Loop AudioSource, playOnAwake, on the given GameObject — clock tick, and (from MemorySceneBuilderV2) fire crackle / interior wind.</summary>
        private static void AddAmbientLoop(GameObject go, string clipName, float volume = 1f, float maxDistance = 4f)
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
            // Volume alone was not what made the clock carry: spatialBlend 1
            // still uses AudioSource's DEFAULT maxDistance of 500 m on a
            // logarithmic curve, so inside a 10 m room it is effectively at
            // full level everywhere. Linear rolloff over a few metres is what
            // actually makes it a fireplace-side detail instead of a room tone.
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 0.5f;
            source.maxDistance = maxDistance;
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

        // SM_Cabin_Stairs measured in Memory_CabinNight: x in [3.90, 5.00]
        // (1.10 m wide), rising along +Z from z = -1.43 to y = 2.72 at
        // z = 3.85. SM_Cabin_StairRailing caps at y = 2.60 on the -X side.
        private const float StairMinX = 3.90f;
        private const float StairMaxX = 5.00f;

        /// <summary>Invisible collision wall across the foot of the staircase.
        ///
        /// The stairs are freely climbable (non-convex MeshCollider, riser
        /// 0.18 against the player's stepOffset 0.28) but lead nowhere:
        /// Cabin_v2/README.md records that the second floor was never built,
        /// only "the ceiling slab and stair opening". A player who walks up
        /// steps off the top tread (y 2.72) onto the ceiling slab's upper face
        /// (y 2.90 — a 0.18 step-up, inside stepOffset), and is then stranded
        /// on a flat 10.4 x 10.4 m plate inside the roof mesh, hunting for an
        /// invisible hole to get back down. CabinFallRecovery cannot rescue
        /// them either: its minimumHeight is -4, and they are at +2.9.
        ///
        /// Prop_BlockedStairs already states the fiction ("Aaron and Ivy went
        /// up an hour ago") but cannot enforce it — it resolves to a real FBX,
        /// so its collider is that model's 0.61 x 0.62 x 0.07 board, roughly a
        /// third of the stair width, and the player simply walks around it.
        /// Widening it is not an option without re-authoring the mesh, hence a
        /// separate invisible volume.
        ///
        /// Placed just up-stair of Prop_BlockedStairs, whose own collider comes
        /// from its FBX and measures 0.61 x 0.62 x 0.07 at (3.90, 0.20, -1.20),
        /// i.e. z [-1.235, -1.165] — so the blocker sits clear of it at
        /// z [-1.125, -0.875] and never intercepts the interaction raycast to
        /// it. Overhangs the stair width by 0.15 m each side so it cannot be
        /// squeezed past. Lives under the "Gameplay" root, which
        /// MemorySceneBuilderV2 creates and reserves for exactly this.
        ///
        /// Paired with a wider StairWarning trigger further down the approach,
        /// so the player is told why before they are stopped.</summary>
        private static void AddStairBlocker()
        {
            GameObject gameplay = GameObject.Find("Gameplay") ?? new GameObject("Gameplay");
            float midX = (StairMinX + StairMaxX) * 0.5f;
            float width = StairMaxX - StairMinX + 0.30f;

            Replace(gameplay, "Blocker_Stairs");
            GameObject blocker = new GameObject("Blocker_Stairs");
            blocker.transform.SetParent(gameplay.transform, false);
            blocker.transform.position = new Vector3(midX, 1.25f, -1.00f);
            BoxCollider box = blocker.AddComponent<BoxCollider>();
            box.size = new Vector3(width, 2.50f, 0.25f);

            Replace(gameplay, "Warn_Stairs");
            GameObject warn = new GameObject("Warn_Stairs");
            warn.transform.SetParent(gameplay.transform, false);
            warn.transform.position = new Vector3(midX, 1.00f, -1.75f);
            BoxCollider trigger = warn.AddComponent<BoxCollider>();
            trigger.size = new Vector3(width + 0.2f, 2.00f, 1.00f);
            trigger.isTrigger = true;
            warn.AddComponent<StairWarning>();
        }

        private static void Replace(GameObject parent, string childName)
        {
            Transform existing = parent.transform.Find(childName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
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
            string labelText, string lookPrompt, string memoryFlag, bool glass = false) where T : Interactable
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
                ApplyMaterialRecursive(visual, color, glass);
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
                ApplyMaterialRecursive(visual, color, glass);
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

        private static void ApplyMaterialRecursive(GameObject go, Color color, bool glass = false)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            if (glass)
            {
                // Blender's Transmission/IOR glass doesn't survive FBX — the props'
                // transparency comes entirely from this material. Setting _Surface
                // alone is a common trap: URP Lit's Blend command reads _SrcBlend/
                // _DstBlend directly, not _Surface/_Blend (those are only metadata
                // the Inspector's ShaderGUI uses to populate them), so both must be
                // set explicitly or the material silently renders opaque.
                Color tint = color;
                tint.a = 0.3f;
                material.color = tint;
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f);   // Alpha
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_ZWrite", 0f);
                material.SetFloat("_Smoothness", 0.9f);
                material.SetOverrideTag("RenderType", "Transparent");
                material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.DisableKeyword("_ALPHATEST_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
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
