using System.Collections.Generic;

namespace FalsePositive.Flow
{
    /// <summary>
    /// The three tracked quantities behind ending selection
    /// (docs/STORY_SCRIPT.md §8) — none of them ever shown to the player as
    /// "truth" (G6). Credibility is a documented placeholder until A10 lands
    /// on Day 2; Composure is real from Day 1 (A1), computed only from
    /// RELIABLE prosody turns, because the sidecar itself flags which turns
    /// it does not trust and this class must not pretend those are signal.
    /// </summary>
    public sealed class SessionScore
    {
        private readonly List<TurnRecord> _turns = new List<TurnRecord>();
        private double _reliableTensionSum;
        private int _reliableTensionCount;

        public IReadOnlyList<TurnRecord> Turns => _turns;

        /// <summary>0..1. Real: mean tension across reliable-signal turns only,
        /// inverted and clamped (high sustained tension -> low composure).</summary>
        public float Composure { get; private set; } = 1f;

        /// <summary>0..1. PLACEHOLDER until A10 (Day 2) wires the real formula from
        /// docs/STORY_SCRIPT.md §8 (mark coverage, consistency, caught
        /// fabrications). Always 1.0 until then — nothing on Day 1 reads this,
        /// and anything that does must not mistake the placeholder for a
        /// finished score.</summary>
        public float Credibility { get; private set; } = 1f;

        public Suspect Accusation { get; private set; } = Suspect.None;
        public float AccusationSupport { get; private set; }
        public int CaughtFabrications { get; private set; }

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

        public void RecordFabrication(string trapId)
        {
            CaughtFabrications++;
        }

        public void SetAccusation(Suspect suspect, float support)
        {
            Accusation = suspect;
            AccusationSupport = UnityEngine.Mathf.Clamp01(support);
        }

        /// <summary>A10 (Day 2) sets this once, at P3 exit, from the real formula.</summary>
        public void SetCredibility(float value) => Credibility = UnityEngine.Mathf.Clamp01(value);

        public void Reset()
        {
            _turns.Clear();
            _reliableTensionSum = 0;
            _reliableTensionCount = 0;
            Composure = 1f;
            Credibility = 1f;
            Accusation = Suspect.None;
            AccusationSupport = 0f;
            CaughtFabrications = 0;
        }
    }
}
