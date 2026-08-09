using System.Text.RegularExpressions;
using FalsePositive.Flow;

namespace FalsePositive.Dialogue
{
    /// <summary>
    /// A9. Reads a P3 transcript and decides whether the witness has actually
    /// accused one person.
    ///
    /// Replaces a bare `transcript.Contains("aaron")` triple, which failed on
    /// the two things players do most: naming someone while ruling another out
    /// ("it wasn't Ivy, it was Aaron" counted as two names and therefore no
    /// accusation at all), and thinking out loud ("maybe Aaron?"), which counted
    /// as a firm accusation.
    ///
    /// The distinction matters beyond tidiness: docs/STORY_SCRIPT.md §8 sends a
    /// named-but-unsupported accusation to E_DAVID, so a false positive here
    /// hands the player an ending they did not earn, and a false negative throws
    /// away one they did.
    /// </summary>
    public static class SuspectNameDetector
    {
        /// <summary>Negated mentions — "not Aaron", "wasn't Ivy", "other than
        /// Priya". These remove a candidate rather than nominate one.</summary>
        private static readonly Regex Negated = new Regex(
            @"\b(?:not|isn'?t|wasn'?t|ain'?t|never|no,?|other\s+than|besides|except|rather\s+than)\b" +
            @"(?:\s+\w+){0,3}?\s+(aaron|ivy|priya)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Hedges anywhere in the sentence. "Maybe Aaron" is the witness
        /// reasoning in front of the officer, which §7 explicitly permits and
        /// does not treat as an accusation.</summary>
        private static readonly Regex Hedged = new Regex(
            @"\b(?:maybe|perhaps|possibly|probably|might|could\s+be|i\s+think|i\s+guess|" +
            @"i'?m\s+not\s+sure|not\s+sure|suppose|either|or\s+maybe|unless)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex AaronWord = new Regex(@"\baaron'?s?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex IvyWord = new Regex(@"\bivy'?s?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PriyaWord = new Regex(@"\bpriya'?s?\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>Returns the single suspect accused, or None.
        ///
        /// None is returned for: no name, a hedged name, every name negated, or
        /// two or more still standing after negations are removed — §8 requires
        /// the accusation to be unambiguous.</summary>
        public static Suspect Detect(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return Suspect.None;

            bool aaron = AaronWord.IsMatch(transcript);
            bool ivy = IvyWord.IsMatch(transcript);
            bool priya = PriyaWord.IsMatch(transcript);
            if (!aaron && !ivy && !priya) return Suspect.None;

            // Strike out anyone explicitly ruled out, so "it wasn't Ivy, it was
            // Aaron" leaves Aaron standing alone instead of cancelling itself.
            foreach (Match match in Negated.Matches(transcript))
            {
                switch (match.Groups[1].Value.ToLowerInvariant())
                {
                    case "aaron": aaron = false; break;
                    case "ivy": ivy = false; break;
                    case "priya": priya = false; break;
                }
            }

            int remaining = (aaron ? 1 : 0) + (ivy ? 1 : 0) + (priya ? 1 : 0);
            if (remaining != 1) return Suspect.None;

            // A hedge only disqualifies a name that survived: "it wasn't Ivy,
            // maybe" should not veto a firm "it was Aaron" in the same breath.
            // Checked after the count so a single clear nomination is not lost
            // to a hedge attached to the name being ruled out.
            if (Hedged.IsMatch(transcript)) return Suspect.None;

            if (aaron) return Suspect.Aaron;
            if (ivy) return Suspect.Ivy;
            return Suspect.Priya;
        }
    }
}
