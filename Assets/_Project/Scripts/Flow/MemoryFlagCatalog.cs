using System;
using System.Collections.Generic;
using UnityEngine;

namespace FalsePositive.Flow
{
    /// <summary>
    /// Parses Assets/_Project/Prompts/memory_flags.txt into the present/absent
    /// sentence pairs MemoryFlags.Describe() renders. Keeping the wording in a
    /// file rather than a C# literal is a G3 requirement (docs/IMPLEMENTATION_PLAN.md)
    /// and lets it be edited without a recompile. Format: one row per flag,
    /// pipe-delimited: `flag_id | present sentence | absent sentence`. Blank
    /// lines and lines starting with # are ignored.
    /// </summary>
    [CreateAssetMenu(menuName = "False Positive/Memory Flag Catalog")]
    public sealed class MemoryFlagCatalog : ScriptableObject
    {
        [SerializeField] private TextAsset source;

        private List<MemoryFlagEntry> _entries;

        public IReadOnlyList<MemoryFlagEntry> Entries
        {
            get
            {
                if (_entries == null) Parse();
                return _entries;
            }
        }

        private void OnEnable() => _entries = null;

        private void Parse()
        {
            _entries = new List<MemoryFlagEntry>();
            if (source == null)
            {
                Debug.LogError("[MemoryFlagCatalog] No source TextAsset assigned.");
                return;
            }

            var seen = new HashSet<string>();
            string[] lines = source.text.Split('\n');
            foreach (string rawLine in lines)
            {
                string line = rawLine.Trim().TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] parts = line.Split('|');
                if (parts.Length != 3)
                {
                    Debug.LogError($"[MemoryFlagCatalog] Malformed row (expected 3 '|'-delimited fields): \"{line}\"");
                    continue;
                }

                string flagId = parts[0].Trim();
                string present = parts[1].Trim();
                string absent = parts[2].Trim();
                if (flagId.Length == 0 || present.Length == 0 || absent.Length == 0)
                {
                    Debug.LogError($"[MemoryFlagCatalog] Empty field in row: \"{line}\"");
                    continue;
                }

                seen.Add(flagId);
                _entries.Add(new MemoryFlagEntry(flagId, present, absent));
            }

            foreach (string expected in MemoryFlagIds.All)
            {
                if (!seen.Contains(expected))
                {
                    Debug.LogError($"[MemoryFlagCatalog] {source.name} is missing a row for \"{expected}\" — " +
                        "that trap/clue will be silently absent from every briefing.");
                }
            }
        }
    }
}
