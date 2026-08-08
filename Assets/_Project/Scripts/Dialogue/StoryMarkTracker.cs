using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FalsePositive.Dialogue
{
    /// <summary>
    /// Client-side fallback for backend ask B2 (docs/GAME_COMPLETION_PLAN.md
    /// §7) — folds each turn's transcript into the seven story marks
    /// (docs/STORY_SCRIPT.md §6) via a keyword table loaded from
    /// Assets/_Project/Prompts/story_marks.txt. Cruder than a real NLU
    /// classifier; PhaseDialogueController treats AllCovered as the
    /// preferred P2 exit condition with the turn cap as the backstop, so a
    /// missed keyword costs nothing but an early exit.
    ///
    /// Guardrail #6/S2 (docs/GAME_COMPLETION_PLAN.md §8): Observe takes the
    /// transcript purely AS DATA — it is pattern-matched to emit mark ids,
    /// never re-emitted into any prompt or instruction sent back to the LLM.
    /// </summary>
    public sealed class StoryMarkTracker
    {
        private static readonly StoryMarkId[] AllMarks =
        {
            StoryMarkId.Fire, StoryMarkId.Argument, StoryMarkId.NickLeft, StoryMarkId.Door,
            StoryMarkId.Lock, StoryMarkId.Sleep, StoryMarkId.Morning,
        };

        private readonly Dictionary<StoryMarkId, Regex[]> _patterns = new Dictionary<StoryMarkId, Regex[]>();
        private readonly HashSet<StoryMarkId> _covered = new HashSet<StoryMarkId>();

        public event Action<StoryMarkId> MarkCovered;

        public StoryMarkTracker(TextAsset source)
        {
            LoadPatterns(source);
        }

        public bool IsCovered(StoryMarkId id) => _covered.Contains(id);
        public int CoveredCount => _covered.Count;

        /// <summary>The seven marks of docs/STORY_SCRIPT.md §6, so callers
        /// scoring coverage don't hard-code the count.</summary>
        public static int TotalMarks => AllMarks.Length;
        public bool AllCovered => _covered.Count >= AllMarks.Length;

        public IReadOnlyList<StoryMarkId> Uncovered
        {
            get
            {
                var result = new List<StoryMarkId>();
                foreach (StoryMarkId id in AllMarks)
                {
                    if (!_covered.Contains(id)) result.Add(id);
                }
                return result;
            }
        }

        public void Observe(string transcript)
        {
            if (string.IsNullOrWhiteSpace(transcript)) return;

            foreach (StoryMarkId id in AllMarks)
            {
                if (_covered.Contains(id)) continue;
                if (!_patterns.TryGetValue(id, out Regex[] patterns)) continue;

                foreach (Regex pattern in patterns)
                {
                    if (pattern.IsMatch(transcript))
                    {
                        _covered.Add(id);
                        MarkCovered?.Invoke(id);
                        break;
                    }
                }
            }
        }

        public void Reset() => _covered.Clear();

        private void LoadPatterns(TextAsset source)
        {
            if (source == null)
            {
                Debug.LogError("[StoryMarkTracker] No source TextAsset assigned — story marks will never be covered.");
                return;
            }

            foreach (string rawLine in source.text.Split('\n'))
            {
                string line = rawLine.Trim().TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;

                int separator = line.IndexOf('|');
                if (separator < 0)
                {
                    Debug.LogError($"[StoryMarkTracker] Malformed row (expected 'mark_id | keywords'): \"{line}\"");
                    continue;
                }

                string idText = line.Substring(0, separator).Trim();
                string keywordsText = line.Substring(separator + 1).Trim();
                if (!TryParseMarkId(idText, out StoryMarkId id))
                {
                    Debug.LogError($"[StoryMarkTracker] Unknown mark id \"{idText}\" in row: \"{line}\"");
                    continue;
                }

                string[] keywords = keywordsText.Split(',');
                var patterns = new List<Regex>();
                foreach (string rawKeyword in keywords)
                {
                    string keyword = rawKeyword.Trim();
                    if (keyword.Length == 0) continue;
                    // \b works at the edges of a multi-word phrase too — it
                    // only needs a word-char/non-word-char boundary at each
                    // end, not per internal word.
                    string escaped = Regex.Escape(keyword);
                    patterns.Add(new Regex($@"\b{escaped}\b", RegexOptions.IgnoreCase));
                }
                _patterns[id] = patterns.ToArray();
            }

            foreach (StoryMarkId id in AllMarks)
            {
                if (!_patterns.ContainsKey(id))
                {
                    Debug.LogError($"[StoryMarkTracker] {source.name} has no row for \"{id}\" — that mark can never be covered.");
                }
            }
        }

        private static bool TryParseMarkId(string text, out StoryMarkId id)
        {
            switch (text.ToLowerInvariant())
            {
                case "fire": id = StoryMarkId.Fire; return true;
                case "argument": id = StoryMarkId.Argument; return true;
                case "nick_left": id = StoryMarkId.NickLeft; return true;
                case "door": id = StoryMarkId.Door; return true;
                case "lock": id = StoryMarkId.Lock; return true;
                case "sleep": id = StoryMarkId.Sleep; return true;
                case "morning": id = StoryMarkId.Morning; return true;
                default:
                    id = default;
                    return false;
            }
        }
    }
}
