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

        // STORY_SCRIPT.md §4 P3_VERDICT / §5 CS-16A and CS-16B. Appended at
        // the end deliberately: CutsceneRecipeBuilder serializes recipes by
        // enumValueIndex, so inserting these in story order would silently
        // repoint every recipe already wired on the CutsceneDirector.
        GoodYears,
        WhenItWentWrong,

        // The officer's scripted P3 beats, spoken in the interrogation
        // room with the mic down (STORY_SCRIPT.md §4 P3_VERDICT). They
        // bracket the memory pair: photograph -> CS-16A -> question ->
        // CS-16B -> "Who, David?", at which point the mic opens.
        P3Photograph,
        P3AfterGoodYears,
        P3WhoDavid,
    }
}
