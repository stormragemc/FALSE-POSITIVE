using System.Collections.Generic;
using System.Text;

namespace FalsePositive.Dialogue
{
    /// <summary>One thing the witness said, and when he said it.</summary>
    public readonly struct QuotedLine
    {
        public int TurnNumber { get; }
        public string Text { get; }

        public QuotedLine(int turnNumber, string text)
        {
            TurnNumber = turnNumber;
            Text = text;
        }
    }

    /// <summary>
    /// A11 — builds the closing card of docs/STORY_SCRIPT.md §4 P4_ENDING: the
    /// fixed three lines, then two or three of the witness's own sentences
    /// quoted back with turn numbers.
    ///
    /// **G6.** The card presents the lines and stops. No verdict word, no
    /// commentary, no framing that tells the player what the quotes prove —
    /// §4 states it plainly: *"It never says they lied."* The whole effect is
    /// that the player recognises their own certainty without being told what
    /// to make of it, so anything editorial here would both break a graded
    /// requirement and blunt the ending. Keep the header neutral.
    /// </summary>
    public static class OutcomeCardComposer
    {
        public const int MaxQuotes = 3;

        /// <summary>Long enough that the quote reads as a claim rather than a
        /// fragment of back-and-forth.</summary>
        private const int MinQuoteLength = 12;

        /// <summary>Past this the card stops being a card. Trimmed on a word
        /// boundary so a quote never ends mid-word.</summary>
        private const int MaxQuoteLength = 140;

        private const string FixedCard =
            "14:20 — Ivy has asked to make a second statement.\n" +
            "She is still waiting.\n\nFALSE POSITIVE";

        /// <summary>Neutral by design — see the G6 note on the class.</summary>
        private const string QuoteHeader = "You said:";

        public static string Compose(IReadOnlyList<QuotedLine> quotable)
        {
            var card = new StringBuilder(FixedCard);
            if (quotable == null || quotable.Count == 0) return card.ToString();

            var kept = new List<QuotedLine>(MaxQuotes);
            var seenTurns = new HashSet<int>();

            foreach (QuotedLine line in quotable)
            {
                if (kept.Count >= MaxQuotes) break;
                string text = (line.Text ?? string.Empty).Trim();
                if (text.Length < MinQuoteLength) continue;
                // A turn can be both an unsupported detail and the accusation;
                // it is still one sentence and gets quoted once.
                if (!seenTurns.Add(line.TurnNumber)) continue;
                kept.Add(new QuotedLine(line.TurnNumber, Shorten(text)));
            }

            if (kept.Count == 0) return card.ToString();

            card.Append("\n\n").Append(QuoteHeader);
            foreach (QuotedLine line in kept)
            {
                card.Append("\n\nTurn ").Append(line.TurnNumber).Append(" — \"")
                    .Append(line.Text).Append('"');
            }
            return card.ToString();
        }

        private static string Shorten(string text)
        {
            if (text.Length <= MaxQuoteLength) return text;
            int cut = text.LastIndexOf(' ', MaxQuoteLength - 1);
            if (cut < MaxQuoteLength / 2) cut = MaxQuoteLength - 1;
            return text.Substring(0, cut).TrimEnd(',', ';', ':', ' ') + "…";
        }
    }
}
