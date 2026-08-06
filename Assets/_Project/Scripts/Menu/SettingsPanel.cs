using System.Collections.Generic;
using FalsePositive.Audio;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.Menu
{
    /// <summary>
    /// Mic device, master/voice/SFX volume, mouse sensitivity, subtitles,
    /// invert-Y — all PlayerPrefs-backed via SettingsStore. Reused from both
    /// the main menu and (A15, Day 2) the in-game pause menu.
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Dropdown micDeviceDropdown;
        [SerializeField] private Slider masterVolumeSlider;
        [SerializeField] private Slider voiceVolumeSlider;
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider mouseSensitivitySlider;
        [SerializeField] private Toggle subtitlesToggle;
        [SerializeField] private Toggle invertYToggle;
        [SerializeField] private Button backButton;
        [SerializeField] private MicrophoneService mic;

        private bool _isRefreshing;

        private void Awake()
        {
            if (backButton != null) backButton.onClick.AddListener(Hide);

            if (masterVolumeSlider != null) masterVolumeSlider.onValueChanged.AddListener(v => { if (!_isRefreshing) SettingsStore.MasterVolume = v; });
            if (voiceVolumeSlider != null) voiceVolumeSlider.onValueChanged.AddListener(v => { if (!_isRefreshing) SettingsStore.VoiceVolume = v; });
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(v => { if (!_isRefreshing) SettingsStore.SfxVolume = v; });
            if (mouseSensitivitySlider != null) mouseSensitivitySlider.onValueChanged.AddListener(v => { if (!_isRefreshing) SettingsStore.MouseSensitivity = v; });
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
        }

        public void Show()
        {
            Refresh();
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            SettingsStore.Save();
            if (root != null) root.SetActive(false);
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

            _isRefreshing = false;
        }
    }
}
