using FalsePositive.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// The single owner of Cursor.lockState/Cursor.visible for anything that
    /// isn't CabinFirstPersonController's standalone level (see that file's
    /// comment). Lives in _Persistent so it runs for the whole playthrough.
    ///
    /// "Is anything clickable right now" is answered with Unity's own count
    /// of enabled Selectables (Button/Slider/Toggle/Dropdown) across every
    /// loaded scene, rather than a hand-maintained per-phase table — every
    /// current UI panel is built root.SetActive(false) and only enables its
    /// Selectables via its own Show() (see ProjectBootstrapBuilder), and
    /// SceneRouter deactivates non-active scenes' roots, so this count is
    /// already correct for consent/calibration/settings/outcome and zero
    /// during P1-P4/M1/M2. The Boot/Menu phase clause covers the fade+load
    /// window in GameFlowDirector.TransitionRoutine where the count is
    /// briefly zero before MainMenu's roots activate — without it the cursor
    /// would flicker locked-then-free on the very screen this exists to fix.
    ///
    /// Polls in LateUpdate (after every other Awake/OnEnable/Update this
    /// frame, so it wins any same-frame race) rather than eventing, the same
    /// cheap-poller pattern OfflineModeLabel uses. Only reasserts on a
    /// mismatch — it never fights the cursor being freed while show is
    /// false, so an editor Esc-to-release still works during development.
    /// </summary>
    public sealed class CursorVisibilityController : MonoBehaviour
    {
        private bool _hasApplied;
        private bool _applied;

        private void LateUpdate()
        {
            GameFlowDirector flow = GameFlowDirector.Instance;
            GamePhase phase = flow != null ? flow.Phase : GamePhase.Boot;
            bool show = Selectable.allSelectablesCount > 0
                || phase == GamePhase.Boot || phase == GamePhase.Menu;

            if (!_hasApplied || show != _applied || (show && Cursor.lockState != CursorLockMode.None))
            {
                Apply(show);
            }
        }

        private void Apply(bool show)
        {
            Cursor.lockState = show ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = show;
            _applied = show;
            _hasApplied = true;
        }
    }
}
