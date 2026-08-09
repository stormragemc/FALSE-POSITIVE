using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Exterior playable-area collision for Memory_CabinMorning: a safety
    /// ground plane under the snow and an invisible wall ring around it.
    ///
    /// Why this exists: the player fell out of the world walking around
    /// outside. The scene YAML looked innocent — Snow Terrain carried an
    /// enabled TerrainCollider pointing at CabinNightTerrain.asset — but that
    /// asset was truncated three bytes short of its own header and deserialized
    /// to nothing, so terrainData resolved to null and the exterior had no
    /// heightmap and no collider at all. SnowTerrainBuilder (T04b) rebuilds it;
    /// run that first, since the safety plane below is placed from a measured
    /// terrain sample and measuring a null TerrainData gives the wrong answer.
    ///
    /// This builder is the second half: a safety plane just under the snow so a
    /// future hole or heightmap dip cannot become another fall-through, and the
    /// wall ring the exterior never had — nothing bounded the play area, so
    /// walking past the terrain edge dropped the player into
    /// CabinFallRecovery's -4 trapdoor. ProbeExteriorGround reports coverage
    /// before and after.
    ///
    /// Follows MemorySceneDressing.AddStairBlocker's collider-only-GameObject
    /// pattern (no MeshFilter/MeshRenderer, parented under the "Gameplay" root
    /// MemorySceneBuilderV2 reserves for exactly this), and the project's
    /// OpenScene -> mutate -> MarkSceneDirty -> SaveScene bootstrap convention.
    /// </summary>
    public static class MemoryExteriorBounds
    {
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";

        /// <summary>Invisible wall ring, in world units. Chosen to sit just
        /// outside the pine treeline: MemorySceneBuilderV2.Pines spans
        /// x [-23, 23] and z [-17, 16], so all 14 trees fall inside the ring
        /// and the treeline is always the *visual* boundary the player reads
        /// before they ever touch the wall.</summary>
        private const float BoundMaxX = 26f;
        private const float BoundMaxZ = 22f;

        /// <summary>Wall span in Y. The player capsule is 1.76 m tall with a
        /// 0.28 m step offset, so a wall from -1 to +5 cannot be stepped onto,
        /// jittered over, or slipped under on a downhill sample.</summary>
        private const float WallBottomY = -1f;
        private const float WallTopY = 5f;
        private const float WallThickness = 0.5f;

        /// <summary>How far below the *lowest measured* terrain sample the
        /// safety plane's top face sits. Small on purpose: where the terrain
        /// works, the plane is below it and never wins collide-and-slide, so
        /// it changes nothing; where the terrain has a hole the player lands
        /// 5 cm under the surrounding snow instead of falling through to
        /// CabinFallRecovery's -4 trapdoor, which is imperceptible.</summary>
        private const float SafetyClearance = 0.05f;

        private const float SafetyThickness = 1f;

        /// <summary>Probe grid, 1 m spacing, a little wider than the wall ring
        /// so the cells immediately outside it are measured too.</summary>
        private const float ProbeMaxX = 28f;
        private const float ProbeMaxZ = 24f;
        private const float ProbeStep = 1f;

        [MenuItem("Tools/False Positive/Diagnostics/Probe Exterior Ground")]
        public static void ProbeExteriorGround()
        {
            EditorSceneManager.OpenScene(MorningScenePath, OpenSceneMode.Single);

            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogError("[ExteriorBounds] No active Terrain in Memory_CabinMorning — that alone explains the fall-through.");
                return;
            }

            ReportHoles(terrain);

            // Edit-mode physics queries read the last-synced collider poses;
            // force a sync so the raycasts below see the scene as just opened.
            Physics.SyncTransforms();

            int noHit = 0;
            int lowHit = 0;
            int cells = 0;
            float minSample = float.MaxValue;
            float maxSample = float.MinValue;
            Vector3 worstCell = Vector3.zero;
            StringBuilder failures = new StringBuilder();

            for (float x = -ProbeMaxX; x <= ProbeMaxX; x += ProbeStep)
            {
                for (float z = -ProbeMaxZ; z <= ProbeMaxZ; z += ProbeStep)
                {
                    cells++;
                    Vector3 at = new Vector3(x, 0f, z);

                    // Terrain.SampleHeight is relative to the terrain object's
                    // own Y, which sits at -0.5 here — add it back for world Y.
                    float sampled = terrain.transform.position.y + terrain.SampleHeight(at);
                    if (sampled < minSample) { minSample = sampled; worstCell = at; }
                    if (sampled > maxSample) maxSample = sampled;

                    // Physics.Raycast is the question that actually matters:
                    // SampleHeight reads the heightmap and happily returns a
                    // height for a painted hole, where the collider has none.
                    if (!Physics.Raycast(new Vector3(x, 5f, z), Vector3.down, out RaycastHit hit, 20f,
                            ~0, QueryTriggerInteraction.Ignore))
                    {
                        noHit++;
                        if (failures.Length < 4000) failures.AppendLine($"  NO GROUND at ({x}, {z})");
                    }
                    else if (hit.point.y < -1f)
                    {
                        lowHit++;
                        if (failures.Length < 4000)
                            failures.AppendLine($"  ground at ({x}, {z}) is y={hit.point.y:F2} on '{hit.collider.name}'");
                    }
                }
            }

            string walls = ProbeWalls();

            Debug.Log(
                $"[ExteriorBounds] Probed {cells} cells over x +-{ProbeMaxX}, z +-{ProbeMaxZ}.\n" +
                $"  terrain surface world Y: min {minSample:F3} (at {worstCell.x}, {worstCell.z}), max {maxSample:F3}\n" +
                $"  cells with NO ground collider: {noHit}\n" +
                $"  cells whose ground is below y=-1: {lowHit}\n" +
                (failures.Length > 0 ? failures.ToString() : "  (every cell has ground above y=-1)\n") +
                walls +
                (noHit == 0 && lowHit == 0
                    ? "  ground is covered."
                    : "  Run 'Tools/False Positive/Bootstrap/T04c - Build Exterior Bounds' to fix."));
        }

        /// <summary>Fires a ray outward along each axis at capsule height and at
        /// head height, from the middle of the play area, and reports where it is
        /// stopped. This is the "walk into all four walls" check: a wall that
        /// exists in the hierarchy but sits at the wrong height, or whose collider
        /// never got added, shows up here as a ray that reaches the terrain edge.</summary>
        private static string ProbeWalls()
        {
            var report = new StringBuilder();
            (string name, Vector3 direction, float expected)[] rays =
            {
                ("N", Vector3.forward, BoundMaxZ),
                ("S", Vector3.back, BoundMaxZ),
                ("E", Vector3.right, BoundMaxX),
                ("W", Vector3.left, BoundMaxX),
            };

            foreach ((string name, Vector3 direction, float expected) in rays)
            {
                // From well outside the cabin footprint so the cabin's own walls
                // can't be mistaken for the ring, at 0.9 m (mid-capsule) and
                // 1.7 m (head) — the two heights a wall is most likely to miss.
                Vector3 from = new Vector3(direction.x * 8f, 0f, direction.z * 8f);
                foreach (float height in new[] { 0.9f, 1.7f })
                {
                    Vector3 origin = from + Vector3.up * height;
                    bool hit = Physics.Raycast(origin, direction, out RaycastHit info, 60f, ~0,
                        QueryTriggerInteraction.Ignore);
                    float reached = hit ? Vector3.Distance(origin, info.point) + 8f : float.NaN;
                    report.AppendLine(hit
                        ? $"  wall {name} @ y={height}: stopped at {reached:F1} m by '{info.collider.name}' (ring at {expected})"
                        : $"  wall {name} @ y={height}: *** NOTHING STOPS THE PLAYER ***");
                }
            }

            return report.ToString();
        }

        private static void ReportHoles(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            if (data == null)
            {
                Debug.LogError("[ExteriorBounds] Terrain has no TerrainData.");
                return;
            }

            int res = data.holesResolution;
            if (res <= 0)
            {
                Debug.Log("[ExteriorBounds] Terrain has no holes texture.");
                return;
            }

            bool[,] holes = data.GetHoles(0, 0, res, res);
            int holeCount = 0;
            foreach (bool solid in holes)
            {
                if (!solid) holeCount++;
            }

            Debug.Log(holeCount > 0
                ? $"[ExteriorBounds] Terrain has {holeCount} painted HOLE texels of {res * res} — holes are (part of) the fall-through."
                : $"[ExteriorBounds] Terrain has no painted holes ({res}x{res} texels all solid).");
        }

        [MenuItem("Tools/False Positive/Bootstrap/T04c - Build Exterior Bounds")]
        public static void BuildExteriorBounds()
        {
            Scene scene = EditorSceneManager.OpenScene(MorningScenePath, OpenSceneMode.Single);

            GameObject gameplay = GameObject.Find("Gameplay");
            if (gameplay == null)
            {
                gameplay = new GameObject("Gameplay");
                gameplay.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            }

            float safetyTop = MeasureLowestTerrainY() - SafetyClearance;

            // Safety ground: slightly larger than the wall ring so there is no
            // uncovered sliver between the last probed cell and the wall.
            AddBox(gameplay, "Ground_ExteriorSafety",
                new Vector3(0f, safetyTop - SafetyThickness * 0.5f, 0f),
                new Vector3(BoundMaxX * 2f + 4f, SafetyThickness, BoundMaxZ * 2f + 4f));

            float wallCentreY = (WallBottomY + WallTopY) * 0.5f;
            float wallHeight = WallTopY - WallBottomY;
            float spanX = BoundMaxX * 2f + WallThickness;
            float spanZ = BoundMaxZ * 2f - WallThickness;

            AddBox(gameplay, "Blocker_Exterior_N", new Vector3(0f, wallCentreY, BoundMaxZ),
                new Vector3(spanX, wallHeight, WallThickness));
            AddBox(gameplay, "Blocker_Exterior_S", new Vector3(0f, wallCentreY, -BoundMaxZ),
                new Vector3(spanX, wallHeight, WallThickness));
            AddBox(gameplay, "Blocker_Exterior_E", new Vector3(BoundMaxX, wallCentreY, 0f),
                new Vector3(WallThickness, wallHeight, spanZ));
            AddBox(gameplay, "Blocker_Exterior_W", new Vector3(-BoundMaxX, wallCentreY, 0f),
                new Vector3(WallThickness, wallHeight, spanZ));

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log(
                $"[ExteriorBounds] Memory_CabinMorning: safety ground top at y={safetyTop:F3}, " +
                $"wall ring x +-{BoundMaxX}, z +-{BoundMaxZ}, y {WallBottomY}..{WallTopY}.");
        }

        /// <summary>Lowest terrain surface inside the wall ring, measured rather
        /// than hardcoded so the safety plane re-derives itself if the heightmap
        /// is ever repainted. Falls back to the known pine-base range
        /// (-0.188 .. -0.34, see MemorySceneBuilderV2.Pines) if there is no
        /// terrain to sample.</summary>
        private static float MeasureLowestTerrainY()
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null)
            {
                Debug.LogWarning("[ExteriorBounds] No active Terrain — safety ground falls back to y=-0.40.");
                return -0.40f;
            }

            float lowest = float.MaxValue;
            for (float x = -BoundMaxX; x <= BoundMaxX; x += ProbeStep)
            {
                for (float z = -BoundMaxZ; z <= BoundMaxZ; z += ProbeStep)
                {
                    float sampled = terrain.transform.position.y + terrain.SampleHeight(new Vector3(x, 0f, z));
                    if (sampled < lowest) lowest = sampled;
                }
            }

            return lowest;
        }

        private static void AddBox(GameObject parent, string name, Vector3 centre, Vector3 size)
        {
            Transform existing = parent.transform.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.identity;
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
