using System;
using System.Collections.Generic;
using FalsePositive.CabinNight;
using FalsePositive.Core;
using FalsePositive.Player;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Rebuilds the authored cast for Nobody Went Out's 00:50 cabin scene.
    /// The imported o3n bodies are used directly so the level remains usable
    /// when the optional UMA package is not installed.
    /// </summary>
    public static class CabinNightCharacterBuilder
    {
        private const string MaterialRoot = "Assets/_Project/CabinNight/Materials/";
        private const string PrefabRoot = "Assets/_Project/CabinNight/Prefabs/";

        private const string MaleBody = "Assets/o3n/o3nBaseUMARaces/Races/o3nMaleRace/FBX/o3nMale_unified.fbx";
        private const string FemaleBody = "Assets/o3n/o3nBaseUMARaces/Races/o3nFemaleRace/FBX/o3nFemale_unified.fbx";
        private const string MaleHoodie = "Assets/o3n/o3nBaseUMARaces/Content/Clothing/o3n_male_hoodie_01/o3n_male_hoodie_01_LOD0/o3n_male_hoodie_01_LOD0_Skinned.prefab";
        private const string FemaleShirt = "Assets/o3n/o3nBaseUMARaces/Content/Clothing/o3n_female_shirt_03/o3n_female_shirt_03_LOD0/o3n_female_shirt_03_LOD0_Skinned.prefab";
        private const string FemaleDress = "Assets/o3n/o3nBaseUMARaces/Content/Clothing/o3n_female_dress_01/o3n_female_dress_01_LOD0/o3n_female_dress_01_LOD0_Skinned.prefab";
        private const string MaleHair = "Assets/o3n/o3nBaseUMARaces/Content/Hair/o3n_male_hair_02/o3n_male_hair_02_LOD0/o3n_male_hair_02_LOD0_Skinned.prefab";
        private const string MaleMilitaryHair = "Assets/o3n/o3nBaseUMARaces/Content/Hair/o3n_male_military_hair_01/o3n_male_military_hair_01_LOD0/o3n_male_military_hair_01_LOD0_Skinned.prefab";
        private const string FemaleLongHair = "Assets/o3n/o3nBaseUMARaces/Content/Hair/o3n_female_longhair_01/o3n_female_longhair_01_LOD0/o3n_female_longhair_01_LOD0_Skinned.prefab";
        private const string FemalePonytail = "Assets/o3n/o3nBaseUMARaces/Content/Hair/o3n_female_hair_02_ponytail/o3n_female_hair_02_ponytail_LOD0/o3n_female_hair_02_ponytail_LOD0_Skinned.prefab";
        private const string MaleShoes = "Assets/o3n/o3nBaseUMARaces/Content/Shoes/o3n_male_shoes_01/o3n_male_shoes_01_LOD0/o3n_male_shoes_01_LOD0_Skinned.prefab";
        private const string FemaleShoes = "Assets/o3n/o3nBaseUMARaces/Content/Shoes/o3n_female_shoes_01/o3n_female_shoes_01_LOD0/o3n_female_shoes_01_LOD0_Skinned.prefab";

        /// <summary>
        /// MemorySceneBuilderV2's cast builder for the Cabin_v2 shell. Does
        /// not open/save a scene itself — the caller already has the scene
        /// open and saves once at the end of its own build pass, so this
        /// only mutates the given Characters root in place.
        ///
        /// Per the plan's Phase 2b staging: M1_Night only needs the player
        /// (seated at SM_Chair_05) and Priya (asleep — reuses the Sofa, no
        /// separate armchair model exists in Cabin_v2) visibly present; Nick
        /// is already outside and Aaron/Ivy are upstairs per STORY_SCRIPT.md
        /// §4, so those three are built but start inactive (Phase 4's
        /// CutsceneStage re-enables them for later beats that need them, e.g.
        /// "TheyComeDown"). M2_Morning stages everyone per §4: player on the
        /// sofa, Priya at the window, Aaron/Ivy at the top of the landing.
        /// </summary>
        public static void BuildCastInScene(Transform charactersRoot, bool isMorning)
        {
            while (charactersRoot.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(charactersRoot.GetChild(0).gameObject);
            }

            GameObject player = isMorning
                ? BuildCharacter(charactersRoot, "Player (Male - First Person)", false,
                    new Vector3(0.75f, 0f, 0.25f), new Vector3(0f, -90f, 0f), 0.98f,
                    Material("MaleBodyJeans"), CabinIdleProfile.Controlled, null, null, null, null, null)
                : BuildCharacter(charactersRoot, "Player (Male - First Person)", false,
                    new Vector3(-3.0f, 0f, 0.85f), new Vector3(0f, 0f, 0f), 0.98f,
                    Material("MaleBodyJeans"), CabinIdleProfile.Controlled, null, null, null, null, null);
            ConfigurePlayer(player);
            ConvertToRouterRig(player);
            SaveCharacter(player, "Player_FirstPerson");

            GameObject nick = BuildCharacter(charactersRoot, "Nick Vlahos (Male)", false,
                new Vector3(2.3f, 0f, -6.3f), new Vector3(0f, 20f, 0f), 0.98f,
                Material("MaleBodyJeansShirt"), CabinIdleProfile.Confrontational,
                null, null, MaleHair, Material("HairBrown"), MaleShoes);
            SaveCharacter(nick, "Nick_Vlahos");

            GameObject aaron = isMorning
                ? BuildCharacter(charactersRoot, "Aaron Teague (Male)", false,
                    new Vector3(4.3f, 2.7f, 3.4f), new Vector3(0f, 226f, 0f), 1.02f,
                    Material("MaleBodyJeans"), CabinIdleProfile.Controlled,
                    MaleHoodie, Material("HoodieGray"), MaleMilitaryHair, Material("HairDark"), MaleShoes)
                : BuildCharacter(charactersRoot, "Aaron Teague (Male)", false,
                    new Vector3(4.3f, 2.7f, 3.4f), new Vector3(0f, 226f, 0f), 1.02f,
                    Material("MaleBodyJeans"), CabinIdleProfile.Controlled,
                    MaleHoodie, Material("HoodieGray"), MaleMilitaryHair, Material("HairDark"), MaleShoes);
            SaveCharacter(aaron, "Aaron_Teague");

            GameObject ivy = BuildCharacter(charactersRoot, "Ivy Teague (Female)", true,
                new Vector3(4.6f, 2.7f, 3.0f), new Vector3(0f, 248f, 0f), 0.98f,
                Material("FemaleBodyJeans"), CabinIdleProfile.Guarded,
                FemaleShirt, Material("IvyYellowShirt"), FemaleLongHair, Material("HairBlack"), FemaleShoes);
            SaveCharacter(ivy, "Ivy_Teague");

            GameObject priya = isMorning
                ? BuildCharacter(charactersRoot, "Priya Raman (Female)", true,
                    new Vector3(2.3f, 0f, -4.4f), new Vector3(0f, 0f, 0f), 0.96f,
                    Material("FemaleBodyJeans"), CabinIdleProfile.Panicked,
                    FemaleDress, Material("PriyaDress"), FemalePonytail, Material("HairBlack"), FemaleShoes)
                : BuildCharacter(charactersRoot, "Priya Raman (Female)", true,
                    new Vector3(0.75f, 0f, -0.5f), new Vector3(0f, 30f, 0f), 0.96f,
                    Material("FemaleBodyJeans"), CabinIdleProfile.Sleeping,
                    FemaleDress, Material("PriyaDress"), FemalePonytail, Material("HairBlack"), FemaleShoes);
            SaveCharacter(priya, "Priya_Raman");

            if (!isMorning)
            {
                // Not physically present in M1_Night's staging — Nick is
                // already outside, Aaron/Ivy are upstairs (blocked stairs).
                // Phase 4's CutsceneStage re-enables whichever it needs.
                nick.SetActive(false);
                aaron.SetActive(false);
                ivy.SetActive(false);
            }
        }

        private static Material Material(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Material>(MaterialRoot + name + ".mat");
        }

        private static GameObject InstantiateAsset(string path)
        {
            GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null)
            {
                throw new InvalidOperationException("Missing cabin character asset: " + path);
            }

            return (GameObject)PrefabUtility.InstantiatePrefab(asset);
        }

        private static GameObject BuildCharacter(
            Transform parent,
            string name,
            bool female,
            Vector3 position,
            Vector3 eulerAngles,
            float scale,
            Material bodyMaterial,
            CabinIdleProfile profile,
            string clothingPath,
            Material clothingMaterial,
            string hairPath,
            Material hairMaterial,
            string shoesPath)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = position;
            root.transform.eulerAngles = eulerAngles;
            root.transform.localScale = Vector3.one * scale;

            GameObject body = InstantiateAsset(female ? FemaleBody : MaleBody);
            body.name = "Body";
            body.transform.SetParent(root.transform, false);
            AssignBodyMaterials(body, female, bodyMaterial);
            ApplyPose(body, profile);

            Dictionary<string, Transform> bodyBones = BuildBoneMap(body);
            AddAccessory(root, bodyBones, clothingPath, "Clothing", clothingMaterial);
            AddAccessory(root, bodyBones, hairPath, "Hair", hairMaterial);
            AddAccessory(root, bodyBones, shoesPath, "Shoes", Material("Shoes"));

            if (!name.StartsWith("Player", StringComparison.Ordinal))
            {
                CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
                collider.height = 1.72f;
                collider.radius = 0.27f;
                collider.center = new Vector3(0f, 0.86f, 0f);
                CabinCharacterIdle idle = root.AddComponent<CabinCharacterIdle>();
                idle.Configure(profile, Math.Abs(name.GetHashCode() % 1000) / 1000f);
                // CutsceneStage (Phase 4) drives this NPC's movement/pose
                // during cutscene beats — see Cutscene.ScriptedActor.
                root.AddComponent<FalsePositive.Cutscene.ScriptedActor>();
            }

            return root;
        }

        private static void ConfigurePlayer(GameObject player)
        {
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            }

            CharacterController controller = player.AddComponent<CharacterController>();
            controller.height = 1.76f;
            controller.radius = 0.28f;
            controller.center = new Vector3(0f, 0.88f, 0f);
            controller.stepOffset = 0.28f;
            controller.slopeLimit = 48f;

            GameObject view = new GameObject("FirstPersonView");
            view.transform.SetParent(player.transform, false);
            view.transform.localPosition = new Vector3(0f, 1.64f, 0.04f);
            Camera camera = view.AddComponent<Camera>();
            camera.fieldOfView = 66f;
            camera.nearClipPlane = 0.045f;
            camera.farClipPlane = 100f;
            camera.allowHDR = true;
            Type cameraDataType = Type.GetType(
                "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData, Unity.RenderPipelines.Universal.Runtime");
            if (cameraDataType != null)
            {
                Component cameraData = view.AddComponent(cameraDataType);
                cameraDataType.GetProperty("renderPostProcessing")?.SetValue(cameraData, true);
            }
            view.AddComponent<AudioListener>();
            view.tag = "MainCamera";

            CabinFirstPersonController movement = player.AddComponent<CabinFirstPersonController>();
            movement.SetView(view.transform);
            CabinFallRecovery recovery = player.AddComponent<CabinFallRecovery>();
            recovery.Configure(player.transform.position);
        }

        /// <summary>
        /// Swaps ConfigurePlayer's standalone CabinFirstPersonController
        /// (reads Mouse/Keyboard directly, cannot be input-gated during a
        /// cutscene) for the router-based rig the memory scenes actually
        /// use. Called by BuildCastInScene right after ConfigurePlayer —
        /// omitting this once left the player with BOTH controllers fighting
        /// for the CharacterController and no FreeLookCameraRig at all,
        /// caught via a Play-mode traversal test (player levitated to the
        /// ceiling instead of walking on the floor).
        /// </summary>
        private static void ConvertToRouterRig(GameObject player)
        {
            CabinFirstPersonController legacy = player.GetComponent<CabinFirstPersonController>();
            if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);

            Transform view = player.transform.Find("FirstPersonView");
            if (view == null)
            {
                throw new InvalidOperationException("[CabinNightCharacterBuilder] FirstPersonView child not found on Player.");
            }

            InterrogationConfig config = AssetDatabase.LoadAssetAtPath<InterrogationConfig>(
                "Assets/_Project/Config/InterrogationConfig.asset");

            PlayerInputRouter router = player.GetComponent<PlayerInputRouter>();
            if (router == null) router = player.AddComponent<PlayerInputRouter>();
            FreeLookCameraRig rig = player.GetComponent<FreeLookCameraRig>();
            if (rig == null) rig = player.AddComponent<FreeLookCameraRig>();
            SetField(rig, "input", router);
            SetField(rig, "playerCamera", view);
            SetField(rig, "config", config);
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (field == null)
            {
                throw new InvalidOperationException($"[CabinNightCharacterBuilder] {target.GetType().Name} has no field '{fieldName}'.");
            }
            field.SetValue(target, value);
        }

        private static void AssignBodyMaterials(GameObject body, bool female, Material bodyMaterial)
        {
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                string rendererName = renderer.name.ToLowerInvariant();
                if (rendererName.Contains("unified"))
                {
                    renderer.sharedMaterials = new[] { Material(female ? "FemaleFace" : "MaleFace"), bodyMaterial };
                }
                else if (rendererName.Contains("eyelash"))
                {
                    renderer.sharedMaterial = Material(female ? "FemaleLashes" : "MaleLashes");
                }
                else if (rendererName.Contains("eyes"))
                {
                    renderer.sharedMaterial = Material(female ? "FemaleEyes" : "MaleEyes");
                }
                else if (rendererName.Contains("mouth"))
                {
                    renderer.sharedMaterial = Material(female ? "FemaleMouth" : "MaleMouth");
                }
            }
        }

        private static Dictionary<string, Transform> BuildBoneMap(GameObject body)
        {
            Dictionary<string, Transform> result = new Dictionary<string, Transform>();
            foreach (Transform transform in body.GetComponentsInChildren<Transform>(true))
            {
                if (!result.ContainsKey(transform.name))
                {
                    result.Add(transform.name, transform);
                }
            }

            return result;
        }

        private static void AddAccessory(
            GameObject root,
            IReadOnlyDictionary<string, Transform> bodyBones,
            string path,
            string name,
            Material material)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            GameObject item = InstantiateAsset(path);
            item.name = name;
            item.transform.SetParent(root.transform, false);
            foreach (SkinnedMeshRenderer renderer in item.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                Transform[] remappedBones = new Transform[renderer.bones.Length];
                for (int boneIndex = 0; boneIndex < renderer.bones.Length; boneIndex++)
                {
                    Transform sourceBone = renderer.bones[boneIndex];
                    remappedBones[boneIndex] = sourceBone != null && bodyBones.TryGetValue(sourceBone.name, out Transform bodyBone)
                        ? bodyBone
                        : sourceBone;
                }

                renderer.bones = remappedBones;
                if (renderer.rootBone != null && bodyBones.TryGetValue(renderer.rootBone.name, out Transform bodyRootBone))
                {
                    renderer.rootBone = bodyRootBone;
                }

                renderer.updateWhenOffscreen = true;
            }

            foreach (Renderer renderer in item.GetComponentsInChildren<Renderer>(true))
            {
                Material[] materials = new Material[Mathf.Max(1, renderer.sharedMaterials.Length)];
                for (int index = 0; index < materials.Length; index++)
                {
                    materials[index] = material;
                }

                renderer.sharedMaterials = materials;
            }
        }

        private static void ApplyPose(GameObject body, CabinIdleProfile profile)
        {
            Animator animator = body.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                return;
            }

            using HumanPoseHandler handler = new HumanPoseHandler(animator.avatar, animator.transform);
            HumanPose pose = new HumanPose();
            handler.GetHumanPose(ref pose);
            // Muscle tuning lives in CabinPoseLibrary (runtime assembly) so
            // Cutscene.ScriptedActor can apply the same poses at Play-mode
            // time for procedural cutscene staging, not just this one-shot
            // Editor-time bake.
            CabinPoseLibrary.Apply(ref pose, profile);
            handler.SetHumanPose(ref pose);
        }

        private static void SaveCharacter(GameObject character, string fileName)
        {
            PrefabUtility.SaveAsPrefabAssetAndConnect(
                character,
                PrefabRoot + fileName + ".prefab",
                InteractionMode.AutomatedAction);
        }
    }
}
