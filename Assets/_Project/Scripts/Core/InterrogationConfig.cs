using System;
using UnityEngine;

namespace FalsePositive.Core
{
    /// <summary>
    /// All tunable numbers for the interrogation loop in one place. See
    /// Assets/_Project/Config/InterrogationConfig.asset for the instance
    /// used by the scene.
    ///
    /// The deployed build must carry <see cref="backendClientKey"/> to reach
    /// the hosted backend, so that value is extractable by design. It bounds
    /// drive-by traffic but is not a security boundary; server-side turn caps
    /// bound cost. No vendor API key is ever stored in this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "InterrogationConfig", menuName = "False Positive/Interrogation Config")]
    public sealed class InterrogationConfig : ScriptableObject
    {
        [Header("Backend connection")]
        [Tooltip("Full https:// URL of the hosted backend. Leave empty to fall back to the local host/port below.")]
        public string backendBaseUrl = "";
        [Tooltip("Shared secret sent as X-FP-Client-Key. Extractable from a shipped build by design — it stops drive-by traffic, it is not a security boundary.")]
        public string backendClientKey = "";
        [Tooltip("Used only when backendBaseUrl is empty — local container or local python process.")]
        public string sidecarHost = "127.0.0.1";
        public int sidecarPort = 8080;
        [Tooltip("Per-request timeout. Generous because it now covers network latency as well as model work.")]
        public float requestTimeoutSeconds = 60f;

        [Header("Local sidecar launch (developers only)")]
        [Tooltip("Off for shipped builds. Only starts a local python process when backendBaseUrl is empty.")]
        public bool autoLaunchSidecar = false;
        [Tooltip("How long to poll /health before giving up (first run downloads models).")]
        public float sidecarLaunchTimeoutSeconds = 90f;
        public float sidecarHealthPollIntervalSeconds = 0.5f;

        [Header("Look / camera")]
        [Tooltip("Scale applied to raw pointer delta. Pointer delta is already per-frame — never multiply by Time.deltaTime.")]
        public float lookSensitivity = 0.08f;
        [Tooltip("Max accumulated look offset from seat-forward, on each axis independently (so the cone is square, not circular).")]
        public float seatedMaxLookAngleDegrees = 10f;
        public float standingPitchClampDegrees = 85f;
        public float fadeDurationSeconds = 0.25f;

        [Header("Microphone / VAD")]
        public int micTargetSampleRate = 16000;
        public int micRingBufferLengthSeconds = 10;
        public float noiseFloorCalibrationSeconds = 1.0f;
        public float vadEnterMultiplier = 2.0f;
        public float vadExitMultiplier = 1.2f;
        public float vadMinUtteranceSeconds = 0.3f;
        public float vadSilenceTimeoutSeconds = 0.7f;
        public float vadMaxUtteranceSeconds = 20f;
        [Tooltip("How long after the cop's audio stops playing before the mic re-arms — covers the audible reverb tail.")]
        public float ttsEchoGateTailSeconds = 0.25f;

        [Header("Voice calibration (A4) and the call-for-Nick loudness gate (A6)")]
        [Tooltip("Room-tone sampling window during calibration's first stage.")]
        public float calibrationSilenceSeconds = 1.0f;
        [Tooltip("\"Speak normally\" sampling window during calibration's second stage.")]
        public float calibrationSpeechSeconds = 4.0f;
        [Tooltip("How long calibration waits for any signal above the noise floor before offering Retry.")]
        public float calibrationTimeoutSeconds = 10.0f;
        [Tooltip("Percentile of per-frame RMS (0..1) used as the loud reference — the 90th percentile, not the peak, so a single cough or chair scrape can't set an unpassable threshold.")]
        [Range(0f, 1f)]
        public float loudReferencePercentile = 0.9f;
        [Tooltip("The call-for-Nick gate requires peak RMS >= loudReferenceRms * this factor.")]
        public float yellFactor = 1.6f;

        public string SidecarBaseUrl =>
            string.IsNullOrWhiteSpace(backendBaseUrl)
                ? $"http://{sidecarHost}:{sidecarPort}"
                : backendBaseUrl.TrimEnd('/');

        public bool IsBackendUrlAllowed
        {
            get
            {
                if (!Uri.TryCreate(SidecarBaseUrl, UriKind.Absolute, out Uri uri))
                {
                    return false;
                }

                return uri.Scheme == Uri.UriSchemeHttps
                    || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
            }
        }
    }
}
