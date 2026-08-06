using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.UI
{
    /// <summary>
    /// Persistent corner label, shown only while GameFlowDirector.OfflineMode
    /// is on — the docs/GAME_COMPLETION_PLAN.md §10 "never fake" requirement
    /// that the player never mistake the Offline demo's canned script for
    /// the live officer reacting to what they actually said. Polls rather
    /// than eventing off GameFlowDirector: OfflineMode is set once per
    /// playthrough (StartNewPlaythrough) and there is no existing change
    /// event for it, so a cheap per-frame check on a single label is
    /// simpler than adding one.
    /// </summary>
    public sealed class OfflineModeLabel : MonoBehaviour
    {
        [SerializeField] private GameObject root;

        private void Update()
        {
            GameFlowDirector flow = GameFlowDirector.Instance;
            bool shouldShow = flow != null && flow.OfflineMode && flow.Phase != GamePhase.Menu;
            if (root != null && root.activeSelf != shouldShow) root.SetActive(shouldShow);
        }
    }
}
