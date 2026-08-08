using System.IO;
using System.Text;
using UnityEngine;

namespace FalsePositive.Audio
{
    /// <summary>
    /// float32 &lt;-&gt; PCM16 LE conversions, and AudioClip construction from raw
    /// PCM bytes. The WAV writer here is used ONLY for the debug-dump-to-disk
    /// affordance (Phase 2 verification) — the network transport itself never
    /// uses a WAV container, just raw PCM16 bytes plus a separate sample-rate
    /// field, so there's one less format for a bug to hide in.
    /// </summary>
    public static class PcmUtility
    {
        /// <summary>
        /// Resamples mono microphone audio before upload. Some devices reject
        /// Unity's requested 16 kHz capture rate and fall back to 44.1/48 kHz;
        /// normalizing here keeps request sizes and the backend contract stable.
        /// </summary>
        public static float[] ResampleMono(float[] samples, int sourceSampleRate, int targetSampleRate)
        {
            if (samples == null) return null;
            if (sourceSampleRate <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(sourceSampleRate));
            if (targetSampleRate <= 0)
                throw new System.ArgumentOutOfRangeException(nameof(targetSampleRate));
            if (samples.Length == 0 || sourceSampleRate == targetSampleRate) return samples;

            int outputLength = Mathf.Max(
                1,
                Mathf.RoundToInt(samples.Length * (targetSampleRate / (float)sourceSampleRate))
            );
            float[] output = new float[outputLength];
            for (int i = 0; i < outputLength; i++)
            {
                float sourcePosition = i * (sourceSampleRate / (float)targetSampleRate);
                int left = Mathf.Min(Mathf.FloorToInt(sourcePosition), samples.Length - 1);
                int right = Mathf.Min(left + 1, samples.Length - 1);
                output[i] = Mathf.Lerp(samples[left], samples[right], sourcePosition - left);
            }
            return output;
        }

        public static byte[] FloatsToPcm16Bytes(float[] samples)
        {
            byte[] bytes = new byte[samples.Length * 2];
            for (int i = 0; i < samples.Length; i++)
            {
                short s = (short)Mathf.Clamp(Mathf.RoundToInt(samples[i] * 32767f), short.MinValue, short.MaxValue);
                bytes[i * 2] = (byte)(s & 0xFF);
                bytes[i * 2 + 1] = (byte)((s >> 8) & 0xFF);
            }
            return bytes;
        }

        public static float[] Pcm16BytesToFloats(byte[] bytes)
        {
            int sampleCount = bytes.Length / 2;
            float[] samples = new float[sampleCount];
            for (int i = 0; i < sampleCount; i++)
            {
                short s = (short)(bytes[i * 2] | (bytes[i * 2 + 1] << 8));
                samples[i] = s / 32768f;
            }
            return samples;
        }

        /// <summary>Decodes PCM16 into a clip, optionally applying gain.
        ///
        /// <paramref name="gain"/> exists because the officer's live TTS comes
        /// back noticeably quieter than the pre-rendered cast VO, which is
        /// jarring when a cutscene line and a live reply sit seconds apart.
        /// AudioSource.volume cannot fix it — it is clamped at 1.0 and already
        /// there — so the boost has to happen in the sample data.
        ///
        /// Hard-clipping a doubled signal would sound worse than the quiet it
        /// fixes, so peaks are limited with tanh, which compresses what would
        /// clip and leaves everything below it essentially untouched.</summary>
        public static AudioClip ToAudioClip(byte[] pcm16Bytes, int sampleRate, int channels, string clipName,
            float gain = 1f)
        {
            float[] samples = Pcm16BytesToFloats(pcm16Bytes);
            if (!Mathf.Approximately(gain, 1f))
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    float boosted = samples[i] * gain;
                    // Only engage the limiter where it would actually clip.
                    samples[i] = boosted > 0.95f || boosted < -0.95f
                        ? (float)System.Math.Tanh(boosted)
                        : boosted;
                }
            }
            channels = Mathf.Max(channels, 1);
            int frameCount = Mathf.Max(samples.Length / channels, 1);
            AudioClip clip = AudioClip.Create(clipName, frameCount, channels, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        /// <summary>Debug-only: writes a playable 16-bit PCM mono WAV file to disk.</summary>
        public static void WriteWavFile(string path, float[] samples, int sampleRate)
        {
            byte[] pcm = FloatsToPcm16Bytes(samples);
            int byteRate = sampleRate * 2; // mono, 16-bit

            using var stream = new FileStream(path, FileMode.Create);
            using var writer = new BinaryWriter(stream);

            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcm.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);   // PCM
            writer.Write((short)1);   // mono
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)2);   // block align
            writer.Write((short)16);  // bits per sample
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcm.Length);
            writer.Write(pcm);
        }
    }
}
