using FalsePositive.Audio;
using FalsePositive.Core;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Guardrail #2 (docs/GAME_COMPLETION_PLAN.md §8): a persistent,
    /// honest mic-state indicator for the whole session. Copy is fixed at
    /// "Microphone active" / "Microphone inactive" — never "recording", and
    /// never a red dot, both of which read as "a file is being written",
    /// which is never true (guardrail #3). It polls the shared capture/VAD
    /// state and visualizes the same threshold the recorder uses.
    /// </summary>
    public sealed class MicIndicator : MonoBehaviour
    {
        private const string ActiveText = "Microphone active";
        private const string InactiveText = "Microphone inactive";
        // Push-to-talk needs an instruction, not a status: "Microphone inactive"
        // is true but tells a first-time player nothing about what to do.
        private const string PushToTalkFormat = "Hold [{0}] to speak";

        [SerializeField] private MicrophoneService mic;
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private UtteranceRecorder recorder;
        [SerializeField] private InterrogationConfig config;
        [SerializeField] private Text label;
        [SerializeField] private Image dot;
        [SerializeField] private GameObject levelMeterRoot;
        [SerializeField] private Image levelMeterFill;
        [SerializeField] private Color activeColor = new Color(0.25f, 0.85f, 0.35f);
        [SerializeField] private Color inactiveColor = new Color(0.5f, 0.5f, 0.5f);
        [SerializeField] private Color tooQuietColor = new Color(0.6f, 0.6f, 0.6f);
        [SerializeField] private Color tooLoudColor = new Color(0.95f, 0.15f, 0.12f);
        [SerializeField] private float attackSpeed = 8f;
        [SerializeField] private float releaseSpeed = 3f;

        private float _displayLevel;

        /// <summary>True from the moment VAD says "speaking" until the resulting
        /// utterance is either sent (UtteranceCaptured) or thrown away
        /// (UtteranceDiscarded) — drives the meter's green/grey, replacing a
        /// per-frame level-vs-threshold comparison that used to flicker on
        /// every transient and go grey mid-sentence during syllable gaps.</summary>
        private bool _utteranceActive;

        private void OnEnable()
        {
            if (vad != null) vad.SpeakingStateChanged += OnSpeakingStateChanged;
            if (recorder != null)
            {
                recorder.UtteranceCaptured += OnUtteranceResolved;
                recorder.UtteranceDiscarded += OnUtteranceDiscarded;
            }
        }

        private void OnDisable()
        {
            if (vad != null) vad.SpeakingStateChanged -= OnSpeakingStateChanged;
            if (recorder != null)
            {
                recorder.UtteranceCaptured -= OnUtteranceResolved;
                recorder.UtteranceDiscarded -= OnUtteranceDiscarded;
            }
        }

        private void OnSpeakingStateChanged(bool speaking)
        {
            if (speaking) _utteranceActive = true;
        }

        // UtteranceCaptured's signature carries the samples/sampleRate the
        // recorder just flushed — unused here, only the "it was sent" fact matters.
        private void OnUtteranceResolved(float[] samples, int sampleRate) => _utteranceActive = false;

        private void OnUtteranceDiscarded() => _utteranceActive = false;

        /// <summary>Cached because Update runs every frame and string.Format
        /// would otherwise allocate a new string per frame for a label that
        /// changes only when the config does.</summary>
        private string _idleText;

        private string IdleText()
        {
            if (_idleText != null) return _idleText;
            _idleText = config != null && config.pushToTalk
                ? string.Format(PushToTalkFormat, config.pushToTalkKey)
                : InactiveText;
            return _idleText;
        }

        private void Update()
        {
            bool active = mic != null && mic.IsCapturing && (vad == null || !vad.Gated);
            if (!active) _utteranceActive = false; // gated/stopped mic can't leave the bar stuck green

            if (label != null) label.text = active ? ActiveText : IdleText();
            if (dot != null) dot.color = active ? activeColor : inactiveColor;

            if (levelMeterRoot != null) levelMeterRoot.SetActive(active);
            if (levelMeterFill == null) return;

            float rms = active && vad != null ? vad.DisplayRms : 0f;
            float tooLoudRms = config != null ? config.micTooLoudRms : 0.25f;
            float targetLevel = RmsToMeter(rms, tooLoudRms);
            float speed = targetLevel > _displayLevel ? attackSpeed : releaseSpeed;
            _displayLevel = Mathf.MoveTowards(_displayLevel, targetLevel, speed * Time.deltaTime);

            RectTransform fillTransform = levelMeterFill.rectTransform;
            Vector2 anchorMax = fillTransform.anchorMax;
            anchorMax.x = _displayLevel;
            fillTransform.anchorMax = anchorMax;

            bool tooLoud = rms >= tooLoudRms;
            levelMeterFill.color = tooLoud ? tooLoudColor : _utteranceActive ? activeColor : tooQuietColor;
        }

        /// <summary>Maps the useful microphone range logarithmically. Human
        /// voice RMS spans orders of magnitude, so a linear bar would spend
        /// almost all normal speech in its first few pixels.</summary>
        public static float RmsToMeter(float rms, float tooLoudRms)
        {
            const float meterFloorRms = 0.0001f; // -80 dBFS
            rms = Mathf.Max(rms, meterFloorRms);
            tooLoudRms = Mathf.Max(tooLoudRms, meterFloorRms * 2f);
            float floorDb = 20f * Mathf.Log10(meterFloorRms);
            float loudDb = 20f * Mathf.Log10(tooLoudRms);
            float rmsDb = 20f * Mathf.Log10(rms);
            return Mathf.InverseLerp(floorDb, loudDb, rmsDb);
        }
    }
}
