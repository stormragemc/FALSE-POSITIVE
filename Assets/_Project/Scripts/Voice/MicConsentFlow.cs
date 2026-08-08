using System;
using System.Collections;
using System.Collections.Generic;
using FalsePositive.Audio;
using FalsePositive.Flow;
using FalsePositive.Menu;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.Voice
{
    /// <summary>
    /// The diegetic mic-consent card — ROADMAP S8, guardrail #1
    /// (docs/GAME_COMPLETION_PLAN.md §8). No capture starts before this is
    /// accepted; Enable is the only path that calls MicConsentGate.Grant().
    /// Copy is verbatim from docs/STORY_SCRIPT.md §4 (S0) — do not paraphrase
    /// it, it is a graded/demoed artifact. Lives on the always-active window
    /// Host in _Persistent's HUD canvas (see CreateWindow's doc comment in
    /// ProjectBootstrapBuilder.cs) so it can show over the menu without
    /// Interrogation needing to be active yet, and so its fade coroutine
    /// survives Hide() deactivating Root — same constraint SettingsPanel.cs
    /// and MenuWindow.cs document.
    /// </summary>
    public sealed class MicConsentFlow : MonoBehaviour
    {
        private const string ConsentCopy =
            "This game listens. Your microphone stays on for the whole session so the officer can " +
            "hear you. Your voice is processed to transcribe what you say and read the tone you say " +
            "it in. Nothing is recorded to disk.";

        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI copyText;
        [SerializeField] private Dropdown deviceDropdown;
        [SerializeField] private Button enableButton;
        [SerializeField] private Button backButton;
        [SerializeField] private MicrophoneService mic;
        [SerializeField] private float fadeDuration = 0.14f;

        private int _fadeGeneration;

        public bool HasConsentThisSession { get; private set; }

        /// <summary>GameFlowDirector's consent->calibration handoff times its
        /// cross-fade off this so the two panels never drift out of sync.</summary>
        public float FadeDuration => fadeDuration;

        public event Action Accepted;
        public event Action Declined;

        private void Awake()
        {
            if (copyText != null) copyText.text = ConsentCopy;
            if (enableButton != null) enableButton.onClick.AddListener(HandleEnable);
            if (backButton != null) backButton.onClick.AddListener(Decline);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        public void Show()
        {
            PopulateDevices();
            if (root != null) root.SetActive(true);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            StartCoroutine(Fade(1f));
        }

        public void Hide()
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            StartCoroutine(FadeOutAndDeactivate());
        }

        /// <summary>The Back button's handler. Hides the card and only then
        /// fires Declined, so GameFlowDirector's subscriber never returns to
        /// the menu while this card is still visible on top of it.</summary>
        public void Decline()
        {
            Hide();
            Declined?.Invoke();
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

        private IEnumerator FadeOutAndDeactivate()
        {
            yield return Fade(0f);
            if (root != null) root.SetActive(false);
        }

        /// <summary>Structurally the same fade as SettingsPanel.cs/MenuWindow.cs
        /// (generation counter, unscaled time, SmoothStep).</summary>
        private IEnumerator Fade(float to)
        {
            if (canvasGroup == null) yield break;

            int generation = ++_fadeGeneration;
            float from = canvasGroup.alpha;

            if (fadeDuration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < fadeDuration)
            {
                if (generation != _fadeGeneration) yield break;
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.SmoothStep(from, to, t / fadeDuration);
                yield return null;
            }
            if (generation == _fadeGeneration) canvasGroup.alpha = to;
        }
    }
}
