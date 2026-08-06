using System;
using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// M1's radio, on the fireplace mantel. Built to the §10 cut-ladder's own
    /// sanctioned cheap form for this beat ("a single E press — the cutscene
    /// is the point, not the puzzle") rather than the one-axis snap minigame,
    /// so this is not a placeholder to upgrade later so much as the shipped
    /// Day-1 form; a real minigame can replace OnInteract's body without
    /// touching Cleared or the memory flag it writes.
    /// </summary>
    public sealed class RadioTuner : Interactable
    {
        public event Action Cleared;

        public override void OnInteract()
        {
            MarkComplete();
            Cleared?.Invoke();
        }
    }
}
