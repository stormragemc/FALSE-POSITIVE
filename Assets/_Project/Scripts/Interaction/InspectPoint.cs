namespace FalsePositive.Interaction
{
    /// <summary>
    /// Look-and-press placeholder prop — the cups, the mantel clock, the coat,
    /// the broken window, the body. One press writes memoryFlag and marks
    /// itself complete; there is nothing else to the interaction.
    /// </summary>
    public sealed class InspectPoint : Interactable
    {
        public override void OnInteract() => MarkComplete();
    }
}
