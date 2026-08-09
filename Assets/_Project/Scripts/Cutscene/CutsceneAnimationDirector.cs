using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Cross-scene audio-proxy redirect for the cop's lip sync, active on
    /// whichever beat of whichever cutscene he's actually the speaker on —
    /// not scoped to a single cutscene id. CutsceneDirector itself still
    /// owns fades/subtitles/VO and never touches Timeline/Animator (see its
    /// class doc).
    ///
    /// Superseded design: an earlier version of this class also played a
    /// Timeline AnimationClip on the cop's Animator for body motion, and
    /// disabled CopIdleAnimator/CopTalkGestureAnimator while it played. That
    /// Timeline path is retired — see Editor.ProjectBootstrapBuilder.
    /// WireAnimationDirector's class doc and Cop.CopTalkGestureAnimator's
    /// class doc for why: the body is now driven procedurally, uniformly for
    /// every dialogue turn AND every cutscene, off uLipSync's own volume, so
    /// there is nothing left here to suppress or to Play()/Stop() — idle and
    /// the talk gesture both keep running straight through every cutscene.
    ///
    /// What this class still needs to do: redirect uLipSync's audio analysis
    /// to a cutscene's own VO exactly while a Spassky-voiced beat of it is
    /// playing. uLipSync only analyzes an AudioSource living on its own
    /// GameObject by default (the Cop's own AudioSource — which is why live
    /// dialogue turns need zero extra wiring, and why CopTalkGestureAnimator's
    /// volume read gets the right answer for both cases with no gating of its
    /// own), but every cutscene's VO plays through _Persistent's
    /// CutsceneVoSource, a different AudioSource in a different scene. Keyed
    /// off CutsceneBeat.speaker rather than a fixed CutsceneId because a
    /// single cutscene can mix speakers — EndingPriya is Priya's line, then
    /// his — so the previous per-cutscene-id design could not have gotten
    /// that beat right even with more ids added to it, and every Spassky
    /// cutscene added since (P3Photograph, P3AfterGoodYears, P3WhoDavid,
    /// the Ending* beats) needed its id added by hand to keep working.
    ///
    /// One instance per scene that needs it (currently only Interrogation.unity,
    /// wired by Editor.ProjectBootstrapBuilder.FixInterrogationScene). Follows
    /// the same cross-scene pattern as Scripts/Cutscene/CutsceneStage.cs: finds
    /// the single persistent CutsceneDirector in OnEnable, unsubscribes in
    /// OnDisable, and relies on SceneRouter.SetRootsActive deactivating
    /// inactive scenes' roots so only the active scene's instance is ever
    /// subscribed — no phase-checking needed here.
    /// </summary>
    public sealed class CutsceneAnimationDirector : MonoBehaviour
    {
        [SerializeField] private uLipSync.uLipSync lipSync;

        private CutsceneDirector _cutscenes;

        private void OnEnable()
        {
            _cutscenes = FindAnyObjectByType<CutsceneDirector>();
            if (_cutscenes != null)
            {
                _cutscenes.BeatStarted += HandleBeatStarted;
                _cutscenes.Finished += HandleFinished;
            }
        }

        private void OnDisable()
        {
            if (_cutscenes != null)
            {
                _cutscenes.BeatStarted -= HandleBeatStarted;
                _cutscenes.Finished -= HandleFinished;
            }

            // Interrogation.unity is only ever deactivated, never unloaded
            // (see InterrogationSceneBinder's class doc) — so this disable
            // can happen mid-beat if a memory scene interrupts P1. Leaving
            // the proxy pointed at CutsceneVoSource across that gap would
            // make live dialogue turns, which rely on uLipSync's default
            // (the Cop's own AudioSource), silently analyze the wrong
            // source once this scene reactivates.
            if (lipSync != null) lipSync.audioSourceProxy = null;
        }

        private void HandleBeatStarted(CutsceneBeat beat)
        {
            if (lipSync == null || _cutscenes == null) return;

            // Redirect only while THIS beat is Spassky's — explicitly
            // clearing it for every other speaker (not just leaving it
            // alone) is what makes a mixed cutscene like EndingPriya read
            // correctly: without this, the proxy set by an earlier Spassky
            // beat would stay pointed at CutsceneVoSource while a later,
            // different character's clip plays through the same source, and
            // the cop's mouth would flap to their line instead of staying
            // still.
            //
            // Null-then-set, not a direct assignment: uLipSync only
            // (re)subscribes its OnDataReceived listener when
            // audioSourceProxy actually changes value between two of its own
            // Update() calls (see UpdateAudioSourceProxy's early-return on
            // audioSourceProxy == _currentAudioSourceProxy). Observed live:
            // the first beat played immediately after Interrogation.unity's
            // own scene activation — i.e. the same frame this field is first
            // written — produced fully audible VO with uLipSync.result.
            // rawVolume pinned at 0 for the entire beat, while an identical
            // replay moments later worked correctly. Forcing a null in
            // between guarantees a real change every time this beat starts,
            // even on a scene that only just went active.
            lipSync.audioSourceProxy = null;
            lipSync.audioSourceProxy = beat.speaker == "SPASSKY" ? _cutscenes.VoSourceLipSync : null;
        }

        private void HandleFinished(CutsceneId id)
        {
            // Safety net if the cutscene ends without a clean last beat
            // (empty recipe, etc.) — always leave live dialogue turns able
            // to fall back to uLipSync's default: the Cop's own AudioSource.
            if (lipSync != null) lipSync.audioSourceProxy = null;
        }
    }
}
