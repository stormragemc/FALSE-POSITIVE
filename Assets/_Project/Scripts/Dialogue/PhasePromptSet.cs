using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Dialogue
{
    /// <summary>
    /// Phase system prompts as files under Assets/_Project/Prompts/ — a G3
    /// requirement (docs/IMPLEMENTATION_PLAN.md; the persona itself is a
    /// literal at Sidecar/llm.py, our phase prompts must not repeat that).
    /// Referenced as serialized TextAssets, never Resources.Load, so a
    /// missing prompt fails to resolve in the Editor rather than silently
    /// on the demo machine.
    ///
    /// case_file.txt is prepended to P2_Recall only — P1_Tutorial never
    /// talks to the backend at all (see docs/STORY_SCRIPT.md §4, P1 is
    /// pre-rendered VO over a scripted local prompt), so P2's opening turn
    /// is genuinely the first thing the officer says to the model, and the
    /// case file needs to land there.
    /// </summary>
    [CreateAssetMenu(menuName = "False Positive/Phase Prompt Set")]
    public sealed class PhasePromptSet : ScriptableObject
    {
        [SerializeField] private TextAsset caseFile;
        [SerializeField] private TextAsset p2Recall;
        [SerializeField] private TextAsset p3Verdict;

        public string CaseFile => TextOf(caseFile);

        public string TextFor(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.P2_Recall:
                    return Combine(CaseFile, TextOf(p2Recall));
                case GamePhase.P3_Verdict:
                    return TextOf(p3Verdict);
                default:
                    return string.Empty;
            }
        }

        private static string TextOf(TextAsset asset) => asset != null ? asset.text : string.Empty;

        private static string Combine(string a, string b)
        {
            if (string.IsNullOrEmpty(a)) return b;
            if (string.IsNullOrEmpty(b)) return a;
            return $"{a}\n\n{b}";
        }
    }
}
