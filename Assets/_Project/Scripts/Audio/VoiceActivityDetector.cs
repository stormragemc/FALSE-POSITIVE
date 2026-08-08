using System;
using System.Collections.Generic;
using FalsePositive.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FalsePositive.Audio
{
    /// <summary>
    /// Pure speaking/not-speaking decision logic: RMS-threshold with
    /// hysteresis (a single threshold chatters on every syllable gap) plus a
    /// noise-floor calibration window, read from MicrophoneService's shared
    /// ring buffer. Owns no sample buffer itself — see UtteranceRecorder,
    /// which subscribes to this class's events. That split keeps the
    /// decision logic and the buffer bookkeeping independently testable.
    ///
    /// Calibration no longer starts automatically on the first Update — with
    /// consent gating (MicConsentGate), the device is not open at scene
    /// start, so auto-calibrating then would have sampled silence and set
    /// the near-zero floor, making the VAD fire on room tone. BeginCalibration
    /// must be called explicitly (A4's MicCalibration is the only caller);
    /// IsSpeaking stays false until IsCalibrated.
    ///
    /// Refuses to leave the Gated state (set by DialogueManager while the
    /// cop's reply audio is playing, plus a short tail) — the headphones-
    /// assumed mitigation for the cop's own TTS re-triggering the mic;
    /// there's no acoustic echo cancellation in scope.
    /// </summary>
    public sealed class VoiceActivityDetector : MonoBehaviour
    {
        [SerializeField] private MicrophoneService mic;
        [SerializeField] private InterrogationConfig config;

        /// <summary>Fires each frame new mic samples are available (ungated only) — UtteranceRecorder decides what to do with them. IsSpeaking already reflects this frame's decision by the time this fires.</summary>
        public event Action<float[], int> NewSamplesAvailable;
        /// <summary>Fires only on an actual speaking/not-speaking transition.</summary>
        public event Action<bool> SpeakingStateChanged;
        /// <summary>Fires once calibration finishes, with the resulting noise floor.</summary>
        public event Action<float> CalibrationCompleted;

        public bool IsSpeaking { get; private set; }
        public bool Gated { get; private set; }
        public float DisplayRms => Gated ? 0f : mic.CurrentRms;
        public float NoiseFloor => _noiseFloor;
        /// <summary>Live RMS required to begin an utterance. UI uses the same
        /// value as the detector, so its quiet/accepted indication is honest.</summary>
        public float SpeechThresholdRms => IsCalibrated
            ? _noiseFloor * config.vadEnterMultiplier
            : float.PositiveInfinity;
        public bool IsCalibrated { get; private set; }

        /// <summary>Floor applied to the calibrated noise floor so a dead-silent
        /// room (or, since Calibrate takes a minimum, a single near-zero frame
        /// from mic warm-up/a buffer gap) can't collapse it to ~0 and latch the
        /// VAD permanently on. Shared by Calibrate and ApplyCalibration.</summary>
        private const float MinNoiseFloor = 0.0005f;

        private readonly List<float> _readScratch = new List<float>();

        /// <summary>Percentile of the calibration window taken as the noise
        /// floor. The median is deliberately NOT the minimum: the minimum is the
        /// quietest single frame in the window — a hard lower bound, not what the
        /// room actually sounds like between words. Multiplying it by
        /// vadExitMultiplier produced an exit threshold BELOW ambient room tone,
        /// so silence was never detected and every utterance ran to
        /// vadMaxUtteranceSeconds (20s) before the turn was sent. Measured in a
        /// normal room: min 0.00098 against speech around 0.01-0.05, i.e. the
        /// floor sat 10-50x under the signal it was supposed to be scaled
        /// against, which is why the multipliers appeared to do nothing.
        ///
        /// A percentile keeps the property the minimum was chosen for — one
        /// cough or chair-creak in the window is a handful of frames out of
        /// ~130 and cannot drag it up — while still reflecting real room tone.
        /// </summary>
        private const float NoiseFloorPercentile = 0.5f;

        private readonly List<float> _calibrationSamples = new List<float>();
        private int _calibrationSampleCount;
        private float _calibrationTimer;
        private float _calibrationDuration;
        private bool _calibrating;
        private float _noiseFloor;
        private float _silenceTimer;
        private float _speakingTimer;

        public void SetGated(bool gated)
        {
            if (Gated == gated) return;
            Gated = gated;
            if (gated && IsSpeaking)
            {
                // Discard rather than ship a recording that straddles the
                // cop starting to talk.
                SetSpeaking(false);
            }
        }

        /// <summary>Starts (or restarts, e.g. after a device swap) a noise-floor
        /// calibration window of the given duration. IsCalibrated is false and
        /// IsSpeaking cannot become true until it completes.</summary>
        public void BeginCalibration(float seconds)
        {
            IsCalibrated = false;
            IsSpeaking = false;
            _calibrationSamples.Clear();
            _calibrationSampleCount = 0;
            _calibrationTimer = 0f;
            _calibrationDuration = Mathf.Max(0.05f, seconds);
            _calibrating = true;
        }

        /// <summary>Restores a previously computed noise floor without re-sampling —
        /// used when VoiceCalibrationState already holds a valid result (e.g. a
        /// scene reload) and a fresh silent-room sample isn't needed.</summary>
        public void ApplyCalibration(float noiseFloor)
        {
            _noiseFloor = Mathf.Max(noiseFloor, MinNoiseFloor);
            _calibrating = false;
            IsCalibrated = true;
        }

        private void Update()
        {
            _readScratch.Clear();
            int newCount = mic.ReadNewSamples(_readScratch);

            if (_calibrating)
            {
                Calibrate(newCount);
                return;
            }

            if (Gated || newCount == 0) return;

            // Push-to-talk bypasses the calibrated thresholds entirely: the key
            // IS the decision, so there is no noise floor to get wrong, nothing
            // that varies with the room, and no multipliers to tune. Everything
            // downstream (UtteranceRecorder, DialogueManager, LoudnessGate,
            // RequestSpokenPrompt) drives off IsSpeaking and does not care how
            // the state was reached.
            //
            // Calibration still runs when it is asked to, because
            // LoudnessGate's call-for-Nick threshold uses its own calibrated
            // reference — but PTT never waits on IsCalibrated to capture.
            if (config.pushToTalk)
            {
                bool held = Keyboard.current != null
                    && Keyboard.current[config.pushToTalkKey].isPressed;

                if (held && !IsSpeaking)
                {
                    SetSpeaking(true);
                    _silenceTimer = 0f;
                    _speakingTimer = 0f;
                }
                else if (IsSpeaking)
                {
                    _speakingTimer += Time.deltaTime;
                    // Released, or held past the upload cap the sidecar enforces.
                    if (!held || _speakingTimer >= config.vadMaxUtteranceSeconds)
                    {
                        SetSpeaking(false);
                    }
                }

                float[] pttSamples = _readScratch.ToArray();
                NewSamplesAvailable?.Invoke(pttSamples, mic.DeviceSampleRate);
                return;
            }

            if (!IsCalibrated) return;

            float rms = mic.CurrentRms;
            float enterThreshold = _noiseFloor * config.vadEnterMultiplier;
            float exitThreshold = _noiseFloor * config.vadExitMultiplier;

            if (!IsSpeaking)
            {
                if (rms >= enterThreshold)
                {
                    SetSpeaking(true);
                    _silenceTimer = 0f;
                    _speakingTimer = 0f;
                }
            }
            else
            {
                _speakingTimer += Time.deltaTime;
                _silenceTimer = rms < exitThreshold ? _silenceTimer + Time.deltaTime : 0f;

                if (_silenceTimer >= config.vadSilenceTimeoutSeconds ||
                    _speakingTimer >= config.vadMaxUtteranceSeconds)
                {
                    SetSpeaking(false);
                }
            }

            // Fire AFTER the state transition above, so a subscriber reading
            // IsSpeaking inside its handler sees this frame's decision — in
            // particular, the very first chunk that crosses the enter
            // threshold is correctly seen as "speaking" rather than dropped.
            float[] samples = _readScratch.ToArray();
            NewSamplesAvailable?.Invoke(samples, mic.DeviceSampleRate);
        }

        private void SetSpeaking(bool speaking)
        {
            if (IsSpeaking == speaking) return;
            IsSpeaking = speaking;
            SpeakingStateChanged?.Invoke(speaking);
        }

        /// <summary>Nth percentile of an unsorted sample set. Sorts in place —
        /// the list is calibration-local and cleared straight after.</summary>
        private static float PercentileOf(List<float> samples, float percentile)
        {
            if (samples == null || samples.Count == 0) return 0f;
            samples.Sort();
            int index = Mathf.Clamp(
                Mathf.RoundToInt((samples.Count - 1) * Mathf.Clamp01(percentile)),
                0, samples.Count - 1);
            return samples[index];
        }

        private void Calibrate(int newCount)
        {
            _calibrationTimer += Time.deltaTime;
            if (newCount > 0)
            {
                _calibrationSamples.Add(mic.CurrentRms);
                _calibrationSampleCount++;
            }

            if (_calibrationTimer >= _calibrationDuration)
            {
                float measured = PercentileOf(_calibrationSamples, NoiseFloorPercentile);
                _noiseFloor = Mathf.Max(measured, MinNoiseFloor);
                Debug.Log($"[VAD] Noise floor {_noiseFloor:F5} (p{NoiseFloorPercentile * 100f:F0} of " +
                    $"{_calibrationSampleCount} frame(s) over {_calibrationDuration:F1}s, raw {measured:F5}). " +
                    $"enter>={_noiseFloor * config.vadEnterMultiplier:F5}, " +
                    $"exit<{_noiseFloor * config.vadExitMultiplier:F5}.");
                _calibrationSamples.Clear();
                _calibrating = false;
                IsCalibrated = true;
                CalibrationCompleted?.Invoke(_noiseFloor);
            }
        }
    }
}
