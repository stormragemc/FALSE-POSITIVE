using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Authors Memory_CabinMorning's weather: fog, ambient, an overcast sky, a
    /// fog-coloured backdrop ring, and a grade profile for the Volume that has
    /// been sitting in the scene doing nothing.
    ///
    /// Why any of this is needed: MemorySceneBuilderV2 has never touched
    /// RenderSettings, and OpenOrCreateEmptyScene only destroys root
    /// GameObjects — RenderSettings are scene-level state that survives a
    /// rebuild. So the morning scene has been running the NIGHT look verbatim
    /// (fog (0.055, 0.075, 0.12) at 0.011 exp-squared, night ambient, StormSky)
    /// since it was first created, because that is what the file happened to be
    /// saved with. At 0.011 the fog does not bite until roughly 100 m while the
    /// terrain stops at +/-40, so the player looking outward sees the world
    /// simply end — the "unfinished game" read this fixes.
    ///
    /// Night is deliberately untouched: its RenderSettings are already correct
    /// in Memory_CabinNight.unity, and CabinNightVolume.asset is shared with
    /// ProjectBootstrapBuilder's menu backdrop.
    /// </summary>
    public static class MemoryAtmosphereBuilder
    {
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";
        private const string DataRoot = "Assets/_Project/CabinNight/Data/";
        private const string MeshRoot = "Assets/_Project/CabinNight/Meshes/";
        private const string SkyMaterialPath = DataRoot + "MorningOvercastSky.mat";
        private const string RingMaterialPath = DataRoot + "DistanceFogRing.mat";
        private const string MorningVolumePath = DataRoot + "CabinMorningVolume.asset";
        private const string RingMeshPath = MeshRoot + "DistanceFogRing.mesh";
        private const string RingShaderName = "FalsePositive/DistanceFogRing";
        private const string RingObjectName = "Distance Fog Ring";

        /// <summary>Flat, pale, no blue. Everything else here is tuned around
        /// this being close to the snow's lit albedo — that is what lets the
        /// backdrop ring disappear into the haze in front of it.</summary>
        private static readonly Color FogColor = new Color(0.62f, 0.66f, 0.70f);

        /// <summary>Exp-squared, so transmittance is exp(-(density*d)^2):
        ///   interior, 9 m across the room ..... 95%  (no visible indoor haze)
        ///   pines at 23 m ...................... 70%  (aerial perspective, still crisp)
        ///   fog ring at 34 m ................... 46%  (ring reads as haze, not a wall)
        ///   terrain edge at 40 m ............... 34%
        /// The plan called for 0.020; that leaves the ring at 63% transmissive,
        /// which is enough contrast against the snow in front of it to read as a
        /// band. 0.026 halves that without touching the interior.</summary>
        private const float FogDensity = 0.026f;

        // Ring geometry. Radius sits between the barrier ring
        // (MemoryExteriorBounds, +/-26 and +/-22) and the terrain edge (+/-40),
        // so the snow continues BEHIND the ring — there is no terrain edge to
        // see, only ground running into fog. Bottom well under the snow
        // (SnowTerrainBuilder.SnowSurfaceY = -0.05, pine bases to -0.34); top
        // high enough that it covers ~23 degrees above the horizon from a 1.64 m
        // eye, by which point the alpha has already faded to nothing.
        private const int RingSegments = 48;
        private const float RingRadius = 34f;
        private const float RingBottomY = -2f;
        private const float RingTopY = 16f;
        private const float RingFadeStartY = 4f;
        private const float RingFadeEndY = 15f;

        [MenuItem("Tools/False Positive/Bootstrap/T04e - Build Memory Atmosphere")]
        public static void BuildMorningAtmosphere()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MorningScenePath) == null)
            {
                Debug.LogError($"[MemoryAtmosphereBuilder] {MorningScenePath} does not exist — " +
                               "run 'T04b - Build Memory_CabinMorning (Cabin_v2)' first.");
                return;
            }

            Scene scene = EditorSceneManager.OpenScene(MorningScenePath, OpenSceneMode.Single);
            ApplyMorning();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, MorningScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[MemoryAtmosphereBuilder] Memory_CabinMorning atmosphere built.");
        }

        /// <summary>Applies everything to the ACTIVE scene. Called both by the
        /// menu item above and from the tail of MemorySceneBuilderV2's morning
        /// build, so a T04b rebuild does not silently revert to the night look.</summary>
        public static void ApplyMorning()
        {
            ApplyRenderSettings();
            BuildFogRing(FindRoot("Environment"));
            AssignMorningGrade(FindRoot("Lighting"));
        }

        private static void ApplyRenderSettings()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = FogDensity;

            // Overcast: a bright, almost shadowless dome. The night values here
            // (sky 0.075, ground 0.012) are what made the morning interior read
            // as a night interior with a brighter directional light in it.
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.35f, 0.36f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.28f, 0.28f, 0.29f);
            RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.13f);

            Material sky = EnsureSkyMaterial();
            if (sky != null) RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
        }

        /// <summary>A procedural overcast dome. Night keeps StormSky.mat (a
        /// 6-sided cubemap); reusing it here is what put a night sky above a
        /// morning scene. SunDisk is off — an overcast sky has no visible sun,
        /// and one punching through the fog ring's faded top edge would be the
        /// one thing that gives the backdrop away.</summary>
        private static Material EnsureSkyMaterial()
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[MemoryAtmosphereBuilder] Skybox/Procedural not found — morning sky unchanged.");
                return null;
            }

            Material sky = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (sky == null)
            {
                sky = new Material(shader) { name = "MorningOvercastSky" };
                AssetDatabase.CreateAsset(sky, SkyMaterialPath);
            }
            else if (sky.shader != shader)
            {
                sky.shader = shader;
            }

            sky.SetFloat("_SunDisk", 0f);
            sky.SetFloat("_SunSize", 0.02f);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", 1.6f);
            sky.SetColor("_SkyTint", new Color(0.62f, 0.66f, 0.72f));
            sky.SetColor("_GroundColor", new Color(0.56f, 0.59f, 0.62f));
            sky.SetFloat("_Exposure", 1.05f);

            // _SunDisk is a keyword-driven enum; the float alone changes nothing.
            sky.DisableKeyword("_SUNDISK_SIMPLE");
            sky.DisableKeyword("_SUNDISK_HIGH_QUALITY");
            sky.EnableKeyword("_SUNDISK_NONE");

            EditorUtility.SetDirty(sky);
            return sky;
        }

        // ---- the fog ring ------------------------------------------------

        private static void BuildFogRing(Transform environment)
        {
            if (environment == null)
            {
                Debug.LogWarning("[MemoryAtmosphereBuilder] No 'Environment' root in the active scene — " +
                                 "no fog ring built.");
                return;
            }

            Mesh mesh = EnsureRingMesh();
            Material material = EnsureRingMaterial();
            if (mesh == null || material == null) return;

            Transform existing = environment.Find(RingObjectName);
            GameObject ring = existing != null ? existing.gameObject : new GameObject(RingObjectName);
            ring.transform.SetParent(environment, false);
            ring.transform.localPosition = Vector3.zero;
            ring.transform.localRotation = Quaternion.identity;
            ring.transform.localScale = Vector3.one;

            MeshFilter filter = ring.GetComponent<MeshFilter>();
            if (filter == null) filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = ring.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            // A backdrop, not a surface: no shadows either way, no GI, and no
            // collider — the out-of-bounds blocking is MemoryExteriorBounds'
            // collider ring, well inside this.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        private static Material EnsureRingMaterial()
        {
            Shader shader = Shader.Find(RingShaderName);
            if (shader == null)
            {
                Debug.LogWarning($"[MemoryAtmosphereBuilder] Shader '{RingShaderName}' not found " +
                                 "(Assets/_Project/Shaders/DistanceFogRing.shader) — no fog ring built.");
                return null;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(RingMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "DistanceFogRing" };
                AssetDatabase.CreateAsset(material, RingMaterialPath);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            material.SetFloat("_FadeStartY", RingFadeStartY);
            material.SetFloat("_FadeEndY", RingFadeEndY);
            material.SetFloat("_Opacity", 1f);
            material.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>An open-ended cylinder wound to face OUTWARD; the shader is
        /// Cull Front, so that is what makes it visible from the inside and
        /// invisible if anything ever renders it from outside.</summary>
        private static Mesh EnsureRingMesh()
        {
            Vector3[] vertices = new Vector3[(RingSegments + 1) * 2];
            int[] triangles = new int[RingSegments * 6];

            for (int i = 0; i <= RingSegments; i++)
            {
                float angle = (Mathf.PI * 2f * i) / RingSegments;
                float x = Mathf.Cos(angle) * RingRadius;
                float z = Mathf.Sin(angle) * RingRadius;
                vertices[i * 2] = new Vector3(x, RingBottomY, z);
                vertices[i * 2 + 1] = new Vector3(x, RingTopY, z);
            }

            for (int i = 0; i < RingSegments; i++)
            {
                int bottom = i * 2;
                int top = bottom + 1;
                int nextBottom = bottom + 2;
                int nextTop = bottom + 3;

                int t = i * 6;
                triangles[t] = bottom;
                triangles[t + 1] = top;
                triangles[t + 2] = nextTop;
                triangles[t + 3] = bottom;
                triangles[t + 4] = nextTop;
                triangles[t + 5] = nextBottom;
            }

            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(RingMeshPath);
            bool created = mesh == null;
            if (created) mesh = new Mesh();

            mesh.Clear();
            mesh.name = "DistanceFogRing";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            if (created)
            {
                System.IO.Directory.CreateDirectory(MeshRoot);
                AssetDatabase.CreateAsset(mesh, RingMeshPath);
            }
            else
            {
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        // ---- the grade ---------------------------------------------------

        /// <summary>MemorySceneBuilderV2 points the morning Volume at the shared
        /// CabinNightVolume profile at weight 0.4 and calls it "a rough-pass
        /// compromise noted for a later lighting pass" — and that profile has
        /// `components: []`, so the Volume has always been a no-op. This is the
        /// later pass: its own profile, at full weight.
        ///
        /// Volume/VolumeProfile live in the render-pipeline core assembly, which
        /// this asmdef deliberately doesn't reference (same reason
        /// MemorySceneBuilderV2.BuildLighting reaches them by reflection — HDRP
        /// is also installed per Packages/manifest.json).</summary>
        private static void AssignMorningGrade(Transform lighting)
        {
            if (lighting == null)
            {
                Debug.LogWarning("[MemoryAtmosphereBuilder] No 'Lighting' root in the active scene — " +
                                 "morning grade not assigned.");
                return;
            }

            Type volumeType = Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");
            if (volumeType == null)
            {
                Debug.LogWarning("[MemoryAtmosphereBuilder] Volume type unresolved — morning grade not assigned.");
                return;
            }

            ScriptableObject profile = EnsureMorningProfile();
            if (profile == null) return;

            Transform gradeTransform = lighting.Find("Cabin Morning Grade");
            GameObject grade = gradeTransform != null
                ? gradeTransform.gameObject
                : new GameObject("Cabin Morning Grade");
            if (gradeTransform == null) grade.transform.SetParent(lighting, false);

            Component volume = grade.GetComponent(volumeType);
            if (volume == null) volume = grade.AddComponent(volumeType);
            volumeType.GetField("isGlobal")?.SetValue(volume, true);
            volumeType.GetProperty("sharedProfile")?.SetValue(volume, profile);
            volumeType.GetField("weight")?.SetValue(volume, 1f);
            volumeType.GetField("priority")?.SetValue(volume, 0f);
            EditorUtility.SetDirty(grade);
        }

        private static ScriptableObject EnsureMorningProfile()
        {
            Type profileType = Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime");
            if (profileType == null)
            {
                Debug.LogWarning("[MemoryAtmosphereBuilder] VolumeProfile type unresolved — no morning grade profile.");
                return null;
            }

            ScriptableObject profile = AssetDatabase.LoadAssetAtPath<ScriptableObject>(MorningVolumePath);
            bool created = profile == null || !profileType.IsInstanceOfType(profile);
            if (created)
            {
                profile = ScriptableObject.CreateInstance(profileType);
                profile.name = "CabinMorningVolume";
                AssetDatabase.CreateAsset(profile, MorningVolumePath);
            }
            else
            {
                // Rebuild the overrides in place so the asset GUID — and the
                // scene reference pointing at it — survives a re-run. The
                // VolumeComponents are sub-assets, so clearing the list alone
                // would leave orphans behind in the file.
                foreach (UnityEngine.Object sub in AssetDatabase.LoadAllAssetsAtPath(MorningVolumePath))
                {
                    if (sub == profile || sub == null) continue;
                    UnityEngine.Object.DestroyImmediate(sub, true);
                }

                if (profileType.GetField("components")?.GetValue(profile) is IList components) components.Clear();
            }

            // Overcast morning after a night of bad weather: desaturated toward
            // grey, flattened, lifted a touch so the fog reads as bright haze
            // rather than gloom, plus enough grain to keep the flat greys from
            // banding.
            object colorAdjustments = AddOverride(profile, profileType,
                "UnityEngine.Rendering.Universal.ColorAdjustments, Unity.RenderPipelines.Universal.Runtime");
            if (colorAdjustments != null)
            {
                SetParameter(colorAdjustments, "saturation", -45f);
                SetParameter(colorAdjustments, "contrast", -10f);
                SetParameter(colorAdjustments, "postExposure", 0.1f);
            }

            object filmGrain = AddOverride(profile, profileType,
                "UnityEngine.Rendering.Universal.FilmGrain, Unity.RenderPipelines.Universal.Runtime");
            if (filmGrain != null)
            {
                SetParameter(filmGrain, "intensity", 0.15f);
                SetParameter(filmGrain, "response", 0.8f);
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            return profile;
        }

        private static object AddOverride(ScriptableObject profile, Type profileType, string componentTypeName)
        {
            Type componentType = Type.GetType(componentTypeName);
            if (componentType == null)
            {
                Debug.LogWarning($"[MemoryAtmosphereBuilder] {componentTypeName.Split(',')[0]} unresolved — skipped.");
                return null;
            }

            MethodInfo add = profileType.GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            if (add == null) return null;

            // false: SetAllOverridesTo(false), so only the parameters touched
            // below are marked as overriding the defaults.
            object component = add.Invoke(profile, new object[] { componentType, false });
            if (component is UnityEngine.Object asset) AssetDatabase.AddObjectToAsset(asset, profile);
            return component;
        }

        /// <summary>VolumeParameter&lt;T&gt; exposes `value` and `overrideState`;
        /// setting the value without the override state leaves the parameter
        /// inert, which is the classic way to author a profile that looks right
        /// in the inspector and does nothing at runtime.</summary>
        private static void SetParameter(object component, string fieldName, object value)
        {
            FieldInfo field = component.GetType().GetField(fieldName);
            object parameter = field?.GetValue(component);
            if (parameter == null)
            {
                Debug.LogWarning($"[MemoryAtmosphereBuilder] {component.GetType().Name}.{fieldName} not found — skipped.");
                return;
            }

            parameter.GetType().GetProperty("value")?.SetValue(parameter, value);
            parameter.GetType().GetProperty("overrideState")?.SetValue(parameter, true);
        }

        private static Transform FindRoot(string name)
        {
            foreach (GameObject root in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                if (root.name == name) return root.transform;
            }

            return null;
        }
    }
}
