using System;
using System.Collections;
using System.Collections.Generic;
using FalsePositive.Audio;
using FalsePositive.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace FalsePositive.Net
{
    /// <summary>
    /// The only script in the project that talks to the sidecar. Never
    /// throws into the game loop — every failure path (connection error,
    /// non-2xx response, malformed JSON, ok:false body) resolves through
    /// <paramref name="onError"/> instead, so callers can always recover by
    /// re-arming the VAD and playing a canned line rather than crashing.
    /// </summary>
    public sealed class InterrogationSidecarClient : MonoBehaviour
    {
        private const int BackendSampleRate = 16000;
        // Mirrors Sidecar/config.py's SIDECAR_MAX_AUDIO_SECONDS default (20s).
        // The server value is configurable; this is a client-side safety
        // clamp against the documented default, not an attempt to mirror a
        // runtime-configurable server setting exactly.
        private const int MaxAudioSeconds = 20;

        [SerializeField] private InterrogationConfig config;

        // Fixed session id used only to probe /session/reset for an
        // authorized-key check during the health gate. /session/reset on an
        // id with no active session is a cheap, side-effect-free no-op
        // server-side (Sidecar/app.py:_session_store.reset et al.) — it
        // never consumes turn budget and never evicts a real session.
        private const string HealthProbeSessionId = "__health_probe__";

        public bool IsBusy { get; private set; }

        /// <summary>Outcome of a health gate check. /health itself is
        /// deliberately unauthenticated (Sidecar/app.py exempts it), so a
        /// wrong or missing client key still returns 200 there — ServiceHealthy
        /// can be true while KeyAuthorized is false. Both must be true before
        /// gameplay proceeds.</summary>
        public readonly struct BackendStatus
        {
            public readonly bool ServiceHealthy;
            public readonly bool KeyAuthorized;

            public BackendStatus(bool serviceHealthy, bool keyAuthorized)
            {
                ServiceHealthy = serviceHealthy;
                KeyAuthorized = keyAuthorized;
            }

            public bool Ready => ServiceHealthy && KeyAuthorized;
        }

        private void Awake()
        {
            // Resolves the hosted URL/key from a gitignored StreamingAssets
            // override if one is present, on a clone — never mutates the
            // committed asset. See BackendRuntimeOverride for why.
            config = BackendRuntimeOverride.Apply(config);
        }

        /// <summary>Convenience overload for callers that only care whether
        /// the backend is fully usable (service warmed up AND key accepted).</summary>
        public void CheckHealth(Action<bool> onResult)
        {
            CheckHealth(status => onResult?.Invoke(status.Ready));
        }

        public void CheckHealth(Action<BackendStatus> onResult)
        {
            StartCoroutine(HealthRoutine(onResult));
        }

        private IEnumerator HealthRoutine(Action<BackendStatus> onResult)
        {
            if (!config.IsBackendUrlAllowed)
            {
                onResult?.Invoke(new BackendStatus(false, false));
                yield break;
            }

            string url = $"{config.SidecarBaseUrl}/health";
            using UnityWebRequest healthReq = UnityWebRequest.Get(url);
            healthReq.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);
            ApplyClientKey(healthReq);
            yield return healthReq.SendWebRequest();

            bool serviceHealthy = false;
            if (healthReq.result == UnityWebRequest.Result.Success)
            {
                string body = healthReq.downloadHandler != null ? healthReq.downloadHandler.text : null;
                if (!string.IsNullOrEmpty(body))
                {
                    try
                    {
                        SidecarHealthResponse health = JsonUtility.FromJson<SidecarHealthResponse>(body);
                        serviceHealthy = health != null && health.status == "ok" && health.models_loaded;
                    }
                    catch (Exception)
                    {
                        serviceHealthy = false;
                    }
                }
            }

            if (!serviceHealthy)
            {
                onResult?.Invoke(new BackendStatus(false, false));
                yield break;
            }

            // /health is auth-exempt (Sidecar/app.py), so a missing or wrong
            // backendClientKey still gets here. /session/reset is the
            // cheapest authenticated endpoint available to tell them apart.
            var resetForm = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("session_id", HealthProbeSessionId),
            };
            string resetUrl = $"{config.SidecarBaseUrl}/session/reset";
            using UnityWebRequest authReq = UnityWebRequest.Post(resetUrl, resetForm);
            authReq.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);
            ApplyClientKey(authReq);
            yield return authReq.SendWebRequest();

            bool keyAuthorized = authReq.result == UnityWebRequest.Result.Success;
            onResult?.Invoke(new BackendStatus(true, keyAuthorized));
        }

        /// <summary>
        /// Posts one conversational turn. <paramref name="pcmSamples"/> may
        /// be null/empty to request the officer's scripted opening line (or,
        /// mid-session, his next line with no witness utterance to react to
        /// — see Sidecar/app.py's is_session_opening/has_audio split)
        /// instead of running STT/SER on captured audio.
        /// <paramref name="sceneInstruction"/> may be null/empty on most
        /// turns; when non-empty it becomes the sidecar's remembered phase
        /// briefing for this session (re-applied automatically to every
        /// subsequent turn server-side) — send it once per phase, not every
        /// turn. See docs/GAME_COMPLETION_PLAN.md A7.
        /// </summary>
        public void PostTurn(
            string sessionId,
            float[] pcmSamples,
            int sampleRate,
            int onsetDelayMs,
            string sceneInstruction,
            Action<SidecarTurnResponse> onSuccess,
            Action<string> onError)
        {
            if (IsBusy)
            {
                onError?.Invoke("A turn is already in flight.");
                return;
            }
            StartCoroutine(TurnRoutine(sessionId, pcmSamples, sampleRate, onsetDelayMs, sceneInstruction, onSuccess, onError));
        }

        /// <summary>Requests a full server-side session reset — dialogue history and the
        /// HuBERT affect baseline for <paramref name="sessionId"/> are both cleared.
        /// Used by GameFlowDirector.StartNewPlaythrough and quit-to-menu (A15).</summary>
        public void ResetSession(string sessionId, Action<bool> onDone)
        {
            StartCoroutine(ResetSessionRoutine(sessionId, onDone));
        }

        private IEnumerator ResetSessionRoutine(string sessionId, Action<bool> onDone)
        {
            if (!config.IsBackendUrlAllowed)
            {
                onDone?.Invoke(false);
                yield break;
            }

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("session_id", sessionId),
            };
            string url = $"{config.SidecarBaseUrl}/session/reset";
            using UnityWebRequest req = UnityWebRequest.Post(url, form);
            req.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);
            ApplyClientKey(req);
            yield return req.SendWebRequest();
            onDone?.Invoke(req.result == UnityWebRequest.Result.Success);
        }

        private IEnumerator TurnRoutine(
            string sessionId,
            float[] pcmSamples,
            int sampleRate,
            int onsetDelayMs,
            string sceneInstruction,
            Action<SidecarTurnResponse> onSuccess,
            Action<string> onError)
        {
            IsBusy = true;

            if (!config.IsBackendUrlAllowed)
            {
                IsBusy = false;
                onError?.Invoke("The interrogation service URL must use HTTPS (localhost may use HTTP).");
                yield break;
            }

            int uploadSampleRate = BackendSampleRate;
            float[] uploadSamples = pcmSamples;
            if (pcmSamples != null && pcmSamples.Length > 0)
            {
                try
                {
                    uploadSamples = PcmUtility.ResampleMono(pcmSamples, sampleRate, uploadSampleRate);
                }
                catch (ArgumentOutOfRangeException)
                {
                    IsBusy = false;
                    onError?.Invoke("Microphone audio has an invalid sample rate.");
                    yield break;
                }

                // Sidecar/app.py rejects any payload strictly greater than
                // SIDECAR_MAX_AUDIO_SECONDS (20s) * sample_rate * 2 bytes.
                // VoiceActivityDetector's wall-clock timer is checked once per
                // frame, so a maximal utterance can land one frame past the
                // limit; clamp here rather than let that 400 the turn.
                int maxUploadSamples = MaxAudioSeconds * uploadSampleRate;
                if (uploadSamples.Length > maxUploadSamples)
                {
                    Array.Resize(ref uploadSamples, maxUploadSamples);
                }
            }

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("session_id", sessionId),
                new MultipartFormDataSection("sample_rate", uploadSampleRate.ToString()),
                new MultipartFormDataSection("onset_delay_ms", Mathf.Max(0, onsetDelayMs).ToString()),
            };

            if (uploadSamples != null && uploadSamples.Length > 0)
            {
                byte[] pcmBytes = PcmUtility.FloatsToPcm16Bytes(uploadSamples);
                form.Add(new MultipartFormFileSection("audio", pcmBytes, "utterance.pcm", "application/octet-stream"));
            }

            if (!string.IsNullOrEmpty(sceneInstruction))
            {
                form.Add(new MultipartFormDataSection("scene_instruction", sceneInstruction));
            }

            string url = $"{config.SidecarBaseUrl}/turn";
            using UnityWebRequest req = UnityWebRequest.Post(url, form);
            req.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);
            ApplyClientKey(req);

            yield return req.SendWebRequest();

            IsBusy = false;

            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                onError?.Invoke("Could not reach the interrogation service. Check your connection and try again.");
                yield break;
            }

            string bodyText = req.downloadHandler != null ? req.downloadHandler.text : null;

            if (req.result == UnityWebRequest.Result.ProtocolError)
            {
                string serverError = TryExtractError(bodyText);
                onError?.Invoke(string.IsNullOrEmpty(serverError)
                    ? DescribeHttpStatus(req.responseCode)
                    : serverError);
                yield break;
            }

            if (string.IsNullOrEmpty(bodyText))
            {
                onError?.Invoke("Sidecar returned an empty response.");
                yield break;
            }

            SidecarTurnResponse response;
            try
            {
                response = JsonUtility.FromJson<SidecarTurnResponse>(bodyText);
            }
            catch (Exception e)
            {
                onError?.Invoke($"Failed to parse sidecar response: {e.Message}");
                yield break;
            }

            if (response == null || !response.ok)
            {
                onError?.Invoke(string.IsNullOrEmpty(response?.error) ? "Sidecar reported failure." : response.error);
                yield break;
            }

            onSuccess?.Invoke(response);
        }

        private void ApplyClientKey(UnityWebRequest request)
        {
            if (!string.IsNullOrEmpty(config.backendClientKey))
            {
                request.SetRequestHeader("X-FP-Client-Key", config.backendClientKey);
            }
        }

        private static string TryExtractError(string bodyText)
        {
            if (string.IsNullOrEmpty(bodyText)) return null;
            try
            {
                SidecarTurnResponse parsed = JsonUtility.FromJson<SidecarTurnResponse>(bodyText);
                if (!string.IsNullOrEmpty(parsed?.error))
                {
                    return parsed.error;
                }
            }
            catch
            {
                // Not the standard _empty_response() shape — fall through and
                // try FastAPI's own {"detail": ...} error envelope instead
                // (returned for validation errors that never reach app.py's
                // handlers, e.g. an unrecognised route).
            }

            try
            {
                ErrorDetailEnvelope detail = JsonUtility.FromJson<ErrorDetailEnvelope>(bodyText);
                return string.IsNullOrEmpty(detail?.detail) ? null : detail.detail;
            }
            catch
            {
                return null;
            }
        }

        [Serializable]
        private sealed class ErrorDetailEnvelope
        {
            public string detail;
        }

        /// <summary>Fallback message when the response body carried neither
        /// SidecarTurnResponse's "error" nor FastAPI's "detail" field —
        /// distinguishes the common failure modes for the player instead of
        /// a bare status code.</summary>
        private static string DescribeHttpStatus(long code)
        {
            switch (code)
            {
                case 401:
                    return "The interrogation service rejected the configured client key.";
                case 413:
                    return "That recording was too large for the interrogation service to accept.";
                case 429:
                    return "The interrogation service has reached its usage limit for now.";
                case 500:
                    return "The interrogation service failed to process that turn. Try again.";
                case 504:
                    return "The interrogation service timed out on that turn. Try again.";
                default:
                    return $"Sidecar returned an error ({code}).";
            }
        }
    }
}
