using FalsePositive.Flow;
using FalsePositive.Rendering;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Ramps the drunk post-process effect (Rendering/DrunkEffectController) across
    /// CutsceneId.Wake -- the player waking up at the interrogation table, disoriented,
    /// to "David. David. David!" on the intercom. Same shape as
    /// CutsceneAnimationDirector: one instance in _Persistent, finds the single
    /// CutsceneDirector in OnEnable, unsubscribes in OnDisable.
    ///
    /// Wake is authored screen-lit (CutsceneRecipeBuilder.cs) specifically so this
    /// effect is visible rather than hidden behind a fade -- see
    /// PhaseDialogueController.EnterP1 for where it's requested.
    /// </summary>
    public sealed class DrunkCutsceneBinder : MonoBehaviour
    {
        [SerializeField] private CutsceneId cutsceneId = CutsceneId.Wake;
        [SerializeField] private DrunkEffectController effect;
        [SerializeField] private float rampInSeconds = 0.4f;
        [SerializeField] private float rampOutSeconds = 1.5f;

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
            // Never leave the effect stuck on if this component (or the scene it
            // lives in) goes away mid-cutscene.
            effect?.Clear();
        }

        private void HandleStarted(CutsceneId id)
        {
            if (id != cutsceneId || effect == null) return;
            effect.RampTo(1f, rampInSeconds);
        }

        private void HandleFinished(CutsceneId id)
        {
            if (id != cutsceneId || effect == null) return;
            // Bleeds out into the room rather than snapping off the instant the
            // cutscene's VO ends -- the disorientation is meant to linger a beat
            // into the spoken-prompt gate that follows.
            effect.RampTo(0f, rampOutSeconds);
        }
    }
}
