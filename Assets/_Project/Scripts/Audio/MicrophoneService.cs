using System;
using System.Collections.Generic;
using FalsePositive.Core;
using FalsePositive.Voice;
using UnityEngine;

namespace FalsePositive.Audio
{
    /// <summary>
    /// The SOLE owner of Microphone.Start in the project — grep for
    /// "Microphone.Start" and TryBeginCapture below must be the only match.
    /// Exposes a shared ring-buffer read API so the level meter and the
    /// VAD/utterance recorder both read from the same buffer instead of each
    /// starting their own microphone capture, which would fight over the
    /// device.
    ///
    /// Capture no longer starts automatically in Start() — this lives in
    /// _Persistent and consent is decided in the menu, before Interrogation
    /// is meaningfully active. TryBeginCapture refuses to open the device
    /// unless MicConsentGate.Granted is true (guardrail #1,
    /// docs/GAME_COMPLETION_PLAN.md §8). Once open, the device is kept open
    /// for the rest of the session, including while standing — stopping and
    /// restarting causes a device-acquire hitch on every sit. Gate
    /// consumption logically via VoiceActivityDetector.Gated instead.
    /// </summary>
    public sealed class MicrophoneService : MonoBehaviour
    {
        [SerializeField] private InterrogationConfig config;

        public bool IsCapturing { get; private set; }
        public int DeviceSampleRate { get; private set; }
        public float CurrentRms { get; private set; }
        public string ActiveDevice { get; private set; }
        public IReadOnlyList<string> AvailableDevices => Microphone.devices;

        /// <summary>Fires once capture actually starts on a device.</summary>
        public event Action CaptureStarted;
        /// <summary>Fires if the active device disappears mid-session (unplugged) —
        /// drives A13's F5 fault card.</summary>
        public event Action<string> CaptureLost;

        private AudioClip _clip;
        private int _lastReadPos;
        private bool _wasRecordingLastFrame;

        /// <summary>
        /// Opens the microphone on <paramref name="deviceName"/> (or the system
        /// default if null/empty), or returns false with a displayable
        /// <paramref name="error"/>. Never called automatically — the consent
        /// flow (A3) and calibration retry (A4) are the only callers.
        /// </summary>
        public bool TryBeginCapture(string deviceName, out string error)
        {
            if (!MicConsentGate.Granted)
            {
                error = "Microphone consent has not been granted.";
                return false;
            }

            if (Microphone.devices.Length == 0)
            {
                error = "No microphone devices found.";
                return false;
            }

            string device = string.IsNullOrEmpty(deviceName) || Array.IndexOf(Microphone.devices, deviceName) < 0
                ? Microphone.devices[0]
                : deviceName;

            EndCapture();

            Microphone.GetDeviceCaps(device, out int min, out int max);
            int requested = config.micTargetSampleRate;
            bool noHardLimit = min == 0 && max == 0;
            int rate = (noHardLimit || (requested >= min && requested <= max)) ? requested : max;

            _clip = Microphone.Start(device, true, config.micRingBufferLengthSeconds, rate);
            if (_clip == null)
            {
                error = $"Could not open microphone device \"{device}\".";
                return false;
            }

            ActiveDevice = device;
            DeviceSampleRate = rate;
            _lastReadPos = 0;
            _wasRecordingLastFrame = false;
            IsCapturing = true;
            error = null;
            CaptureStarted?.Invoke();
            return true;
        }

        public void EndCapture()
        {
            if (IsCapturing && !string.IsNullOrEmpty(ActiveDevice))
            {
                Microphone.End(ActiveDevice);
            }
            IsCapturing = false;
            ActiveDevice = null;
            _clip = null;
            CurrentRms = 0f;
        }

        private void OnDestroy() => EndCapture();

        private void Update()
        {
            // A device can disappear mid-session (unplugged, driver reset).
            // Microphone.IsRecording silently goes false rather than
            // throwing — surface it so the UI can show the F5 fault card
            // instead of silently going deaf.
            if (!IsCapturing) return;

            bool recording = Microphone.IsRecording(ActiveDevice);
            if (_wasRecordingLastFrame && !recording)
            {
                string lostDevice = ActiveDevice;
                EndCapture();
                CaptureLost?.Invoke(lostDevice);
            }
            _wasRecordingLastFrame = recording;
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
            if (!IsCapturing || !Microphone.IsRecording(ActiveDevice)) return 0;

            int pos = Microphone.GetPosition(ActiveDevice);
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
