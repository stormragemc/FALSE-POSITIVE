namespace FalsePositive.Flow
{
    /// <summary>
    /// The fixed order of the whole playthrough. GameFlowDirector.AdvancePhase
    /// walks this order and nothing else defines sequence — see
    /// docs/GAME_COMPLETION_PLAN.md §5 A1 and docs/STORY_SCRIPT.md §3.
    /// </summary>
    public enum GamePhase
    {
        Boot,
        Menu,
        P1_Tutorial,
        M1_Night,
        P2_Recall,
        M2_Morning,
        P3_Verdict,
        P4_Ending,
        Outcome,
    }
}
