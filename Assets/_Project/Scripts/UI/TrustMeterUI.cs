using FalsePositive.Flow;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Top-left meter showing how far the officer is willing to go on the
    /// witness's account.
    ///
    /// It displays SessionScore.Credibility directly — the same value
    /// EndingSelector reads. That is deliberate: a separate display number
    /// would eventually disagree with the ending, and the player would watch a
    /// bar that did not explain the result they got.
    ///
    /// **What moves it.** Credibility is
    /// `0.20 + 0.60*coverage - 0.18*unsupported + composure`, so it rises as the
    /// witness accounts for more of the night and falls when he states as memory
    /// something he was not there to see. Composure is clamped to +/-0.1 by §8
    /// and can never decide anything on its own.
    ///
    /// **What it is not.** It is not a readout on whether the witness is being
    /// straight with anyone, and it must never be presented as one — that claim
    /// is the thing the game exists to argue against, and G6 forbids the
    /// language for it in any visible string. It measures how much of the
    /// account is supported, which is a different question and the only one the
    /// game is entitled to answer.
    ///
    /// Visible only during the interrogation phases. During the memories the
    /// player is not being assessed, and leaving a meter on screen would imply
    /// the flashbacks are being judged too.
    /// </summary>
    public sealed class TrustMeterUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image fill;
        [SerializeField] private Text valueText;
        [SerializeField] private Text captionText;

        /// <summary>Matches EndingSelector.CarryThreshold. Below this at the
        /// verdict, the officer will not take the witness's word for who did
        /// it, whoever they name.</summary>
        [SerializeField, Range(0f, 1f)] private float threshold = EndingSelector.CarryThreshold;

        [SerializeField] private Color belowThreshold = new Color(0.72f, 0.24f, 0.20f);
        [SerializeField] private Color aboveThreshold = new Color(0.55f, 0.62f, 0.45f);

        /// <summary>The bar eases rather than jumping, so a drop is legible as
        /// something that just happened rather than a value that was always
        /// there.</summary>
        [SerializeField, Range(0.5f, 8f)] private float easeSpeed = 2.5f;

        private GameFlowDirector _flow;
        private float _target;
        private float _shown;
        private int _shownPercent = -1;
        private bool _shownCarrying = true;

        private void OnEnable()
        {
            _flow = GameFlowDirector.Instance;
            if (_flow == null) return;

            _flow.Score.CredibilityChanged += OnCredibilityChanged;
            _flow.PhaseChanged += OnPhaseChanged;

            // Built in _Persistent long before the interrogation starts, so pull
            // the current value rather than waiting for the next change.
            _target = _flow.Score.Credibility;
            _shown = _target;
            OnPhaseChanged(_flow.Phase);
        }

        private void OnDisable()
        {
            if (_flow == null) return;
            _flow.Score.CredibilityChanged -= OnCredibilityChanged;
            _flow.PhaseChanged -= OnPhaseChanged;
        }

        private void OnCredibilityChanged(float value) => _target = value;

        private void OnPhaseChanged(GamePhase phase)
        {
            bool interrogating = phase == GamePhase.P2_Recall || phase == GamePhase.P3_Verdict;
            if (root != null) root.SetActive(interrogating);
        }

        private void Update()
        {
            if (root == null || !root.activeSelf) return;

            _shown = Mathf.MoveTowards(_shown, _target, easeSpeed * Time.deltaTime * 0.4f);

            if (fill != null)
            {
                fill.fillAmount = _shown;
                fill.color = _shown >= threshold ? aboveThreshold : belowThreshold;
            }

            // Both strings are rebuilt only when they would actually differ.
            // This runs every frame while the meter is up, and formatting a
            // percentage unconditionally allocates a string per frame for a
            // number that changes a handful of times per session.
            int percent = Mathf.RoundToInt(_shown * 100f);
            if (percent != _shownPercent)
            {
                _shownPercent = percent;
                if (valueText != null) valueText.text = percent + "%";
            }

            bool carrying = _shown >= threshold;
            if (carrying != _shownCarrying)
            {
                _shownCarrying = carrying;
                if (captionText != null)
                {
                    // Says what the officer will do, not what he thinks of the
                    // witness. Below the line he will not take their word for a
                    // name — a statement about him, not a verdict on them.
                    captionText.text = carrying
                        ? "He is following you"
                        : "He is not taking your word";
                }
            }
        }
    }
}
