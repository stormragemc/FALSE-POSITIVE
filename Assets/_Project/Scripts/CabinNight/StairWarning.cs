using UnityEngine;
using FalsePositive.Flow;

namespace FalsePositive.CabinNight
{
    /// <summary>
    /// Speaks a one-line refusal when the player walks at the staircase, so the
    /// stairs read as "David won't go up" rather than as an invisible wall the
    /// level forgot to finish.
    ///
    /// This is the soft half of a pair. The hard half is the sibling
    /// "Blocker_Stairs" volume (MemorySceneDressing.AddStairBlocker), which is
    /// what actually stops movement — the second floor was never built
    /// (Cabin_v2/README.md: "only the ceiling slab and stair opening"), so a
    /// player who gets up there steps onto the ceiling slab at y 2.9 and is
    /// stranded with no geometry, no rail, and no fall recovery
    /// (CabinFallRecovery only fires below y -4).
    ///
    /// Kept as its own trigger in FRONT of that blocker rather than folded into
    /// it: a collision callback on the blocker would only fire once the player
    /// was already grinding against a wall, which reads as jank. Warning first,
    /// then stopping them, reads as a decision.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class StairWarning : MonoBehaviour
    {
        [SerializeField] private string speaker = "David";
        [SerializeField] private string line = "Aaron and Ivy don't want to be disturbed.";
        [SerializeField] private float holdSeconds = 3f;

        [Tooltip("Silence window after a showing. Without it the line re-fires " +
                 "every time the player clips the trigger edge while pressed " +
                 "against the blocker behind it.")]
        [SerializeField] private float repeatCooldownSeconds = 8f;

        private float _lastShownAt = float.NegativeInfinity;

        public void Configure(string warningLine, string warningSpeaker)
        {
            line = warningLine;
            speaker = warningSpeaker;
        }

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            // Identified by CharacterController, not by tag: the cabin player
            // root ships Untagged (only its FirstPersonView child carries
            // MainCamera), and the cast are moved by Transform lerps rather
            // than controllers, so this cannot be tripped by an NPC.
            if (other.GetComponentInParent<CharacterController>() == null) return;
            if (Time.time - _lastShownAt < repeatCooldownSeconds) return;
            _lastShownAt = Time.time;

            GameFlowDirector flow = FindAnyObjectByType<GameFlowDirector>();
            if (flow == null || flow.Subtitles == null) return;
            flow.Subtitles.Show(speaker, line, holdSeconds);
        }
    }
}
