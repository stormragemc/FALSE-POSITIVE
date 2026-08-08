using FalsePositive.Flow;
using FalsePositive.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
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
    ///
    /// Quit confirmation, Credits and How-to-play are scene-local windows
    /// (MainMenu/MenuOverlay) reachable only from here, so this stays a plain
    /// router over them: this class owns none of MenuWindow's fade/focus/
    /// escape logic, just the buttons that open each window and the one
    /// Update() poll that dispatches Escape to whichever window is topmost.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        [SerializeField] private Button playButton;
        [SerializeField] private Button offlineButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;
        [SerializeField] private Button controlsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private QuitConfirmWindow quitConfirm;
        [SerializeField] private MenuWindow creditsWindow;
        [SerializeField] private MenuWindow controlsWindow;

        private void Awake()
        {
            EnsureEventSystem();

            if (playButton != null) playButton.onClick.AddListener(() => HandlePlay(offline: false));
            if (offlineButton != null) offlineButton.onClick.AddListener(() => HandlePlay(offline: true));
            if (settingsButton != null) settingsButton.onClick.AddListener(HandleSettings);
            if (quitButton != null) quitButton.onClick.AddListener(HandleQuit);
            if (controlsButton != null && controlsWindow != null) controlsButton.onClick.AddListener(controlsWindow.Open);
            if (creditsButton != null && creditsWindow != null) creditsButton.onClick.AddListener(creditsWindow.Open);
        }

        /// <summary>One Escape poller for every open MenuWindow, rather than
        /// each window polling for itself and racing to close two at once —
        /// see MenuWindowStack's own doc comment for why. Settings is not a
        /// MenuWindow (it predates that system and is reused as the Day-2
        /// pause menu — see SettingsPanel's class doc), so it needs its own
        /// fallthrough once the stack reports nothing else open.</summary>
        private void Update()
        {
            if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;

            if (MenuWindowStack.AnyOpen)
            {
                MenuWindowStack.CloseTop();
                return;
            }

            SettingsPanel settings = GameFlowDirector.Instance?.SettingsPanel;
            if (settings != null && settings.IsOpen) settings.Hide();
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

        /// <summary>Offline demo runs the identical consent -> calibration -> P1
        /// hand-off as normal Play — the mic is still required (P1's spoken
        /// prompt and M1's yell gate are local-only and never touched the
        /// backend either way). Only P2/P3's officer turns differ, via
        /// GameFlowDirector.OfflineMode — see PhaseDialogueController.</summary>
        private void HandlePlay(bool offline)
        {
            GameFlowDirector flow = GameFlowDirector.Instance;
            if (flow == null)
            {
                Debug.LogError("[MainMenuController] No GameFlowDirector found — is _Persistent loaded?");
                return;
            }

            flow.StartNewPlaythrough(offline);
            flow.ConsentFlow?.Show();
        }

        private void HandleSettings()
        {
            GameFlowDirector.Instance?.SettingsPanel?.Show();
        }

        private void HandleQuit()
        {
            if (quitConfirm != null)
            {
                quitConfirm.Open();
                return;
            }

            Debug.LogWarning("[MainMenuController] No QuitConfirmWindow wired — quitting immediately with no confirmation.");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
