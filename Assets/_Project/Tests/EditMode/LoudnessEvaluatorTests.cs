using FalsePositive.Voice;
using NUnit.Framework;

namespace FalsePositive.Tests
{
    /// <summary>
    /// The one piece of Track A checkable without a microphone and a quiet
    /// room — see docs/GAME_COMPLETION_PLAN.md A6. First thing to cut if the
    /// day runs short; everything here is also verifiable by hand in-Editor.
    /// </summary>
    public class LoudnessEvaluatorTests
    {
        private const int SampleRate = 16000;
        private const int WindowSamples = 800; // 50ms

        private static float[] Silence(int count) => new float[count];

        private static float[] Sine(int count, float amplitude, float frequencyHz)
        {
            var samples = new float[count];
            for (int i = 0; i < count; i++)
            {
                samples[i] = amplitude * SineValue(i, frequencyHz);
            }
            return samples;
        }

        private static float SineValue(int sampleIndex, float frequencyHz)
        {
            return (float)System.Math.Sin(2.0 * System.Math.PI * frequencyHz * sampleIndex / SampleRate);
        }

        [Test]
        public void Silence_NeverClearsGate()
        {
            float[] samples = Silence(SampleRate);
            Assert.IsFalse(LoudnessEvaluator.ClearsGate(samples, WindowSamples, thresholdRms: 0.01f));
        }

        [Test]
        public void HalfThresholdAmplitude_DoesNotClearGate()
        {
            // RMS of a sine wave is amplitude / sqrt(2). Pick a reference
            // threshold and generate a signal whose RMS sits well below it.
            const float referenceRms = 0.2f;
            float[] loudEnough = Sine(SampleRate, amplitude: referenceRms * 1.41421356f, frequencyHz: 220f);
            float measuredReference = LoudnessEvaluator.PeakRms(loudEnough, WindowSamples);

            float[] tooQuiet = Sine(SampleRate, amplitude: referenceRms * 0.5f * 1.41421356f, frequencyHz: 220f);

            Assert.IsFalse(LoudnessEvaluator.ClearsGate(tooQuiet, WindowSamples, measuredReference * 1.6f));
        }

        [Test]
        public void DoubleThresholdAmplitude_ClearsGate()
        {
            const float referenceRms = 0.2f;
            float[] reference = Sine(SampleRate, amplitude: referenceRms * 1.41421356f, frequencyHz: 220f);
            float measuredReference = LoudnessEvaluator.PeakRms(reference, WindowSamples);

            float[] loud = Sine(SampleRate, amplitude: referenceRms * 2f * 1.41421356f, frequencyHz: 220f);

            Assert.IsTrue(LoudnessEvaluator.ClearsGate(loud, WindowSamples, measuredReference * 1.6f));
        }

        [Test]
        public void SingleSampleClick_DoesNotPassWindowedEvaluation()
        {
            float[] samples = Silence(SampleRate);
            samples[SampleRate / 2] = 5.0f; // one wildly loud sample, everything else silent

            // A raw peak-sample check would pass this; the windowed RMS
            // check must not, because a click is not sustained loudness.
            Assert.IsFalse(LoudnessEvaluator.ClearsGate(samples, WindowSamples, thresholdRms: 0.3f));
        }

        [Test]
        public void PercentileRms_IsRobustToASingleLoudOutlierWindow()
        {
            // Mostly quiet speech-like signal with one loud cough window.
            float[] samples = Sine(SampleRate, amplitude: 0.05f, frequencyHz: 150f);
            for (int i = 0; i < WindowSamples; i++)
            {
                samples[i] = 2.0f; // one very loud window at the very start (the "cough")
            }

            float p90 = LoudnessEvaluator.PercentileRms(samples, WindowSamples, 0.9f);
            float peak = LoudnessEvaluator.PeakRms(samples, WindowSamples);

            // The 90th percentile must sit well below the single-window peak —
            // otherwise a cough during calibration sets an unpassable yell gate.
            Assert.Less(p90, peak * 0.5f);
        }
    }
}
