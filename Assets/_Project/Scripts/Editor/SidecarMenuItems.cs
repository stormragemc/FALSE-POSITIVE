using System;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace FalsePositive.EditorTools
{
    /// <summary>
    /// Manual sidecar control for iterating on the Python side without
    /// relying on SidecarProcessLauncher's auto-launch (which only starts
    /// the process when nothing already answers /health — this menu is the
    /// affordance for the "keep it running across multiple Play sessions"
    /// workflow while iterating).
    /// </summary>
    internal static class SidecarMenuItems
    {
        private static Process _process;

        [MenuItem("Tools/Interrogation/Start Sidecar")]
        private static void StartSidecar()
        {
            if (_process != null && !_process.HasExited)
            {
                Debug.Log("[Sidecar] Already running (tracked by this Editor session).");
                return;
            }

            string sidecarDir = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Sidecar"));
            string scriptPath = Path.Combine(sidecarDir, "run_sidecar.bat");

            if (!File.Exists(scriptPath))
            {
                Debug.LogError($"[Sidecar] run_sidecar.bat not found at {scriptPath}");
                return;
            }

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{scriptPath}\"\"",
                WorkingDirectory = sidecarDir,
                UseShellExecute = false,
            };

            try
            {
                _process = Process.Start(psi);
                Debug.Log("[Sidecar] Started. Check the console window it opened for progress/errors.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[Sidecar] Failed to start: {e.Message}");
            }
        }

        [MenuItem("Tools/Interrogation/Stop Sidecar")]
        private static void StopSidecar()
        {
            if (_process == null || _process.HasExited)
            {
                Debug.Log("[Sidecar] Not running (or not started from this menu).");
                return;
            }

            try
            {
                _process.Kill();
                Debug.Log("[Sidecar] Stopped.");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Sidecar] Failed to stop cleanly: {e.Message}");
            }
        }
    }
}
