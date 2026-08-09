using System.Collections;
using FalsePositive.Flow;
using FalsePositive.Rendering;
using FalsePositive.UI;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Everything that sells CutsceneId.Wake -- the player waking up at the
    /// interrogation table, disoriented, to "David. David. David!" on the
    /// intercom -- except the camera sway, which needs the scene-local player
    /// camera and lives on the Player rig instead (Player/DrunkCameraSway.cs).
    /// One instance in _Persistent, finds the single CutsceneDirector in
    /// OnEnable, unsubscribes in OnDisable, same shape as
    /// Cutscene.CutsceneAnimationDirector.
    ///
    /// Four things ramp/toggle together on Started(Wake) and unwind on
    /// Finished(Wake):
    /// - the drunk post-process (colour/trail) via DrunkEffectController
    /// - a slow fade in from black (the eyes-opening beat) via ScreenFader
    /// - VO played at a lowered pitch (slow-motion, slurred)
    /// - an AudioEchoFilter on the VO source
    ///
    /// Wake is authored screen-lit / keepScreenLit (CutsceneRecipeBuilder.cs),
    /// so CutsceneDirector.PlayRoutine itself never touches the fader for this
    /// recipe -- the fade-in below is driven directly against ScreenFader
    /// instead of through CutsceneRecipe's fadeOut/fadeInSeconds, which are a
    /// one-shot pre/post-cutscene fade rather than the "slowly reveal while
    /// the cutscene plays" effect wanted here.
    /// </summary>
    public sealed class DrunkCutsceneBinder : MonoBehaviour
    {
        [SerializeField] private CutsceneId cutsceneId = CutsceneId.Wake;
        [SerializeField] private DrunkEffectController effect;
        [SerializeField] private float rampInSeconds = 0.4f;
        [SerializeField] private float rampOutSeconds = 1.5f;

        [SerializeField] private ScreenFader fader;
        // A quick snap rather than 0 -- an instant hard cut to black still
        // reads as "eyes were already shut," while a literal duration: 0 call
        // would hit ScreenFader.Fade's already-there early-out and do nothing
        // if the screen happened to already be lit.
        [SerializeField] private float blackSnapSeconds = 0.15f;
        // The slow reveal the player actually watches -- eyes opening while
        // "David." calls out, roughly spanning the beat's own VO.
        [SerializeField] private float fadeInSeconds = 3f;

        [SerializeField] private AudioSource voSource;
        [SerializeField] private AudioEchoFilter voEcho;
        // CutsceneDirector.PlayBeat divides each beat's hold by voSource.pitch
        // so the slowed clip is never cut off mid-word -- see that method's
        // comment.
        [SerializeField, Range(0.1f, 1f)] private float slowPitch = 0.75f;

        private CutsceneDirector _cutscenes;
        private Coroutine _fadeRoutine;

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
            // Never leave anything stuck on if this component (or the scene it
            // lives in) goes away mid-cutscene.
            effect?.Clear();
            ResetAudio();
        }

        private void HandleStarted(CutsceneId id)
        {
            if (id != cutsceneId) return;

            effect?.RampTo(1f, rampInSeconds);

            if (fader != null)
            {
                if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
                _fadeRoutine = StartCoroutine(FadeInRoutine());
            }

            if (voSource != null) voSource.pitch = slowPitch;
            if (voEcho != null) voEcho.enabled = true;
        }

        private void HandleFinished(CutsceneId id)
        {
            if (id != cutsceneId) return;

            // Bleeds out into the room rather than snapping off the instant the
            // cutscene's VO ends -- the disorientation is meant to linger a beat
            // into the spoken-prompt gate that follows.
            effect?.RampTo(0f, rampOutSeconds);

            // No fade-out here: Wake hands off straight into the spoken prompt
            // with the screen already lit. Only the wake-up itself fades from
            // black -- if a fade routine is still running (fadeInSeconds long
            // outlasting a fast VO), just let it finish reaching 0 on its own.
            ResetAudio();
        }

        private IEnumerator FadeInRoutine()
        {
            yield return fader.FadeToBlack(blackSnapSeconds);
            yield return fader.FadeFromBlack(fadeInSeconds);
        }

        private void ResetAudio()
        {
            if (voSource != null) voSource.pitch = 1f;
            if (voEcho != null) voEcho.enabled = false;
        }
    }
}
