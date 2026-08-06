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
    ///
    /// Audio (Phase 5): a static loop plays from Awake until tuned, then a
    /// tuning sweep + lock-on one-shot play and the static stops.
    /// </summary>
    public sealed class RadioTuner : Interactable
    {
        [SerializeField] private AudioSource staticLoopSource;
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioClip tuningSweepClip;
        [SerializeField] private AudioClip lockOnClip;

        public event Action Cleared;

        // OnEnable, not Awake — SceneRouter often saves memory-scene props
        // inactive and activates them later when their scene becomes
        // current, and Awake() only fires once (the object's first-ever
        // activation), not on every re-activation. See Audio.LoopOnEnable's
        // doc comment for how this was caught.
        private void OnEnable()
        {
            if (staticLoopSource != null && !IsComplete) staticLoopSource.Play();
        }

        public override void OnInteract()
        {
            if (staticLoopSource != null) staticLoopSource.Stop();
            if (sfxSource != null && tuningSweepClip != null) sfxSource.PlayOneShot(tuningSweepClip);
            if (sfxSource != null && lockOnClip != null) sfxSource.PlayOneShot(lockOnClip);

            MarkComplete();
            Cleared?.Invoke();
        }
    }
}
