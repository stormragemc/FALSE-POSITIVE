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
    /// Audio (Phase 5): a static loop plays from the moment M1_Night begins
    /// until tuned — see OnEnable for why that is a PhaseChanged subscription
    /// and not a phase check. The
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

        private GameFlowDirector _flow;

        // OnEnable, not Awake — SceneRouter often saves memory-scene props
        // inactive and activates them later when their scene becomes
        // current, and Awake() only fires once (the object's first-ever
        // activation), not on every re-activation. See Audio.LoopOnEnable's
        // doc comment for how this was caught.
        //
        // Subscribe rather than read Phase here. The radio belongs to the
        // FIRST cabin visit only: the P3 memory pair re-enters
        // Memory_CabinNight through GameFlowDirector.RequestMemoryInterlude
        // -> SceneRouter.Activate, which fires OnEnable again, and
        // IsComplete is per-instance so a reloaded scene arrives with it
        // false. Testing the live Phase looked like the way to tell "we are
        // actually playing M1_Night" from "we are looking back at it", but it
        // silenced the static entirely: TransitionRoutine calls
        // SceneRouter.Activate (which is what fires this OnEnable) three
        // lines BEFORE it assigns Phase, so the phase read here is always the
        // outgoing one and the check could never pass. PhaseChanged carries
        // the same information at a moment when it is actually set, and the
        // interlude never fires it because it deliberately does not change
        // Phase — so the look-back stays silent for the original reason.
        private void OnEnable()
        {
            if (staticLoopSource == null || IsComplete) return;

            _flow = GameFlowDirector.Instance;

            // No flow at all means the scene is being played on its own in the
            // Editor, with no phase to wait for. Play immediately — this is
            // also why the bug above never showed up in isolation.
            if (_flow == null)
            {
                staticLoopSource.Play();
                return;
            }

            _flow.PhaseChanged += OnPhaseChanged;
            // Catch up, in case this activation lands after M1_Night is
            // already current and no further PhaseChanged is coming.
            OnPhaseChanged(_flow.Phase);
        }

        private void OnDisable()
        {
            if (_flow != null) _flow.PhaseChanged -= OnPhaseChanged;
        }

        private void OnPhaseChanged(GamePhase phase)
        {
            if (phase != GamePhase.M1_Night) return;
            if (staticLoopSource == null || IsComplete) return;
            if (staticLoopSource.isPlaying) return;

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
