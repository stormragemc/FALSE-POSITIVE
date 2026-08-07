using UnityEngine;

namespace FalsePositive.Core
{
    /// <summary>
    /// All tunable numbers for the interrogation loop in one place. Contains
    /// no provider API credentials. The client key is an abuse deterrent that
    /// ships with the build and is therefore not a protected secret. See
    /// Assets/_Project/Config/InterrogationConfig.asset for the instance
    /// used by the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "InterrogationConfig", menuName = "False Positive/Interrogation Config")]
    public sealed class InterrogationConfig : ScriptableObject
    {
        [Header("Sidecar connection")]
        public string sidecarHost = "127.0.0.1";
        public int sidecarPort = 8765;
        [Tooltip("Optional HTTPS backend URL. Leave blank to use the local sidecar host and port.")]
        public string backendBaseUrl = "";
        [Tooltip("Must match FP_CLIENT_KEY on the sidecar. Treat this as an abuse deterrent, not a secret in a shipped build.")]
        public string backendClientKey = "";
        [Tooltip("Per-request timeout. Generous because first-run model loading on the sidecar is slow.")]
        public float requestTimeoutSeconds = 60f;

        [Header("Sidecar launch")]
        public bool autoLaunchSidecar = true;
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
        public float vadEnterMultiplier = 3.0f;
        public float vadExitMultiplier = 1.8f;
        public float vadMinUtteranceSeconds = 0.3f;
        public float vadSilenceTimeoutSeconds = 0.7f;
        public float vadMaxUtteranceSeconds = 20f;
        [Tooltip("How long after the cop's audio stops playing before the mic re-arms — covers the audible reverb tail.")]
        public float ttsEchoGateTailSeconds = 0.25f;

        public string SidecarBaseUrl => string.IsNullOrWhiteSpace(backendBaseUrl)
            ? $"http://{sidecarHost}:{sidecarPort}"
            : backendBaseUrl.TrimEnd('/');
    }
}
