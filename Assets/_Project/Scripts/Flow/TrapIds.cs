namespace FalsePositive.Flow
{
    /// <summary>
    /// The six details the witness cannot have observed
    /// (docs/STORY_SCRIPT.md §7), mirrored from the sidecar's
    /// <c>fabrication.TRAP_IDS</c>. A confident answer to any of them is a
    /// fabrication — the witness stating as memory something they never
    /// witnessed. Being right is not the same as having seen it.
    ///
    /// Kept in step with the backend by Sidecar/tests/test_unity_contract.py.
    /// A rename on one side alone fails no runtime check: SessionScore would
    /// simply stop recognising the id and nothing would ever be caught again,
    /// which is why the contract test exists.
    /// </summary>
    public static class TrapIds
    {
        /// <summary>Who went through the door — it was already closing.</summary>
        public const string Door = "trap_door";

        /// <summary>A clock time — readable only by looking at the mantel clock.</summary>
        public const string Time = "trap_time";

        /// <summary>That Nick answered — the yell gets nothing back, deliberately.</summary>
        public const string Answer = "trap_answer";

        /// <summary>That Nick was already outside — never seen after the fire.</summary>
        public const string Outside = "trap_outside";

        /// <summary>Who locked the door — David was unconscious.</summary>
        public const string Lock = "trap_lock";

        /// <summary>The window — broken while he was unconscious, unseeable at night.</summary>
        public const string Window = "trap_window";

        public static readonly string[] All = { Door, Time, Answer, Outside, Lock, Window };

        /// <summary>True for an id this build understands. SessionScore checks
        /// this before counting, so a backend typo — or an id coaxed out of the
        /// model by something a player said — can never inflate the count.</summary>
        public static bool IsKnown(string trapId)
        {
            if (string.IsNullOrEmpty(trapId)) return false;
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i] == trapId) return true;
            }
            return false;
        }
    }
}
