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

        // Cozy decor pass (Aug 2026 follow-up): CC0 Poly Haven/ambientCG props
        // and textures placed directly in the prefab. See Cabin_v2/README.md's
        // "Decor pass" section for the coordinate table and
        // Art/Sourced/README.md for sourcing/licensing detail. Distinct from
        // FurnitureRoot above: these models are already real-world scaled
        // ("grounded prefab" per Art/PolyHaven/README.md), unlike the
        // furniture swap where a blockout dictated the target size.
        private const string SourcedRoot = "Assets/_Project/Art/Sourced/Cabin/";

        // Round 2 (stairwell paintings / coat / shoe rack). rubber_boots is
        // an existing Poly Haven download (Art/PolyHaven/README.md: "Four
        // pairs of cabin footwear") never wired to any builder script — see
        // BuildShoeRack. Its own rubber_boots_URP.mat has a dangling
        // _BaseMap GUID (round-1 finding), so a fresh material is built here
        // instead of reusing it.
        private const string PolyHavenRoot = "Assets/_Project/Art/PolyHaven/";
        private const string RubberBootsFbxPath = PolyHavenRoot + "rubber_boots/rubber_boots_1k.fbx";

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

        // ---- Room height ------------------------------------------------
        //
        // Cabin.fbx is authored at a 2.7 m ceiling (Cabin_v2/README.md). The
        // playable cabin wants a taller room. That raise started life as a
        // hand-made override on the prefab INSTANCE in each memory scene,
        // which every T04a/T04b rebuild silently wiped — it lives here now, on
        // the prefab itself, so it survives a rebuild and there is exactly one
        // number to change.
        //
        // Every shell object carries the FBX importer's baked (270, 0, 0)
        // rotation (same fact DoorClosedRotation and OrientSofaToFireplace
        // document), so their LOCAL Z axis is world UP — the vertical scales
        // below are all localScale.z, never .y. Measured, not assumed: walls
        // at localScale.z 112.963 read 3.05 m tall in world space.
        public const float CeilingHeight = 3.05f;
        private const float AuthoredCeilingHeight = 2.7f;

        /// <summary>The room-height raise above deliberately lands several
        /// pairs of faces flush — wall top against ceiling underside, ceiling
        /// top against roof deck underside, the chimney extension against
        /// both the fireplace breast and the ceiling. "Flush" in floats means
        /// bit-for-bit coincident, which reads as z-fighting flicker in the
        /// Scene view rather than a clean seam: the depth buffer can't
        /// consistently pick a winner between two faces at the same depth.
        /// This is the standard fix — make abutting solid geometry overlap by
        /// a hair instead of touching exactly, so no two visible faces are
        /// ever coplanar. 2 mm is far below what's visible at this room's
        /// scale but well above float precision here.</summary>
        private const float ZFightEpsilon = 0.002f;

        // The ceiling slab's TOP face (2.7–2.9 as authored) is the upper floor
        // the staircase has to land on — a different number from the ceiling
        // the walls have to meet, and it rises by the same absolute amount.
        private const float AuthoredFloorToFloor = 2.9f;

        /// <summary>Vertical scale for the wall shell and for anything that
        /// fills an opening cut INTO it. Scaling the wall mesh stretches its
        /// door and window cut-outs by this same factor — measured on the
        /// result: the doorway head goes 2.100 -> 2.372 and the window opening
        /// 0.950–2.250 -> 1.073–2.542 — so the door leaf and the window grille
        /// must be stretched with it or each leaves a gap at its head.</summary>
        public static readonly float WallHeightScale = CeilingHeight / AuthoredCeilingHeight;

        /// <summary>How far the ceiling slab and roof deck move up. They are
        /// translated, not scaled: their thickness is not a function of the
        /// room height.</summary>
        public static readonly float CeilingRise = CeilingHeight - AuthoredCeilingHeight;

        /// <summary>Vertical scale for the stairs and their railing. Derived
        /// from the FLOOR-TO-FLOOR rise, not the ceiling height — the top
        /// riser has to land on the upper floor surface, which is the ceiling
        /// slab's top face. Leaving the stairs at the authored scale strands
        /// the top tread 0.53 m below the landing.</summary>
        public static readonly float StairHeightScale =
            (AuthoredFloorToFloor + CeilingRise) / AuthoredFloorToFloor;

        /// <summary>Re-seats a Y coordinate that was authored against the
        /// pre-raise wall shell (window furniture, wall-mounted props).</summary>
        public static float WallHeight(float authoredY) => authoredY * WallHeightScale;

        /// <summary>Re-seats a Y coordinate that was authored against the
        /// pre-raise stair run (anything standing on a tread or the landing).</summary>
        public static float StairHeight(float authoredY) => authoredY * StairHeightScale;

        // Scaled by WallHeightScale: the shell itself, plus the two pieces that
        // fill openings the shell's own scale stretches.
        private static readonly string[] WallHeightObjects =
        {
            "SM_Cabin_Walls", "BO_WindowGrille", "SM_Door",
        };

        // Scaled by StairHeightScale — the railing follows the stair rake, so
        // it has to use the stairs' factor and not the wall's.
        private static readonly string[] StairHeightObjects =
        {
            "SM_Cabin_Stairs", "SM_Cabin_StairRailing",
        };

        // Translated by CeilingRise.
        private static readonly string[] CeilingRiseObjects =
        {
            "SM_Cabin_Ceiling", "SM_Cabin_Roof",
        };

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
                // Cozy decor pass — new normal maps downloaded 2026-08-09.
                SourcedRoot + "Carpet016/Carpet016_nor_gl_2k.jpg",
                SourcedRoot + "Shelf_01/Shelf_01_nor_gl_2k.exr",
                SourcedRoot + "book_encyclopedia_set_01/book_encyclopedia_set_01_cover_nor_gl_2k.jpg",
                SourcedRoot + "ceramic_vase_01/ceramic_vase_01_nor_gl_2k.exr",
                // Round 2 — shoe rack. Not already a normal map on disk
                // (verified: rubber_boots_nor_gl_1k.jpg.meta reads
                // textureType: 0, Default) despite the pre-existing
                // rubber_boots_URP.mat referencing it as one.
                PolyHavenRoot + "rubber_boots/rubber_boots_nor_gl_1k.jpg",
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

            // Cozy decor pass (Aug 2026) — CC0 assets under
            // Assets/_Project/Art/Sourced/Cabin/. See Art/Sourced/README.md
            // for licensing and Cabin_v2/README.md's "Decor pass" section for
            // placement. Same blockout-quality shortcut as every material
            // above; smoothness kept inside this project's established
            // 0.05-0.30 band (ART_DIRECTION.md §2) rather than URP's
            // plasticky 0.5 default.
            //
            // Two rug materials, not one shared one: a primitive cube's UVs
            // are 0-1 PER FACE regardless of localScale, so the tiling below
            // has to encode each rug's own real-world size in metres (one
            // tile per metre) or one of the two rugs stretches. A
            // MaterialPropertyBlock override isn't an option — it isn't
            // serialized into a prefab asset.
            CreateTexturedMaterial("M_Rug_Oxblood_Hearth",
                SourcedRoot + "Carpet016/Carpet016_diff_2k.jpg",
                SourcedRoot + "Carpet016/Carpet016_nor_gl_2k.jpg", smoothness: 0.06f,
                baseColor: new Color(0.45f, 0.10f, 0.07f), tiling: new Vector2(2.7f, 1.8f));

            CreateTexturedMaterial("M_Rug_Oxblood_Dining",
                SourcedRoot + "Carpet016/Carpet016_diff_2k.jpg",
                SourcedRoot + "Carpet016/Carpet016_nor_gl_2k.jpg", smoothness: 0.06f,
                baseColor: new Color(0.40f, 0.13f, 0.10f), tiling: new Vector2(3.6f, 3.6f));

            // Same carpet texture, warmer tint, tighter tiling — reads as a
            // distinct woven wall hanging rather than a third rug.
            //
            // Round-2 tuning: the original (0.55, 0.30, 0.18) tint was narrow
            // enough across all three channels that it crushed the texture's
            // own light/dark variation into a near-flat rust block on both
            // -X and +X galleries (visually confirmed via screenshot) —
            // multiplying diffuse RGB by a tint this dark leaves almost no
            // headroom for the weave pattern to show through. Brighter and
            // wider-spread here so the underlying texture detail survives
            // the multiply.
            CreateTexturedMaterial("M_Textile_WallHanging",
                SourcedRoot + "Carpet016/Carpet016_diff_2k.jpg",
                SourcedRoot + "Carpet016/Carpet016_nor_gl_2k.jpg", smoothness: 0.05f,
                baseColor: new Color(0.78f, 0.58f, 0.40f), tiling: new Vector2(1.5f, 1.2f));

            // The project's own already-committed, never-referenced Poly Haven
            // sky HDRI, repurposed as a landscape print for the two framed
            // pictures — there is no CC0 painting/canvas texture anywhere on
            // Poly Haven or ambientCG (both checked this pass). Its .meta
            // already imports it as a plain 2D sRGB texture (textureShape: 1,
            // not Cube), so it drops straight into _BaseMap with no importer
            // change and no risk to any skybox that might reference it later.
            // Both crops are offset below the HDRI's horizon (v < 0.5) and the
            // >1 HDR values are tamed by _BaseColor, or the sky blows out white.
            CreateTexturedMaterial("M_Art_Landscape_A",
                SourcedRoot + "snowy_forest/snowy_forest_2k.hdr", null, smoothness: 0.05f,
                baseColor: new Color(0.55f, 0.55f, 0.55f),
                tiling: new Vector2(0.16f, 0.28f), offset: new Vector2(0.30f, 0.34f));

            CreateTexturedMaterial("M_Art_Landscape_B",
                SourcedRoot + "snowy_forest/snowy_forest_2k.hdr", null, smoothness: 0.05f,
                baseColor: new Color(0.55f, 0.55f, 0.55f),
                tiling: new Vector2(0.16f, 0.28f), offset: new Vector2(0.62f, 0.31f));

            // Frame rails for BuildWallArt. Tiled at 6, not this project's
            // usual 2 m/tile wood rule (Cabin_v2/README.md) — a 0.06 m rail at
            // 2 m/tile would sample a nearly featureless 3% crop of the texture.
            CreateTexturedMaterial("M_Wood_Frame",
                TextureRoot + "weathered_plank_siding_diff_4k.jpg",
                TextureRoot + "Cabin_Normal.png", smoothness: 0.20f,
                baseColor: new Color(0.30f, 0.17f, 0.10f), tiling: new Vector2(6f, 6f));

            CreateTexturedMaterial("M_Wood_Shelf",
                SourcedRoot + "Shelf_01/Shelf_01_diff_2k.jpg",
                SourcedRoot + "Shelf_01/Shelf_01_nor_gl_2k.exr", smoothness: 0.18f);

            CreateTexturedMaterial("M_Books_Encyclopedia",
                SourcedRoot + "book_encyclopedia_set_01/book_encyclopedia_set_01_cover_diff_2k.jpg",
                SourcedRoot + "book_encyclopedia_set_01/book_encyclopedia_set_01_cover_nor_gl_2k.jpg",
                smoothness: 0.12f);

            CreateTexturedMaterial("M_Ceramic_Vase",
                SourcedRoot + "ceramic_vase_01/ceramic_vase_01_diff_2k.jpg",
                SourcedRoot + "ceramic_vase_01/ceramic_vase_01_nor_gl_2k.exr", smoothness: 0.28f);

            // Round 2 (stairwell paintings / coat / shoe rack).
            //
            // Built fresh rather than reusing rubber_boots_URP.mat — that
            // material's own _BaseMap GUID is dangling (round-1 finding), so
            // it renders untextured/white as shipped.
            CreateTexturedMaterial("M_Rubber_Boots",
                PolyHavenRoot + "rubber_boots/rubber_boots_diff_1k.jpg",
                PolyHavenRoot + "rubber_boots/rubber_boots_nor_gl_1k.jpg", smoothness: 0.25f);

            // The coat (see BuildDrapedCoatVisual in MemorySceneDressing.cs).
            // Flat color, no texture — nothing on disk fits a canvas parka,
            // and no CC0 source could be confirmed (the one Sketchfab hit had
            // a self-contradicting license badge). Dark olive/drab, distinct
            // from every wood/upholstery tone already in the palette so it
            // reads as "a garment" rather than "more furniture."
            CreateFlatMaterial("M_Fabric_Coat", new Color(0.09f, 0.11f, 0.08f), smoothness: 0.12f);
        }

        /// <summary>baseColor/tiling/offset are the cozy decor pass's
        /// additions (Aug 2026) — every existing call site above omits them
        /// and is unaffected. baseColor multiplies the diffuse map, letting a
        /// shared texture (e.g. Carpet016) serve several tinted materials.
        /// tiling/offset apply to BOTH _BaseMap and _BumpMap together, or the
        /// normal detaches from the albedo — needed because the rug/frame/
        /// wall-art materials reuse a texture at a different scale than its
        /// own authored 1:1 UVs (see BuildMaterials' comment on why there are
        /// two rug materials, not one).</summary>
        private static void CreateTexturedMaterial(string name, string diffusePath, string normalPath, float smoothness,
            Color? baseColor = null, Vector2? tiling = null, Vector2? offset = null)
        {
            string assetPath = MaterialRoot + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, assetPath);
            }

            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(diffusePath);
            Texture2D normal = string.IsNullOrEmpty(normalPath) ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            if (diffuse == null) Debug.LogWarning($"[CabinV2Builder] Missing diffuse texture {diffusePath} for {name}.");
            if (normal == null && !string.IsNullOrEmpty(normalPath)) Debug.LogWarning($"[CabinV2Builder] Missing normal texture {normalPath} for {name}.");

            material.SetTexture("_BaseMap", diffuse);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            if (baseColor.HasValue) material.SetColor("_BaseColor", baseColor.Value);
            if (normal != null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
            }
            if (tiling.HasValue)
            {
                material.SetTextureScale("_BaseMap", tiling.Value);
                if (normal != null) material.SetTextureScale("_BumpMap", tiling.Value);
            }
            if (offset.HasValue)
            {
                material.SetTextureOffset("_BaseMap", offset.Value);
                if (normal != null) material.SetTextureOffset("_BumpMap", offset.Value);
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

            // AFTER the colliders, deliberately. Both branches of AddColliders
            // read UNSCALED mesh bounds (see ComputeLocalBounds' doc) and Unity
            // re-applies the transform's own scale on top, so a vertical scale
            // set here is picked up by the collider automatically — scaling
            // first would double-apply it.
            ApplyRoomHeight(instance);
            DisableDuplicateDoorLeaf(instance);
            OrientSofaToFireplace(instance);

            // Runs AFTER OrientSofaToFireplace so the sofa's yaw is already on
            // the instance when the prefab is saved. The swap SetActive(false)s
            // SM_Table/SM_Chair_01..06 but leaves them in place at their
            // authored positions — CutsceneStage.SeatedAtChair still reads those
            // transforms for seating, so it must look them up in a way that
            // sees inactive objects (see the note on SeatedAtChair).
            SwapFurnitureWithRealModels(instance);

            // Cozy decor pass (Aug 2026): rugs, CC0 furniture accents, and
            // framed wall art baked into the prefab so they show up in both
            // memory scenes AND the MainMenu 3D backdrop, all three of which
            // hold an instance of this prefab. Rugs first — they are the
            // ground plane the other props are laid out beside. See
            // Cabin_v2/README.md's "Decor pass" section for the full
            // coordinate table and the collision budget this was checked
            // against before writing it.
            BuildFloorRugs(instance);
            BuildSourcedDecor(instance);
            BuildWallArt(instance);
            BuildShoeRack(instance);

            System.IO.Directory.CreateDirectory(PrefabRoot);
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabRoot + "Cabin_v2.prefab");
            UnityEngine.Object.DestroyImmediate(instance);
        }

        /// <summary>Raises the room from the FBX's authored 2.7 m ceiling to
        /// CeilingHeight, moving every piece that has to stay in contact with
        /// something else. Runs on the in-memory instance before
        /// SaveAsPrefabAsset, so the height is baked into Cabin_v2.prefab and
        /// every scene holding an instance picks it up.
        ///
        /// No-ops cleanly when CeilingHeight == AuthoredCeilingHeight (both
        /// scales become 1 and the rise 0), so reverting the raise is a
        /// one-constant edit plus a re-run of this menu item.</summary>
        private static void ApplyRoomHeight(GameObject cabin)
        {
            foreach (string objectName in WallHeightObjects) ScaleHeight(cabin, objectName, WallHeightScale);
            foreach (string objectName in StairHeightObjects) ScaleHeight(cabin, objectName, StairHeightScale);
            foreach (string objectName in CeilingRiseObjects) RaiseObject(cabin, objectName, CeilingRise);
            BuildChimneyExtension(cabin);

            // Two more of the seams the raise landed flush (see ZFightEpsilon's
            // doc) — walls-vs-ceiling and ceiling-vs-roof. Not folded into the
            // shared loops above: WallHeightScale is also used by BO_WindowGrille
            // and SM_Door, which must stay at exactly the wall's opening size,
            // and CeilingRise is a single rigid translation shared by both the
            // ceiling and the roof, so nudging it would move both faces of
            // whichever object it's applied to, not just the one touching its
            // neighbour. Each fix below only grows/moves the ONE object whose
            // face needs to poke past its neighbour, in the direction that turns
            // "touching" into "overlapping" rather than opening a gap:
            //  - the wall's TOP needs to rise past the (unmoved) ceiling
            //    underside, so it grows taller from its floor-anchored base.
            //  - the roof's UNDERSIDE needs to drop past the (unmoved) ceiling
            //    top, so the whole roof is nudged down a hair.
            ScaleHeight(cabin, "SM_Cabin_Walls", 1f + ZFightEpsilon / CeilingHeight);
            RaiseObject(cabin, "SM_Cabin_Roof", -ZFightEpsilon);
        }

        /// <summary>Cabin.fbx bakes its own door leaf, SM_Door, into the wall
        /// shell — but MemorySceneBuilderV2 separately instantiates
        /// Door_v2.prefab at the same doorway (CabinV2Builder.DoorHingePosition)
        /// and renames it Prop_FrontDoor_Locked. That instance is the real,
        /// functional door: it carries DoorInteractable, CutsceneStage.
        /// SwingFrontDoor animates it, and MemorySceneDressing/MemorySceneWiring
        /// look it up by name. SM_Door does none of that — it's a second,
        /// purely visual leaf sitting exactly where the real one is, and the
        /// two exactly-coincident meshes z-fight every time the doorway is in
        /// view. Disabled here the same way SwapOneFurnitureObject disables
        /// the blockout furniture it replaces, rather than deleted, so the
        /// object (and its WallHeightObjects scale) still exists if anyone
        /// needs it for reference.</summary>
        private static void DisableDuplicateDoorLeaf(GameObject cabin)
        {
            Transform door = cabin.transform.Find("SM_Door");
            if (door == null)
            {
                Debug.LogWarning("[CabinV2Builder] SM_Door not found on Cabin_v2 — duplicate-leaf disable skipped.");
                return;
            }
            door.gameObject.SetActive(false);
        }

        private static void ScaleHeight(GameObject cabin, string objectName, float scale)
        {
            Transform t = cabin.transform.Find(objectName);
            if (t == null)
            {
                Debug.LogWarning($"[CabinV2Builder] {objectName} not found on Cabin_v2 — room-height scale skipped for it.");
                return;
            }
            Vector3 s = t.localScale;
            t.localScale = new Vector3(s.x, s.y, s.z * scale);
        }

        private static void RaiseObject(GameObject cabin, string objectName, float rise)
        {
            Transform t = cabin.transform.Find(objectName);
            if (t == null)
            {
                Debug.LogWarning($"[CabinV2Builder] {objectName} not found on Cabin_v2 — room-height rise skipped for it.");
                return;
            }
            t.localPosition += new Vector3(0f, rise, 0f);
        }

        private const string ChimneyExtensionName = "SM_Fireplace_ChimneyExt";

        // Measured off SM_Fireplace_Brick's own top face in the built scene
        // (12 verts at y >= 2.69), not copied from the README: the breast is a
        // constant-section box, x [-0.5, 0.5] by z [4.3, 5.0].
        private const float ChimneyBreastMinX = -0.5f;
        private const float ChimneyBreastMaxX = 0.5f;
        private const float ChimneyBreastMinZ = 4.3f;
        private const float ChimneyBreastMaxZ = 5.0f;

        /// <summary>Bridges the chimney breast up to the raised ceiling.
        ///
        /// The fireplace is deliberately NOT scaled with the walls. Its four
        /// objects are interlocking: SM_Fireplace_Brick carries the firebox
        /// opening, SM_Fireplace_Stone's rim frames that exact opening,
        /// SM_Fireplace_Wood lines the cavity behind it, and the firewood pile
        /// sits on the hearth — a vertical scale stretches the opening and the
        /// three pieces have to be stretched in lockstep to keep up. Worse,
        /// MemorySceneDressing seats Prop_Radio and Prop_MantelClock on the
        /// mantel shelf at a measured y 1.380 and derives each prop's y from
        /// its own model bounds against that number; scaling the stone would
        /// lift the shelf out from under both of them. Scaling the whole
        /// fireplace also stretches the log cylinders, which read as squashed.
        ///
        /// So the breast keeps its authored geometry and a plain brick box
        /// fills the band it no longer reaches. Same technique
        /// MemorySceneBuilderV2 already uses for the woodshed snow cap.</summary>
        private static void BuildChimneyExtension(GameObject cabin)
        {
            Transform existing = cabin.transform.Find(ChimneyExtensionName);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            if (CeilingRise <= 0.0001f) return;

            GameObject ext = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ext.name = ChimneyExtensionName;
            ext.transform.SetParent(cabin.transform, false);

            // A primitive cube has NO baked import rotation, so unlike every
            // FBX object around it this one's local Y really is world up.
            ext.transform.localPosition = new Vector3(
                (ChimneyBreastMinX + ChimneyBreastMaxX) * 0.5f,
                AuthoredCeilingHeight + CeilingRise * 0.5f,
                (ChimneyBreastMinZ + ChimneyBreastMaxZ) * 0.5f);
            ext.transform.localRotation = Quaternion.identity;
            // Height GROWN by ZFightEpsilon, scaled from the (unmoved) center
            // set above — pushes the top and bottom faces out by half that
            // each, past the ceiling underside and the brick top respectively,
            // so this filler overlaps both neighbours by a hair instead of
            // touching either exactly (see ZFightEpsilon's doc). This is a
            // bridge piece meeting a DIFFERENT object at each end, unlike the
            // wall/roof nudges above — shrinking it would pull it back from
            // both and open a visible gap at both seams instead of closing
            // either.
            ext.transform.localScale = new Vector3(
                ChimneyBreastMaxX - ChimneyBreastMinX,
                CeilingRise + ZFightEpsilon,
                ChimneyBreastMaxZ - ChimneyBreastMinZ);

            Material brick = LoadMaterial("M_Brick_Red");
            Renderer renderer = ext.GetComponent<Renderer>();
            if (renderer != null && brick != null) renderer.sharedMaterial = brick;

            // CreatePrimitive already gives it a BoxCollider, which is what the
            // rest of the fireplace's MeshColliders would give here anyway on a
            // shape this simple — the player can't walk into the chimney.
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
            // copied, because the two originals disagree about where their own
            // origin sits — SM_Chair_0X's is at its base (y 0), SM_Table's is
            // at its mid-height (y 0.725) — while both PolyHaven models are
            // base-origin. The cabin floor is y 0 (every SM_Chair_0X sits at
            // exactly 0).
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
            // it generically.
            old.gameObject.SetActive(false);

            // See SpawnModelProp's doc for the shared recipe and the traps it
            // exists to avoid (preserving the visual's own imported
            // localRotation/localScale, measuring bounds after the rescale,
            // multiplying rather than replacing importedScale).
            SpawnModelProp(instance, newName, modelSource, material, position, rotation, targetHeight);
        }

        /// <summary>Shared core of the "spawn a real model in place of a
        /// blockout, or as new decoration" recipe. Extracted from what used to
        /// be SwapOneFurnitureObject's entire body so the furniture swap above
        /// and the cozy decor pass below (BuildSourcedDecor) share one
        /// implementation of three traps that used to live only in that
        /// method's comments:
        ///  - the visual's OWN imported localRotation/localScale must be
        ///    preserved, not replaced (export_furniture.py-style exports
        ///    survive their Z-up -> Y-up conversion as a baked rotation on the
        ///    imported root rather than folding it into the vertices —
        ///    assigning Quaternion.identity here laid every table and stool on
        ///    its face before this was caught);
        ///  - bounds must be measured with the visual's rotation/scale already
        ///    in place, and again after any rescale, or the collider is built
        ///    from the wrong size;
        ///  - a height correction must MULTIPLY importedScale, never replace
        ///    it, or a non-uniform imported scale collapses.
        ///
        /// targetHeight &lt;= 0 skips the rescale — the CC0 models under
        /// Art/Sourced/ are already real-world sized ("grounded prefab" per
        /// Art/PolyHaven/README.md), unlike the furniture swap above where a
        /// blockout dictated the target size.
        ///
        /// `position.y` is always treated as the exact height the model's
        /// BASE should rest at, not merely copied onto the transform: after
        /// any rescale, the visual is nudged so its measured bounds.min.y
        /// lands there. This works identically for a floor prop (position.y
        /// 0), a tabletop prop (0.75), or a mantel/shelf prop (measured shelf-
        /// top height) without needing the caller to know or trust whether the
        /// source FBX's pivot is exactly at its base.
        ///
        /// addCollider false skips the BoxCollider — appropriate for small
        /// tabletop/mantel/shelf dressing the player cannot reach into.
        ///
        /// Idempotent (destroys and rebuilds `name` if present), so it is safe
        /// to call from a method that runs once per BuildCabinPrefab rather
        /// than only from that always-fresh FBX instance.</summary>
        private static GameObject SpawnModelProp(GameObject cabin, string name, GameObject modelSource,
            Material material, Vector3 position, Quaternion rotation,
            float targetHeight = 0f, bool addCollider = true)
        {
            Transform existing = cabin.transform.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            if (modelSource == null)
            {
                Debug.LogWarning($"[CabinV2Builder] {name}'s model source is missing — skipping.");
                return null;
            }

            GameObject replacement = new GameObject(name);
            replacement.transform.SetParent(cabin.transform, false);
            replacement.transform.SetPositionAndRotation(position, rotation);

            GameObject visual = (GameObject)PrefabUtility.InstantiatePrefab(modelSource, replacement.transform);
            visual.name = "Visual";
            visual.transform.localPosition = Vector3.zero;
            Vector3 importedScale = visual.transform.localScale;

            if (targetHeight > 0f)
            {
                // Measured with the imported rotation/scale already in place,
                // so the height read here is the model's real upright height
                // and the correction below multiplies importedScale rather
                // than replacing it.
                Bounds rawBounds = ComputeWorldRelativeLocalBounds(visual, replacement.transform);
                float rawHeight = rawBounds.size.y;
                if (rawHeight > 0.001f)
                {
                    visual.transform.localScale = importedScale * (targetHeight / rawHeight);
                }
                else
                {
                    Debug.LogWarning($"[CabinV2Builder] {name}'s model has near-zero measured height — leaving scale at 1.");
                }
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

            // Ground the visual's measured base at exactly position.y, rather
            // than trusting the source FBX's pivot to already be there — self-
            // correcting for both a floor prop (position.y 0, a no-op if the
            // pivot really is base-aligned) and a tabletop/mantel/shelf prop
            // (position.y the target surface height).
            Bounds measuredBounds = ComputeWorldRelativeLocalBounds(visual, replacement.transform);
            float currentBaseWorldY = replacement.transform.TransformPoint(
                new Vector3(0f, measuredBounds.min.y, 0f)).y;
            visual.transform.position += new Vector3(0f, position.y - currentBaseWorldY, 0f);
            measuredBounds = ComputeWorldRelativeLocalBounds(visual, replacement.transform);

            if (addCollider)
            {
                BoxCollider collider = replacement.AddComponent<BoxCollider>();
                collider.center = measuredBounds.center;
                collider.size = Vector3.Max(measuredBounds.size, new Vector3(0.05f, 0.05f, 0.05f));
            }

            Debug.Log($"[CabinV2Builder] {name} measured size {measuredBounds.size:F3} at {replacement.transform.position:F3}.");
            return replacement;
        }

        private static GameObject LoadSourcedModel(string id)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{SourcedRoot}{id}/{id}_2k.fbx");
        }

        private static Material LoadSourcedMaterial(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>($"{SourcedRoot}Materials/{name}_Sourced.mat");
        }

        // ---- Cozy decor pass (Aug 2026) ----------------------------------
        //
        // Everything below adds furniture accents, two rugs and three framed
        // wall pieces to the otherwise-bare interior. All coordinates were
        // checked against the room's measured occupancy (table, chairs, sofa
        // sweep, fireplace, stairs, shoes, coat hanger, and the props
        // MemorySceneDressing adds later) before being written down here —
        // see Cabin_v2/README.md's "Decor pass" section for that collision
        // budget and the full coordinate table with clearances.

        private const float MantelShelfHeight = 1.380f;

        private static void BuildFloorRugs(GameObject cabin)
        {
            Material hearthRug = LoadMaterial("M_Rug_Oxblood_Hearth");
            Material diningRug = LoadMaterial("M_Rug_Oxblood_Dining");

            // Hearth rug: clear of the sofa's swept footprint, the fireplace
            // hearth slab (z >= 3.84), and the armchair below.
            BuildRug(cabin, "Decor_Rug_Hearth", new Vector3(0.35f, 0f, 2.35f), 2.7f, 1.8f, hearthRug);
            // Dining rug: covers the table and all six stools with margin.
            BuildRug(cabin, "Decor_Rug_Dining", new Vector3(-3.0f, 0f, 2.30f), 3.6f, 3.6f, diningRug);
        }

        private const float RugThickness = 0.012f;

        private static void BuildRug(GameObject cabin, string name, Vector3 floorCenter,
            float sizeX, float sizeZ, Material material)
        {
            Transform existing = cabin.transform.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject rug = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rug.name = name;
            rug.transform.SetParent(cabin.transform, false);
            // A primitive cube has NO baked FBX import rotation (same fact
            // BuildChimneyExtension's doc states for the chimney box), so
            // plain Y-up math is correct here, unlike everywhere else in this
            // file that touches shell/furniture geometry.
            //
            // Centred RugThickness/2 above the floor, MINUS ZFightEpsilon, so
            // the underside buries a hair into the floor slab instead of
            // sitting bit-coincident with it (see ZFightEpsilon's doc for why
            // exactly-touching faces z-fight).
            rug.transform.localPosition =
                new Vector3(floorCenter.x, RugThickness * 0.5f - ZFightEpsilon, floorCenter.z);
            rug.transform.localRotation = Quaternion.identity;
            rug.transform.localScale = new Vector3(sizeX, RugThickness, sizeZ);

            // Nothing should physically collide with a rug.
            UnityEngine.Object.DestroyImmediate(rug.GetComponent<BoxCollider>());
            Renderer renderer = rug.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        /// <summary>Yaw (degrees, world Y) that turns a fresh GameObject's
        /// default forward (+Z) to face `to` from `from`. Only valid for
        /// objects with no baked FBX import rotation — SpawnModelProp's
        /// `replacement` parent qualifies, Cabin.fbx children do not (see the
        /// (270,0,0) trap documented on DoorClosedRotation).</summary>
        private static Quaternion FacingYaw(Vector3 from, Vector3 to)
        {
            Vector3 toward = Vector3.ProjectOnPlane(to - from, Vector3.up);
            return toward.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(toward.normalized, Vector3.up)
                : Quaternion.identity;
        }

        /// <summary>Five free CC0 props (the other four already in
        /// Art/Sourced/Cabin/ are reserved for a future MemorySceneDressing
        /// retarget, or deferred outright — see Art/Sourced/README.md's "Why
        /// three props are unplaced" section) plus three Poly Haven purchases
        /// made for this pass (Shelf_01, its books, and a vase).</summary>
        private static void BuildSourcedDecor(GameObject cabin)
        {
            Vector3 fireplaceAnchor = new Vector3(0f, 0f, 4.3f);
            Vector3 armChairPosition = new Vector3(2.45f, 0f, 2.75f);

            SpawnModelProp(cabin, "Decor_ArmChair",
                LoadSourcedModel("ArmChair_01"), LoadSourcedMaterial("ArmChair"),
                armChairPosition, FacingYaw(armChairPosition, fireplaceAnchor));

            SpawnModelProp(cabin, "Decor_Cabinet",
                LoadSourcedModel("vintage_cabinet_01"), LoadSourcedMaterial("VintageCabinet"),
                new Vector3(-3.6f, 0f, 4.70f), Quaternion.Euler(0f, 180f, 0f));

            SpawnModelProp(cabin, "Decor_LogBasket",
                LoadSourcedModel("wicker_basket_01"), LoadSourcedMaterial("WickerBasket"),
                new Vector3(1.45f, 0f, 4.30f), Quaternion.Euler(0f, 20f, 0f));

            // Lantern and candlestick are dressing on top of an existing
            // surface, not floor furniture — addCollider: false, since a box
            // collider around a 0.3 m lantern/candlestick on a tabletop the
            // player can't reach into is a pure snag hazard.
            SpawnModelProp(cabin, "Decor_TableLantern",
                LoadSourcedModel("wooden_lantern_01"), LoadSourcedMaterial("WoodenLantern"),
                new Vector3(-3.05f, TableTargetHeight, 3.22f), Quaternion.Euler(0f, 15f, 0f),
                addCollider: false);

            SpawnModelProp(cabin, "Decor_MantelCandle",
                LoadSourcedModel("wooden_candlestick"), LoadSourcedMaterial("WoodenCandlestick"),
                new Vector3(0.72f, MantelShelfHeight, 3.925f), Quaternion.identity,
                addCollider: false);

            GameObject shelf = SpawnModelProp(cabin, "Decor_Shelf",
                LoadSourcedModel("Shelf_01"), LoadMaterial("M_Wood_Shelf"),
                new Vector3(0.60f, 0f, -4.72f), Quaternion.identity);

            // Books and vase sit on the shelf's own top board — measured off
            // its rebuilt bounds rather than guessed, same doctrine
            // SpawnModelProp's grounding step already applies to every prop.
            float shelfTopY = shelf != null
                ? ComputeWorldRelativeLocalBounds(shelf, cabin.transform).max.y
                : 1.1f;

            SpawnModelProp(cabin, "Decor_Books",
                LoadSourcedModel("book_encyclopedia_set_01"), LoadMaterial("M_Books_Encyclopedia"),
                new Vector3(0.42f, shelfTopY, -4.70f), Quaternion.identity,
                addCollider: false);

            SpawnModelProp(cabin, "Decor_Vase",
                LoadSourcedModel("ceramic_vase_01"), LoadMaterial("M_Ceramic_Vase"),
                new Vector3(0.90f, shelfTopY, -4.70f), Quaternion.identity,
                addCollider: false);
        }

        private const float FrameBorder = 0.06f;
        private const float FrameDepth = 0.05f;

        /// <summary>Six framed pieces across the two walls with clear
        /// picture-height space — the -Z wall has the window and coat hanger,
        /// and the +Z wall is the fireplace, so neither works.
        ///
        /// **-X wall** (x=-5.0, yaw 90 -> local +Z points +X into the room):
        /// only exists for z &gt;= -2.5 north of the door's 45-degree chamfer —
        /// the first three panels sit well inside that.
        ///
        /// **+X wall** (x=+5.0, yaw -90 -> local +Z points -X into the room):
        /// the stairs (SM_Cabin_Stairs, x in [3.90, 5.00]) only occupy
        /// z in [-1.43, 3.85] — z in [-5.0, -1.43] is a full 3.25 m of clean,
        /// unbroken wall with flat ceiling overhead the whole way (the stairs
        /// climb in +Z, away from this stretch, so there's no rake-headroom
        /// loss here). The second trio of panels goes there, kept at
        /// z &lt;= -2.35 to clear both `Blocker_Stairs` and the `Warn_Stairs`
        /// trigger (MemorySceneDressing.cs) with margin.
        ///
        /// No CC0 painting/canvas/tapestry texture exists on Poly Haven or
        /// ambientCG (checked twice, across two passes) — panels reuse the
        /// project's own unused snowy_forest HDRI as a landscape print
        /// instead (M_Art_Landscape_A/B, two different crops of the same
        /// file), and the rug's carpet texture as a woven wall hanging
        /// (M_Textile_WallHanging). The +X trio deliberately reorders which
        /// material lands where so no two frames on the same wall repeat
        /// back-to-back.</summary>
        private static void BuildWallArt(GameObject cabin)
        {
            Material landscapeA = LoadMaterial("M_Art_Landscape_A");
            Material landscapeB = LoadMaterial("M_Art_Landscape_B");
            Material hanging = LoadMaterial("M_Textile_WallHanging");

            BuildFramedPanel(cabin, "Decor_WallArt_01",
                new Vector3(-5.0f, WallHeight(1.80f), 1.15f), 90f, 0.66f, 0.50f, landscapeA);
            BuildFramedPanel(cabin, "Decor_WallArt_02",
                new Vector3(-5.0f, WallHeight(1.86f), 2.35f), 90f, 0.86f, 0.62f, hanging);
            BuildFramedPanel(cabin, "Decor_WallArt_03",
                new Vector3(-5.0f, WallHeight(1.80f), 3.55f), 90f, 0.66f, 0.50f, landscapeB);

            // Stairwell gallery, +X wall, south of the stair foot.
            BuildFramedPanel(cabin, "Decor_WallArt_04",
                new Vector3(5.0f, WallHeight(1.80f), -4.55f), -90f, 0.66f, 0.50f, landscapeB);
            BuildFramedPanel(cabin, "Decor_WallArt_05",
                new Vector3(5.0f, WallHeight(1.86f), -3.45f), -90f, 0.86f, 0.62f, hanging);
            BuildFramedPanel(cabin, "Decor_WallArt_06",
                new Vector3(5.0f, WallHeight(1.80f), -2.35f), -90f, 0.66f, 0.50f, landscapeA);
        }

        /// <summary>Five cubes (a panel plus four frame rails), not a quad —
        /// a Quad is single-sided, has no thickness (so it must float, which
        /// is exactly the coplanar-face case ZFightEpsilon exists to avoid),
        /// and casts shadows badly. Local +Z is the outward wall normal here
        /// — `wallPoint`'s parent carries no baked FBX import rotation (a
        /// fresh GameObject, not a Cabin.fbx child), so plain Y-up math is
        /// correct, unlike everywhere else in this file that touches shell
        /// geometry.
        ///
        /// Every part below OVERLAPS its neighbour in depth rather than
        /// abutting it exactly: the panel's back buries into the wall, and
        /// the four rails start behind the panel's front face and overlap
        /// each other by a few millimetres at the corners — intersecting
        /// solids can't z-fight, touching ones can (ZFightEpsilon's doc).</summary>
        private static GameObject BuildFramedPanel(GameObject cabin, string name, Vector3 wallPoint,
            float wallYawDegrees, float width, float height, Material panelMaterial)
        {
            Transform existing = cabin.transform.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);

            GameObject parent = new GameObject(name);
            parent.transform.SetParent(cabin.transform, false);
            parent.transform.localPosition = wallPoint;
            parent.transform.localRotation = Quaternion.Euler(0f, wallYawDegrees, 0f);

            Material frameMaterial = LoadMaterial("M_Wood_Frame");

            CreatePanelPart(parent.transform, "Panel",
                new Vector3(width - 2f * FrameBorder + 0.02f, height - 2f * FrameBorder + 0.02f, 0.03f),
                new Vector3(0f, 0f, 0.012f), panelMaterial);
            CreatePanelPart(parent.transform, "Rail_Top",
                new Vector3(width, FrameBorder, FrameDepth),
                new Vector3(0f, (height - FrameBorder) * 0.5f, 0.030f), frameMaterial);
            CreatePanelPart(parent.transform, "Rail_Bottom",
                new Vector3(width, FrameBorder, FrameDepth),
                new Vector3(0f, -(height - FrameBorder) * 0.5f, 0.030f), frameMaterial);
            CreatePanelPart(parent.transform, "Stile_Left",
                new Vector3(FrameBorder, height - 2f * FrameBorder + 0.004f, FrameDepth),
                new Vector3(-(width - FrameBorder) * 0.5f, 0f, 0.030f), frameMaterial);
            CreatePanelPart(parent.transform, "Stile_Right",
                new Vector3(FrameBorder, height - 2f * FrameBorder + 0.004f, FrameDepth),
                new Vector3((width - FrameBorder) * 0.5f, 0f, 0.030f), frameMaterial);

            return parent;
        }

        private static void CreatePanelPart(Transform parent, string name, Vector3 size,
            Vector3 localPosition, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = Quaternion.identity;
            part.transform.localScale = size;

            // Flush against the wall / the panel's own neighbours — nothing
            // here should be a physical obstacle.
            UnityEngine.Object.DestroyImmediate(part.GetComponent<BoxCollider>());
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        /// <summary>Retires the four blue-metal-plate `BO_Shoes_0X` blockouts
        /// (SetActive(false), same non-destructive convention
        /// SwapOneFurnitureObject uses for SM_Table/SM_Chair_0X) and builds a
        /// wood shoe rack in the same spot — x in [-5.0, -4.2], z in
        /// [-2.5, +0.5], the entryway pocket the shoes already occupied,
        /// completely clear of the dining rug (z >= 0.5), the wall art
        /// (z >= 0.82) and the door's swing arc (x in [-4.18, -2.46], which
        /// this rack's x range [-5.0, -4.65] never reaches). No shoe-rack
        /// asset exists anywhere in the project (Poly Haven, ambientCG, the
        /// gitignored TDG pack all checked) — built from primitives the same
        /// way BuildFramedPanel/BuildChimneyExtension are, reusing
        /// CreatePanelPart.
        ///
        /// Boots come from `rubber_boots.fbx`, already imported under
        /// Art/PolyHaven/ specifically for this (its own README: "Four pairs
        /// of cabin footwear") but never wired to any builder — this finishes
        /// that. The FBX itself bundles TWO pairs already (a clean pair and a
        /// dirty-variant pair, four individual boot meshes side by side,
        /// confirmed by instantiating it and reading each child renderer:
        /// `rubber_boots_l/r_LOD0` + `rubber_boots_dirty_l/dirt_r_LOD0`, each
        /// 0.091 x 0.373 x 0.266 m, spanning ~0.89 m across) — one instance
        /// per shelf is the whole set, not one boot each.</summary>
        private static void BuildShoeRack(GameObject cabin)
        {
            foreach (string shoeName in new[] { "BO_Shoes_01", "BO_Shoes_02", "BO_Shoes_03", "BO_Shoes_04" })
            {
                Transform shoe = cabin.transform.Find(shoeName);
                if (shoe == null)
                {
                    Debug.LogWarning($"[CabinV2Builder] {shoeName} not found on Cabin_v2 — shoe-rack retire skipped for it.");
                    continue;
                }
                shoe.gameObject.SetActive(false);
            }

            const string rackName = "Decor_ShoeRack";
            Transform existingRack = cabin.transform.Find(rackName);
            if (existingRack != null) UnityEngine.Object.DestroyImmediate(existingRack.gameObject);

            const float depth = 0.35f;   // x in [-5.0, -4.65]
            const float length = 2.2f;   // z in [-2.45, -0.25]
            const float height = 0.75f;
            const float boardThickness = 0.03f;
            const float bottomShelfTopY = 0.20f;
            const float topShelfTopY = 0.55f;

            Material shelfMaterial = LoadMaterial("M_Wood_Shelf");

            GameObject rack = new GameObject(rackName);
            rack.transform.SetParent(cabin.transform, false);
            // A fresh GameObject, not a Cabin.fbx child — no baked import
            // rotation, so plain Y-up math is correct (same situation as
            // BuildFramedPanel's parent). Origin at floor level, centred on
            // the rack's own footprint.
            rack.transform.localPosition = new Vector3(-5.0f + depth * 0.5f, 0f, -1.35f);
            rack.transform.localRotation = Quaternion.identity;

            CreatePanelPart(rack.transform, "Back",
                new Vector3(boardThickness, height, length),
                new Vector3(-depth * 0.5f + boardThickness * 0.5f, height * 0.5f, 0f), shelfMaterial);
            CreatePanelPart(rack.transform, "End_A",
                new Vector3(depth, height, boardThickness),
                new Vector3(0f, height * 0.5f, -length * 0.5f), shelfMaterial);
            CreatePanelPart(rack.transform, "End_B",
                new Vector3(depth, height, boardThickness),
                new Vector3(0f, height * 0.5f, length * 0.5f), shelfMaterial);
            CreatePanelPart(rack.transform, "Shelf_Bottom",
                new Vector3(depth, boardThickness, length),
                new Vector3(0f, bottomShelfTopY - boardThickness * 0.5f, 0f), shelfMaterial);
            CreatePanelPart(rack.transform, "Shelf_Top",
                new Vector3(depth, boardThickness, length),
                new Vector3(0f, topShelfTopY - boardThickness * 0.5f, 0f), shelfMaterial);

            // CreatePanelPart destroys each part's own collider (right for
            // wall-flush frame pieces with nothing to collide with) — but
            // this rack sits out in the entryway where the player walks, so
            // it needs one collider of its own, unlike BuildFramedPanel.
            BoxCollider rackCollider = rack.AddComponent<BoxCollider>();
            rackCollider.center = new Vector3(0f, height * 0.5f, 0f);
            rackCollider.size = new Vector3(depth, height, length);

            Material bootsMaterial = LoadMaterial("M_Rubber_Boots");
            GameObject bootsSource = AssetDatabase.LoadAssetAtPath<GameObject>(RubberBootsFbxPath);

            float rackWorldX = rack.transform.localPosition.x;
            float rackWorldZ = rack.transform.localPosition.z;

            // The FBX's own long axis (~0.89 m, the two pairs side by side)
            // is native local X — yawed 90 degrees here so that axis lies
            // along the shelf's length (world Z, 2.2 m of room) instead of
            // its depth (world X, only 0.35 m — the un-rotated bundle would
            // hang off the front of the shelf).
            SpawnModelProp(cabin, "Decor_Boots_Bottom", bootsSource, bootsMaterial,
                new Vector3(rackWorldX, bottomShelfTopY, rackWorldZ), Quaternion.Euler(0f, 90f, 0f),
                addCollider: false);
            SpawnModelProp(cabin, "Decor_Boots_Top", bootsSource, bootsMaterial,
                new Vector3(rackWorldX, topShelfTopY, rackWorldZ), Quaternion.Euler(0f, 90f, 0f),
                addCollider: false);
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

            // Same stretched doorway the in-shell SM_Door leaf fills (see
            // WallHeightScale): this prefab is the one MemorySceneBuilderV2
            // places as Prop_FrontDoor_Locked, and at the authored 2.10 m it
            // leaves 0.27 m of daylight over the head. Local Z is world up
            // here too — the root carries DoorClosedRotation's baked
            // (270, 0, 0) — and the BoxCollider assigned just above is built
            // from unscaled mesh bounds, so it follows this scale for free.
            Vector3 doorScale = instance.transform.localScale;
            instance.transform.localScale =
                new Vector3(doorScale.x, doorScale.y, doorScale.z * WallHeightScale);

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
