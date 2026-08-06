using System;
using System.Linq;
using System.Collections.Generic;
using FalsePositive.CabinNight;
using FalsePositive.Cutscene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Phase 2 (rough end-to-end playable pass): replaces MemorySceneBuilder's
    /// "duplicate the teaser scene" approach, which is exactly the dependency
    /// this plan cuts. Generates Memory_CabinNight.unity and
    /// Memory_CabinMorning.unity from scratch on the Cabin_v2 shell built by
    /// CabinV2Builder, in the same root layout the old scenes used
    /// (Environment / Interior / Lighting / Atmosphere / Characters /
    /// Gameplay / StoryProps / Sequencing) so downstream code (MemoryScene-
    /// Dressing, MemorySceneWiring, M1NightController, M2MorningController)
    /// keeps working unmodified except for updated coordinates.
    ///
    /// Environment/Lighting/Atmosphere dressing is read from real ASSETS
    /// (Tree.prefab, CabinNightTerrain.asset, CabinNightVolume.asset), not
    /// copied live from the teaser scene — the position/rotation/scale
    /// numbers below were extracted from the teaser scene ONCE via
    /// Unity_RunCommand during planning and are now plain constants, so this
    /// builder has no runtime dependency on NobodyWentOut_CabinNight.unity
    /// and Phase 6 can delete it safely.
    /// </summary>
    public static class MemorySceneBuilderV2
    {
        private const string NightScenePath = "Assets/_Project/Scenes/Memory_CabinNight.unity";
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";
        private const string CabinPrefabPath = "Assets/_Project/Art/Cabin_v2/Prefabs/Cabin_v2.prefab";
        private const string DoorPrefabPath = "Assets/_Project/Art/Cabin_v2/Prefabs/Door_v2.prefab";
        private const string TreePrefabPath = "Assets/Cabin/Terrain/Tree/Tree.prefab";
        private const string GaragePrefabPath = "Assets/TDG Storage Solutions/Prefabs/Garage.prefab";
        private const string TerrainDataPath = "Assets/_Project/CabinNight/Data/CabinNightTerrain.asset";
        private const string NightGradeVolumePath = "Assets/_Project/CabinNight/Data/CabinNightVolume.asset";

        // Extracted once from NobodyWentOut_CabinNight.unity — see class doc.
        private static readonly (Vector3 pos, float yaw, float scale)[] Pines =
        {
            (new Vector3(-12f, -0.281f, -8f), 0f, 0.72f),
            (new Vector3(-16f, -0.238f, 2f), 47f, 0.795f),
            (new Vector3(-10f, -0.303f, 12f), 94f, 0.87f),
            (new Vector3(-2f, -0.294f, 16f), 141f, 0.945f),
            (new Vector3(7f, -0.249f, 15f), 188f, 1.02f),
            (new Vector3(14f, -0.261f, 11f), 235f, 0.72f),
            (new Vector3(18f, -0.218f, 1f), 282f, 0.795f),
            (new Vector3(15f, -0.259f, -10f), 329f, 0.87f),
            (new Vector3(8f, -0.243f, -15f), 16f, 0.945f),
            (new Vector3(-3f, -0.3f, -17f), 63f, 1.02f),
            (new Vector3(-18f, -0.281f, -12f), 110f, 0.72f),
            (new Vector3(23f, -0.289f, 7f), 157f, 0.795f),
            (new Vector3(-23f, -0.188f, 8f), 204f, 0.87f),
            (new Vector3(22f, -0.34f, -16f), 251f, 0.945f),
        };

        [MenuItem("Tools/False Positive/Bootstrap/T04 - Build Memory Scenes (Cabin_v2)")]
        public static void BuildBoth()
        {
            BuildScene(NightScenePath, isMorning: false);
            BuildScene(MorningScenePath, isMorning: true);
            Debug.Log("[MemorySceneBuilderV2] Both memory scenes rebuilt on Cabin_v2.");
        }

        [MenuItem("Tools/False Positive/Bootstrap/T04a - Build Memory_CabinNight (Cabin_v2)")]
        public static void BuildNight() => BuildScene(NightScenePath, isMorning: false);

        [MenuItem("Tools/False Positive/Bootstrap/T04b - Build Memory_CabinMorning (Cabin_v2)")]
        public static void BuildMorning() => BuildScene(MorningScenePath, isMorning: true);

        private static void BuildScene(string path, bool isMorning)
        {
            Scene scene = OpenOrCreateEmptyScene(path);

            Transform environment = NewChild(null, "Environment").transform;
            Transform interior = NewChild(null, "Interior").transform;
            Transform lighting = NewChild(null, "Lighting").transform;
            Transform atmosphere = NewChild(null, "Atmosphere").transform;
            Transform characters = NewChild(null, "Characters").transform;
            NewChild(null, "Gameplay"); // reserved — Cabin_v2's own colliders (Phase 1) cover collision
            NewChild(null, "StoryProps"); // populated by MemorySceneDressing next step
            GameObject sequencing = NewChild(null, "Sequencing"); // M1NightController/M2MorningController populate this further via MemorySceneWiring

            BuildEnvironment(environment);
            GameObject door = BuildInterior(interior, isMorning);
            BuildLighting(lighting, isMorning);
            BuildAtmosphere(atmosphere);
            CabinNightCharacterBuilder.BuildCastInScene(characters, isMorning);

            if (door != null)
            {
                door.name = "Prop_FrontDoor_Locked"; // MemorySceneDressing/Wiring find it by this name
            }

            // CutsceneStage (Phase 4) — procedural staging for the beats that
            // need more than CutsceneDirector's default fade+VO.
            CutsceneStage stage = sequencing.AddComponent<CutsceneStage>();
            stage.Configure(isMorning);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[MemorySceneBuilderV2] {path} shell/lighting/characters built.");
        }

        private static void BuildEnvironment(Transform root)
        {
            GameObject cabinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CabinPrefabPath);
            if (cabinPrefab == null)
            {
                Debug.LogError($"[MemorySceneBuilderV2] {CabinPrefabPath} missing — run Bootstrap step 0 first.");
            }
            else
            {
                GameObject cabin = (GameObject)PrefabUtility.InstantiatePrefab(cabinPrefab, root);
                cabin.name = "Cabin_v2";
            }

            TerrainData terrainData = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (terrainData != null)
            {
                GameObject terrainGo = Terrain.CreateTerrainGameObject(terrainData);
                terrainGo.name = "Snow Terrain";
                terrainGo.transform.SetParent(root, false);
                terrainGo.transform.position = new Vector3(-40f, -0.5f, -40f);
            }
            else
            {
                Debug.LogWarning($"[MemorySceneBuilderV2] {TerrainDataPath} missing — no exterior terrain.");
            }

            GameObject treePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(TreePrefabPath);
            if (treePrefab != null)
            {
                GameObject treesRoot = NewChild(root, "Trees");
                for (int i = 0; i < Pines.Length; i++)
                {
                    GameObject tree = (GameObject)PrefabUtility.InstantiatePrefab(treePrefab, treesRoot.transform);
                    tree.name = $"Pine_{i + 1:00}";
                    tree.transform.position = Pines[i].pos;
                    tree.transform.rotation = Quaternion.Euler(0f, Pines[i].yaw, 0f);
                    tree.transform.localScale = Vector3.one * Pines[i].scale;
                }
            }
            else
            {
                Debug.LogWarning($"[MemorySceneBuilderV2] {TreePrefabPath} missing — no treeline.");
            }

            GameObject garagePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GaragePrefabPath);
            if (garagePrefab != null)
            {
                GameObject woodshed = (GameObject)PrefabUtility.InstantiatePrefab(garagePrefab, root);
                woodshed.name = "Woodshed";
                woodshed.transform.position = new Vector3(15f, -0.239f, 6f);
                woodshed.transform.rotation = Quaternion.Euler(0f, 22f, 0f);
                woodshed.transform.localScale = new Vector3(0.38f, 0.45f, 0.38f);

                GameObject cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cap.name = "Woodshed Snow Cap";
                UnityEngine.Object.DestroyImmediate(cap.GetComponent<Collider>());
                cap.AddComponent<BoxCollider>();
                cap.transform.SetParent(root, false);
                cap.transform.position = new Vector3(15f, 1.961f, 6f);
                cap.transform.rotation = Quaternion.Euler(0f, 22f, 0f);
                cap.transform.localScale = new Vector3(4.2f, 0.12f, 3.15f);
                ApplyFlatColor(cap, new Color(0.92f, 0.94f, 0.97f));
            }
            else
            {
                Debug.LogWarning($"[MemorySceneBuilderV2] {GaragePrefabPath} missing — no woodshed.");
            }
        }

        private static GameObject BuildInterior(Transform root, bool isMorning)
        {
            GameObject doorPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(DoorPrefabPath);
            GameObject door = null;
            if (doorPrefab != null)
            {
                door = (GameObject)PrefabUtility.InstantiatePrefab(doorPrefab, root);
                door.transform.position = CabinV2Builder.DoorHingePosition;
                door.transform.rotation = CabinV2Builder.DoorClosedRotation;
            }
            else
            {
                Debug.LogError($"[MemorySceneBuilderV2] {DoorPrefabPath} missing — run Bootstrap step 0 first.");
            }

            // Fire VFX/light on Cabin_v2's own BO_Fireplace — the shell
            // provides the mesh, not the flicker; reuses CabinFireFlicker
            // exactly as the teaser scene did.
            GameObject cabin = GameObject.Find("Cabin_v2");
            Transform fireplace = cabin != null ? cabin.transform.Find("BO_Fireplace") : null;
            if (fireplace != null)
            {
                GameObject fireLightGo = NewChild(fireplace, "Firelight");
                fireLightGo.transform.localPosition = new Vector3(0f, 0.4f, -0.3f);
                Light fireLight = fireLightGo.AddComponent<Light>();
                fireLight.type = LightType.Point;
                fireLight.color = new Color(1f, 0.55f, 0.22f);
                fireLight.intensity = 3.2f;
                fireLight.range = 6f;

                CabinFireFlicker flicker = fireplace.gameObject.GetComponent<CabinFireFlicker>();
                if (flicker == null) flicker = fireplace.gameObject.AddComponent<CabinFireFlicker>();
                flicker.Configure(new[] { fireLight });

                AddLoopingAudio(fireplace.gameObject, "fire_crackle_loop", volume: 0.6f);
            }

            AddLoopingAudio(root.gameObject, "interior_wind_loop", volume: 0.25f, spatial: false);

            return door;
        }

        private const string SfxRoot = "Assets/_Project/Art/Audio/SFX/";

        /// <summary>Loop AudioSource, playOnAwake — fire crackle (3D, from the
        /// fireplace) and interior wind (2D bed, heard evenly through the
        /// whole cabin — spatializing it would make it drop out mid-room).</summary>
        private static void AddLoopingAudio(GameObject go, string clipName, float volume, bool spatial = true)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + clipName + ".mp3");
            if (clip == null)
            {
                Debug.LogWarning($"[MemorySceneBuilderV2] {SfxRoot}{clipName}.mp3 not found — skipping ambient loop on {go.name}.");
                return;
            }
            AudioSource source = go.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = true;
            source.volume = volume;
            source.spatialBlend = spatial ? 1f : 0f;
            go.AddComponent<FalsePositive.Audio.LoopOnEnable>();
        }

        private static void BuildLighting(Transform root, bool isMorning)
        {
            GameObject moon = NewChild(root, isMorning ? "Grey Morning Light" : "Cold Moonlight");
            Light moonLight = moon.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.shadows = LightShadows.Soft;
            if (isMorning)
            {
                moon.transform.rotation = Quaternion.Euler(35f, 300f, 0f);
                moonLight.color = new Color(0.78f, 0.8f, 0.85f);
                moonLight.intensity = 1.1f;
            }
            else
            {
                moon.transform.rotation = Quaternion.Euler(38f, 328f, 0f);
                moonLight.color = new Color(0.48f, 0.6f, 1f);
                moonLight.intensity = 0.52f;
            }

            // Volume/VolumeProfile live in the render-pipeline core assembly,
            // which this asmdef deliberately doesn't reference (same reason
            // ProjectBootstrapBuilder.cs reaches UniversalAdditionalCameraData
            // via reflection instead of a hard reference — HDRP is also
            // installed per Packages/manifest.json).
            Type profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime");
            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            UnityEngine.Object profile = profileType != null
                ? AssetDatabase.LoadAssetAtPath(NightGradeVolumePath, profileType)
                : null;
            if (profile != null && volumeType != null)
            {
                GameObject gradeGo = NewChild(root, isMorning ? "Cabin Morning Grade" : "Cabin Night Grade");
                Component volume = gradeGo.AddComponent(volumeType);
                volumeType.GetField("isGlobal")?.SetValue(volume, true);
                volumeType.GetProperty("sharedProfile")?.SetValue(volume, profile);
                // Morning is a lighter grade than the shared night profile
                // affords — a rough-pass compromise (reusing rather than
                // authoring a second profile) noted for a later lighting pass.
                volumeType.GetField("weight")?.SetValue(volume, isMorning ? 0.4f : 1f);
            }
            else
            {
                Debug.LogWarning($"[MemorySceneBuilderV2] {NightGradeVolumePath} missing or Volume type unresolved — no color grade.");
            }

            GameObject probeGo = NewChild(root, "Interior Reflection Probe");
            probeGo.transform.position = new Vector3(0f, 1.4f, 0f);
            ReflectionProbe probe = probeGo.AddComponent<ReflectionProbe>();
            probe.size = new Vector3(9f, 2.7f, 9f);
            probe.center = Vector3.zero;
        }

        private static void BuildAtmosphere(Transform root)
        {
            GameObject windGo = NewChild(root, "Exterior Wind");
            windGo.transform.rotation = Quaternion.Euler(4f, 68f, 0f);
            WindZone wind = windGo.AddComponent<WindZone>();
            wind.mode = WindZoneMode.Directional;
            wind.windMain = 0.72f;
            wind.windTurbulence = 0.5f;
            wind.radius = 20f;

            GameObject snowGo = NewChild(root, "Windblown Snow");
            snowGo.transform.position = new Vector3(1f, 9f, 0f);
            ParticleSystem ps = snowGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = 8f;
            main.startSpeed = 3.1f;
            main.maxParticles = 1400;
            main.startSize = 0.03f;
            main.startColor = Color.white;
            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 120f;
            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(20f, 1f, 20f);
        }

        private static void ApplyFlatColor(GameObject go, Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material material = new Material(shader) { color = color };
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
        }

        private static Scene OpenOrCreateEmptyScene(string path)
        {
            Scene scene = SceneManager.GetSceneByPath(path);
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null)
            {
                scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                return scene;
            }

            scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, path);
            return scene;
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }
    }
}
