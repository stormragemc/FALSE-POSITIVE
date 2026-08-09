using FalsePositive.Flow;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// How long to leave between spoken beats in a cutscene.
    ///
    /// This lives in one place because two systems have to agree on it exactly
    /// or the cutscene desyncs:
    ///
    ///  * VoTimelineBuilder bakes the gap into each .playable by advancing its
    ///    cursor, which is what actually spaces the audio.
    ///  * CutsceneDirector waits the same gap between beats, which is what
    ///    spaces the subtitles.
    ///
    /// Every cutscene id has a Timeline binding, so Timeline owns the audio and
    /// the director's beat loop only drives subtitles. If the two used
    /// different gaps the subtitles would drift further behind the voices with
    /// every line — about three seconds by the end of a seven-beat memory.
    /// Keeping the number here makes that mismatch impossible to introduce by
    /// editing one file and forgetting the other.
    ///
    /// **Changing these requires regenerating the timelines**: the gap is baked
    /// into the .playable assets, so edit here, then re-run
    /// Tools/False Positive/Bootstrap/6 - Populate Cutscene Recipes,
    /// 7 - Attach VO Clips, and the VO timeline build. Editing this file alone
    /// moves the subtitles and leaves the audio where it was.
    /// </summary>
    public static class CutscenePacing
    {
        /// <summary>Ordinary spacing — enough that lines do not run together.
        /// </summary>
        public const float DefaultBeatGapSeconds = 0.45f;

        /// <summary>The two flashbacks (CS-16A/CS-16B) run slower than the
        /// interrogation around them.
        ///
        /// They are the only place the player is asked to absorb a room, five
        /// faces and a relationship they have never been shown, all without
        /// being able to ask a question — and they were previously cut at
        /// conversation speed, which is why they were hard to follow. The
        /// interrogation beats keep the default: Spassky is talking *to* the
        /// player, who already has the context.</summary>
        public const float MemoryBeatGapSeconds = 1.1f;

        /// <summary>Held on the last beat of a memory before it gives way, so
        /// the image is allowed to settle instead of snapping out on the final
        /// syllable.</summary>
        public const float MemoryOutroSeconds = 1.4f;

        public static bool IsMemory(CutsceneId id) =>
            id == CutsceneId.GoodYears || id == CutsceneId.WhenItWentWrong;

        /// <summary>The gap to leave after each beat of <paramref name="id"/>.
        /// </summary>
        public static float BeatGapFor(CutsceneId id) =>
            IsMemory(id) ? MemoryBeatGapSeconds : DefaultBeatGapSeconds;
    }
}
