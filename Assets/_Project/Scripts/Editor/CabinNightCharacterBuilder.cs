using System;
using System.Collections.Generic;
using System.Linq;
using FalsePositive.CabinNight;
using FalsePositive.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Rebuilds the authored cast for Nobody Went Out's 00:50 cabin scene.
    /// The imported o3n bodies are used directly so the level remains usable
    /// when the optional UMA package is not installed.
    /// </summary>
    public static class CabinNightCharacterBuilder
    {
        private const string ScenePath = "Assets/_Project/Scenes/NobodyWentOut_CabinNight.unity";
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

        [MenuItem("Tools/False Positive/Rebuild Cabin Night Cast")]
        public static void BuildCharacters()
        {
            Scene scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            Transform cast = GameObject.Find("Characters").transform;
            while (cast.childCount > 0)
            {
                UnityEngine.Object.DestroyImmediate(cast.GetChild(0).gameObject);
            }

            GameObject player = BuildCharacter(
                cast, "Player (Male - First Person)", false,
                new Vector3(-0.65f, 0.08f, -1.65f), new Vector3(0f, 12f, 0f), 0.98f,
                Material("MaleBodyJeans"), CabinIdleProfile.Controlled,
                null, null, null, null, null);
            ConfigurePlayer(player);
            SaveCharacter(player, "Player_FirstPerson");

            GameObject nico = BuildCharacter(
                cast, "Nico Vlahos (Male)", false,
                new Vector3(0.12f, 0.08f, 1.28f), new Vector3(0f, 192f, 0f), 0.98f,
                Material("MaleBodyJeansShirt"), CabinIdleProfile.Confrontational,
                null, null, MaleHair, Material("HairBrown"), MaleShoes);
            SaveCharacter(nico, "Nico_Vlahos");

            GameObject aaron = BuildCharacter(
                cast, "Aaron Teague (Male)", false,
                new Vector3(3.82f, 0.08f, 0.92f), new Vector3(0f, 226f, 0f), 1.02f,
                Material("MaleBodyJeans"), CabinIdleProfile.Controlled,
                MaleHoodie, Material("HoodieGray"), MaleMilitaryHair, Material("HairDark"), MaleShoes);
            SaveCharacter(aaron, "Aaron_Teague");

            GameObject ivy = BuildCharacter(
                cast, "Ivy Teague (Female)", true,
                new Vector3(4.58f, 1.72f, -1.68f), new Vector3(0f, 248f, 0f), 0.98f,
                Material("FemaleBodyJeans"), CabinIdleProfile.Guarded,
                FemaleShirt, Material("IvyYellowShirt"), FemaleLongHair, Material("HairBlack"), FemaleShoes);
            SaveCharacter(ivy, "Ivy_Teague");

            GameObject priya = BuildCharacter(
                cast, "Priya Raman (Female)", true,
                new Vector3(-2.35f, 0.62f, -0.25f), new Vector3(0f, 92f, -76f), 0.96f,
                Material("FemaleBodyJeans"), CabinIdleProfile.Sleeping,
                FemaleDress, Material("PriyaDress"), FemalePonytail, Material("HairBlack"), FemaleShoes);
            SaveCharacter(priya, "Priya_Raman");

            GameObject fireplace = GameObject.Find("Fireplace");
            if (fireplace != null && fireplace.GetComponent<CabinFireFlicker>() == null)
            {
                CabinFireFlicker flicker = fireplace.AddComponent<CabinFireFlicker>();
                flicker.Configure(fireplace.GetComponentsInChildren<Light>(true));
            }

            List<EditorBuildSettingsScene> buildScenes = EditorBuildSettings.scenes.ToList();
            if (buildScenes.All(item => item.path != ScenePath))
            {
                buildScenes.Add(new EditorBuildSettingsScene(ScenePath, true));
                EditorBuildSettings.scenes = buildScenes.ToArray();
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("Cabin Night cast rebuilt: player, Nico, Aaron, Ivy, and Priya.");
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
            Array.Clear(pose.muscles, 0, pose.muscles.Length);
            SetMuscle(ref pose, "Left Arm Down-Up", -0.72f);
            SetMuscle(ref pose, "Right Arm Down-Up", -0.72f);
            SetMuscle(ref pose, "Left Forearm Stretch", -0.18f);
            SetMuscle(ref pose, "Right Forearm Stretch", -0.18f);

            switch (profile)
            {
                case CabinIdleProfile.Confrontational:
                    SetMuscle(ref pose, "Spine Front-Back", -0.12f);
                    SetMuscle(ref pose, "Chest Front-Back", -0.08f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.48f);
                    SetMuscle(ref pose, "Left Arm Front-Back", -0.18f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.12f);
                    SetMuscle(ref pose, "Head Turn Left-Right", -0.08f);
                    break;
                case CabinIdleProfile.Controlled:
                    SetMuscle(ref pose, "Spine Front-Back", 0.05f);
                    SetMuscle(ref pose, "Head Nod Down-Up", 0.08f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.32f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.32f);
                    break;
                case CabinIdleProfile.Guarded:
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.52f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.52f);
                    SetMuscle(ref pose, "Left Arm Front-Back", 0.18f);
                    SetMuscle(ref pose, "Right Arm Front-Back", 0.18f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.58f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.58f);
                    SetMuscle(ref pose, "Spine Twist Left-Right", -0.08f);
                    SetMuscle(ref pose, "Head Turn Left-Right", -0.12f);
                    break;
                case CabinIdleProfile.Sleeping:
                    pose.bodyPosition += new Vector3(0f, -0.12f, 0f);
                    SetMuscle(ref pose, "Spine Front-Back", -0.42f);
                    SetMuscle(ref pose, "Chest Front-Back", -0.34f);
                    SetMuscle(ref pose, "Head Nod Down-Up", -0.35f);
                    SetMuscle(ref pose, "Neck Nod Down-Up", -0.22f);
                    SetMuscle(ref pose, "Left Upper Leg Front-Back", -0.62f);
                    SetMuscle(ref pose, "Right Upper Leg Front-Back", -0.42f);
                    SetMuscle(ref pose, "Left Lower Leg Stretch", -0.58f);
                    SetMuscle(ref pose, "Right Lower Leg Stretch", -0.7f);
                    SetMuscle(ref pose, "Left Arm Down-Up", -0.15f);
                    SetMuscle(ref pose, "Right Arm Down-Up", -0.32f);
                    SetMuscle(ref pose, "Left Forearm Stretch", -0.7f);
                    SetMuscle(ref pose, "Right Forearm Stretch", -0.52f);
                    break;
            }

            handler.SetHumanPose(ref pose);
        }

        private static void SetMuscle(ref HumanPose pose, string muscleName, float value)
        {
            for (int index = 0; index < HumanTrait.MuscleName.Length; index++)
            {
                if (string.Equals(HumanTrait.MuscleName[index], muscleName, StringComparison.OrdinalIgnoreCase))
                {
                    pose.muscles[index] = value;
                    return;
                }
            }
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
