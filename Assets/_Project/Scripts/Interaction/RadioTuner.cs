using System;
using FalsePositive.Flow;
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
    /// Audio (Phase 5): a static loop plays from Awake until tuned. The
    /// tuning sweep + lock-on one-shots have since moved onto the
    /// Cutscene_RadioTune Timeline's "Radio Tune" audio track (see
    /// RadioTuneTimelineBuilder), so they land on the knob-turn animation
    /// beat instead of on the E-press itself. sfxSource/tuningSweepClip/
    /// lockOnClip stay wired by MemorySceneDressing even though OnInteract
    /// no longer plays them directly — the Timeline track reads the same
    /// clips by asset path, not through these fields.
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
            if (staticLoopSource == null || IsComplete) return;

            // The radio belongs to the FIRST cabin visit only. That same
            // re-activation behaviour above is what made it come back later:
            // the P3 memory pair re-enters Memory_CabinNight through
            // GameFlowDirector.RequestMemoryInterlude -> SceneRouter.Activate,
            // which fires OnEnable again, and IsComplete is per-instance so a
            // reloaded scene arrives with it false. The interlude deliberately
            // does NOT change Phase (it only swaps the active scene), so the
            // live phase is the reliable way to tell "we are actually playing
            // M1_Night" from "we are looking back at it".
            GameFlowDirector flow = GameFlowDirector.Instance;
            if (flow != null && flow.Phase != GamePhase.M1_Night) return;

            staticLoopSource.Play();
        }

        public override void OnInteract()
        {
            if (staticLoopSource != null) staticLoopSource.Stop();

            MarkComplete();
            Cleared?.Invoke();
        }
    }
}
