using FalsePositive.Flow;
using UnityEngine;
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
            if (playButton != null) playButton.onClick.AddListener(HandlePlay);
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
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
