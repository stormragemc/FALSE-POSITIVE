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
    /// Checks whether the sidecar is already running (the normal workflow
    /// while iterating on the Python side — a manually-started sidecar is
    /// transparently reused) and auto-launches Sidecar/run_sidecar.bat if
    /// not. Kills the child process on quit/disable and passes --parent-pid
    /// so the sidecar self-exits if Unity disappears without a clean
    /// shutdown (e.g. an Editor crash), rather than orphaning a process
    /// holding the port.
    /// </summary>
    public sealed class SidecarProcessLauncher : MonoBehaviour
    {
        [SerializeField] private InterrogationConfig config;
        [SerializeField] private InterrogationSidecarClient client;

        public event Action<string> OnStatus;
        public event Action OnReady;
        public event Action<string> OnFailed;

        private Process _process;

        public void Begin()
        {
            StartCoroutine(LaunchRoutine());
        }

        private IEnumerator LaunchRoutine()
        {
            OnStatus?.Invoke("Checking for voice services...");

            bool? initialHealthy = null;
            client.CheckHealth(r => initialHealthy = r);
            yield return new WaitUntil(() => initialHealthy.HasValue);

            if (initialHealthy!.Value)
            {
                OnReady?.Invoke();
                yield break;
            }

            if (!config.autoLaunchSidecar)
            {
                OnFailed?.Invoke(
                    "Voice services are not running. Start Sidecar/run_sidecar.bat manually, then press Play again.");
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
                bool? polled = null;
                client.CheckHealth(r => polled = r);
                yield return new WaitUntil(() => polled.HasValue);

                if (polled!.Value)
                {
                    OnReady?.Invoke();
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
