using System;
using System.Collections.Generic;
using FalsePositive.Core;
using UnityEngine;

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

        public bool IsSpeaking { get; private set; }
        public bool Gated { get; private set; }
        public float DisplayRms => Gated ? 0f : mic.CurrentRms;

        private readonly List<float> _readScratch = new List<float>();

        private float _calibrationSum;
        private int _calibrationSampleCount;
        private float _calibrationTimer;
        private bool _calibrated;
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

        private void Update()
        {
            _readScratch.Clear();
            int newCount = mic.ReadNewSamples(_readScratch);

            if (!_calibrated)
            {
                Calibrate(newCount);
                return;
            }

            if (Gated || newCount == 0) return;

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

        private void Calibrate(int newCount)
        {
            _calibrationTimer += Time.deltaTime;
            if (newCount > 0)
            {
                _calibrationSum += mic.CurrentRms;
                _calibrationSampleCount++;
            }

            if (_calibrationTimer >= config.noiseFloorCalibrationSeconds)
            {
                _noiseFloor = _calibrationSampleCount > 0 ? _calibrationSum / _calibrationSampleCount : 0f;
                // Floor so a dead-silent room doesn't yield a zero threshold.
                _noiseFloor = Mathf.Max(_noiseFloor, 0.0005f);
                _calibrated = true;
            }
        }
    }
}
