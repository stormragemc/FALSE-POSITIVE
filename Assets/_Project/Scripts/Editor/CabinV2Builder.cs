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
        };
        private static readonly string[] BrickObjects = { "BO_Fireplace" };
        private static readonly string[] MetalObjects =
        {
            "BO_WindowGrille", "BO_CoatHanger",
            "BO_Shoes_01", "BO_Shoes_02", "BO_Shoes_03", "BO_Shoes_04",
        };
        private static readonly string[] BlockoutObjects = { "BO_Sofa" };

        // MeshCollider for large static structural pieces, BoxCollider
        // (cheaper, and fine for convex-enough furniture) for everything else.
        private static readonly string[] MeshColliderObjects =
        {
            "SM_Cabin_Floor", "SM_Cabin_Walls", "SM_Cabin_Ceiling",
            "SM_Cabin_Stairs", "SM_Cabin_StairRailing", "BO_Fireplace",
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
            string[] normalMaps = { "Cabin_Normal.png", "Brick_Normal.png", "Metal_Normal.png" };
            foreach (string fileName in normalMaps)
            {
                string path = TextureRoot + fileName;
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

            CreateFlatMaterial("M_Blockout_Grey", new Color(0.55f, 0.55f, 0.55f), smoothness: 0.2f);
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
            Material blockout = LoadMaterial("M_Blockout_Grey");

            AssignMaterial(instance, WoodObjects, wood);
            AssignMaterial(instance, BrickObjects, brick);
            AssignMaterial(instance, MetalObjects, metal);
            AssignMaterial(instance, BlockoutObjects, blockout);

            AddColliders(instance, MeshColliderObjects, useMeshCollider: true);
            AddColliders(instance, BoxColliderObjects, useMeshCollider: false);

            System.IO.Directory.CreateDirectory(PrefabRoot);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabRoot + "Cabin_v2.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
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
        /// A BoxCollider's center/size live in the GameObject's own LOCAL
        /// space and rotate automatically with its transform — so the right
        /// source is the mesh's own local bounds (MeshFilter.sharedMesh.bounds)
        /// scaled by localScale, NOT an inverse-transform of the renderer's
        /// WORLD-space AABB. Every object here (Cabin.fbx's children and
        /// Door.fbx's root alike) carries a baked (270, 0, 0) import rotation
        /// converting the source's Z-up to Unity's Y-up, plus a 100x
        /// localScale compensating the source's unit scale (confirmed via
        /// Unity_RunCommand: mesh.bounds reads ~0.008 units, times 100 = the
        /// real ~0.8 m footprint). Inverse-transforming a world AABB size
        /// through that 270 degree rotation mixes axes nonsensically and
        /// collapses the result near zero — this collapsed every BoxCollider
        /// this builder made until caught by an empirical bounds check.
        /// Assumes the mesh sits directly on `target` (true for every call
        /// site here — Cabin.fbx's per-object children and Door.fbx's root).
        /// </summary>
        private static Bounds ComputeLocalBounds(Transform target)
        {
            MeshFilter mf = target.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return new Bounds(Vector3.zero, Vector3.one * 0.1f);
            Bounds meshBounds = mf.sharedMesh.bounds;
            return new Bounds(
                Vector3.Scale(meshBounds.center, target.localScale),
                Vector3.Scale(meshBounds.size, target.localScale));
        }
    }
}
