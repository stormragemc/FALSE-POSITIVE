using System;
using System.Collections;
using System.Collections.Generic;
using FalsePositive.Audio;
using FalsePositive.Core;
using UnityEngine;

namespace FalsePositive.Voice
{
    public enum CalibrationStage
    {
        Idle,
        SamplingRoom,
        SamplingVoice,
        Done,
        Failed,
    }

    /// <summary>
    /// Two-stage calibration run once, right after mic consent: a silent
    /// room-tone window feeds VoiceActivityDetector's noise floor, then a
    /// "speak normally" window derives the loud reference RMS the M1_Night
    /// yell gate (A6) is measured against. See docs/STORY_SCRIPT.md §4 (S0)
    /// for the exact copy at each stage.
    ///
    /// The voice stage waits up to config.calibrationTimeoutSeconds for the
    /// player to cross the noise floor at all (Failed fires if they never
    /// do), then keeps sampling calibrationSpeechSeconds further once they
    /// have, so the percentile reference is built from real speech rather
    /// than the first syllable.
    /// </summary>
    public sealed class MicCalibration : MonoBehaviour
    {
        [SerializeField] private MicrophoneService mic;
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private InterrogationConfig config;
        [SerializeField] private int evaluationWindowSamples = 800; // 50ms at 16kHz

        public float Progress01 { get; private set; }
        public CalibrationStage Stage { get; private set; } = CalibrationStage.Idle;

        public event Action<CalibrationResult> Completed;
        public event Action<string> Failed;

        private Coroutine _routine;

        public void Begin()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(CalibrationRoutine());
        }

        public void Cancel()
        {
            if (_routine != null) StopCoroutine(_routine);
            _routine = null;
            Stage = CalibrationStage.Idle;
        }

        /// <summary>Switches the active mic device and restarts calibration from
        /// the top — the failure card's Retry action.</summary>
        public void Retry(string deviceName)
        {
            if (mic.TryBeginCapture(deviceName, out string error))
            {
                Begin();
            }
            else
            {
                Stage = CalibrationStage.Failed;
                Failed?.Invoke(error);
            }
        }

        private IEnumerator CalibrationRoutine()
        {
            Stage = CalibrationStage.SamplingRoom;
            Progress01 = 0f;
            vad.BeginCalibration(config.calibrationSilenceSeconds);

            while (!vad.IsCalibrated)
            {
                yield return null;
            }

            Stage = CalibrationStage.SamplingVoice;
            var samples = new List<float>();
            var scratch = new List<float>();
            float elapsedSinceStart = 0f;
            float elapsedSinceCross = 0f;
            bool crossedFloor = false;
            float crossThreshold = vad.NoiseFloor * config.vadEnterMultiplier;
            float hardCap = config.calibrationTimeoutSeconds + config.calibrationSpeechSeconds;

            while (true)
            {
                scratch.Clear();
                int newCount = mic.ReadNewSamples(scratch);
                if (newCount > 0)
                {
                    samples.AddRange(scratch);
                    if (!crossedFloor)
                    {
                        float[] chunk = scratch.ToArray();
                        int window = Mathf.Min(evaluationWindowSamples, chunk.Length);
                        if (LoudnessEvaluator.PeakRms(chunk, window) >= crossThreshold)
                        {
                            crossedFloor = true;
                        }
                    }
                }

                elapsedSinceStart += Time.deltaTime;
                if (crossedFloor) elapsedSinceCross += Time.deltaTime;

                Progress01 = Mathf.Clamp01(
                    (config.calibrationSilenceSeconds + elapsedSinceStart) /
                    (config.calibrationSilenceSeconds + config.calibrationTimeoutSeconds));

                if (crossedFloor && elapsedSinceCross >= config.calibrationSpeechSeconds) break;

                if (!crossedFloor && elapsedSinceStart >= config.calibrationTimeoutSeconds)
                {
                    Stage = CalibrationStage.Failed;
                    Failed?.Invoke("I can't hear you. Check your microphone.");
                    yield break;
                }

                if (elapsedSinceStart >= hardCap) break; // safety cap, should be unreachable

                yield return null;
            }

            float[] voiceSamples = samples.ToArray();
            float peakRms = LoudnessEvaluator.PeakRms(voiceSamples, evaluationWindowSamples);
            float percentileRms = LoudnessEvaluator.PercentileRms(voiceSamples, evaluationWindowSamples, config.loudReferencePercentile);

            Stage = CalibrationStage.Done;
            Progress01 = 1f;
            var result = new CalibrationResult(vad.NoiseFloor, percentileRms, peakRms, mic.ActiveDevice);
            Completed?.Invoke(result);
        }
    }
}
