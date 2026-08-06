using System;
using System.Collections.Generic;
using FalsePositive.Audio;
using FalsePositive.Flow;
using FalsePositive.Menu;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.Voice
{
    /// <summary>
    /// The diegetic mic-consent card — ROADMAP S8, guardrail #1
    /// (docs/GAME_COMPLETION_PLAN.md §8). No capture starts before this is
    /// accepted; Enable is the only path that calls MicConsentGate.Grant().
    /// Copy is verbatim from docs/STORY_SCRIPT.md §4 (S0) — do not paraphrase
    /// it, it is a graded/demoed artifact. Lives in _Persistent's HUD canvas
    /// so it can show over the menu without Interrogation needing to be
    /// active yet.
    /// </summary>
    public sealed class MicConsentFlow : MonoBehaviour
    {
        private const string ConsentCopy =
            "This game listens. Your microphone stays on for the whole session so the officer can " +
            "hear you. Your voice is processed to transcribe what you say and read the tone you say " +
            "it in. Nothing is recorded to disk.";

        [SerializeField] private GameObject root;
        [SerializeField] private Text copyText;
        [SerializeField] private Dropdown deviceDropdown;
        [SerializeField] private Button enableButton;
        [SerializeField] private Button backButton;
        [SerializeField] private MicrophoneService mic;

        public bool HasConsentThisSession { get; private set; }

        public event Action Accepted;
        public event Action Declined;

        private void Awake()
        {
            if (copyText != null) copyText.text = ConsentCopy;
            if (enableButton != null) enableButton.onClick.AddListener(HandleEnable);
            if (backButton != null) backButton.onClick.AddListener(HandleBack);
        }

        public void Show()
        {
            PopulateDevices();
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void PopulateDevices()
        {
            if (deviceDropdown == null || mic == null) return;

            deviceDropdown.ClearOptions();
            var options = new List<string>(mic.AvailableDevices);
            if (options.Count == 0) options.Add("No microphone found");
            deviceDropdown.AddOptions(options);

            string preferred = SettingsStore.MicDeviceName;
            int preferredIndex = string.IsNullOrEmpty(preferred) ? -1 : options.IndexOf(preferred);
            deviceDropdown.value = Mathf.Max(0, preferredIndex);
            deviceDropdown.RefreshShownValue();
        }

        private void HandleEnable()
        {
            MicConsentGate.Grant();
            HasConsentThisSession = true;

            string selectedDevice = deviceDropdown != null && deviceDropdown.options.Count > 0
                ? deviceDropdown.options[deviceDropdown.value].text
                : null;

            string error = "no MicrophoneService is wired on this component";
            bool captureStarted = mic != null && mic.TryBeginCapture(selectedDevice, out error);

            if (captureStarted)
            {
                SettingsStore.MicDeviceName = selectedDevice;
            }
            else
            {
                // Consent stays granted — TryBeginCapture failing here is a
                // device problem, not a consent problem. A4's calibration
                // failure card (device dropdown + Retry) is where this gets
                // resolved; Accepted still fires so the flow hands off to it.
                Debug.LogWarning($"[MicConsentFlow] TryBeginCapture failed: {error}");
            }
            Accepted?.Invoke();
        }

        private void HandleBack()
        {
            Declined?.Invoke();
        }
    }
}
