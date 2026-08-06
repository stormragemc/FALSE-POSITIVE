using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Dialogue
{
    [System.Serializable]
    public sealed class OfflineOfficerLine
    {
        [TextArea] public string line;
        public AudioClip voClip;
        public float holdSecondsIfNoClip = 3.5f;
    }

    /// <summary>
    /// Fixed Spassky script for Offline demo mode — see
    /// Editor/OfflineScriptBuilder.cs for the authored content and
    /// docs/GAME_COMPLETION_PLAN.md §10's "never fake" rule for why this is
    /// a distinct asset from the live prompts rather than a silent
    /// substitute for them. Mirrors CutsceneBeat's shape deliberately: this
    /// is the same "fade+VO, no reaction to the player" contract, just
    /// spoken turn-by-turn instead of during a cutscene.
    /// </summary>
    [CreateAssetMenu(menuName = "False Positive/Offline Dialogue Script")]
    public sealed class OfflineDialogueScript : ScriptableObject
    {
        public OfflineOfficerLine[] p2Recall;
        public OfflineOfficerLine[] p3Verdict;

        public OfflineOfficerLine[] LinesFor(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.P2_Recall: return p2Recall;
                case GamePhase.P3_Verdict: return p3Verdict;
                default: return null;
            }
        }
    }
}
