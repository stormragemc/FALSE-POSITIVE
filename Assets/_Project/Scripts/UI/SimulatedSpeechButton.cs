using FalsePositive.Audio;
using FalsePositive.Dialogue;
using FalsePositive.Flow;
using FalsePositive.Voice;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Offline-demo test hook only — see docs/GAME_COMPLETION_PLAN.md §10
    /// "never fake": this is the one deliberate exception, an explicit,
    /// clearly-labelled tester control, not something the game triggers on
    /// its own. Stands in for the player actually speaking at every mic
    /// gate: S0's calibration (MicCalibration.SimulateVoice) and P1/M1/P2/P3
    /// (UtteranceRecorder.SimulateUtterance — the one shared event
    /// DialogueManager, GameFlowDirector.RequestSpokenPrompt and
    /// LoudnessGate all independently subscribe to).
    ///
    /// Deliberately NOT a Selectable — CursorVisibilityController unlocks
    /// the cursor whenever any Selectable is enabled anywhere in the loaded
    /// scenes, so a Button here would free the mouse through the whole of
    /// first-person gameplay. Instead this is a plain Image with pointer
    /// handlers, plus an F3 hotkey for the cursor-locked sections (M1/M2)
    /// where no click can land at all. F3, not F2: F2 is GameFlowDirector's
    /// pre-existing dev phase-skip key, and the two used to share F2 — one
    /// press fired both, force-advancing a whole phase (including its
    /// cutscenes) on top of simulating speech. F1 is DebugOverlayUI.
    ///
    /// Poller/root split matches OfflineModeLabel: this component lives on
    /// the always-active poller so Update() keeps polling, and "root" is
    /// the separate child that's actually shown/hidden. Pointer clicks on
    /// the child Image bubble up to this component via Unity's normal
    /// ExecuteEvents ancestor search, so no separate script is needed on
    /// the visible child.
    /// </summary>
    public sealed class SimulatedSpeechButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TextMeshProUGUI label;
        [SerializeField] private Image plate;
        [SerializeField] private UtteranceRecorder recorder;
        [SerializeField] private MicCalibration calibration;
        [SerializeField] private Color normalColor = Color.gray;
        [SerializeField] private Color hoverColor = Color.white;

        private enum Mode { Hidden, SkipMicCheck, SimulateSpeech }

        private Mode _mode = Mode.Hidden;
        private DialogueManager _dialogue;

        /// <summary>Self-registers with GameFlowDirector so InterrogationSceneBinder
        /// can Bind us to DialogueManager once Interrogation loads — mirrors
        /// RegisterCutscenePlayer's cross-scene pattern. Start, not Awake: Unity
        /// runs every Awake in the scene before any Start, guaranteeing
        /// GameFlowDirector.Instance is already set.</summary>
        private void Start() => GameFlowDirector.Instance?.RegisterSimulatedSpeechButton(this);

        /// <summary>Called once by InterrogationSceneBinder.Awake. Not unwound on
        /// disable — the reference must survive Interrogation's roots
        /// deactivating for M1/M2, and nothing here is a subscription that
        /// could leak.</summary>
        public void Bind(DialogueManager dialogue) => _dialogue = dialogue;
        public void Unbind() => _dialogue = null;

        private void Update()
        {
            Mode mode = ResolveMode();
            if (mode != _mode)
            {
                _mode = mode;
                bool shouldShow = mode != Mode.Hidden;
                if (root != null && root.activeSelf != shouldShow) root.SetActive(shouldShow);
                if (label != null && shouldShow)
                {
                    label.text = mode == Mode.SkipMicCheck ? "SKIP MIC CHECK   [F3]" : "SIMULATE SPEECH   [F3]";
                }
                if (plate != null) plate.color = normalColor;
            }

            if (_mode != Mode.Hidden && Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                Activate();
            }
        }

        /// <summary>Visible iff a press would actually be consumed right now —
        /// mirrors the real event consumers rather than inferring from VAD
        /// gating state, which has false-negative windows around phase
        /// transitions and memory scenes (see GAME_COMPLETION_PLAN.md's note
        /// on this fix).</summary>
        private Mode ResolveMode()
        {
            GameFlowDirector flow = GameFlowDirector.Instance;
            if (flow == null || !flow.OfflineMode) return Mode.Hidden;

            if (calibration != null &&
                (calibration.Stage == CalibrationStage.SamplingRoom || calibration.Stage == CalibrationStage.SamplingVoice))
            {
                return Mode.SkipMicCheck;
            }

            // Phase itself flips mid-transition, a full fade before PhaseChanged
            // fires (GameFlowDirector.TransitionRoutine) — IsTransitioning is the
            // only reliable "nothing is actually listening yet" signal here.
            if (flow.IsTransitioning) return Mode.Hidden;

            // Consumer 1 — GameFlowDirector.RequestSpokenPrompt (P1 "who are
            // you", M1's call-for-Nick yell gate).
            if (flow.AwaitingSpokenPrompt) return Mode.SimulateSpeech;

            // Consumer 2 — DialogueManager (P2/P3 conversational turns).
            // Mirrors OnUtteranceCaptured's own guard exactly, plus
            // isActiveAndEnabled: Interrogation's roots are deactivated for
            // M1/M2, which unsubscribes DialogueManager from UtteranceCaptured.
            if (_dialogue != null && _dialogue.isActiveAndEnabled && _dialogue.IsBound &&
                !_dialogue.IsSuspended && _dialogue.State == DialogueState.Listening)
            {
                return Mode.SimulateSpeech;
            }

            return Mode.Hidden;
        }

        private void Activate()
        {
            switch (_mode)
            {
                case Mode.SkipMicCheck:
                    calibration?.SimulateVoice();
                    break;
                case Mode.SimulateSpeech:
                    recorder?.SimulateUtterance();
                    break;
            }
        }

        public void OnPointerClick(PointerEventData eventData) => Activate();

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (plate != null) plate.color = hoverColor;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (plate != null) plate.color = normalColor;
        }
    }
}
