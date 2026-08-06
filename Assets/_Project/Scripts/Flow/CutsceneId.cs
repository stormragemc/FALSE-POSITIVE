namespace FalsePositive.Flow
{
    /// <summary>
    /// Every cutscene in docs/STORY_SCRIPT.md §5, declared up front even though
    /// most have no Timeline behind them until Day 2. Person A owns this enum;
    /// Person B owns CutsceneDirector, the ICutscenePlayer implementation that
    /// plays them. Splitting it this way means GameFlowDirector.RequestCutscene
    /// compiles on Person A's branch without waiting on Person B's Timeline work.
    /// </summary>
    public enum CutsceneId
    {
        None,
        Wake,
        SpasskyAnswer,
        FuzzyToNight,
        StandFromChair,
        RadioClears,
        SomeoneLeft,
        CallForNick,
        FuzzyToInterrogation,
        FuzzyToMorning,
        PriyaScreams,
        TheyComeDown,
        OutIntoTheSnow,
        TheCarry,
        TheSofa,
        FuzzyToVerdict,
        FlashbackAaron,
        FlashbackIvy,
        FlashbackPriya,
        EndingDavid,
        EndingAaron,
        EndingIvy,
        EndingPriya,
    }
}
