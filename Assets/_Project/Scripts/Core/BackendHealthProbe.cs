using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Core
{
    /// <summary>
    /// Formerly GameBootstrap. Reduced to a pure health probe as part of the
    /// A0 seam commit (docs/GAME_COMPLETION_PLAN.md) — it used to hard-wire
    /// health-gate -> BeginSeated() -> BeginConversation(), which is the
    /// wrong boot order now that Menu -> consent -> calibration happens
    /// first. It now reports readiness to GameFlowDirector and does nothing
    /// else; GameFlowDirector decides what happens next.
    ///
    /// Lives in _Persistent — see the "Unity scene setup" checklist in this
    /// commit for exactly which GameObject to attach it to and how to wire
    /// sidecarLauncher.
    ///
    /// Formerly also locked/hid the cursor in Awake() as a leftover from its
    /// GameBootstrap days (starting the game seated, before Menu existed) —
    /// that locked the cursor at boot, before the main menu ever appeared,
    /// and nothing ever unlocked it again. Cursor ownership now belongs
    /// entirely to UI.CursorVisibilityController.
    /// </summary>
    public sealed class BackendHealthProbe : MonoBehaviour
    {
        [SerializeField] private SidecarProcessLauncher sidecarLauncher;

        private void Start()
        {
            sidecarLauncher.OnStatus += HandleStatus;
            sidecarLauncher.OnReady += HandleReady;
            sidecarLauncher.OnFailed += HandleFailed;
            sidecarLauncher.Begin();
        }

        private void HandleStatus(string text)
        {
            GameFlowDirector.Instance?.ReportBackendStatus(text);
        }

        private void HandleReady()
        {
            GameFlowDirector.Instance?.ReportBackendReady();
        }

        private void HandleFailed(string reason)
        {
            Debug.LogError($"[BackendHealthProbe] {reason}");
            GameFlowDirector.Instance?.ReportBackendFailed(reason);
        }
    }
}
