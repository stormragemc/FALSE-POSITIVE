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
        // This component's own scene — never unloaded, never deactivated,
        // and Activate() must never iterate into it even when it shows up
        // in SceneManager's loaded-scene list.
        private const string PersistentSceneName = "_Persistent";

        private readonly HashSet<string> _loaded = new HashSet<string>();

        public string ActiveSceneName { get; private set; }

        public bool IsLoaded(string sceneName) => !string.IsNullOrEmpty(sceneName) && _loaded.Contains(sceneName);

        public IEnumerator EnsureLoaded(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName) || _loaded.Contains(sceneName)) yield break;

            // Already open (e.g. the scene the user pressed Play from, or one
            // PersistentSceneBootstrap pulled in) — track it instead of
            // loading a second copy, which would otherwise duplicate every
            // GameObject in it.
            Scene existing = SceneManager.GetSceneByName(sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                _loaded.Add(sceneName);
                SetRootsActive(sceneName, false);
                yield break;
            }

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
        /// other loaded scene (never touches _Persistent). Also sets it as
        /// SceneManager's active scene, which matters for where
        /// newly-instantiated objects and lighting settings default to.
        ///
        /// Deliberately walks every scene SceneManager currently has loaded,
        /// not just the ones tracked in _loaded — a scene already open in
        /// the editor when Play is pressed (e.g. a memory scene opened
        /// directly to iterate on it) is live and fully active before its
        /// own phase ever calls EnsureLoaded, so relying on _loaded alone
        /// left it active for the whole session: two simultaneously-active
        /// memory scenes, two Cameras, two AudioListeners, and every
        /// GameObject.Find("Player ...") in CutsceneStage liable to resolve
        /// to the wrong cabin. Any such scene is adopted into _loaded here
        /// so EnsureLoaded doesn't later load a duplicate copy of it.</summary>
        public IEnumerator Activate(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) yield break;

            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene loadedScene = SceneManager.GetSceneAt(i);
                if (!loadedScene.isLoaded) continue;
                if (loadedScene.name == PersistentSceneName || loadedScene.name == sceneName) continue;

                _loaded.Add(loadedScene.name);
                SetRootsActive(loadedScene.name, false);
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
