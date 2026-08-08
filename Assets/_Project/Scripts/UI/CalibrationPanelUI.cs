using System.Collections;
using System.Collections.Generic;
using FalsePositive.Audio;
using FalsePositive.Core;
using FalsePositive.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Drives the calibration card's copy, level meter, and progress bar,
    /// plus the failure state's device dropdown + Retry/Cancel buttons. Copy
    /// is verbatim from docs/STORY_SCRIPT.md §4 (S0). Purely a view over
    /// MicCalibration — GameFlowDirector owns starting calibration and what
    /// happens after it completes. Lives on the always-active window Host in
    /// _Persistent's HUD canvas (see CreateWindow's doc comment in
    /// ProjectBootstrapBuilder.cs), same constraint as MicConsentFlow.cs:
    /// the fade coroutine must outlive Hide() deactivating Root, so the
    /// calibration.Completed/Failed subscription lives in Show()/Hide()
    /// rather than OnEnable/OnDisable, and Update() early-outs while hidden.
    /// </summary>
    public sealed class CalibrationPanelUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private TextMeshProUGUI statusText;
        [SerializeField] private Image levelMeterFill;
        [SerializeField] private Image progressBarFill;
        [SerializeField] private TextMeshProUGUI progressReadout;
        [SerializeField] private GameObject failurePanel;
        [SerializeField] private Dropdown deviceDropdown;
        [SerializeField] private Button retryButton;
        [SerializeField] private Button cancelButton;
        [SerializeField] private MicCalibration calibration;
        [SerializeField] private MicrophoneService mic;
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private InterrogationConfig config;
        [SerializeField] private Color tooQuietColor = new Color(0.361f, 0.400f, 0.459f);
        [SerializeField] private Color activeColor = new Color(0.431f, 0.565f, 0.722f);
        [SerializeField] private Color tooLoudColor = new Color(0.698f, 0.227f, 0.180f);
        [SerializeField] private float fadeDuration = 0.14f;

        /// <summary>Cancel button on a failed calibration — closes the same
        /// "player is stranded with no exit" bug class as MicConsentFlow's
        /// Back button, one step later in the flow.</summary>
        public event System.Action Cancelled;

        private bool _isShown;
        private int _fadeGeneration;
        private float _displayLevel;
        private float _displayProgress;

        private void Awake()
        {
            if (retryButton != null) retryButton.onClick.AddListener(HandleRetry);
            if (cancelButton != null) cancelButton.onClick.AddListener(HandleCancel);
            if (canvasGroup != null) canvasGroup.alpha = 0f;
        }

        private void Update()
        {
            if (!_isShown) return;

            if (levelMeterFill != null && vad != null)
            {
                float rms = vad.DisplayRms;
                float tooLoudRms = config != null ? config.micTooLoudRms : 0.25f;
                float targetLevel = MicIndicator.RmsToMeter(rms, tooLoudRms);
                float speed = targetLevel > _displayLevel ? 8f : 3f;
                _displayLevel = Mathf.MoveTowards(_displayLevel, targetLevel, speed * Time.deltaTime);

                RectTransform fillRt = levelMeterFill.rectTransform;
                Vector2 anchorMax = fillRt.anchorMax;
                anchorMax.x = _displayLevel;
                fillRt.anchorMax = anchorMax;

                bool tooLoud = rms >= tooLoudRms;
                bool accepted = vad.IsCalibrated && rms >= vad.SpeechThresholdRms;
                levelMeterFill.color = tooLoud ? tooLoudColor : accepted ? activeColor : tooQuietColor;
            }

            DriveProgress();

            if (statusText == null || calibration == null) return;
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

        /// <summary>MicCalibration.Progress01 isn't smooth — it holds at 0
        /// through the room-tone stage, then advances through the voice
        /// stage, then snaps to 1 on Done. Driving the bar through
        /// Mathf.Max(current, target) before MoveTowards makes it monotone,
        /// so that never reads as a visible jump backward or a skip.</summary>
        private void DriveProgress()
        {
            float target = calibration != null ? Mathf.Clamp01(calibration.Progress01) : 0f;
            float monotoneTarget = Mathf.Max(_displayProgress, target);
            _displayProgress = Mathf.MoveTowards(_displayProgress, monotoneTarget, 1.5f * Time.deltaTime);

            if (progressBarFill != null)
            {
                RectTransform fillRt = progressBarFill.rectTransform;
                Vector2 anchorMax = fillRt.anchorMax;
                anchorMax.x = _displayProgress;
                fillRt.anchorMax = anchorMax;
            }
            if (progressReadout != null) progressReadout.text = Mathf.RoundToInt(_displayProgress * 100f) + "%";
        }

        public void Show()
        {
            ResetVisualState();
            SetFailureVisible(false);

            // Idempotent re-subscribe rather than assuming OnEnable/OnDisable
            // parity — this component's GameObject never deactivates (see
            // class doc), so Show()/Hide() are the only lifecycle hooks.
            if (calibration != null)
            {
                calibration.Completed -= OnCompleted;
                calibration.Completed += OnCompleted;
                calibration.Failed -= OnFailed;
                calibration.Failed += OnFailed;
            }

            _isShown = true;
            if (root != null) root.SetActive(true);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
            StartCoroutine(Fade(1f));
        }

        public void Hide()
        {
            _isShown = false;
            if (calibration != null)
            {
                calibration.Completed -= OnCompleted;
                calibration.Failed -= OnFailed;
            }
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            StartCoroutine(FadeOutAndDeactivate());
        }

        /// <summary>Zeroes the meter/progress/status visuals so a second
        /// playthrough's calibration card doesn't flash the previous run's
        /// stale state for a frame before Update() catches up.</summary>
        private void ResetVisualState()
        {
            _displayLevel = 0f;
            _displayProgress = 0f;

            if (levelMeterFill != null)
            {
                RectTransform fillRt = levelMeterFill.rectTransform;
                fillRt.anchorMax = new Vector2(0f, fillRt.anchorMax.y);
            }
            if (progressBarFill != null)
            {
                RectTransform fillRt = progressBarFill.rectTransform;
                fillRt.anchorMax = new Vector2(0f, fillRt.anchorMax.y);
            }
            if (progressReadout != null) progressReadout.text = "0%";
            if (statusText != null) statusText.text = string.Empty;
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

        private void SetFailureVisible(bool visible)
        {
            if (failurePanel != null) failurePanel.SetActive(visible);
            if (retryButton != null) retryButton.gameObject.SetActive(visible);
            if (cancelButton != null) cancelButton.gameObject.SetActive(visible);
        }

        private void OnCompleted(CalibrationResult result)
        {
            SetFailureVisible(false);
        }

        private void OnFailed(string reason)
        {
            if (statusText != null) statusText.text = reason;
            SetFailureVisible(true);
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
            SetFailureVisible(false);
            calibration.Retry(device);
        }

        private void HandleCancel()
        {
            calibration?.Cancel();
            Hide();
            Cancelled?.Invoke();
        }
    }
}
