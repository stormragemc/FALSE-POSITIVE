using System;

namespace FalsePositive.Flow
{
    /// <summary>
    /// Person A owns this interface; Person B implements it on CutsceneDirector.
    /// GameFlowDirector.RequestCutscene works with no player registered at all —
    /// it does a ScreenFader blink and raises completion on the next frame — so
    /// Track A can run the whole game end to end on Day 1 with zero cutscenes
    /// built. See docs/GAME_COMPLETION_PLAN.md §5, Day-1 exit criterion #12.
    /// </summary>
    public interface ICutscenePlayer
    {
        bool IsPlaying { get; }
        event Action<CutsceneId> Finished;
        void Play(CutsceneId id);
    }
}
