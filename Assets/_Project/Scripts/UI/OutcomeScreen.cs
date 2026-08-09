using FalsePositive.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// The closing card at GamePhase.Outcome (A11). Presentation only — the
    /// text is built by OutcomeCardComposer, which owns the fixed card from
    /// docs/STORY_SCRIPT.md §4 P4_ENDING, the quoted lines, and the G6 rule
    /// governing them. Lives in _Persistent's HUD canvas so it can show
    /// over whichever scene the ending cutscene left active.
    /// </summary>
    public sealed class OutcomeScreen : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text cardText;
        [SerializeField] private Button quitToMenuButton;

        private void Awake()
        {
            if (quitToMenuButton != null) quitToMenuButton.onClick.AddListener(HandleQuitToMenu);
        }

        public void Show(string card)
        {
            if (cardText != null) cardText.text = card;
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void HandleQuitToMenu()
        {
            Hide();
            GameFlowDirector.Instance?.AbortToMenu();
        }
    }
}
