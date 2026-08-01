using System;
using System.Collections.Generic;
using FalsePositive.Core;
using UnityEngine;

namespace FalsePositive.Audio
{
    /// <summary>
    /// The SOLE owner of Microphone.Start in the project. Exposes a shared
    /// ring-buffer read API so the level meter and the VAD/utterance
    /// recorder both read from the same buffer instead of each starting
    /// their own microphone capture, which would fight over the device.
    ///
    /// The device is kept open at all times, including while standing —
    /// stopping/restarting causes a device-acquire hitch on every sit.
    /// Gate consumption logically via VoiceActivityDetector.Gated instead.
    /// </summary>
    public sealed class MicrophoneService : MonoBehaviour
    {
        [SerializeField] private InterrogationConfig config;

        public bool IsCapturing { get; private set; }
        public int DeviceSampleRate { get; private set; }
        public float CurrentRms { get; private set; }

        private string _device;
        private AudioClip _clip;
        private int _lastReadPos;

        private void Start()
        {
            if (Microphone.devices.Length == 0)
            {
                Debug.LogError("[Mic] No microphone devices found.");
                IsCapturing = false;
                return;
            }

            _device = Microphone.devices[0];
            Microphone.GetDeviceCaps(_device, out int min, out int max);

            int requested = config.micTargetSampleRate;
            bool noHardLimit = min == 0 && max == 0;
            int rate = (noHardLimit || (requested >= min && requested <= max)) ? requested : max;
            DeviceSampleRate = rate;

            _clip = Microphone.Start(_device, true, config.micRingBufferLengthSeconds, rate);
            _lastReadPos = 0;
            IsCapturing = true;
        }

        private void OnDestroy()
        {
            if (IsCapturing && !string.IsNullOrEmpty(_device))
            {
                Microphone.End(_device);
            }
        }

        /// <summary>
        /// Appends all samples captured since the last call to
        /// <paramref name="destination"/> and updates CurrentRms from the
        /// tail of that new chunk. Returns the number of new samples.
        /// </summary>
        public int ReadNewSamples(List<float> destination)
        {
            // GetPosition returns 0 before the device has actually spun up —
            // reading then would poison noise-floor calibration with
            // silence, so wait until IsRecording is genuinely true.
            if (!IsCapturing || !Microphone.IsRecording(_device)) return 0;

            int pos = Microphone.GetPosition(_device);
            int clipLength = _clip.samples;
            if (pos == _lastReadPos) return 0;

            float[] newSamples;
            if (pos > _lastReadPos)
            {
                int count = pos - _lastReadPos;
                newSamples = new float[count];
                _clip.GetData(newSamples, _lastReadPos);
            }
            else
            {
                // The ring wrapped since the last read — copy in two segments.
                int tailCount = clipLength - _lastReadPos;
                int headCount = pos;
                newSamples = new float[tailCount + headCount];

                if (tailCount > 0)
                {
                    float[] tail = new float[tailCount];
                    _clip.GetData(tail, _lastReadPos);
                    Array.Copy(tail, 0, newSamples, 0, tailCount);
                }
                if (headCount > 0)
                {
                    float[] head = new float[headCount];
                    _clip.GetData(head, 0);
                    Array.Copy(head, 0, newSamples, tailCount, headCount);
                }
            }

            _lastReadPos = pos;
            destination.AddRange(newSamples);
            UpdateMeter(newSamples);
            return newSamples.Length;
        }

        private void UpdateMeter(float[] newSamples)
        {
            int take = Mathf.Min(newSamples.Length, 1024);
            if (take <= 0) return;

            double sumSq = 0;
            for (int i = newSamples.Length - take; i < newSamples.Length; i++)
            {
                sumSq += newSamples[i] * newSamples[i];
            }
            CurrentRms = Mathf.Sqrt((float)(sumSq / take));
        }
    }
}
