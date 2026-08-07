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
        [SerializeField] private InterrogationConfig config;

        public bool IsBusy { get; private set; }

        public void CheckHealth(Action<bool> onResult)
        {
            StartCoroutine(HealthRoutine(onResult));
        }

        private IEnumerator HealthRoutine(Action<bool> onResult)
        {
            string url = $"{config.SidecarBaseUrl}/health";
            using UnityWebRequest req = UnityWebRequest.Get(url);
            req.timeout = 5;
            yield return req.SendWebRequest();
            onResult?.Invoke(req.result == UnityWebRequest.Result.Success);
        }

        /// <summary>
        /// Posts one conversational turn. <paramref name="pcmSamples"/> may
        /// be null/empty to request the officer's scripted opening line
        /// instead of running STT/SER on captured audio.
        /// </summary>
        public void PostTurn(
            string sessionId,
            float[] pcmSamples,
            int sampleRate,
            int onsetDelayMs,
            Action<SidecarTurnResponse> onSuccess,
            Action<SidecarTurnResponse> onSessionEnded,
            Action<string> onError)
        {
            if (IsBusy)
            {
                onError?.Invoke("A turn is already in flight.");
                return;
            }
            StartCoroutine(TurnRoutine(
                sessionId,
                pcmSamples,
                sampleRate,
                onsetDelayMs,
                onSuccess,
                onSessionEnded,
                onError));
        }

        private IEnumerator TurnRoutine(
            string sessionId,
            float[] pcmSamples,
            int sampleRate,
            int onsetDelayMs,
            Action<SidecarTurnResponse> onSuccess,
            Action<SidecarTurnResponse> onSessionEnded,
            Action<string> onError)
        {
            IsBusy = true;

            var form = new List<IMultipartFormSection>
            {
                new MultipartFormDataSection("session_id", sessionId),
                new MultipartFormDataSection("sample_rate", sampleRate.ToString()),
                new MultipartFormDataSection("onset_delay_ms", Mathf.Max(0, onsetDelayMs).ToString()),
            };

            if (pcmSamples != null && pcmSamples.Length > 0)
            {
                byte[] pcmBytes = PcmUtility.FloatsToPcm16Bytes(pcmSamples);
                form.Add(new MultipartFormFileSection("audio", pcmBytes, "utterance.pcm", "application/octet-stream"));
            }

            string url = $"{config.SidecarBaseUrl}/turn";
            using UnityWebRequest req = UnityWebRequest.Post(url, form);
            req.SetRequestHeader("X-FP-Client-Key", config.backendClientKey);
            req.timeout = Mathf.CeilToInt(config.requestTimeoutSeconds);

            yield return req.SendWebRequest();

            IsBusy = false;

            if (req.result == UnityWebRequest.Result.ConnectionError)
            {
                onError?.Invoke("Voice service unreachable. Is the sidecar running?");
                yield break;
            }

            string bodyText = req.downloadHandler != null ? req.downloadHandler.text : null;

            if (req.result == UnityWebRequest.Result.ProtocolError)
            {
                SidecarTurnResponse cappedResponse = TryParseResponse(bodyText);
                if (cappedResponse != null && cappedResponse.session_ended)
                {
                    onSessionEnded?.Invoke(cappedResponse);
                    yield break;
                }
                string serverError = cappedResponse?.error;
                onError?.Invoke(string.IsNullOrEmpty(serverError)
                    ? $"Sidecar returned an error ({req.responseCode})."
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

        private static SidecarTurnResponse TryParseResponse(string bodyText)
        {
            if (string.IsNullOrEmpty(bodyText)) return null;
            try
            {
                return JsonUtility.FromJson<SidecarTurnResponse>(bodyText);
            }
            catch
            {
                return null;
            }
        }
    }
}
