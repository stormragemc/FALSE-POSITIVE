using System.Collections.Generic;

namespace FalsePositive.Flow
{
    /// <summary>
    /// The three tracked quantities behind ending selection
    /// (docs/STORY_SCRIPT.md §8) — none of them ever shown to the player as
    /// "truth" (G6). Composure is real from Day 1 (A1), computed only from
    /// RELIABLE prosody turns, because the sidecar itself flags which turns
    /// it does not trust and this class must not pretend those are signal.
    /// Credibility is real as of §7 (see UpdateCredibility) but partial —
    /// read its remarks before treating it as the finished §8 formula.
    /// </summary>
    public sealed class SessionScore
    {
        private readonly List<TurnRecord> _turns = new List<TurnRecord>();
        private readonly HashSet<string> _caughtTrapIds = new HashSet<string>();
        private double _reliableTensionSum;
        private int _reliableTensionCount;

        public IReadOnlyList<TurnRecord> Turns => _turns;

        /// <summary>0..1. Real: mean tension across reliable-signal turns only,
        /// inverted and clamped (high sustained tension -> low composure).</summary>
        public float Composure { get; private set; } = 1f;

        /// <summary>0..1, from UpdateCredibility. Real, but a PARTIAL reading of
        /// docs/STORY_SCRIPT.md §8: it covers mark coverage, caught fabrications
        /// and composure. §8's "consistency across turns" input is deliberately
        /// not modelled, because no consistency signal exists on the client —
        /// do not describe this as the whole §8 rule.</summary>
        public float Credibility { get; private set; } = 1f;

        public Suspect Accusation { get; private set; } = Suspect.None;
        public float AccusationSupport { get; private set; }

        /// <summary>How many DISTINCT §7 traps were caught. §8 sends two or more
        /// straight to E_DAVID, so this counts traps rather than occurrences:
        /// the officer re-baits the same trap across P2 and P3 by design, and a
        /// single mistake asked about twice must not decide the ending.</summary>
        public int CaughtFabrications => _caughtTrapIds.Count;

        public IReadOnlyCollection<string> CaughtTrapIds => _caughtTrapIds;

        /// <summary>Peak story-mark coverage seen this playthrough. Tracked here
        /// rather than read live off StoryMarkTracker because that tracker is
        /// Reset() at the top of every live phase, so P2's coverage is gone by
        /// the time P3 is scored. Reset() below is per playthrough, which is
        /// exactly the lifetime this needs.</summary>
        public int MarkCoverage { get; private set; }

        public void RecordTurn(in TurnRecord record)
        {
            _turns.Add(record);

            if (record.ProsodyReliable)
            {
                _reliableTensionSum += record.Tension;
                _reliableTensionCount++;
                float meanTension = (float)(_reliableTensionSum / _reliableTensionCount);
                Composure = UnityEngine.Mathf.Clamp01(1f - meanTension);
            }
        }

        /// <summary>Records a caught §7 trap. Returns true only the first time a
        /// given trap is seen. Unknown ids are ignored rather than counted —
        /// the id arrives over the wire, so the allowlist lives here rather
        /// than in the caller.</summary>
        public bool RecordFabrication(string trapId)
        {
            if (!TrapIds.IsKnown(trapId)) return false;
            return _caughtTrapIds.Add(trapId);
        }

        /// <summary>Keeps the high-water mark of covered story marks (§6).</summary>
        public void ObserveMarkCoverage(int covered)
        {
            if (covered > MarkCoverage) MarkCoverage = covered;
        }

        /// <summary>
        /// docs/STORY_SCRIPT.md §8. Cheap and idempotent, so it is recomputed
        /// every turn: the value at P3 exit is then correct no matter which exit
        /// path fired, and the F1 overlay can show it moving during a playtest.
        ///
        /// Calibrated against §8's thresholds. A clean 7/7 run lands at 0.90 and
        /// cannot be pushed under 0.6 by composure alone. One fabrication is
        /// survivable for a composed witness (0.72) but not for a badly rattled
        /// one (0.52) — composure decides only at the margin, which is its job.
        /// Two fabrications can never reach 0.6 whatever the composure, though
        /// they stay above §8's 0.45 floor: §8's own "two-plus caught
        /// fabrications" clause is what settles that run, and it belongs to the
        /// ending selector rather than being doubled up here.
        /// </summary>
        public void UpdateCredibility(int totalMarks)
        {
            float coverage = totalMarks > 0
                ? UnityEngine.Mathf.Clamp01(MarkCoverage / (float)totalMarks)
                : 0f;

            // §8: composure moves credibility by at most ±0.1 and never decides
            // an ending on its own. That bound is a G6 requirement, not a
            // balance choice, so it is clamped rather than merely scaled.
            float composureAdjustment =
                UnityEngine.Mathf.Clamp((Composure - 0.5f) * 0.2f, -0.1f, 0.1f);

            SetCredibility(
                CredibilityFloor
                + CoverageWeight * coverage
                - FabricationPenalty * CaughtFabrications
                + composureAdjustment);
        }

        /// <summary>What a witness who covers nothing is left with. It puts a
        /// stonewalled run at 0.30 even at perfect composure — under §8's 0.45
        /// floor, so saying nothing is its own answer.</summary>
        public const float CredibilityFloor = 0.20f;
        public const float CoverageWeight = 0.60f;
        public const float FabricationPenalty = 0.18f;

        public void SetAccusation(Suspect suspect, float support)
        {
            Accusation = suspect;
            AccusationSupport = UnityEngine.Mathf.Clamp01(support);
        }

        public void SetCredibility(float value) => Credibility = UnityEngine.Mathf.Clamp01(value);

        public void Reset()
        {
            _turns.Clear();
            _caughtTrapIds.Clear();
            _reliableTensionSum = 0;
            _reliableTensionCount = 0;
            Composure = 1f;
            Credibility = 1f;
            Accusation = Suspect.None;
            AccusationSupport = 0f;
            MarkCoverage = 0;
        }
    }
}
