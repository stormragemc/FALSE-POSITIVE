using FalsePositive.Flow;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace FalsePositive.Menu
{
    /// <summary>
    /// Lives in MainMenu.unity. Play -> StartNewPlaythrough then hands off to
    /// _Persistent's consent card (MicConsentFlow). SettingsPanel also lives
    /// in _Persistent (reused as the pause menu on Day 2 — A15), so it too
    /// is reached through GameFlowDirector rather than a scene-local
    /// reference — see docs/GAME_COMPLETION_PLAN.md A0 on why a cross-scene
    /// reference cannot be a plain [SerializeField] here.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        private void Awake()
        {
            EnsureEventSystem();

            if (playButton != null) playButton.onClick.AddListener(HandlePlay);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
        }

        /// <summary>Backstop for the case where this scene is played on its
        /// own rather than via PersistentSceneBootstrap/_Persistent — without
        /// an EventSystem + InputSystemUIInputModule in the loaded scene set,
        /// the Canvas's GraphicRaycaster is never queried and every button
        /// silently stops responding to clicks. The normal path already has
        /// one from _Persistent, so this never fires there.</summary>
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;

            GameObject go = new GameObject("EventSystem (fallback)",
                typeof(EventSystem), typeof(InputSystemUIInputModule));
            Debug.LogWarning("[MainMenuController] No EventSystem was loaded — created a fallback one. " +
                "This scene is normally expected to run with _Persistent loaded (see PersistentSceneBootstrap).");
            DontDestroyOnLoad(go);
        }

        private void HandlePlay()
        {
            GameFlowDirector flow = GameFlowDirector.Instance;
            if (flow == null)
            {
                Debug.LogError("[MainMenuController] No GameFlowDirector found — is _Persistent loaded?");
                return;
            }

            flow.StartNewPlaythrough();
            flow.ConsentFlow?.Show();
        }

        private void HandleSettings()
        {
            GameFlowDirector.Instance?.SettingsPanel?.Show();
        }

        private void HandleQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
