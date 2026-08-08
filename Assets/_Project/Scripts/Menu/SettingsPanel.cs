using System.Collections;
using System.Collections.Generic;
using FalsePositive.Audio;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.Menu
{
    /// <summary>
    /// Mic device, master/voice/SFX volume, mouse sensitivity, subtitles,
    /// invert-Y — all PlayerPrefs-backed via SettingsStore. Reused from both
    /// the main menu and (A15, Day 2) the in-game pause menu.
    ///
    /// This component must live on an always-active GameObject *above* root
    /// (see CreateWindow in ProjectBootstrapBuilder.cs: WindowHost -> Root) —
    /// the same constraint MenuWindow.cs documents, and for the same reason:
    /// Hide() fades out on a coroutine before calling root.SetActive(false),
    /// and a coroutine hosted on root itself would die the instant that call
    /// runs. root.SetActive() stays the hard outer visibility gate regardless
    /// (CursorVisibilityController.cs counts enabled Selectables) —
    /// canvasGroup only drives the fade.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Dropdown micDeviceDropdown;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider voiceVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Button backButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Button backdropButton;
        [SerializeField] private MicrophoneService mic;

        [SerializeField] private TextMeshProUGUI masterVolumeReadout;
        [SerializeField] private TextMeshProUGUI voiceVolumeReadout;
        [SerializeField] private TextMeshProUGUI sfxVolumeReadout;
        [SerializeField] private TextMeshProUGUI mouseSensitivityReadout;
        [SerializeField] private float fadeDuration = 0.14f;

        private bool _isRefreshing;
        private int _fadeGeneration;

        /// <summary>Used by MainMenuController's single Escape poller to fall
        /// through to Settings once MenuWindowStack reports nothing open —
        /// SettingsPanel is deliberately not a MenuWindow (see class doc) so
        /// it never registers with that stack itself.</summary>
        public bool IsOpen => root != null && root.activeSelf;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);
            if (resetButton != null) resetButton.onClick.AddListener(HandleReset);
            if (backdropButton != null) backdropButton.onClick.AddListener(Hide);

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
            if (voiceVolumeSlider != null) voiceVolumeSlider.onValueChanged.AddListener(OnVoiceVolumeChanged);
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
            if (subtitlesToggle != null) subtitlesToggle.onValueChanged.AddListener(v => { if (!_isRefreshing) SettingsStore.SubtitlesEnabled = v; });
            if (invertYToggle != null) invertYToggle.onValueChanged.AddListener(v => { if (!_isRefreshing) SettingsStore.InvertY = v; });

            if (micDeviceDropdown != null)
            {
                micDeviceDropdown.onValueChanged.AddListener(index =>
                {
                    if (_isRefreshing || micDeviceDropdown.options.Count == 0) return;
                    SettingsStore.MicDeviceName = micDeviceDropdown.options[index].text;
                });
            }

            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        public void Show()
        {
            Refresh();
            if (root != null) root.SetActive(true);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            StartCoroutine(Fade(1f));
        }

        public void Hide()
        {
            SettingsStore.Save();
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            StartCoroutine(FadeOutAndDeactivate());
        }

        private void HandleReset()
        {
            SettingsStore.ResetToDefaults();
            Refresh();
        }

        private void OnMasterVolumeChanged(float value)
        {
            if (!_isRefreshing) SettingsStore.MasterVolume = value;
            SetPercentReadout(masterVolumeReadout, value);
        }

        private void OnVoiceVolumeChanged(float value)
        {
            if (!_isRefreshing) SettingsStore.VoiceVolume = value;
            SetPercentReadout(voiceVolumeReadout, value);
        }

        private void OnSfxVolumeChanged(float value)
        {
            if (!_isRefreshing) SettingsStore.SfxVolume = value;
            SetPercentReadout(sfxVolumeReadout, value);
        }

        private void OnMouseSensitivityChanged(float value)
        {
            if (!_isRefreshing) SettingsStore.MouseSensitivity = value;
            if (mouseSensitivityReadout != null) mouseSensitivityReadout.text = value.ToString("0.00") + "×";
        }

        private static void SetPercentReadout(TextMeshProUGUI label, float value)
        {
            if (label != null) label.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        private void Refresh()
        {
            _isRefreshing = true;

            if (micDeviceDropdown != null && mic != null)
            {
                micDeviceDropdown.ClearOptions();
                var options = new List<string>(mic.AvailableDevices);
                if (options.Count == 0) options.Add("No microphone found");
                micDeviceDropdown.AddOptions(options);
                int index = options.IndexOf(SettingsStore.MicDeviceName);
                micDeviceDropdown.value = Mathf.Max(0, index);
                micDeviceDropdown.RefreshShownValue();
            }

            if (masterVolumeSlider != null) masterVolumeSlider.value = SettingsStore.MasterVolume;
            if (voiceVolumeSlider != null) voiceVolumeSlider.value = SettingsStore.VoiceVolume;
            if (sfxVolumeSlider != null) sfxVolumeSlider.value = SettingsStore.SfxVolume;
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.value = SettingsStore.MouseSensitivity;
            if (subtitlesToggle != null) subtitlesToggle.isOn = SettingsStore.SubtitlesEnabled;
            if (invertYToggle != null) invertYToggle.isOn = SettingsStore.InvertY;

            SetPercentReadout(masterVolumeReadout, SettingsStore.MasterVolume);
            SetPercentReadout(voiceVolumeReadout, SettingsStore.VoiceVolume);
            SetPercentReadout(sfxVolumeReadout, SettingsStore.SfxVolume);
            if (mouseSensitivityReadout != null) mouseSensitivityReadout.text = SettingsStore.MouseSensitivity.ToString("0.00") + "×";

            _isRefreshing = false;
        }

        private IEnumerator FadeOutAndDeactivate()
        {
            yield return Fade(0f);
            if (root != null) root.SetActive(false);
        }

        /// <summary>Structurally the same fade as MenuWindow.cs (generation
        /// counter, unscaled time, SmoothStep) — kept as its own small copy
        /// rather than composing a MenuWindow component, since SettingsPanel's
        /// Show()/Hide() API and its GameFlowDirector-routed call sites predate
        /// this and are reused as-is for the Day-2 pause menu (A15).</summary>
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
