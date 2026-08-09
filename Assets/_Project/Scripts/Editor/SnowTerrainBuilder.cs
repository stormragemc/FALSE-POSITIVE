using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Regenerates Assets/_Project/CabinNight/Data/CabinNightTerrain.asset and
    /// re-points both memory scenes at the result.
    ///
    /// Why this exists: that asset is corrupt, and has been since the commit
    /// that introduced it ("Added snow scene"). Its SerializedFile header
    /// declares a file size of 1442300 bytes but only 1442297 were ever written
    /// — truncated by three bytes mid-save — so Unity cannot deserialize a
    /// single object out of it. AssetDatabase.LoadAllAssetsAtPath returns zero
    /// objects, Terrain.terrainData resolves to null, and the console prints
    /// "Terrain has no valid TerrainData!" on any SampleHeight call.
    ///
    /// The consequence is the bug that started all of this: with no TerrainData
    /// there is no heightmap to render and no collider to stand on, so the
    /// entire exterior of both cabin scenes is an empty void. A 1 m probe grid
    /// over x +-28, z +-24 found ground in 134 of 2793 cells, and every one of
    /// those 134 was the cabin's own floor, stairs or ceiling — literally
    /// nothing outside the building. Walking out of the front door dropped the
    /// player straight past y = -4 into CabinFallRecovery's teleport, which is
    /// what "we fall out the map" actually was.
    ///
    /// Nothing can be recovered from the old file, so this builds a new one.
    /// Deliberately flat rather than sculpted: every prop in both scenes was
    /// placed against the original surface and is dressed to sit just under it
    /// (cabin skirt -0.200, woodshed -0.275, pine bases -0.340), so any
    /// undulation would float trees or bury the woodshed. Restoring the ground
    /// is the fix; sculpting it is an art pass.
    ///
    /// Run before T04c (Build Exterior Bounds) — that builder measures the
    /// lowest terrain sample to place its safety plane, and measuring a
    /// null TerrainData gives it the wrong answer.
    /// </summary>
    public static class SnowTerrainBuilder
    {
        private const string TerrainDataPath = "Assets/_Project/CabinNight/Data/CabinNightTerrain.asset";
        private const string SnowLayerPath = "Assets/_Project/CabinNight/Data/SnowTerrainLayer.terrainlayer";

        private static readonly string[] ScenePaths =
        {
            "Assets/_Project/Scenes/Memory_CabinMorning.unity",
            "Assets/_Project/Scenes/Memory_CabinNight.unity",
        };

        /// <summary>Matches the original: an 80x80 m terrain 5 m deep, placed at
        /// (-40, -0.5, -40) so it is centred on the cabin. Read off the old
        /// asset's m_Scale (0.625, 5, 0.625) at 129 heightmap samples before it
        /// was replaced, and off the Snow Terrain transform still in both scenes.</summary>
        private static readonly Vector3 TerrainSize = new Vector3(80f, 5f, 80f);

        private static readonly Vector3 TerrainOrigin = new Vector3(-40f, -0.5f, -40f);

        private const int HeightmapResolution = 129;
        private const int AlphamapResolution = 256;
        private const int BaseMapResolution = 256;
        private const int DetailResolution = 128;
        private const int DetailResolutionPerPatch = 16;

        /// <summary>World Y of the snow surface. Five centimetres under the
        /// cabin floor (measured at y = 0.000): far enough that the terrain and
        /// the floor mesh cannot z-fight where the terrain passes under the
        /// building, small enough that stepping out of the front door is not a
        /// drop — and the CharacterController's 0.28 m step offset absorbs it
        /// entirely either way. Everything dressed onto the snow keeps its
        /// footing: the cabin skirt (-0.200), the woodshed (-0.275) and the pine
        /// bases (-0.340) all stay buried, and Nick's body (y 0.100) still lies
        /// on the surface.</summary>
        private const float SnowSurfaceY = -0.05f;

        [MenuItem("Tools/False Positive/Bootstrap/T04b - Rebuild Snow Terrain")]
        public static void Rebuild()
        {
            TerrainLayer snow = AssetDatabase.LoadAssetAtPath<TerrainLayer>(SnowLayerPath);
            if (snow == null)
            {
                Debug.LogError($"[SnowTerrain] {SnowLayerPath} is missing — the rebuilt terrain would render " +
                               "untextured. Aborting rather than shipping a magenta snowfield.");
                return;
            }

            // The old file cannot be loaded, so it cannot be mutated in place —
            // it has to be deleted and remade. That mints a new guid, which is
            // exactly why both scenes are re-pointed below rather than trusted
            // to keep resolving the old reference.
            if (AssetDatabase.LoadAssetAtPath<Object>(TerrainDataPath) == null &&
                System.IO.File.Exists(TerrainDataPath))
            {
                Debug.Log($"[SnowTerrain] {TerrainDataPath} exists but deserializes to nothing — deleting the corrupt file.");
            }

            AssetDatabase.DeleteAsset(TerrainDataPath);

            TerrainData data = BuildTerrainData(snow);
            AssetDatabase.CreateAsset(data, TerrainDataPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(TerrainDataPath, ImportAssetOptions.ForceUpdate);

            // Reload through the AssetDatabase so the scenes are pointed at the
            // persisted asset rather than the in-memory instance that created it.
            data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                Debug.LogError("[SnowTerrain] The new TerrainData did not survive a round trip through the AssetDatabase.");
                return;
            }

            foreach (string scenePath in ScenePaths) RepointScene(scenePath, data);

            Debug.Log($"[SnowTerrain] Rebuilt {TerrainDataPath}: {TerrainSize.x}x{TerrainSize.z} m, " +
                      $"{HeightmapResolution - 1} heightmap quads, flat snow surface at world y={SnowSurfaceY:F2}. " +
                      "Re-run 'T04c - Build Exterior Bounds' after this so the safety plane re-measures.");
        }

        private static TerrainData BuildTerrainData(TerrainLayer snow)
        {
            var data = new TerrainData { name = "CabinNightTerrain" };

            // Resolution first: assigning heightmapResolution resets the
            // heightmap and rescales size, so setting size before it would be
            // silently undone.
            data.heightmapResolution = HeightmapResolution;
            data.size = TerrainSize;
            data.alphamapResolution = AlphamapResolution;
            data.baseMapResolution = BaseMapResolution;
            data.SetDetailResolution(DetailResolution, DetailResolutionPerPatch);

            // Heights are normalised 0..1 across size.y, measured from the
            // terrain object's own origin.
            float normalised = Mathf.Clamp01((SnowSurfaceY - TerrainOrigin.y) / TerrainSize.y);
            var heights = new float[HeightmapResolution, HeightmapResolution];
            for (int y = 0; y < HeightmapResolution; y++)
            {
                for (int x = 0; x < HeightmapResolution; x++) heights[y, x] = normalised;
            }

            data.SetHeights(0, 0, heights);

            // A fresh TerrainData has no splat weights at all; without this the
            // single layer is present but painted nowhere and the ground renders
            // as the terrain shader's untextured fallback.
            data.terrainLayers = new[] { snow };
            var alphamaps = new float[AlphamapResolution, AlphamapResolution, 1];
            for (int y = 0; y < AlphamapResolution; y++)
            {
                for (int x = 0; x < AlphamapResolution; x++) alphamaps[y, x, 0] = 1f;
            }

            data.SetAlphamaps(0, 0, alphamaps);

            return data;
        }

        private static void RepointScene(string scenePath, TerrainData data)
        {
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            Terrain[] terrains = Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (terrains.Length == 0)
            {
                Debug.LogWarning($"[SnowTerrain] {scenePath} has no Terrain — nothing to re-point.");
                return;
            }

            foreach (Terrain terrain in terrains)
            {
                terrain.terrainData = data;
                terrain.transform.SetPositionAndRotation(TerrainOrigin, Quaternion.identity);
                terrain.transform.localScale = Vector3.one;

                // The collider carries its own reference to the same asset and
                // is the half that actually decides whether the player falls
                // through, so it is set explicitly rather than assumed to
                // follow the Terrain component.
                TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
                if (collider == null) collider = terrain.gameObject.AddComponent<TerrainCollider>();
                collider.terrainData = data;
                collider.enabled = true;

                EditorUtility.SetDirty(terrain);
                EditorUtility.SetDirty(collider);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SnowTerrain] Re-pointed {terrains.Length} terrain(s) in {scenePath}.");
        }
    }
}
