using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using FalsePositive.Net;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FalsePositive.Core
{
    /// <summary>
    /// Health-checks the configured hosted or local backend before gameplay.
    /// Developers may opt into launching Sidecar/run_sidecar.bat for the local
    /// fallback. Any launched child is killed on quit/disable and receives
    /// --parent-pid so an Editor crash cannot orphan a process holding the port.
    /// </summary>
    public sealed class SidecarProcessLauncher : MonoBehaviour
    {
        [SerializeField] private InterrogationConfig config;
        [SerializeField] private InterrogationSidecarClient client;

        public event Action<string> OnStatus;
        public event Action OnReady;
        public event Action<string> OnFailed;

        private Process _process;

        private void Awake()
        {
            // Same override as InterrogationSidecarClient — resolved
            // independently here since this component reads config.backendBaseUrl
            // and config.autoLaunchSidecar directly (not through the client).
            config = BackendRuntimeOverride.Apply(config);
        }

        public void Begin()
        {
            StartCoroutine(LaunchRoutine());
        }

        private IEnumerator LaunchRoutine()
        {
            OnStatus?.Invoke("Checking for voice services...");

            InterrogationSidecarClient.BackendStatus? initialStatus = null;
            client.CheckHealth(r => initialStatus = r);
            yield return new WaitUntil(() => initialStatus.HasValue);

            if (initialStatus!.Value.Ready)
            {
                OnReady?.Invoke();
                yield break;
            }

            if (initialStatus.Value.ServiceHealthy && !initialStatus.Value.KeyAuthorized)
            {
                // /health passed but the key didn't — auto-launching a local
                // process would not fix this, it would just be misconfigured too.
                OnFailed?.Invoke(
                    "The interrogation service rejected the configured client key. " +
                    "Check backendClientKey in Assets/StreamingAssets/backend.local.json.");
                yield break;
            }

            if (!config.autoLaunchSidecar)
            {
                OnFailed?.Invoke(string.IsNullOrWhiteSpace(config.backendBaseUrl)
                    ? "Voice services are not running. Start the local backend, then press Play again."
                    : "Could not reach the interrogation service. Check your connection and try again.");
                yield break;
            }

            OnStatus?.Invoke("Starting voice services… (downloading models, first run only)");

            if (!TryStartProcess(out string startError))
            {
                OnFailed?.Invoke($"Could not launch the sidecar process: {startError}");
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < config.sidecarLaunchTimeoutSeconds)
            {
                InterrogationSidecarClient.BackendStatus? polled = null;
                client.CheckHealth(r => polled = r);
                yield return new WaitUntil(() => polled.HasValue);

                if (polled!.Value.Ready)
                {
                    OnReady?.Invoke();
                    yield break;
                }

                if (polled.Value.ServiceHealthy && !polled.Value.KeyAuthorized)
                {
                    OnFailed?.Invoke(
                        "The interrogation service rejected the configured client key. " +
                        "Check backendClientKey in Assets/StreamingAssets/backend.local.json.");
                    yield break;
                }

                yield return new WaitForSeconds(config.sidecarHealthPollIntervalSeconds);
                elapsed += config.sidecarHealthPollIntervalSeconds;
            }

            OnFailed?.Invoke("Voice services did not start in time. Check the sidecar console window for errors.");
        }

        private bool TryStartProcess(out string error)
        {
            try
            {
                string sidecarDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Sidecar"));
                string scriptPath = Path.Combine(sidecarDir, "run_sidecar.bat");

                if (!File.Exists(scriptPath))
                {
                    error = $"run_sidecar.bat not found at {scriptPath}";
                    return false;
                }

                int parentPid = Process.GetCurrentProcess().Id;
                bool showWindow = Application.isEditor;

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    // Double-quote wrap is the standard cmd.exe /c trick for a
                    // quoted first token followed by more arguments.
                    Arguments = $"/c \"\"{scriptPath}\" --parent-pid {parentPid}\"",
                    WorkingDirectory = sidecarDir,
                    UseShellExecute = false,
                    CreateNoWindow = !showWindow,
                    WindowStyle = showWindow ? ProcessWindowStyle.Normal : ProcessWindowStyle.Hidden,
                };

                _process = Process.Start(psi);
                error = null;
                return _process != null;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        private void OnApplicationQuit() => TryKillProcess();

        private void OnDisable() => TryKillProcess();

        private void TryKillProcess()
        {
            try
            {
                if (_process != null && !_process.HasExited)
                {
                    _process.Kill();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sidecar] Failed to stop sidecar process cleanly: {e.Message}");
            }
        }
    }
}
