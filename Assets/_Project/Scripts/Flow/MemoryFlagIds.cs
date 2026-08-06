namespace FalsePositive.Flow
{
    /// <summary>
    /// Person A owns this file. Person B's Interactable subclasses (RadioTuner,
    /// KeyPickup, DoorInteractable, InspectPoint, ...) write these constants to
    /// GameFlowDirector.Flags and nothing else — a typo'd string literal in a
    /// scene is silent, a typo'd constant does not compile. See
    /// docs/STORY_SCRIPT.md §6/§7/§9 for what each flag is standing in for.
    /// </summary>
    public static class MemoryFlagIds
    {
        public const string SawClock = "saw_clock";
        public const string SawFiveCups = "saw_five_cups";
        public const string SawCoatSwap = "saw_coat_swap";
        public const string HeardRadioWarning = "heard_radio_warning";
        public const string SawDoorClose = "saw_door_close";
        public const string CalledForNick = "called_for_nick";
        public const string LeftDoorUnlocked = "left_door_unlocked";
        public const string FoundDoorLocked = "found_door_locked";
        public const string FoundKeyInside = "found_key_inside";
        public const string SawGrilleIntact = "saw_grille_intact";
        public const string SawGlassInside = "saw_glass_inside";
        public const string SawBody = "saw_body";
        public const string HeardIvyAlibi = "heard_ivy_alibi";
        public const string HeardAaronDeflect = "heard_aaron_deflect";
        public const string CarriedBody = "carried_body";

        /// <summary>All ids, in the order they appear in STORY_SCRIPT.md — used by
        /// MemoryFlagCatalog to validate the prompt file covers every flag, and
        /// by the F1 debug overlay's toggle list (A7b).</summary>
        public static readonly string[] All =
        {
            SawClock, SawFiveCups, SawCoatSwap, HeardRadioWarning, SawDoorClose,
            CalledForNick, LeftDoorUnlocked, FoundDoorLocked, FoundKeyInside,
            SawGrilleIntact, SawGlassInside, SawBody, HeardIvyAlibi,
            HeardAaronDeflect, CarriedBody,
        };
    }
}
