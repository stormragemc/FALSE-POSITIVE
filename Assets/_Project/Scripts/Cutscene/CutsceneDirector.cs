using System;
using System.Collections;
using System.Collections.Generic;
using FalsePositive.Flow;
using FalsePositive.UI;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>One beat within a cutscene: an optional line (subtitle + VO)
    /// and an optional memory flag to write when it plays.</summary>
    [Serializable]
    public sealed class CutsceneBeat
    {
        public string speaker;
        [TextArea] public string line;
        public AudioClip voClip;
        public float holdSecondsIfNoClip = 2.5f;
        public string memoryFlagToSet;
    }

    [Serializable]
    public sealed class CutsceneRecipe
    {
        public CutsceneId id;
        public float fadeOutSeconds = 0.3f;
        public float fadeInSeconds = 0.5f;
        public CutsceneBeat[] beats = Array.Empty<CutsceneBeat>();
    }

    /// <summary>
    /// Person B's ICutscenePlayer implementation. Lives in _Persistent, self-
    /// registers with GameFlowDirector.Instance in Start(). Every cutscene is
    /// the §10 cheap form — fade to black, hold each beat's subtitle/VO, fade
    /// back — never a Timeline asset. That is a Day-1 scope decision, not a
    /// placeholder: docs/GAME_COMPLETION_PLAN.md §10 explicitly sanctions this
    /// as the shipped form for every cutscene, and the honesty ledger commits
    /// to saying so.
    ///
    /// Deliberately reaches only same-scene _Persistent services (fader,
    /// subtitles, its own VO AudioSource) — never a camera or object in
    /// whichever memory/interrogation scene happens to be active. That is
    /// what lets a single persistent instance play every cutscene in the game
    /// without a per-scene binder. Per docs/GAME_COMPLETION_PLAN.md §4.1:
    /// CutsceneDirector never reads game state (Phase/Score) to change what a
    /// cutscene does — only its id parameter decides that — and
    /// GameFlowDirector never reaches into a director. One raises, the other
    /// listens.
    /// </summary>
    public sealed class CutsceneDirector : MonoBehaviour, ICutscenePlayer
    {
        [SerializeField] private ScreenFader fader;
        [SerializeField] private SubtitleUI subtitles;
        [SerializeField] private AudioSource voSource;
        [SerializeField] private CutsceneRecipe[] recipes = Array.Empty<CutsceneRecipe>();

        public bool IsPlaying { get; private set; }
        public event Action<CutsceneId> Finished;

        /// <summary>
        /// Fires once the screen is fully faded to black, before any beats
        /// play — the window Cutscene.CutsceneStage (Phase 4 of the Cabin_v2
        /// pass) uses to move/pose cast members for a beat while the player
        /// can't see it happen. Firing any earlier (e.g. before the fade
        /// starts) would let staging changes be visible mid-fade; there is
        /// no equivalent hook needed before Finished since that already
        /// covers "the cutscene is fully over, fade back in complete."
        /// </summary>
        public event Action<CutsceneId> Started;

        private Dictionary<CutsceneId, CutsceneRecipe> _byId;

        private void Awake()
        {
            _byId = new Dictionary<CutsceneId, CutsceneRecipe>();
            foreach (CutsceneRecipe recipe in recipes)
            {
                if (recipe != null) _byId[recipe.id] = recipe;
            }
        }

        private void Start()
        {
            GameFlowDirector.Instance?.RegisterCutscenePlayer(this);
        }

        public void Play(CutsceneId id) => StartCoroutine(PlayRoutine(id));

        private IEnumerator PlayRoutine(CutsceneId id)
        {
            IsPlaying = true;
            _byId.TryGetValue(id, out CutsceneRecipe recipe);

            if (fader != null) yield return fader.FadeToBlack(recipe?.fadeOutSeconds ?? 0.3f);
            Started?.Invoke(id);

            if (recipe != null)
            {
                foreach (CutsceneBeat beat in recipe.beats)
                {
                    yield return PlayBeat(beat);
                }
            }

            if (fader != null) yield return fader.FadeFromBlack(recipe?.fadeInSeconds ?? 0.5f);

            IsPlaying = false;
            Finished?.Invoke(id);
        }

        private IEnumerator PlayBeat(CutsceneBeat beat)
        {
            if (!string.IsNullOrEmpty(beat.memoryFlagToSet))
            {
                GameFlowDirector.Instance?.Flags.Set(beat.memoryFlagToSet);
            }

            float hold = beat.voClip != null ? beat.voClip.length : beat.holdSecondsIfNoClip;

            if (!string.IsNullOrEmpty(beat.line))
            {
                subtitles?.Show(beat.speaker, beat.line, hold);
            }

            if (beat.voClip != null && voSource != null)
            {
                voSource.clip = beat.voClip;
                voSource.Play();
            }

            yield return new WaitForSeconds(hold);
            subtitles?.Hide();
        }
    }
}
