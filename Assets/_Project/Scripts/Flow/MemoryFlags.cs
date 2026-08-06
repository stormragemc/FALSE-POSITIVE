using System;
using System.Collections.Generic;
using System.Text;

namespace FalsePositive.Flow
{
    /// <summary>
    /// What the witness personally observed during the two memory scenes.
    /// Written by Person B's Interactable subclasses via MemoryFlagIds
    /// constants; read by A7b to brief the officer. See
    /// docs/STORY_SCRIPT.md §6/§7/§9 for why the absent half of Describe()'s
    /// output matters as much as the present half.
    /// </summary>
    public sealed class MemoryFlags
    {
        private readonly HashSet<string> _set = new HashSet<string>();
        private readonly MemoryFlagCatalog _catalog;

        public event Action<string> FlagSet;

        public MemoryFlags(MemoryFlagCatalog catalog = null)
        {
            _catalog = catalog;
        }

        public bool Has(string flagId) => !string.IsNullOrEmpty(flagId) && _set.Contains(flagId);

        public void Set(string flagId)
        {
            if (string.IsNullOrEmpty(flagId)) return;
            if (_set.Add(flagId)) FlagSet?.Invoke(flagId);
        }

        public void Clear() => _set.Clear();

        public IReadOnlyCollection<string> All => _set;

        /// <summary>Renders the witness-knowledge briefing folded into the P2/P3
        /// scene instruction (A7b). Emits BOTH sides for every known flag —
        /// what the witness saw AND, just as importantly, what they
        /// demonstrably did not — so the officer can tell a remembered
        /// detail from an invented one and the traps in
        /// docs/STORY_SCRIPT.md §7 have something to catch. Falls back to a
        /// minimal built-in table if no MemoryFlagCatalog was supplied
        /// (keeps this class usable in isolation, e.g. from the F1 debug
        /// overlay, before A7b wires the real one in).</summary>
        public string Describe()
        {
            var sb = new StringBuilder();
            IReadOnlyList<MemoryFlagEntry> entries = _catalog != null ? _catalog.Entries : DefaultEntries;
            foreach (MemoryFlagEntry entry in entries)
            {
                sb.AppendLine(Has(entry.FlagId) ? entry.PresentSentence : entry.AbsentSentence);
            }
            return sb.ToString().TrimEnd();
        }

        private static readonly MemoryFlagEntry[] DefaultEntries =
        {
            new MemoryFlagEntry(MemoryFlagIds.SawClock,
                "The witness looked at the mantel clock and could know the time was 00:52.",
                "The witness never looked at a clock; any specific time they give is invented, not observed."),
            new MemoryFlagEntry(MemoryFlagIds.SawFiveCups,
                "The witness noticed five cups on the table, meaning nobody had come in from outside.",
                "The witness never counted the cups on the table."),
            new MemoryFlagEntry(MemoryFlagIds.SawCoatSwap,
                "The witness is aware they were wearing Nick's coat, having swapped earlier in the night.",
                "The witness has not consciously registered that they were wearing Nick's coat."),
            new MemoryFlagEntry(MemoryFlagIds.HeardRadioWarning,
                "The witness heard the radio's storm warning to stay indoors.",
                "The witness never heard the radio's storm warning."),
            new MemoryFlagEntry(MemoryFlagIds.SawDoorClose,
                "The witness saw the door swinging shut, but never saw who went through it.",
                "The witness did not see the door close at all."),
            new MemoryFlagEntry(MemoryFlagIds.CalledForNick,
                "The witness went to the door and called Nick's name, and got no answer.",
                "The witness never went to the door or called out."),
            new MemoryFlagEntry(MemoryFlagIds.LeftDoorUnlocked,
                "The witness left the door unlocked when they walked away from it.",
                "The witness does not know what state they left the door in."),
            new MemoryFlagEntry(MemoryFlagIds.FoundDoorLocked,
                "The witness found the front door locked the next morning.",
                "The witness did not personally check whether the door was locked the next morning."),
            new MemoryFlagEntry(MemoryFlagIds.FoundKeyInside,
                "The witness found the key on a hook inside, next to the door.",
                "The witness never located the key."),
            new MemoryFlagEntry(MemoryFlagIds.SawGrilleIntact,
                "The witness inspected the broken window and saw the exterior grille was undamaged.",
                "The witness never closely inspected the broken window or its grille."),
            new MemoryFlagEntry(MemoryFlagIds.SawGlassInside,
                "The witness saw that the glass shards were on the inside of the room.",
                "The witness did not notice which side the glass shards fell on."),
            new MemoryFlagEntry(MemoryFlagIds.SawBody,
                "The witness saw Nick's body outside, face down in the snow.",
                "The witness has not yet seen the body directly."),
            new MemoryFlagEntry(MemoryFlagIds.HeardIvyAlibi,
                "The witness heard Ivy volunteer, unprompted, that she was with Aaron all night.",
                "The witness did not hear Ivy say anything about her or Aaron's whereabouts overnight."),
            new MemoryFlagEntry(MemoryFlagIds.HeardAaronDeflect,
                "The witness heard Aaron deflect Priya's question about who locked the door.",
                "The witness did not hear anyone discuss who locked the door."),
            new MemoryFlagEntry(MemoryFlagIds.CarriedBody,
                "The witness helped carry Nick's body inside, so their own hands and prints are on it.",
                "The witness did not physically touch or move the body."),
        };
    }

    public readonly struct MemoryFlagEntry
    {
        public string FlagId { get; }
        public string PresentSentence { get; }
        public string AbsentSentence { get; }

        public MemoryFlagEntry(string flagId, string presentSentence, string absentSentence)
        {
            FlagId = flagId;
            PresentSentence = presentSentence;
            AbsentSentence = absentSentence;
        }
    }
}
