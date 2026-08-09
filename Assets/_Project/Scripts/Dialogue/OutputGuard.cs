using System.Text.RegularExpressions;

namespace FalsePositive.Dialogue
{
    /// <summary>
    /// A12 — the client-side half of ROADMAP S1's never-cut output filter, and
    /// the last thing to touch a reply before it is shown.
    ///
    /// The backend runs `Sidecar/output_safety.py` before TTS, so this is the
    /// second of two layers, deliberately: a reply reaches the player through a
    /// network boundary, and the client should not display whatever arrives
    /// simply because a server upstream promised to have checked it.
    ///
    /// **What this layer can and cannot do.** The audio is synthesised
    /// server-side, before this runs. Blocking text here changes the subtitle
    /// and everything downstream of it; it does not un-speak the clip. The
    /// spoken line is guarded by `output_safety.filter_spoken_text`, and that
    /// is the layer to fix if something is ever heard but not seen.
    ///
    /// Three jobs, per the plan's A12 row:
    ///  - length cap, so a runaway generation cannot fill the screen;
    ///  - strip markdown and stage directions, which break the fiction;
    ///  - block persona-leak phrases, which break G6.
    ///
    /// Stripping is for formatting only. Anything touching G6 — talk of lying,
    /// of voice as evidence, of the prompt itself — is replaced wholesale
    /// rather than edited, because editing such a line leaves a sentence that
    /// still gestures at the thing it must not say.
    /// </summary>
    public static class OutputGuard
    {
        /// <summary>Matches Sidecar/output_safety.py's FALLBACK_LINE so a
        /// blocked reply reads identically whichever layer caught it.</summary>
        public const string FallbackLine = "Let's come back to that.";

        /// <summary>Matches output_safety.MAX_SPOKEN_CHARACTERS. Kept equal on
        /// purpose: a different cap here would mean the subtitle and the audio
        /// disagree about where the line ends.</summary>
        public const int MaxCharacters = 480;

        /// <summary>Bold and italic runs, unwrapped to the text inside.
        ///
        /// Must run before <see cref="StageDirection"/>, and this ordering is
        /// load-bearing: "**Answer the question.**" also matches the single-
        /// asterisk stage-direction pattern, so stripping directions first
        /// deletes the sentence and leaves the officer saying the fallback.
        /// Two asterisks is emphasis and its text is kept; one is an emote and
        /// its text is not.</summary>
        private static readonly Regex BoldEmphasis = new Regex(
            @"\*{2,3}([^*\n]{1,200})\*{2,3}",
            RegexOptions.Compiled);

        /// <summary>Stage directions and emotes — "*leans forward*",
        /// "(quietly)", and the "[angry]" mood tags, which the server strips
        /// but which must never survive to the screen if one slips through.
        /// </summary>
        private static readonly Regex StageDirection = new Regex(
            @"\*[^*\n]{1,80}\*|\([^)\n]{1,80}\)|\[[^\]\n]{1,80}\]",
            RegexOptions.Compiled);

        /// <summary>Markdown emphasis, headings, list bullets and code fences.
        /// A police officer does not speak in bullet points.</summary>
        private static readonly Regex Markdown = new Regex(
            @"^\s{0,3}(?:#{1,6}\s+|[-*+]\s+|>\s+)|`+|_{2,}|\*{1,3}",
            RegexOptions.Compiled | RegexOptions.Multiline);

        /// <summary>Speaker labels the model sometimes prefixes to its line.
        /// </summary>
        private static readonly Regex SpeakerLabel = new Regex(
            @"^\s*(?:officer\s+)?spassky\s*:\s*",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>Mirrors output_safety._BLOCKED_PATTERNS. Kept in step with
        /// that file — if a pattern is added there, add it here.</summary>
        private static readonly Regex[] Blocked =
        {
            new Regex(@"\b(?:you|the witness)\s+(?:are|were|must be|have been)\s+" +
                @"(?:lying|a liar|truthful|deceptive)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            new Regex(@"\b(?:your|the witness'?s)\s+(?:voice|tone|speech|pauses?|affect)\s+" +
                @"(?:proves|shows|confirms|means|tells me)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            new Regex(@"\b(?:lie|truthfulness|deception|guilt|affect|emotion)\s+" +
                @"(?:score|probability|detector|model|analysis|reading|label)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            new Regex(@"\b(?:system prompt|system instruction|developer message|" +
                @"hidden instructions|cop_persona|local_affect_context|witness_transcript)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),

            // Client-only: the model breaking frame as an assistant. The server
            // list does not cover this because it is a fiction problem rather
            // than a safety one, and this is the layer nearest the screen.
            new Regex(@"\b(?:as an ai|language model|i'?m an ai|i cannot help with)\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase),
        };

        private static readonly Regex Whitespace = new Regex(@"\s+", RegexOptions.Compiled);

        /// <summary>Returns a reply safe to display, or <see cref="FallbackLine"/>.
        /// Never returns null or empty.</summary>
        public static string Filter(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return FallbackLine;

            string cleaned = SpeakerLabel.Replace(text, string.Empty);
            cleaned = BoldEmphasis.Replace(cleaned, "$1");
            cleaned = StageDirection.Replace(cleaned, " ");
            cleaned = Markdown.Replace(cleaned, string.Empty);
            cleaned = Whitespace.Replace(cleaned, " ").Trim();

            // Checked after stripping so "*[system prompt]*" cannot smuggle a
            // blocked phrase past the patterns inside decoration.
            if (cleaned.Length == 0 || cleaned.Length > MaxCharacters) return FallbackLine;

            foreach (Regex pattern in Blocked)
            {
                if (pattern.IsMatch(cleaned)) return FallbackLine;
            }
            return cleaned;
        }
    }
}
