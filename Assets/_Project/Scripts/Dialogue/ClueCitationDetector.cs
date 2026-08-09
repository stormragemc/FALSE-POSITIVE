using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace FalsePositive.Dialogue
{
    /// <summary>The seven pieces of reasoning docs/STORY_SCRIPT.md §8 will
    /// accept as support for naming Aaron.</summary>
    public enum CitedClue
    {
        DoorLocked,
        KeyInside,
        GrilleIntact,
        ThinJacket,
        AaronLearnedOfAffair,
        IvyVolunteeredAlibi,
        AaronProposedTheMove,
    }

    /// <summary>
    /// Counts which of §8's seven clues the witness actually brought up.
    ///
    /// §8 gates E_AARON on citing two or more of them, because naming Aaron
    /// with nothing behind it is the trap in §7 — right, and unable to say
    /// why. Without this the name alone would be enough and that trap would
    /// never close.
    ///
    /// Scope note: citations are counted across the whole session, not only
    /// the verdict phase. A witness who established the intact grille in P2
    /// and then names Aaron in P3 has given his reason; making him repeat it
    /// inside P3's short turn budget would fail players for pacing rather
    /// than for reasoning.
    /// </summary>
    public static class ClueCitationDetector
    {
        private static readonly Dictionary<CitedClue, Regex[]> Patterns = new Dictionary<CitedClue, Regex[]>
        {
            [CitedClue.DoorLocked] = Build(
                @"\bdoor\b(?:\W+\w+){0,4}?\W+(?:was\s+)?(?:locked|bolted|barred)",
                @"\b(?:locked|bolted|barred)\b(?:\W+\w+){0,3}?\W+\bdoor\b",
                @"\bbolt\b(?:\W+\w+){0,3}?\W+(?:across|thrown|drawn|shot)"),

            [CitedClue.KeyInside] = Build(
                @"\bkey\b(?:\W+\w+){0,5}?\W+(?:inside|in\s+the\s+cabin|on\s+the\s+(?:table|hook|nail)|still\s+there)",
                @"\bkey\b(?:\W+\w+){0,4}?\W+(?:never|wasn'?t|not)\W+(?:outside|taken|with)"),

            [CitedClue.GrilleIntact] = Build(
                @"\b(?:grille|grill|grate|bars?|mesh)\b(?:\W+\w+){0,4}?\W+(?:intact|unbroken|still|fine|attached|screwed|in\s+place|not\s+(?:broken|bent|cut))",
                @"\b(?:nobody|no\s+one|couldn'?t|can'?t)\b(?:\W+\w+){0,5}?\W+(?:through|fit)\b(?:\W+\w+){0,3}?\W+\bwindow\b",
                @"\bwindow\b(?:\W+\w+){0,5}?\W+\b(?:broken\s+from\s+)?inside\b"),

            [CitedClue.ThinJacket] = Build(
                @"\b(?:thin|light|my|wrong|summer)\b\W+\w{0,10}\W*\bjackets?\b",
                @"\bjackets?\b(?:\W+\w+){0,4}?\W+(?:swap|swapp?ed|switch|traded|mine|his)",
                @"\b(?:parkas?|coats?)\b(?:\W+\w+){0,4}?\W+(?:swap|swapp?ed|switch|traded|threw|gave)",
                // Verb first — "we swapped jackets" is how players actually say
                // it, and the noun-first patterns above all miss it.
                @"\b(?:swap|swapp?ed|switch(?:ed)?|traded|exchanged|threw|tossed|gave|handed|lent)\b" +
                    @"(?:\W+\w+){0,3}?\W+(?:jackets?|coats?|parkas?)",
                @"\bunderdressed\b|\bnot\s+dressed\s+for\b"),

            [CitedClue.AaronLearnedOfAffair] = Build(
                @"\baaron\b(?:\W+\w+){0,6}?\W+(?:knew|know|knows|found\s+out|heard|learned|realis|realiz)",
                @"\b(?:affair|sleeping\s+with|seeing\s+each\s+other|two\s+years)\b(?:\W+\w+){0,8}?\W+\baaron\b",
                @"\bnick\b(?:\W+\w+){0,6}?\W+\bivy\b(?:\W+\w+){0,8}?\W+\baaron\b(?:\W+\w+){0,4}?\W+(?:heard|there|listening)"),

            [CitedClue.IvyVolunteeredAlibi] = Build(
                @"\bivy\b(?:\W+\w+){0,8}?\W+(?:alibi|vouch|covered?\s+for|spoke\s+up|volunteer|offered)",
                @"\bivy\b(?:\W+\w+){0,8}?\W+(?:said|told\s+you)\b(?:\W+\w+){0,6}?\W+\baaron\b(?:\W+\w+){0,6}?\W+(?:with\s+her|beside|next\s+to|asleep|in\s+bed|never\s+left)",
                @"\b(?:nobody|no\s+one)\s+asked\s+her\b"),

            [CitedClue.AaronProposedTheMove] = Build(
                @"\baaron\b(?:\W+\w+){0,8}?\W+(?:suggested|proposed|wanted|pushed|insisted|idea)\b(?:\W+\w+){0,6}?\W+(?:move|moving|shed|outbuilding|other\s+cabin|split\s+up|separate)",
                @"\b(?:move|moving)\s+(?:to\s+)?the\s+shed\b(?:\W+\w+){0,8}?\W+\baaron\b",
                @"\bhis\s+idea\b(?:\W+\w+){0,6}?\W+(?:move|shed|split)"),
        };

        private static Regex[] Build(params string[] sources)
        {
            var built = new Regex[sources.Length];
            for (int i = 0; i < sources.Length; i++)
            {
                built[i] = new Regex(sources[i], RegexOptions.IgnoreCase | RegexOptions.Compiled);
            }
            return built;
        }

        /// <summary>Adds every clue <paramref name="transcript"/> raises to
        /// <paramref name="into"/>. A set, so repeating one clue across five
        /// turns still counts once — §8 asks for two distinct reasons.</summary>
        public static void Observe(string transcript, HashSet<CitedClue> into)
        {
            if (string.IsNullOrWhiteSpace(transcript) || into == null) return;

            foreach (KeyValuePair<CitedClue, Regex[]> entry in Patterns)
            {
                if (into.Contains(entry.Key)) continue;
                foreach (Regex pattern in entry.Value)
                {
                    if (pattern.IsMatch(transcript))
                    {
                        into.Add(entry.Key);
                        break;
                    }
                }
            }
        }
    }
}
