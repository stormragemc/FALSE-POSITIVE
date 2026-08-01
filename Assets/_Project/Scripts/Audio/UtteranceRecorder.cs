using System;
using System.Collections.Generic;
using FalsePositive.Core;
using UnityEngine;

namespace FalsePositive.Audio
{
    /// <summary>
    /// Accumulates mic samples while VoiceActivityDetector says "speaking"
    /// and emits the finished utterance once it says "not speaking", if the
    /// captured span clears the configured minimum duration (shorter spans
    /// are discarded as coughs/clicks/chair noise). Split from the VAD's
    /// decision logic — this class owns 100% of the buffer bookkeeping and
    /// none of the RMS/hysteresis state, so each half is testable on its
    /// own.
    /// </summary>
    public sealed class UtteranceRecorder : MonoBehaviour
    {
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private InterrogationConfig config;

        public event Action<float[], int> UtteranceCaptured;

        private readonly List<float> _buffer = new List<float>();
        private int _sampleRate;

        private void OnEnable()
        {
            vad.NewSamplesAvailable += OnNewSamples;
            vad.SpeakingStateChanged += OnSpeakingStateChanged;
        }

        private void OnDisable()
        {
            vad.NewSamplesAvailable -= OnNewSamples;
            vad.SpeakingStateChanged -= OnSpeakingStateChanged;
        }

        private void OnNewSamples(float[] samples, int sampleRate)
        {
            _sampleRate = sampleRate;
            if (vad.IsSpeaking)
            {
                _buffer.AddRange(samples);
            }
        }

        private void OnSpeakingStateChanged(bool speaking)
        {
            if (speaking)
            {
                _buffer.Clear();
                return;
            }

            float durationSeconds = _sampleRate > 0 ? _buffer.Count / (float)_sampleRate : 0f;
            if (_buffer.Count > 0 && durationSeconds >= config.vadMinUtteranceSeconds)
            {
                UtteranceCaptured?.Invoke(_buffer.ToArray(), _sampleRate);
            }
            _buffer.Clear();
        }
    }
}
