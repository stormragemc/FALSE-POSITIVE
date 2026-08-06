using FalsePositive.Dialogue;
using FalsePositive.Player;
using UnityEngine;

namespace FalsePositive.Flow
{
    /// <summary>
    /// Lives in Interrogation.unity. Person A owns this file; Person B never
    /// edits it. Interrogation is loaded additively, once, behind the
    /// consent card, and from then on is only ever deactivated/reactivated —
    /// never unloaded (see GameFlowDirector.SessionId). Because Unity cannot
    /// serialize a cross-scene reference, every service that moved to
    /// _Persistent (MicrophoneService, VoiceActivityDetector,
    /// UtteranceRecorder, InterrogationSidecarClient, ScreenFader) has to be
    /// pulled in at runtime instead of wired in the inspector — this is the
    /// one script that does that pulling. One direction only: this binder
    /// reads from GameFlowDirector.Instance; GameFlowDirector never reaches
    /// into this scene directly.
    /// </summary>
    public sealed class InterrogationSceneBinder : MonoBehaviour
    {
        [SerializeField] private DialogueManager dialogueManager;
        [SerializeField] private PlayerStateController playerState;

        public DialogueManager Dialogue => dialogueManager;
        public PlayerStateController PlayerState => playerState;
        public bool IsBound { get; private set; }

        private void Awake()
        {
            GameFlowDirector flow = GameFlowDirector.Instance;
            if (flow == null)
            {
                Debug.LogError("[InterrogationSceneBinder] No GameFlowDirector in the scene — " +
                    "_Persistent must be loaded before Interrogation.");
                return;
            }

            dialogueManager.BindServices(flow.Sidecar, flow.Vad, flow.Recorder, flow.SessionId);
            playerState.SetFader(flow.Fader);
            // DebugOverlayUI lives in _Persistent too — reached through
            // GameFlowDirector rather than a serialized field, same reason
            // as everything else in this method.
            flow.DebugOverlay?.Bind(flow.Vad, dialogueManager);

            IsBound = true;
        }

        private void OnDisable()
        {
            // Deliberately does NOT unbind DialogueManager on deactivate —
            // deactivating this scene's roots during a memory scene must
            // not tear down the session binding, only Suspend() it (see
            // GameFlowDirector.AdvancePhase / DialogueManager.Suspend).
            // Unbind only matters for a genuine teardown, which this
            // project never does to the Interrogation scene.
        }
    }
}
