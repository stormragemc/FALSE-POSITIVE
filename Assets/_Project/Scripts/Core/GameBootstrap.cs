using FalsePositive.Dialogue;
using FalsePositive.Player;
using FalsePositive.UI;
using UnityEngine;

namespace FalsePositive.Core
{
    /// <summary>
    /// The single entry point for the scene: locks the cursor, gates
    /// gameplay behind the sidecar's /health via SidecarProcessLauncher, and
    /// only then starts the player seated and kicks off the officer's
    /// opening line.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private SidecarProcessLauncher sidecarLauncher;
        [SerializeField] private PlayerStateController playerStateController;
        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private DebugOverlayUI debugOverlay;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Start()
        {
            sidecarLauncher.OnStatus += HandleStatus;
            sidecarLauncher.OnReady += HandleReady;
            sidecarLauncher.OnFailed += HandleFailed;
            sidecarLauncher.Begin();
        }

        private void HandleStatus(string text)
        {
            debugOverlay?.SetBootStatus(text);
        }

        private void HandleReady()
        {
            debugOverlay?.SetBootStatus(string.Empty);
            playerStateController.BeginSeated();
            dialogueManager.BeginConversation();
        }

        private void HandleFailed(string reason)
        {
            debugOverlay?.SetBootStatus($"Voice services unavailable: {reason}");
            Debug.LogError($"[GameBootstrap] {reason}");
        }
    }
}
