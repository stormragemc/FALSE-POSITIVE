using UnityEngine;

namespace FalsePositive.Voice
{
    /// <summary>
    /// Pure, static, no Unity MonoBehaviour dependency — unit-testable against
    /// synthetic buffers without entering play mode. Used both by MicCalibration
    /// (A4, to find the loud reference RMS) and by LoudnessGate (A6, to decide
    /// whether a captured utterance clears the call-for-Nick threshold).
    /// </summary>
    public static class LoudnessEvaluator
    {
        /// <summary>RMS over the loudest contiguous window of length
        /// <paramref name="windowSamples"/> in <paramref name="samples"/>, computed
        /// via a running sum so a long utterance stays O(n). A single-sample
        /// click cannot pass a windowed evaluation the way a raw peak sample
        /// could — the window forces sustained loudness, which is the point.</summary>
        public static float PeakRms(float[] samples, int windowSamples)
        {
            if (samples == null || samples.Length == 0) return 0f;
            windowSamples = Mathf.Clamp(windowSamples, 1, samples.Length);

            double windowSumSq = 0;
            for (int i = 0; i < windowSamples; i++)
            {
                windowSumSq += (double)samples[i] * samples[i];
            }

            double bestSumSq = windowSumSq;
            for (int i = windowSamples; i < samples.Length; i++)
            {
                windowSumSq += (double)samples[i] * samples[i] - (double)samples[i - windowSamples] * samples[i - windowSamples];
                if (windowSumSq > bestSumSq) bestSumSq = windowSumSq;
            }

            return Mathf.Sqrt((float)(bestSumSq / windowSamples));
        }

        public static bool ClearsGate(float[] samples, int windowSamples, float thresholdRms)
        {
            return PeakRms(samples, windowSamples) >= thresholdRms;
        }

        /// <summary>The Nth percentile (0..1) of per-frame RMS across
        /// <paramref name="samples"/>, computed over non-overlapping windows of
        /// <paramref name="windowSamples"/>. Used by calibration for the "speak
        /// normally" pass — the 90th percentile rather than the peak, so a
        /// single cough or chair scrape can't set a threshold no later yell
        /// will clear.</summary>
        public static float PercentileRms(float[] samples, int windowSamples, float percentile)
        {
            if (samples == null || samples.Length == 0) return 0f;
            windowSamples = Mathf.Clamp(windowSamples, 1, samples.Length);

            int windowCount = Mathf.Max(1, samples.Length / windowSamples);
            var windowRms = new float[windowCount];
            for (int w = 0; w < windowCount; w++)
            {
                int start = w * windowSamples;
                int end = Mathf.Min(start + windowSamples, samples.Length);
                double sumSq = 0;
                for (int i = start; i < end; i++)
                {
                    sumSq += (double)samples[i] * samples[i];
                }
                windowRms[w] = Mathf.Sqrt((float)(sumSq / (end - start)));
            }

            System.Array.Sort(windowRms);
            int index = Mathf.Clamp(Mathf.FloorToInt(percentile * (windowRms.Length - 1)), 0, windowRms.Length - 1);
            return windowRms[index];
        }
    }
}
