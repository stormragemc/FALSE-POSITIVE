using System;
using System.Collections;
using System.Collections.Generic;
using FalsePositive.Flow;
using FalsePositive.UI;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

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

        /// <summary>When true, CutsceneDirector.PlayRoutine skips both fade calls
        /// entirely and the screen stays lit for the whole cutscene — for beats
        /// meant to be watched (M2's OutIntoTheSnow/TheCarry/TheSofa), not the
        /// cheap fade-to-black+VO form every other cutscene uses. NOTE: setting
        /// fadeOutSeconds/fadeInSeconds to 0 does NOT achieve this —
        /// ScreenFader.Fade's duration&lt;=0 branch snaps straight to
        /// canvasGroup.alpha = 1 (fully black), it doesn't skip the fade.</summary>
        public bool keepScreenLit;
    }

    [Serializable]
    public sealed class CutsceneTimelineBinding
    {
        public CutsceneId id;
        public PlayableDirector director;
    }

    /// <summary>
    /// Person B's ICutscenePlayer implementation. Lives in _Persistent, self-
    /// registers with GameFlowDirector.Instance in Start(). Every cutscene is
    /// the §10 cheap form — fade to black, hold each beat's subtitle/VO, fade
    /// back — never a Timeline asset. That is a Day-1 scope decision, not a
    /// placeholder: docs/GAME_COMPLETION_PLAN.md §10 explicitly sanctions this
    /// as the shipped form for every cutscene, and the honesty ledger commits
    /// to saying so. This class's own job (subtitles, VO, fades) still never
    /// touches Timeline. The one deliberate exception is character animation:
    /// Scripts/Cutscene/CutsceneAnimationDirector.cs listens to this class's
    /// Started/Finished events and plays a Timeline clip on the cop during
    /// SpasskyAnswer — a second, independent system layered on top, not a
    /// reversal of this one's scope.
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
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private uLipSync.uLipSyncAudioSource voSourceLipSync;
        [SerializeField] private CutsceneRecipe[] recipes = Array.Empty<CutsceneRecipe>();
        [SerializeField] private CutsceneTimelineBinding[] timelineDirectors = Array.Empty<CutsceneTimelineBinding>();

        [Header("Pacing")]
        /// <summary>Silence held after a spoken beat before the next one starts.
        ///
        /// Without it every line begins on the exact frame the previous one is
        /// due to end, so a scripted exchange machine-guns and reads as rushed.
        /// This is the single biggest lever on how the P3 memory sequence
        /// feels; raise it if the delivery still runs together.</summary>
        [SerializeField, Range(0f, 1.5f)] private float interBeatSeconds = 0.45f;

        /// <summary>Extra hold on top of a clip's own length.
        ///
        /// A beat used to wait exactly voClip.length, but the wait is frame
        /// quantised and AudioSource.Play does not start on the same frame it
        /// is called, so the next beat reassigned source.clip while the tail of
        /// the current line was still sounding — clipping the last syllable off
        /// essentially every line. The tail absorbs that skew.</summary>
        [SerializeField, Range(0f, 1f)] private float audioTailSeconds = 0.25f;

        /// <summary>Cap on waiting for a still-playing clip. Guards against a
        /// beat hanging forever if a clip loops or an AudioSource is left
        /// paused by something else.</summary>
        private const float MaxAudioOverrunSeconds = 2f;

        public bool IsPlaying { get; private set; }
        public event Action<CutsceneId> Finished;

        /// <summary>Lets Cutscene.CutsceneAnimationDirector point the Cop's
        /// uLipSync at this beat's actual VO — see that class for why:
        /// uLipSync only analyzes an AudioSource living on its own
        /// GameObject by default, and this cutscene's VO plays through
        /// voSource here in _Persistent, not the Cop's own AudioSource in
        /// whichever scene is active.</summary>
        public uLipSync.uLipSyncAudioSource VoSourceLipSync => voSourceLipSync;

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
        private Dictionary<CutsceneId, PlayableDirector> _timelineById;

        private void Awake()
        {
            _byId = new Dictionary<CutsceneId, CutsceneRecipe>();
            foreach (CutsceneRecipe recipe in recipes)
            {
                if (recipe != null) _byId[recipe.id] = recipe;
            }

            _timelineById = new Dictionary<CutsceneId, PlayableDirector>();
            foreach (CutsceneTimelineBinding binding in timelineDirectors)
            {
                if (binding != null && binding.director != null)
                {
                    binding.director.playOnAwake = false;
                    _timelineById[binding.id] = binding.director;
                }
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
            bool keepScreenLit = recipe != null && recipe.keepScreenLit;
            _timelineById.TryGetValue(id, out PlayableDirector timeline);

            if (!keepScreenLit && fader != null) yield return fader.FadeToBlack(recipe?.fadeOutSeconds ?? 0.3f);
            Started?.Invoke(id);

            if (timeline != null)
            {
                BindAnimationTracks(timeline);
                timeline.time = 0d;
                timeline.Play();
            }

            if (recipe != null)
            {
                for (int i = 0; i < recipe.beats.Length; i++)
                {
                    yield return PlayBeat(recipe.beats[i], timeline != null);

                    // Between beats only. The pause after the *last* beat is
                    // owned by whoever is cutting away next — see
                    // GameFlowDirector's interlude lead-in — so that a seam is
                    // paced once rather than twice.
                    if (i < recipe.beats.Length - 1 && interBeatSeconds > 0f)
                    {
                        yield return new WaitForSeconds(interBeatSeconds);
                    }
                }
            }

            if (timeline != null) timeline.Stop();

            if (!keepScreenLit && fader != null) yield return fader.FadeFromBlack(recipe?.fadeInSeconds ?? 0.5f);

            IsPlaying = false;
            Finished?.Invoke(id);
        }

        private IEnumerator PlayBeat(CutsceneBeat beat, bool timelineOwnsAudio)
        {
            if (!string.IsNullOrEmpty(beat.memoryFlagToSet))
            {
                GameFlowDirector.Instance?.Flags.Set(beat.memoryFlagToSet);
            }

            bool spoken = beat.voClip != null;
            float hold = spoken ? beat.voClip.length + audioTailSeconds : beat.holdSecondsIfNoClip;

            if (!string.IsNullOrEmpty(beat.line))
            {
                subtitles?.Show(beat.speaker, beat.line, hold);
            }

            AudioSource playing = null;
            if (!timelineOwnsAudio && spoken)
            {
                AudioSource source = string.IsNullOrEmpty(beat.speaker) && sfxSource != null
                    ? sfxSource
                    : voSource;
                if (source != null)
                {
                    source.clip = beat.voClip;
                    source.Play();
                    playing = source;
                }
            }

            yield return new WaitForSeconds(hold);

            // Belt and braces on the tail above: if the clip is somehow still
            // sounding, let it land rather than cutting it off by reassigning
            // source.clip on the next beat. Capped so a stuck source cannot
            // stall the sequence.
            float waited = 0f;
            while (playing != null && playing.isPlaying && waited < MaxAudioOverrunSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            subtitles?.Hide();
        }

        private static void BindAnimationTracks(PlayableDirector director)
        {
            if (director.playableAsset is not TimelineAsset timeline) return;

            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is not AnimationTrack || !track.name.StartsWith("ANIM_")) continue;

                string actorName = track.name.Substring("ANIM_".Length) switch
                {
                    "SPASSKY" => "Cop",
                    "PRIYA" => "Priya Raman (Female)",
                    "IVY" => "Ivy Teague (Female)",
                    "AARON" => "Aaron Teague (Male)",
                    "NICK" => "Nick Vlahos (Male)",
                    _ => null,
                };

                GameObject actor = string.IsNullOrEmpty(actorName) ? null : GameObject.Find(actorName);
                Animator animator = actor != null ? actor.GetComponentInChildren<Animator>(true) : null;
                if (animator != null)
                {
                    director.SetGenericBinding(track, animator);
                }
                else
                {
                    director.ClearGenericBinding(track);
                }
            }
        }
    }
}
