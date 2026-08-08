using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Re-runnable relight pass on Interrogation.unity: kills ambient/skybox/
    /// reflection fill and the directional light, and narrows/hardens
    /// InterrogationSpotLight, so the room reads as a dark cell with a single
    /// hard pool of light over the table rather than an evenly lit interior.
    /// Interrogation.unity is only ever fixed up in place (see Bootstrap step
    /// 4, ProjectBootstrapBuilder.FixInterrogationScene) — nothing regenerates
    /// it from scratch — so this is safe to leave applied permanently.
    /// </summary>
    public static class InterrogationLightingBuilder
    {
        private const string InterrogationScenePath = "Assets/_Project/Scenes/Interrogation.unity";

        [MenuItem("Tools/False Positive/Bootstrap/4a - Relight Interrogation")]
        public static void Relight()
        {
            Scene scene = EditorSceneManager.OpenScene(InterrogationScenePath, OpenSceneMode.Single);

            // Ambient + environment reflections off. Verified this scene has
            // no baked lightmaps (m_LightingDataAsset is the built-in null
            // reference, no mesh is LightmapStatic) — so ambient/skybox is
            // genuinely what's lighting the walls/floor, and killing it
            // actually darkens them rather than fighting baked GI.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.012f, 0.013f, 0.016f);
            RenderSettings.skybox = null;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflectionTexture = null;
            RenderSettings.reflectionIntensity = 0f;

            GameObject mainCameraGo = GameObject.Find("MainCamera");
            Camera mainCamera = mainCameraGo != null ? mainCameraGo.GetComponent<Camera>() : null;
            if (mainCamera != null)
            {
                mainCamera.clearFlags = CameraClearFlags.SolidColor;
                mainCamera.backgroundColor = Color.black;
            }
            else
            {
                Debug.LogWarning("[InterrogationLightingBuilder] MainCamera not found — clear flags unchanged.");
            }

            GameObject directionalGo = GameObject.Find("Directional Light");
            if (directionalGo != null)
            {
                directionalGo.SetActive(false);
            }
            else
            {
                Debug.LogWarning("[InterrogationLightingBuilder] Directional Light not found.");
            }

            GameObject spotGo = GameObject.Find("InterrogationSpotLight");
            Light spot = spotGo != null ? spotGo.GetComponent<Light>() : null;
            if (spot != null)
            {
                spot.spotAngle = 60f;
                spot.innerSpotAngle = 24f;
                spot.intensity = 8f;
                spot.range = 9f;
                spot.shadows = LightShadows.Soft;
                spot.shadowStrength = 1f;
            }
            else
            {
                Debug.LogError("[InterrogationLightingBuilder] InterrogationSpotLight not found — room not relit.");
            }

            // RecordingIndicatorGlow (the recording LED) is left untouched —
            // a story element that should keep reading against a dark room.

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, InterrogationScenePath);
            Debug.Log("[InterrogationLightingBuilder] Interrogation.unity relit.");
        }
    }
}
