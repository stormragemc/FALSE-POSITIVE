using FalsePositive.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Guardrail #2 (docs/GAME_COMPLETION_PLAN.md §8): a persistent,
    /// honest mic-state indicator for the whole session. Copy is fixed at
    /// "Microphone active" / "Microphone inactive" — never "recording", and
    /// never a red dot, both of which read as "a file is being written",
    /// which is never true (guardrail #3). No public API: it just polls the
    /// two facts that make the state true or false.
    /// </summary>
    public sealed class MicIndicator : MonoBehaviour
    {
        private const string ActiveText = "Microphone active";
        private const string InactiveText = "Microphone inactive";

        [SerializeField] private MicrophoneService mic;
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private Text label;
        [SerializeField] private Image dot;
        [SerializeField] private Color activeColor = new Color(0.25f, 0.85f, 0.35f);
        [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f);

        private void Update()
        {
            bool active = mic != null && mic.IsCapturing && (vad == null || !vad.Gated);

            if (label != null) label.text = active ? ActiveText : InactiveText;
            if (dot != null) dot.color = active ? activeColor : inactiveColor;
        }
    }
}
