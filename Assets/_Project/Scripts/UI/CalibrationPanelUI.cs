using System.Collections.Generic;
using FalsePositive.Audio;
using FalsePositive.Voice;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Drives the calibration card's copy and the failure state's device
    /// dropdown + Retry button. Copy is verbatim from docs/STORY_SCRIPT.md
    /// §4 (S0). Purely a view over MicCalibration — GameFlowDirector owns
    /// starting calibration and what happens after it completes.
    /// </summary>
    public sealed class CalibrationPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text statusText;
        [SerializeField] private Image levelMeterFill;
        [SerializeField] private GameObject failurePanel;
        [SerializeField] private Dropdown deviceDropdown;
        [SerializeField] private Button retryButton;
        [SerializeField] private MicCalibration calibration;
        [SerializeField] private MicrophoneService mic;
        [SerializeField] private VoiceActivityDetector vad;

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(HandleRetry);
        }

        private void OnEnable()
        {
            calibration.Completed += OnCompleted;
            calibration.Failed += OnFailed;
        }

        private void OnDisable()
        {
            calibration.Completed -= OnCompleted;
            calibration.Failed -= OnFailed;
        }

        private void Update()
        {
            if (levelMeterFill != null && vad != null)
            {
                levelMeterFill.fillAmount = Mathf.Clamp01(vad.DisplayRms * 12f);
            }

            if (statusText == null) return;
            switch (calibration.Stage)
            {
                case CalibrationStage.SamplingRoom:
                    statusText.text = "One moment — listening to the room.";
                    break;
                case CalibrationStage.SamplingVoice:
                    statusText.text = "Speak normally for a few seconds. Say anything.";
                    break;
                case CalibrationStage.Done:
                    statusText.text = "Good. The officer can hear you.";
                    break;
            }
        }

        public void Show()
        {
            if (failurePanel != null) failurePanel.SetActive(false);
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }

        private void OnCompleted(CalibrationResult result)
        {
            if (failurePanel != null) failurePanel.SetActive(false);
        }

        private void OnFailed(string reason)
        {
            if (statusText != null) statusText.text = reason;
            if (failurePanel == null) return;

            failurePanel.SetActive(true);
            if (deviceDropdown == null || mic == null) return;

            deviceDropdown.ClearOptions();
            var options = new List<string>(mic.AvailableDevices);
            if (options.Count == 0) options.Add("No microphone found");
            deviceDropdown.AddOptions(options);
            deviceDropdown.RefreshShownValue();
        }

        private void HandleRetry()
        {
            string device = deviceDropdown != null && deviceDropdown.options.Count > 0
                ? deviceDropdown.options[deviceDropdown.value].text
                : null;
            if (failurePanel != null) failurePanel.SetActive(false);
            calibration.Retry(device);
        }
    }
}
