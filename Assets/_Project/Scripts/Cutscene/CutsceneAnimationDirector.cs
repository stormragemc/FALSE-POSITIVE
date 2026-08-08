using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Cross-scene audio-proxy redirect for the cop's lip sync, scoped to
    /// CutsceneId.SpasskyAnswer. CutsceneDirector itself still owns fades/
    /// subtitles/VO and never touches Timeline/Animator (see its class doc).
    ///
    /// Superseded design: an earlier version of this class also played a
    /// Timeline AnimationClip on the cop's Animator for body motion, and
    /// disabled CopIdleAnimator/CopTalkGestureAnimator while it played. That
    /// Timeline path is retired — see Editor.ProjectBootstrapBuilder.
    /// WireAnimationDirector's class doc and Cop.CopTalkGestureAnimator's
    /// class doc for why: the body is now driven procedurally, uniformly for
    /// every dialogue turn AND this cutscene, off uLipSync's own volume, so
    /// there is nothing left here to suppress or to Play()/Stop() — idle and
    /// the talk gesture both keep running straight through this cutscene.
    ///
    /// What this class still needs to do: redirect uLipSync's audio analysis
    /// to this cutscene's own VO for its duration. uLipSync only analyzes an
    /// AudioSource living on its own GameObject by default (the Cop's own
    /// AudioSource — which is why live dialogue turns need zero extra
    /// wiring, and why CopTalkGestureAnimator's volume read gets the right
    /// answer for both cases with no gating of its own), but SpasskyAnswer's
    /// VO plays through _Persistent's CutsceneVoSource, a different
    /// AudioSource in a different scene.
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
        [SerializeField] private CutsceneId cutsceneId = CutsceneId.SpasskyAnswer;
        [SerializeField] private uLipSync.uLipSync lipSync;

        private CutsceneDirector _cutscenes;

        private void OnEnable()
        {
            _cutscenes = FindAnyObjectByType<CutsceneDirector>();
            if (_cutscenes != null)
            {
                _cutscenes.Started += HandleStarted;
                _cutscenes.Finished += HandleFinished;
            }
        }

        private void OnDisable()
        {
            if (_cutscenes != null)
            {
                _cutscenes.Started -= HandleStarted;
                _cutscenes.Finished -= HandleFinished;
            }
        }

        private void HandleStarted(CutsceneId id)
        {
            if (id != cutsceneId) return;

            if (lipSync != null && _cutscenes != null)
            {
                lipSync.audioSourceProxy = _cutscenes.VoSourceLipSync;
            }
        }

        private void HandleFinished(CutsceneId id)
        {
            if (id != cutsceneId) return;

            // Clear the proxy so live dialogue turns go back to uLipSync's
            // default: analyzing the Cop's own AudioSource.
            if (lipSync != null) lipSync.audioSourceProxy = null;
        }
    }
}
