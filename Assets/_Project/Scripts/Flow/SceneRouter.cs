using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Flow
{
    /// <summary>
    /// Owns additive load/activate/deactivate for every scene except
    /// _Persistent, which this component itself lives in and never touches.
    /// GameFlowDirector is the only caller. A scene is loaded at most once
    /// per playthrough and, once loaded, is only ever deactivated —
    /// Interrogation in particular is never unloaded (see
    /// GameFlowDirector.SessionId for why: reloading it would mint a new
    /// session and reset both the backend's conversation history and the
    /// HuBERT affect baseline).
    /// </summary>
    public sealed class SceneRouter : MonoBehaviour
    {
        private readonly HashSet<string> _loaded = new HashSet<string>();

        public string ActiveSceneName { get; private set; }

        public bool IsLoaded(string sceneName) => !string.IsNullOrEmpty(sceneName) && _loaded.Contains(sceneName);

        public IEnumerator EnsureLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || _loaded.Contains(sceneName)) yield break;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            if (op == null)
            {
                Debug.LogError($"[SceneRouter] Scene \"{sceneName}\" is not in Build Settings.");
                yield break;
            }
            yield return op;

            _loaded.Add(sceneName);
            SetRootsActive(sceneName, false);
        }

        /// <summary>Activates <paramref name="sceneName"/>'s roots and deactivates every
        /// other scene this router has loaded (never touches _Persistent). Also
        /// sets it as SceneManager's active scene, which matters for where
        /// newly-instantiated objects and lighting settings default to.</summary>
        public IEnumerator Activate(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) yield break;

            foreach (string loadedSceneName in _loaded)
            {
                if (loadedSceneName != sceneName) SetRootsActive(loadedSceneName, false);
            }

            SetRootsActive(sceneName, true);
            ActiveSceneName = sceneName;

            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (scene.IsValid()) SceneManager.SetActiveScene(scene);

            yield return null;
        }

        public void SetRootsActive(string sceneName, bool active)
        {
            Scene scene = SceneManager.GetSceneByName(sceneName);
            if (!scene.IsValid() || !scene.isLoaded) return;

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                root.SetActive(active);
            }
        }
    }
}
