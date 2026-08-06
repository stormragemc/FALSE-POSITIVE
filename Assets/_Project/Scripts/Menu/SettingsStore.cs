using System;
using UnityEngine;

namespace FalsePositive.Menu
{
    /// <summary>
    /// PlayerPrefs-backed settings, loaded once at boot. Runtime tunables
    /// (mouse sensitivity, volumes) are deliberately NOT written into
    /// InterrogationConfig — that is a ScriptableObject, and a play-mode
    /// write to one of its fields in the Editor persists to the .asset file
    /// on disk, which would let a settings slider permanently alter the
    /// shipped config. Read these values through a runtime multiplier
    /// instead (see PlayerInputRouter/FreeLookCameraRig/SeatedCameraRig
    /// call sites once A2's settings panel is wired to them).
    /// </summary>
    public static class SettingsStore
    {
        private const string KeyMicDevice = "fp.settings.micDevice";
        private const string KeyMasterVolume = "fp.settings.masterVolume";
        private const string KeyVoiceVolume = "fp.settings.voiceVolume";
        private const string KeySfxVolume = "fp.settings.sfxVolume";
        private const string KeyMouseSensitivity = "fp.settings.mouseSensitivity";
        private const string KeySubtitlesEnabled = "fp.settings.subtitlesEnabled";
        private const string KeyInvertY = "fp.settings.invertY";

        private const float DefaultMasterVolume = 1f;
        private const float DefaultVoiceVolume = 1f;
        private const float DefaultSfxVolume = 1f;
        private const float DefaultMouseSensitivity = 1f; // multiplier on InterrogationConfig.lookSensitivity
        private const bool DefaultSubtitlesEnabled = true;
        private const bool DefaultInvertY = false;

        public static event Action Changed;

        private static bool _loaded;
        private static string _micDeviceName = "";
        private static float _masterVolume = DefaultMasterVolume;
        private static float _voiceVolume = DefaultVoiceVolume;
        private static float _sfxVolume = DefaultSfxVolume;
        private static float _mouseSensitivity = DefaultMouseSensitivity;
        private static bool _subtitlesEnabled = DefaultSubtitlesEnabled;
        private static bool _invertY = DefaultInvertY;

        public static string MicDeviceName
        {
            get { EnsureLoaded(); return _micDeviceName; }
            set { EnsureLoaded(); _micDeviceName = value ?? ""; RaiseChanged(); }
        }

        public static float MasterVolume
        {
            get { EnsureLoaded(); return _masterVolume; }
            set
            {
                EnsureLoaded();
                _masterVolume = Mathf.Clamp01(value);
                // No AudioMixer asset in the project yet to give Voice/SFX
                // independent buses (that is an in-Editor asset-creation
                // step for a human, not something safe to hand-author here)
                // — Master routes straight to AudioListener.volume as a
                // functional placeholder until one exists.
                AudioListener.volume = _masterVolume;
                RaiseChanged();
            }
        }

        public static float VoiceVolume
        {
            get { EnsureLoaded(); return _voiceVolume; }
            set { EnsureLoaded(); _voiceVolume = Mathf.Clamp01(value); RaiseChanged(); }
        }

        public static float SfxVolume
        {
            get { EnsureLoaded(); return _sfxVolume; }
            set { EnsureLoaded(); _sfxVolume = Mathf.Clamp01(value); RaiseChanged(); }
        }

        public static float MouseSensitivity
        {
            get { EnsureLoaded(); return _mouseSensitivity; }
            set { EnsureLoaded(); _mouseSensitivity = Mathf.Clamp(value, 0.1f, 5f); RaiseChanged(); }
        }

        public static bool SubtitlesEnabled
        {
            get { EnsureLoaded(); return _subtitlesEnabled; }
            set { EnsureLoaded(); _subtitlesEnabled = value; RaiseChanged(); }
        }

        public static bool InvertY
        {
            get { EnsureLoaded(); return _invertY; }
            set { EnsureLoaded(); _invertY = value; RaiseChanged(); }
        }

        private static void EnsureLoaded()
        {
            if (!_loaded) Load();
        }

        public static void Load()
        {
            _micDeviceName = PlayerPrefs.GetString(KeyMicDevice, "");
            _masterVolume = PlayerPrefs.GetFloat(KeyMasterVolume, DefaultMasterVolume);
            _voiceVolume = PlayerPrefs.GetFloat(KeyVoiceVolume, DefaultVoiceVolume);
            _sfxVolume = PlayerPrefs.GetFloat(KeySfxVolume, DefaultSfxVolume);
            _mouseSensitivity = PlayerPrefs.GetFloat(KeyMouseSensitivity, DefaultMouseSensitivity);
            _subtitlesEnabled = PlayerPrefs.GetInt(KeySubtitlesEnabled, DefaultSubtitlesEnabled ? 1 : 0) != 0;
            _invertY = PlayerPrefs.GetInt(KeyInvertY, DefaultInvertY ? 1 : 0) != 0;
            _loaded = true;
            AudioListener.volume = _masterVolume;
        }

        public static void Save()
        {
            EnsureLoaded();
            PlayerPrefs.SetString(KeyMicDevice, _micDeviceName);
            PlayerPrefs.SetFloat(KeyMasterVolume, _masterVolume);
            PlayerPrefs.SetFloat(KeyVoiceVolume, _voiceVolume);
            PlayerPrefs.SetFloat(KeySfxVolume, _sfxVolume);
            PlayerPrefs.SetFloat(KeyMouseSensitivity, _mouseSensitivity);
            PlayerPrefs.SetInt(KeySubtitlesEnabled, _subtitlesEnabled ? 1 : 0);
            PlayerPrefs.SetInt(KeyInvertY, _invertY ? 1 : 0);
            PlayerPrefs.Save();
        }

        public static void ResetToDefaults()
        {
            _micDeviceName = "";
            _masterVolume = DefaultMasterVolume;
            _voiceVolume = DefaultVoiceVolume;
            _sfxVolume = DefaultSfxVolume;
            _mouseSensitivity = DefaultMouseSensitivity;
            _subtitlesEnabled = DefaultSubtitlesEnabled;
            _invertY = DefaultInvertY;
            _loaded = true;
            RaiseChanged();
        }

        private static void RaiseChanged() => Changed?.Invoke();
    }
}
