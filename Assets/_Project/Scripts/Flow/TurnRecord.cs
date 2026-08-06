namespace FalsePositive.Flow
{
    /// <summary>
    /// One turn's worth of scoring inputs. Transcript is held in memory only
    /// for the lifetime of the playthrough, never written to disk — see
    /// guardrail #3/#4 in docs/GAME_COMPLETION_PLAN.md §8.
    /// </summary>
    public readonly struct TurnRecord
    {
        public int TurnNumber { get; }
        public GamePhase Phase { get; }
        public string Transcript { get; }
        public string ReplyText { get; }
        public float Tension { get; }
        public float Arousal { get; }
        public float SignalConfidence { get; }
        public bool ProsodyReliable { get; }

        public TurnRecord(
            int turnNumber,
            GamePhase phase,
            string transcript,
            string replyText,
            float tension,
            float arousal,
            float signalConfidence,
            bool prosodyReliable)
        {
            TurnNumber = turnNumber;
            Phase = phase;
            Transcript = transcript;
            ReplyText = replyText;
            Tension = tension;
            Arousal = arousal;
            SignalConfidence = signalConfidence;
            ProsodyReliable = prosodyReliable;
        }
    }
}
