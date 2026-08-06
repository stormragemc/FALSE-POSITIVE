using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// Look-and-press placeholder prop — the cups, the mantel clock, the coat,
    /// the broken window, the body. One press writes memoryFlag and marks
    /// itself complete; there is nothing else to the interaction, except an
    /// optional one-shot SFX (Phase 5) for props where inspecting makes a
    /// sound (the broken pane's glass crunch).
    /// </summary>
    public sealed class InspectPoint : Interactable
    {
        [SerializeField] private AudioClip inspectClip;

        public override void OnInteract()
        {
            if (inspectClip != null) AudioSource.PlayClipAtPoint(inspectClip, transform.position);
            MarkComplete();
        }
    }
}
