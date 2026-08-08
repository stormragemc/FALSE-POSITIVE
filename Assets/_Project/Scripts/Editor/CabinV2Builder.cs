using System;
using System.Collections.Generic;
using FalsePositive.Interaction;
using UnityEditor;
using UnityEngine;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Phase 1 (rough end-to-end playable pass, Aug 2026 plan): brings the
    /// second-generation cabin model into Unity for the first time. Cabin_v2's
    /// own README ("Outstanding work") explicitly says no materials/prefabs
    /// were authored yet — this is that follow-up.
    ///
    /// Axis mapping (Blender -> Unity) was NOT guessed; it was derived
    /// empirically by comparing SM_Chair_05's known Blender coordinate against
    /// its imported Unity position:
    ///   Blender (3.0, -0.85, 0.0) -> Unity (-3.0, 0.0, 0.85)
    ///   => Unity(x, y, z) = (-Bx, Bz, -By)
    /// Confirmed consistent against SM_Table, BO_Sofa, BO_CoatHanger and all
    /// four BO_Shoes. See the plan doc for the full derivation.
    ///
    /// Materials are a deliberate rough-pass shortcut: Base Color + Normal +
    /// a scalar smoothness, not the README's inverted-roughness-map bake —
    /// this is a blockout-quality pass, not a final lighting pass.
    /// </summary>
    public static class CabinV2Builder
    {
        private const string ArtRoot = "Assets/_Project/Art/Cabin_v2/";
        private const string TextureRoot = ArtRoot + "Textures/";
        private const string MaterialRoot = ArtRoot + "Materials/";
        private const string PrefabRoot = ArtRoot + "Prefabs/";
        private const string CabinFbxPath = ArtRoot + "Cabin.fbx";
        private const string DoorFbxPath = ArtRoot + "Door.fbx";

        // Real furniture swap (Aug 2026 follow-up pass): SM_Table/SM_Chair_0X
        // above are baked into Cabin.fbx at whatever scale/quality the
        // original blockout pass used; these two PolyHaven models (exported
        // via Tools/blender/export_furniture.py — see that script's own doc
        // for why the .blend sources live in ArtSource/Furniture/, not here)
        // replace them. See SwapFurnitureWithRealModels below.
        private const string FurnitureRoot = "Assets/_Project/Art/Furniture/";
        private const string TableFbxPath = FurnitureRoot + "WoodenTable_01.fbx";
        private const string StoolFbxPath = FurnitureRoot + "WoodenStool_01.fbx";

        // Hinge coordinate, Unity frame, derived per the mapping above from
        // the Blender-frame hinge coordinate (3.378769, 4.121231, 0.0) in the
        // Cabin_v2 README: Unity(x,y,z) = (-3.378769, 0.0, -4.121231).
        public static readonly Vector3 DoorHingePosition = new Vector3(-3.378769f, 0f, -4.121231f);

        // The door's CLOSED orientation as FBX-imported is NOT identity — the
        // importer bakes a (270, 0, 0) rotation converting the source's
        // Z-up axes to Unity's Y-up. Confirmed empirically (Unity_RunCommand,
        // fresh PrefabUtility.InstantiatePrefab, read transform.rotation
        // before touching it): assigning transform.rotation = Euler(0, yaw, 0)
        // directly DISCARDS this baked rotation and knocks the door onto its
        // side (collapses to ~1.1 m tall instead of 2.1 m). Any code that
        // swings this door must PRE-multiply around world Y instead:
        //   transform.rotation = Quaternion.Euler(0f, yawDegrees, 0f) * DoorClosedRotation;
        // Sign convention (also empirically confirmed via bounds center):
        // POSITIVE yawDegrees swings the leaf toward the room interior
        // (increases world x+z away from the chamfer line x+z=-7.5); negative
        // swings it outward/exterior. ~90-100 degrees is a comfortably open door.
        public static readonly Quaternion DoorClosedRotation = Quaternion.Euler(270f, 0f, 0f);
        public const float DoorOpenYawDegrees = 100f;

        private static readonly string[] WoodObjects =
        {
            "SM_Cabin_Floor", "SM_Cabin_Walls", "SM_Cabin_Ceiling", "SM_Cabin_Roof",
            "SM_Cabin_Stairs", "SM_Cabin_StairRailing", "SM_Table",
            "SM_Chair_01", "SM_Chair_02", "SM_Chair_03", "SM_Chair_04", "SM_Chair_05", "SM_Chair_06",
            "SM_Fireplace_Wood",
        };
        private static readonly string[] BrickObjects = { "SM_Fireplace_Brick" };
        private static readonly string[] StoneObjects = { "SM_Fireplace_Stone" };
        private static readonly string[] FirewoodObjects = { "SM_Fireplace_Firewood" };
        private static readonly string[] MetalObjects =
        {
            "BO_WindowGrille", "BO_CoatHanger",
            "BO_Shoes_01", "BO_Shoes_02", "BO_Shoes_03", "BO_Shoes_04",
        };
        private static readonly string[] BlockoutObjects = { "BO_Sofa" };

        // MeshCollider for large static structural pieces, BoxCollider
        // (cheaper, and fine for convex-enough furniture) for everything else.
        // The firewood pile is a single joined, non-convex mesh (13 jumbled
        // logs) sitting on the hearth with no Rigidbody, so a concave
        // MeshCollider is valid here (Unity only requires "Convex" for
        // MeshColliders paired with a non-kinematic Rigidbody).
        private static readonly string[] MeshColliderObjects =
        {
            "SM_Cabin_Floor", "SM_Cabin_Walls", "SM_Cabin_Ceiling",
            "SM_Cabin_Stairs", "SM_Cabin_StairRailing",
            "SM_Fireplace_Brick", "SM_Fireplace_Stone", "SM_Fireplace_Wood", "SM_Fireplace_Firewood",
        };
        private static readonly string[] BoxColliderObjects =
        {
            "SM_Table", "SM_Chair_01", "SM_Chair_02", "SM_Chair_03", "SM_Chair_04", "SM_Chair_05", "SM_Chair_06",
            "BO_Sofa", "BO_WindowGrille", "BO_CoatHanger",
        };

        [MenuItem("Tools/False Positive/Bootstrap/0 - Build Cabin_v2 Materials & Prefabs")]
        public static void BuildAll()
        {
            SetupNormalMapImportSettings();
            BuildMaterials();
            BuildCabinPrefab();
            BuildDoorPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[CabinV2Builder] Cabin_v2 materials and prefabs built.");
        }

        private static void SetupNormalMapImportSettings()
        {
            string[] paths =
            {
                TextureRoot + "Cabin_Normal.png", TextureRoot + "Brick_Normal.png",
                TextureRoot + "Metal_Normal.png", TextureRoot + "Stone_Normal.png",
                TextureRoot + "Firewood_Normal.png",
                // Furniture nor_gl maps landed next to the exported FBX (FBX
                // export's path_mode=COPY dropped them in a *.fbm folder) —
                // full paths here, not TextureRoot-relative like the rest.
                FurnitureRoot + "WoodenTable_01.fbm/WoodenTable_01_nor_gl_4k.exr",
                FurnitureRoot + "WoodenStool_01.fbm/wooden_stool_01_nor_gl_4k.exr",
            };
            SetupNormalMapImportSettings(paths);
        }

        private static void SetupNormalMapImportSettings(string[] paths)
        {
            foreach (string path in paths)
            {
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Debug.LogWarning($"[CabinV2Builder] No texture importer at {path} — skipping import settings.");
                    continue;
                }

                importer.textureType = TextureImporterType.NormalMap;
                importer.maxTextureSize = 2048;
                importer.SaveAndReimport();
            }
        }

        private static void BuildMaterials()
        {
            System.IO.Directory.CreateDirectory(MaterialRoot);

            CreateTexturedMaterial("M_Wood_WeatheredPlank",
                TextureRoot + "weathered_plank_siding_diff_4k.jpg",
                TextureRoot + "Cabin_Normal.png", smoothness: 0.25f);

            CreateTexturedMaterial("M_Brick_Red",
                TextureRoot + "red_brick_diff_4k.jpg",
                TextureRoot + "Brick_Normal.png", smoothness: 0.15f);

            CreateTexturedMaterial("M_Metal_BluePlate",
                TextureRoot + "blue_metal_plate_diff_4k.jpg",
                TextureRoot + "Metal_Normal.png", smoothness: 0.55f);

            CreateTexturedMaterial("M_Stone_CastleWall",
                TextureRoot + "castle_wall_slates_diff_4k.jpg",
                TextureRoot + "Stone_Normal.png", smoothness: 0.2f);

            CreateTexturedMaterial("M_Bark_Brown",
                TextureRoot + "bark_brown_02_diff_4k.jpg",
                TextureRoot + "Firewood_Normal.png", smoothness: 0.1f);

            CreateFlatMaterial("M_Blockout_Grey", new Color(0.55f, 0.55f, 0.55f), smoothness: 0.2f);

            // Real furniture materials (see SwapFurnitureWithRealModels).
            // Diffuse + normal + a scalar smoothness only, same "blockout-
            // quality, not a final lighting pass" shortcut as every material
            // above (class doc) — these packs also ship separate roughness/
            // metallic maps, deliberately not sampled here for the same
            // reason the wood/brick/stone maps above aren't either.
            CreateTexturedMaterial("M_Wood_Table",
                FurnitureRoot + "WoodenTable_01.fbm/WoodenTable_01_diff_4k.jpg",
                FurnitureRoot + "WoodenTable_01.fbm/WoodenTable_01_nor_gl_4k.exr", smoothness: 0.35f);

            CreateTexturedMaterial("M_Wood_Stool",
                FurnitureRoot + "WoodenStool_01.fbm/wooden_stool_01_diff_4k.jpg",
                FurnitureRoot + "WoodenStool_01.fbm/wooden_stool_01_nor_gl_4k.exr", smoothness: 0.3f);
        }

        private static void CreateTexturedMaterial(string name, string diffusePath, string normalPath, float smoothness)
        {
            string assetPath = MaterialRoot + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (diffuse == null) Debug.LogWarning($"[CabinV2Builder] Missing diffuse texture {diffusePath} for {name}.");
            if (normal == null) Debug.LogWarning($"[CabinV2Builder] Missing normal texture {normalPath} for {name}.");

            material.SetTexture("_BaseMap", diffuse);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            // UI.InteractionPromptUI highlights the looked-at Interactable via
            // a per-renderer MaterialPropertyBlock override of _EmissionColor
            // — that can only change the VALUE of an already-active shader
            // keyword, not enable it, so _EMISSION must be on here (Door_v2
            // and the window grille InspectPoint both share these materials
            // with plain, non-interactable shell geometry — MaterialPropertyBlock
            // is per-renderer, so this doesn't make the walls glow too).
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
        }

        private static void CreateFlatMaterial(string name, Color color, float smoothness)
        {
            string assetPath = MaterialRoot + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
        }

        private static void BuildCabinPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(CabinFbxPath);
            if (source == null)
            {
                Debug.LogError($"[CabinV2Builder] {CabinFbxPath} not found.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "Cabin_v2";

            Material wood = LoadMaterial("M_Wood_WeatheredPlank");
            Material brick = LoadMaterial("M_Brick_Red");
            Material metal = LoadMaterial("M_Metal_BluePlate");
            Material stone = LoadMaterial("M_Stone_CastleWall");
            Material bark = LoadMaterial("M_Bark_Brown");
            Material blockout = LoadMaterial("M_Blockout_Grey");

            AssignMaterial(instance, WoodObjects, wood);
            AssignMaterial(instance, BrickObjects, brick);
            AssignMaterial(instance, MetalObjects, metal);
            AssignMaterial(instance, StoneObjects, stone);
            AssignMaterial(instance, FirewoodObjects, bark);
            AssignMaterial(instance, BlockoutObjects, blockout);

            AddColliders(instance, MeshColliderObjects, useMeshCollider: true);
            AddColliders(instance, BoxColliderObjects, useMeshCollider: false);
            OrientSofaToFireplace(instance);

            // Runs AFTER OrientSofaToFireplace so the sofa's yaw is already on
            // the instance when the prefab is saved. The swap SetActive(false)s
            // SM_Table/SM_Chair_01..06 but leaves them in place at their
            // authored positions — CutsceneStage.SeatedAtChair still reads those
            // transforms for seating, so it must look them up in a way that
            // sees inactive objects (see the note on SeatedAtChair).
            SwapFurnitureWithRealModels(instance);

            System.IO.Directory.CreateDirectory(PrefabRoot);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabRoot + "Cabin_v2.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        /// <summary>Yaw that turns BO_Sofa's open face toward the fire.
        ///
        /// The sofa imports axis-aligned at yaw 0, which LOOKS like it faces
        /// forward but does not: its seating face is local -X, not +Z — its
        /// collider is 1.0 deep in X by 3.5 long in Z, so the long axis is the
        /// backrest. At yaw 0 the seat therefore opens due west while
        /// BO_Fireplace sits at (0, 0, 4.3), leaving it ~79.5 degrees off.
        ///
        /// Derived, not eyeballed: rotating the seat normal (-1, 0, 0) by yaw
        /// t gives (-cos t, 0, sin t); matching that to the normalised sofa ->
        /// fireplace vector (-0.182, 0, 0.983) gives t = 79.5.
        ///
        /// Cutscene.CutsceneStage stores its sofa rest spots as sofa-LOCAL
        /// offsets (see SofaPoint there) precisely so this yaw can change
        /// without stranding actors inside the furniture.</summary>
        public const float SofaYaw = 79.5f;

        private static void OrientSofaToFireplace(GameObject cabin)
        {
            Transform sofa = cabin.transform.Find("BO_Sofa");
            if (sofa == null)
            {
                Debug.LogWarning("[CabinV2Builder] BO_Sofa not found — sofa orientation skipped.");
                return;
            }

            // Pre-multiplied, NOT assigned: BO_Sofa comes off the FBX carrying
            // the Blender Z-up import rotation (270 about X, as every SM_/BO_
            // node here does). Assigning a pure yaw would discard it and tip
            // the sofa onto its back. This composes a world-Y turn on top of
            // whatever the import gave us. Safe to run repeatedly only because
            // BuildCabinPrefab always starts from a fresh FBX instance.
            sofa.localRotation = Quaternion.Euler(0f, SofaYaw, 0f) * sofa.localRotation;
        }

        // Real-world target heights for the furniture swap below — matches
        // SM_Table's own already-established top height (0.75 m, also the
        // number MemorySceneDressing.cs's Prop_FiveCups/Prop_Bottles seat
        // against) for the table, and a standard stool/counter-seat height
        // for the chairs. The imported models' OWN raw height is measured
        // live (not assumed) and scaled to hit these.
        private const float TableTargetHeight = 0.75f;
        private const float ChairTargetHeight = 0.45f;

        /// <summary>Disables SM_Table/SM_Chair_01..06 (the blockout-quality
        /// furniture baked into Cabin.fbx) and replaces each with a real
        /// PolyHaven model (WoodenTable_01.fbx / WoodenStool_01.fbx — the
        /// stool has no backrest, a deliberate asset swap the user confirmed,
        /// not a chair-with-back replacement) at the SAME captured world
        /// position/rotation, scaled to a real-world target height. Disabling
        /// rather than destroying the originals is non-destructive — same
        /// convention as every other superseded-asset swap in this project
        /// (e.g. the old T1 cop model): the README's authored numbers stay
        /// on disk and recoverable, just unused.
        ///
        /// Runs on the in-memory `instance` before SaveAsPrefabAsset, so the
        /// swap is baked into Cabin_v2.prefab itself and survives every
        /// re-run of Bootstrap step 0 without any extra wiring elsewhere.</summary>
        private static void SwapFurnitureWithRealModels(GameObject instance)
        {
            GameObject tableSource = AssetDatabase.LoadAssetAtPath<GameObject>(TableFbxPath);
            GameObject stoolSource = AssetDatabase.LoadAssetAtPath<GameObject>(StoolFbxPath);
            Material tableMaterial = LoadMaterial("M_Wood_Table");
            Material stoolMaterial = LoadMaterial("M_Wood_Stool");

            if (tableSource == null || stoolSource == null)
            {
                Debug.LogWarning("[CabinV2Builder] Furniture FBX missing (run Tools/blender/export_furniture.py first) — skipping furniture swap.");
                return;
            }

            Transform tableTransform = instance.transform.Find("SM_Table");
            if (tableTransform == null)
            {
                Debug.LogWarning("[CabinV2Builder] SM_Table not found on Cabin_v2 — skipping furniture swap.");
                return;
            }
            Vector3 tableCenter = tableTransform.position;

            // WoodenTable_01's raw export has its long axis along X (fresh-
            // instance bounds measured 1.80 x 0.55 x 0.66) but the room's
            // table runs long-axis along Z (SM_Table's own world footprint
            // is 1.1 wide x 2.2 long, chairs sit at z=1.6/3.0 either side) —
            // a 90 degree yaw aligns them. Verified against the rebuilt
            // collider's footprint, not assumed.
            SwapOneFurnitureObject(instance, "SM_Table", "Prop_Table", tableSource, tableMaterial,
                TableTargetHeight, Quaternion.Euler(0f, 90f, 0f));

            for (int i = 1; i <= 6; i++)
            {
                string oldName = $"SM_Chair_0{i}";
                string newName = $"Prop_Chair_0{i}";
                Transform chairTransform = instance.transform.Find(oldName);
                if (chairTransform == null)
                {
                    Debug.LogWarning($"[CabinV2Builder] {oldName} not found on Cabin_v2 — skipping furniture swap for it.");
                    continue;
                }

                // Face the table centre — matches the README's stated intent
                // for these chairs ("facing inward"). A round stool has no
                // strong visual front/back, so exact yaw doesn't need to
                // preserve the original's own ±3 degree jitter.
                Vector3 towardTable = Vector3.ProjectOnPlane(tableCenter - chairTransform.position, Vector3.up);
                Quaternion facing = towardTable.sqrMagnitude > 0.0001f
                    ? Quaternion.LookRotation(towardTable.normalized, Vector3.up)
                    : Quaternion.identity;

                SwapOneFurnitureObject(instance, oldName, newName, stoolSource, stoolMaterial,
                    ChairTargetHeight, facing);
            }
        }

        private static void SwapOneFurnitureObject(GameObject instance, string oldName, string newName,
            GameObject modelSource, Material material, float targetHeight, Quaternion rotation)
        {
            Transform old = instance.transform.Find(oldName);
            if (old == null)
            {
                Debug.LogWarning($"[CabinV2Builder] {oldName} not found on Cabin_v2 — skipping furniture swap for it.");
                return;
            }

            // X/Z come from the object being replaced so the authored floor
            // layout is preserved exactly; Y is pinned to the floor instead of
            // copied. The two originals disagree about where their own origin
            // sits — SM_Chair_0X's is at its base (y 0), SM_Table's is at its
            // mid-height (y 0.725) — while both PolyHaven models are
            // base-origin. Copying old.position.y verbatim therefore seated the
            // stools correctly but hung the table 0.725 in the air, putting its
            // top at 1.475 instead of the 0.75 that TableTargetHeight and
            // MemorySceneDressing's Prop_FiveCups/Prop_Bottles both assume.
            // The cabin floor is y 0 (every SM_Chair_0X sits at exactly 0).
            Vector3 position = new Vector3(old.position.x, 0f, old.position.z);
            // `rotation` is passed in explicitly rather than derived from
            // `old.rotation` — every object baked into Cabin.fbx carries a
            // (270, 0, 0)-class rotation (Unity's importer compensating for
            // that FBX not baking its own axis conversion; see
            // BuildDoorPrefab's DoorClosedRotation doc for the same fact on
            // this exact FBX), under which `old`'s OWN local up/forward axes
            // do not point where they intuitively should (confirmed live:
            // SM_Table.up read as world (0,0,-1), not (0,1,0)) — there is no
            // reliable way to recover "the horizontal facing direction" from
            // it generically. `rotation` therefore goes on the `replacement`
            // parent, and the model keeps its own imported transform below it.
            old.gameObject.SetActive(false);

            GameObject replacement = new GameObject(newName);
            replacement.transform.SetParent(instance.transform, false);
            replacement.transform.SetPositionAndRotation(position, rotation);

            // The visual's OWN localRotation/localScale are preserved, not
            // overwritten. export_furniture.py exports with axis_forward="-Z"/
            // axis_up="Y" but deliberately WITHOUT bake_space_transform, and
            // WoodenTable_01.fbx.meta/WoodenStool_01.fbx.meta both carry
            // bakeAxisConversion: 0 — so neither Blender nor the importer folds
            // the Z-up -> Y-up conversion into the vertices. It survives as a
            // -90-degree X rotation on the imported root instead. Assigning
            // Quaternion.identity here discarded it and laid every table and
            // stool on its face; the giveaway was Prop_Table measuring
            // 0.63 x 0.75 x 2.05 (the model's 0.66 DEPTH scaled up to hit the
            // 0.75 height target) instead of the upright 0.90 x 0.75 x 2.45.
            // Same trap OrientSofaToFireplace documents for BO_Sofa: compose
            // onto the import rotation, never replace it.
            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelSource, replacement.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            Vector3 importedScale = visual.transform.localScale;

            // Measured with the imported rotation and scale in place, so the
            // height read here is the model's real upright height and the
            // correction multiplies the imported scale rather than replacing it.
            Bounds rawBounds = ComputeWorldRelativeLocalBounds(visual, replacement.transform);
            float rawHeight = rawBounds.size.y;
            if (rawHeight > 0.001f)
            {
                visual.transform.localScale = importedScale * (targetHeight / rawHeight);
            }
            else
            {
                Debug.LogWarning($"[CabinV2Builder] {newName}'s model has near-zero measured height — leaving scale at 1.");
            }

            if (material != null)
            {
                foreach (Renderer renderer in visual.GetComponentsInChildren<Renderer>())
                {
                    Material[] shared = new Material[renderer.sharedMaterials.Length == 0 ? 1 : renderer.sharedMaterials.Length];
                    for (int i = 0; i < shared.Length; i++) shared[i] = material;
                    renderer.sharedMaterials = shared;
                }
            }

            Bounds scaledLocalBounds = ComputeWorldRelativeLocalBounds(visual, replacement.transform);
            BoxCollider collider = replacement.AddComponent<BoxCollider>();
            collider.center = scaledLocalBounds.center;
            collider.size = Vector3.Max(scaledLocalBounds.size, new Vector3(0.05f, 0.05f, 0.05f));
        }

        /// <summary>World-space-renderer-bounds-based local bounds, scale-safe
        /// (correctly accounts for a non-1 `visual.transform.localScale`) —
        /// unlike this file's own ComputeLocalBounds(Transform) below, which
        /// reads raw UNSCALED mesh bounds and is only valid when the caller
        /// assigns the result directly to a BoxCollider on an object whose
        /// own scale will apply it once (see that method's doc for the
        /// double-scaling bug this caused before). The furniture swap above
        /// introduces a real, deliberate non-1 scale on `visual`, so it needs
        /// this version instead — same technique
        /// Editor.MemorySceneDressing.ComputeLocalBounds already uses for
        /// exactly this reason.</summary>
        private static Bounds ComputeWorldRelativeLocalBounds(GameObject visual, Transform relativeTo)
        {
            Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(Vector3.zero, Vector3.one * 0.1f);

            Bounds worldBounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) worldBounds.Encapsulate(renderers[i].bounds);

            Vector3 localCenter = relativeTo.InverseTransformPoint(worldBounds.center);
            Vector3 localSize = relativeTo.InverseTransformVector(worldBounds.size);
            return new Bounds(localCenter, new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z)));
        }

        private static void BuildDoorPrefab()
        {
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(DoorFbxPath);
            if (source == null)
            {
                Debug.LogError($"[CabinV2Builder] {DoorFbxPath} not found.");
                return;
            }

            // The door's local origin IS the hinge pivot (see the README's
            // "Door hinge" section) — no separate pivot object is needed;
            // rotating this root swings it on the hinge directly.
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            instance.name = "Door_v2";

            Renderer renderer = instance.GetComponentInChildren<Renderer>();
            if (renderer != null) renderer.sharedMaterial = LoadMaterial("M_Wood_WeatheredPlank");

            BoxCollider collider = instance.AddComponent<BoxCollider>();
            Bounds local = ComputeLocalBounds(instance.transform);
            collider.center = local.center;
            collider.size = local.size;

            instance.AddComponent<DoorInteractable>();

            System.IO.Directory.CreateDirectory(PrefabRoot);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabRoot + "Door_v2.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        private static Material LoadMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + name + ".mat");
        }

        private static void AssignMaterial(GameObject root, IEnumerable<string> objectNames, Material material)
        {
            if (material == null) return;
            foreach (string objectName in objectNames)
            {
                Transform t = root.transform.Find(objectName);
                if (t == null)
                {
                    Debug.LogWarning($"[CabinV2Builder] {objectName} not found on Cabin_v2 for material assignment.");
                    continue;
                }
                Renderer renderer = t.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }
        }

        private static void AddColliders(GameObject root, IEnumerable<string> objectNames, bool useMeshCollider)
        {
            foreach (string objectName in objectNames)
            {
                Transform t = root.transform.Find(objectName);
                if (t == null)
                {
                    Debug.LogWarning($"[CabinV2Builder] {objectName} not found on Cabin_v2 for collider assignment.");
                    continue;
                }

                if (useMeshCollider)
                {
                    MeshFilter mf = t.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    MeshCollider mc = t.gameObject.GetComponent<MeshCollider>();
                    if (mc == null) mc = t.gameObject.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                }
                else
                {
                    BoxCollider bc = t.gameObject.GetComponent<BoxCollider>();
                    if (bc == null) bc = t.gameObject.AddComponent<BoxCollider>();
                    Bounds local = ComputeLocalBounds(t);
                    bc.center = local.center;
                    bc.size = local.size;
                }
            }
        }

        /// <summary>
        /// A BoxCollider's center/size are ALREADY local-space fields, and
        /// Unity automatically multiplies them by the GameObject's own
        /// transform.lossyScale to get the real world collision volume — so
        /// the right source is the mesh's raw local bounds
        /// (MeshFilter.sharedMesh.bounds), UNSCALED. Two bugs lived here
        /// before this was caught by an in-Play-mode Physics.OverlapSphere
        /// check (a local-field comparison against renderer.bounds looked
        /// "correct" by coincidence and missed both):
        ///
        /// 1. The original version inverse-transformed the renderer's WORLD
        ///    AABB size through each object's baked (270, 0, 0) import
        ///    rotation (Blender Z-up -> Unity Y-up) — invalid for a rotated
        ///    object, and collapsed every collider near zero.
        /// 2. The fix for #1 pre-multiplied meshBounds by target.localScale
        ///    (every object here carries localScale=100, compensating the
        ///    source mesh's small units) BEFORE assigning to collider.size —
        ///    but Unity applies that same localScale AGAIN automatically,
        ///    so the real-world collider ballooned to ~100x too large
        ///    (confirmed: SM_Chair_05's collider measured ~95 units tall via
        ///    Collider.bounds in Play mode, and the player's CharacterController
        ///    spawned wedged inside it and got shoved up to y=3.3).
        ///
        /// Assumes the mesh sits directly on `target` (true for every call
        /// site here — Cabin.fbx's per-object children and Door.fbx's root).
        /// </summary>
        private static Bounds ComputeLocalBounds(Transform target)
        {
            MeshFilter mf = target.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            return mf.sharedMesh.bounds;
        }
    }
}
