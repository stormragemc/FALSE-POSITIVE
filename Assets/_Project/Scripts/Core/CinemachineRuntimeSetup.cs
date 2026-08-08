using System;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Core
{
    /// <summary>
    /// Adds CinemachineBrain to every runtime camera after Cinemachine is
    /// installed. Reflection keeps the project compilable while Package
    /// Manager is still resolving the optional package. A Brain with no live
    /// CinemachineCamera leaves the existing first-person camera authoritative.
    /// </summary>
    public static class CinemachineRuntimeSetup
    {
        private const string BrainTypeName =
            "Unity.Cinemachine.CinemachineBrain, Unity.Cinemachine";

        private static Type _brainType;
        private static bool _resolvedType;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            ConfigureLoadedCameras();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ConfigureLoadedCameras();
        }

        private static void ConfigureLoadedCameras()
        {
            Type brainType = ResolveBrainType();
            if (brainType == null) return;

            Camera[] cameras = UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (Camera camera in cameras)
            {
                if (camera == null || camera.GetComponent(brainType) != null) continue;
                camera.gameObject.AddComponent(brainType);
            }
        }

        private static Type ResolveBrainType()
        {
            if (_resolvedType) return _brainType;
            _resolvedType = true;
            _brainType = Type.GetType(BrainTypeName, throwOnError: false);
            return _brainType;
        }
    }
}
