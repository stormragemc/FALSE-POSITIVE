using System.Collections.Generic;
using FalsePositive.Dialogue;

namespace FalsePositive.Flow
{
    /// <summary>Which ending was chosen, and the one condition that chose it.
    /// The reason is developer-facing — it goes to the debug overlay and the
    /// log, never to the player.</summary>
    public readonly struct EndingDecision
    {
        public CutsceneId Cutscene { get; }
        public Suspect Accused { get; }
        public int CitedClues { get; }
        public string Reason { get; }

        public EndingDecision(CutsceneId cutscene, Suspect accused, int citedClues, string reason)
        {
            Cutscene = cutscene;
            Accused = accused;
            CitedClues = citedClues;
            Reason = reason;
        }
    }

    /// <summary>
    /// A10 — docs/STORY_SCRIPT.md §8's ending table, replacing the Day-1
    /// stopgap that picked on the name alone.
    ///
    /// | E_DAVID | credibility &lt; 0.45, or 2+ caught fabrications, or no name |
    /// | E_AARON | credibility >= 0.6, named Aaron, cited >= 2 clues |
    /// | E_IVY   | credibility >= 0.6, named Ivy |
    /// | E_PRIYA | credibility >= 0.6, named Priya |
    ///
    /// §8 leaves 0.45 &lt;= credibility &lt; 0.6 unstated. It falls to E_DAVID:
    /// every positive row requires 0.6, and E_DAVID is the ending for a
    /// witness who could not carry his own account.
    ///
    /// Composure is deliberately absent here. §8 binds it to at most +/-0.1 of
    /// credibility and forbids it from deciding an ending on its own, so it is
    /// applied inside SessionScore.UpdateCredibility and never read again.
    /// Treating delivery as evidence is exactly what the game is arguing
    /// against; that bound is a G6 requirement, not a balance knob.
    /// </summary>
    public static class EndingSelector
    {
        public const float CollapseThreshold = 0.45f;
        public const float CarryThreshold = 0.60f;
        public const int FabricationLimit = 2;
        public const int CluesRequiredForAaron = 2;

        public static EndingDecision Select(SessionScore score, IReadOnlyCollection<CitedClue> cited)
        {
            int clueCount = cited != null ? cited.Count : 0;
            if (score == null)
            {
                return new EndingDecision(CutsceneId.EndingDavid, Suspect.None, clueCount,
                    "no session score");
            }

            Suspect accused = score.Accusation;

            if (score.Credibility < CollapseThreshold)
            {
                return new EndingDecision(CutsceneId.EndingDavid, accused, clueCount,
                    $"credibility {score.Credibility:0.00} < {CollapseThreshold:0.00}");
            }

            if (score.CaughtFabrications >= FabricationLimit)
            {
                return new EndingDecision(CutsceneId.EndingDavid, accused, clueCount,
                    $"{score.CaughtFabrications} unsupported details");
            }

            if (accused == Suspect.None)
            {
                return new EndingDecision(CutsceneId.EndingDavid, accused, clueCount,
                    "no name given");
            }

            if (score.Credibility < CarryThreshold)
            {
                return new EndingDecision(CutsceneId.EndingDavid, accused, clueCount,
                    $"credibility {score.Credibility:0.00} < {CarryThreshold:0.00}");
            }

            switch (accused)
            {
                case Suspect.Aaron:
                    // §7's thesis in one branch: being right is not the same as
                    // having seen it. Aaron with nothing behind it is E_DAVID.
                    return clueCount >= CluesRequiredForAaron
                        ? new EndingDecision(CutsceneId.EndingAaron, accused, clueCount,
                            $"named Aaron, {clueCount} clues cited")
                        : new EndingDecision(CutsceneId.EndingDavid, accused, clueCount,
                            $"named Aaron, only {clueCount} clue(s) cited");

                case Suspect.Ivy:
                    return new EndingDecision(CutsceneId.EndingIvy, accused, clueCount, "named Ivy");

                case Suspect.Priya:
                    return new EndingDecision(CutsceneId.EndingPriya, accused, clueCount, "named Priya");

                default:
                    return new EndingDecision(CutsceneId.EndingDavid, accused, clueCount, "no name given");
            }
        }
    }
}
