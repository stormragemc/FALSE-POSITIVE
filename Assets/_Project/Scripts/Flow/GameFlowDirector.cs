using System;
using System.Collections;
using FalsePositive.Audio;
using FalsePositive.Core;
using FalsePositive.Menu;
using FalsePositive.Net;
using FalsePositive.UI;
using FalsePositive.Voice;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FalsePositive.Flow
{
    /// <summary>
    /// Lives in _Persistent, never unloaded. The single owner of the phase
    /// order, the session id, and every persistent-scene service — see the
    /// frozen contract in docs/GAME_COMPLETION_PLAN.md A0.5. Person A owns
    /// this file.
    ///
    /// Interrogation is loaded once, the first time consent is accepted
    /// (see A3's MicConsentFlow), and from then on is only ever
    /// deactivated/reactivated by SceneRouter — never unloaded. That, plus
    /// SessionId being minted exactly once per playthrough (in
    /// StartNewPlaythrough) rather than by DialogueManager on every
    /// Awake, is what lets the officer remember what the witness said three
    /// phases ago and what lets the HuBERT affect baseline hold across the
    /// whole interrogation instead of resetting every time the player
    /// leaves for a memory scene.
    /// </summary>
    // Runs before every default-order MonoBehaviour so Instance exists by the
    // time anything reads it. InterrogationSceneBinder.Awake resolves
    // GameFlowDirector.Instance, and Unity does not guarantee Awake order
    // ACROSS scenes: in the shipped flow that is masked, because _Persistent is
    // build index 0 and SceneRouter loads Interrogation additively long after
    // this component is alive. In the editor, with Interrogation already open
    // alongside _Persistent, both Awakes fire in the same startup batch and the
    // order is arbitrary — which surfaced as a spurious "No GameFlowDirector in
    // the scene" error on roughly every other Play.
    [DefaultExecutionOrder(-100)]
    public sealed class GameFlowDirector : MonoBehaviour
    {
        private static readonly GamePhase[] PhaseOrder =
        {
            GamePhase.Menu, GamePhase.P1_Tutorial, GamePhase.M1_Night, GamePhase.P2_Recall,
            GamePhase.M2_Morning, GamePhase.P3_Verdict, GamePhase.P4_Ending, GamePhase.Outcome,
        };

        [Header("Config")]
        [SerializeField] private InterrogationConfig config;

        [Header("Persistent services")]
        [SerializeField] private MicrophoneService mic;
        [SerializeField] private VoiceActivityDetector vad;
        [SerializeField] private UtteranceRecorder recorder;
        [SerializeField] private InterrogationSidecarClient sidecar;
        [SerializeField] private LoudnessGate loudnessGate;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private SubtitleUI subtitles;
        [SerializeField] private SpeechPrompt prompt;
        [SerializeField] private ObjectiveHud objectives;
        [SerializeField] private SceneRouter sceneRouter;
        [SerializeField] private MemoryFlagCatalog memoryFlagCatalog;
        [SerializeField] private MicConsentFlow consentFlow;
        [SerializeField] private MicCalibration calibration;
        [SerializeField] private CalibrationPanelUI calibrationPanel;
        [SerializeField] private DebugOverlayUI debugOverlay;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private OutcomeScreen outcomeScreen;

        [Header("Scene names — must match Build Settings exactly")]
        [SerializeField] private string mainMenuSceneName = "MainMenu";
        [SerializeField] private string interrogationSceneName = "Interrogation";
        [SerializeField] private string nightSceneName = "Memory_CabinNight";
        [SerializeField] private string morningSceneName = "Memory_CabinMorning";

        public static GameFlowDirector Instance { get; private set; }

        public GamePhase Phase { get; private set; } = GamePhase.Boot;
        public MemoryFlags Flags { get; private set; }
        public SessionScore Score { get; } = new SessionScore();
        public string SessionId { get; private set; }
        public bool BackendReady { get; private set; }
        public bool OfflineMode { get; private set; }

        /// <summary>True from RequestSpokenPrompt until the qualifying utterance
        /// (or a phase change that abandons it) — see CancelSpokenPrompt. The
        /// simulate-speech test button uses this to know a press would actually
        /// be consumed here, rather than guessing from VAD gating state.</summary>
        public bool AwaitingSpokenPrompt { get; private set; }

        /// <summary>True for the whole fade-out/scene-swap/fade-in of
        /// TransitionRoutine. Phase itself flips partway through (:368-ish),
        /// a full fade before PhaseChanged fires, so callers that need "is a
        /// transition actually settled right now" must use this, not Phase.</summary>
        public bool IsTransitioning => _transitioning;
        public VoiceCalibrationState Calibration { get; } = new VoiceCalibrationState();
        public InterrogationConfig Config => config;

        public MicrophoneService Mic => mic;
        public VoiceActivityDetector Vad => vad;
        public UtteranceRecorder Recorder => recorder;
        public InterrogationSidecarClient Sidecar => sidecar;
        public LoudnessGate LoudnessGate => loudnessGate;
        public ScreenFader Fader => fader;
        public SubtitleUI Subtitles => subtitles;
        public SpeechPrompt Prompt => prompt;
        public ObjectiveHud Objectives => objectives;
        public MicConsentFlow ConsentFlow => consentFlow;
        public MicCalibration MicCalibration => calibration;
        public DebugOverlayUI DebugOverlay => debugOverlay;
        public SettingsPanel SettingsPanel => settingsPanel;
        public OutcomeScreen OutcomeScreen => outcomeScreen;

        /// <summary>Self-registered by SimulatedSpeechButton.Start, same shape as
        /// RegisterCutscenePlayer — the button lives in _Persistent but needs
        /// Interrogation's DialogueManager, so InterrogationSceneBinder binds it
        /// here once that scene loads.</summary>
        public SimulatedSpeechButton SimulatedSpeech { get; private set; }
        public void RegisterSimulatedSpeechButton(SimulatedSpeechButton button) => SimulatedSpeech = button;

        public event Action<GamePhase> PhaseExiting;
        public event Action<GamePhase> PhaseChanged;
        public event Action<string> BackendFault;
        public event Action<string> BackendStatusChanged;
        public event Action BackendReadyChanged;

        private ICutscenePlayer _cutscenePlayer;
        private bool _transitioning;
        private Action _cancelSpokenPrompt;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[GameFlowDirector] A second instance was loaded; destroying it. " +
                    "_Persistent must only ever be loaded once.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Flags = new MemoryFlags(memoryFlagCatalog);
        }

        private void Start()
        {
            // Consent -> calibration -> P1 handoff (docs/STORY_SCRIPT.md §4, S0).
            // Kicking off the Interrogation scene load in parallel with
            // calibration (rather than waiting for TransitionRoutine to do it
            // on AdvancePhase) hides the load hitch behind the calibration
            // card instead of showing it as a hang right after "Good."
            if (consentFlow != null)
            {
                consentFlow.Accepted += HandleConsentAccepted;
                consentFlow.Declined += HandleAbortRequested;
            }
            if (calibration != null)
            {
                calibration.Completed += HandleCalibrationCompleted;
            }
            if (calibrationPanel != null)
            {
                calibrationPanel.Cancelled += HandleAbortRequested;
            }

            // The menu itself does not need the backend to be reachable —
            // only the officer's first line does. A13 (Day 2) adds a proper
            // "interrogation service offline" fault card for that moment;
            // Day 1 accepts PostTurn failing with a connection error and
            // DialogueManager.TurnFailed firing, same as any other turn fault.
            if (Phase == GamePhase.Boot) GoToPhase(GamePhase.Menu);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (consentFlow != null)
            {
                consentFlow.Accepted -= HandleConsentAccepted;
                consentFlow.Declined -= HandleAbortRequested;
            }
            if (calibration != null) calibration.Completed -= HandleCalibrationCompleted;
            if (calibrationPanel != null) calibrationPanel.Cancelled -= HandleAbortRequested;
        }

        private void HandleConsentAccepted()
        {
            if (sceneRouter != null && !string.IsNullOrEmpty(interrogationSceneName))
            {
                StartCoroutine(sceneRouter.EnsureLoaded(interrogationSceneName));
            }
            StartCoroutine(ConsentToCalibrationHandoff());
        }

        /// <summary>Waits for the consent card's own fade-out to finish
        /// before showing the calibration card, rather than Show()ing it
        /// immediately — the two cards' backdrop scrims are independent
        /// CanvasGroups, so overlapping their fades stacks two translucent
        /// scrims into a visible flash instead of a clean crossfade.</summary>
        private IEnumerator ConsentToCalibrationHandoff()
        {
            consentFlow?.Hide();
            if (consentFlow != null) yield return new WaitForSecondsRealtime(consentFlow.FadeDuration);
            calibrationPanel?.Show();
            calibration?.Begin();
        }

        /// <summary>Shared by the consent card's Back button and the
        /// calibration failure card's Cancel button — both strand the
        /// player identically if left unhandled. Guarded because
        /// GoToPhase/TransitionRoutine always runs its full fade cycle even
        /// when the target phase already equals the current one: Declined
        /// fires with Phase already Menu on the normal consent-Back path, so
        /// an unconditional AbortToMenu() would fade to black and back for
        /// no reason on every Back click.</summary>
        private void HandleAbortRequested()
        {
            if (Phase != GamePhase.Menu) AbortToMenu();
        }

        private void HandleCalibrationCompleted(CalibrationResult result)
        {
            Calibration.Apply(result);
            StartCoroutine(FinishCalibrationHandoff());
        }

        private IEnumerator FinishCalibrationHandoff()
        {
            // Hold on "Good. The officer can hear you." for a beat before the
            // fade — CalibrationPanelUI already shows that copy on
            // CalibrationStage.Done.
            yield return new WaitForSeconds(1.2f);
            calibrationPanel?.Hide();
            AdvancePhase();
        }

        // --- Backend readiness, reported by BackendHealthProbe ---

        public void ReportBackendStatus(string text) => BackendStatusChanged?.Invoke(text);

        public void ReportBackendReady()
        {
            BackendReady = true;
            BackendReadyChanged?.Invoke();
        }

        public void ReportBackendFailed(string reason)
        {
            BackendReady = false;
            BackendFault?.Invoke(reason);
        }

        // --- Cutscenes ---

        /// <summary>Person B's CutsceneDirector self-registers here once it exists.
        /// Until it does, RequestCutscene degrades to a screen-fader blink — see
        /// docs/GAME_COMPLETION_PLAN.md, Day-1 exit criterion #12.</summary>
        public void RegisterCutscenePlayer(ICutscenePlayer player) => _cutscenePlayer = player;

        public void RequestCutscene(CutsceneId id, Action onFinished)
        {
            if (_cutscenePlayer == null)
            {
                StartCoroutine(BlinkThenFinish(onFinished));
                return;
            }

            void OnFinishedHandler(CutsceneId finishedId)
            {
                if (finishedId != id) return;
                _cutscenePlayer.Finished -= OnFinishedHandler;
                onFinished?.Invoke();
            }
            _cutscenePlayer.Finished += OnFinishedHandler;
            _cutscenePlayer.Play(id);
        }

        private IEnumerator BlinkThenFinish(Action onFinished)
        {
            if (fader != null)
            {
                yield return fader.FadeToBlack(0.15f);
                yield return fader.FadeFromBlack(0.15f);
            }
            onFinished?.Invoke();
        }

        // --- Scripted spoken prompts (P1's "who are you", M1's call-for-Nick) ---

        /// <summary>Shows a prompt and waits for a qualifying utterance, entirely
        /// client-side — never routed through DialogueManager/the backend. See
        /// A6's LoudnessGate for the requireLoud path and STORY_SCRIPT.md §4
        /// for both call sites.
        ///
        /// Phase-scoped: captures the requesting phase and only ever finishes
        /// while still in it. Without this, a phase abandoned mid-prompt (e.g.
        /// the dev F2 phase-skip) left its handler subscribed to the shared
        /// _Persistent recorder/gate, so it could fire from a *later* phase and
        /// run the old phase's onSatisfied — including its cutscene chain and a
        /// stray AdvancePhase() — out from under the player.</summary>
        public void RequestSpokenPrompt(string promptText, bool requireLoud, Action onSatisfied)
        {
            CancelSpokenPrompt(); // tear down anything a previous phase left pending
            prompt?.Show(promptText);
            // Under push-to-talk the prompt has to say how, not just what: the
            // player is being asked to speak and nothing else on screen tells
            // them a key is involved. The loud path overwrites this with its own
            // "Louder" hint on a failed attempt, which is the more useful
            // instruction at that point.
            if (config != null && config.pushToTalk)
            {
                prompt?.SetHint($"Hold [{config.pushToTalkKey}] to speak");
            }
            vad.SetGated(false);
            AwaitingSpokenPrompt = true;
            GamePhase requestedIn = Phase;

            if (requireLoud && loudnessGate != null)
            {
                Action handleSatisfied = null;
                Action handleTooQuiet = () =>
                {
                    if (Phase != requestedIn) return;
                    prompt?.SetHint("Louder — the storm is taking your voice.");
                };
                handleSatisfied = () =>
                {
                    if (Phase != requestedIn) return;
                    CancelSpokenPrompt();
                    FinishSpokenPrompt(onSatisfied);
                };
                _cancelSpokenPrompt = () =>
                {
                    loudnessGate.Satisfied -= handleSatisfied;
                    loudnessGate.TooQuiet -= handleTooQuiet;
                    loudnessGate.Disarm();
                };
                loudnessGate.Satisfied += handleSatisfied;
                loudnessGate.TooQuiet += handleTooQuiet;
                loudnessGate.Arm(Calibration.LoudReferenceRms, config != null ? config.yellFactor : 1.6f);
            }
            else
            {
                Action<float[], int> handleUtterance = null;
                handleUtterance = (samples, sampleRate) =>
                {
                    if (Phase != requestedIn) return;
                    CancelSpokenPrompt();
                    FinishSpokenPrompt(onSatisfied);
                };
                _cancelSpokenPrompt = () => recorder.UtteranceCaptured -= handleUtterance;
                recorder.UtteranceCaptured += handleUtterance;
            }
        }

        /// <summary>Unsubscribes whatever RequestSpokenPrompt last registered and
        /// clears the prompt UI. Safe to call when nothing is pending. Called from
        /// TransitionRoutine on every phase exit, so a prompt can never outlive
        /// the phase that asked for it.</summary>
        public void CancelSpokenPrompt()
        {
            AwaitingSpokenPrompt = false;
            Action cancel = _cancelSpokenPrompt;
            if (cancel == null) return;
            _cancelSpokenPrompt = null;
            cancel.Invoke();
            prompt?.ClearHint();
            prompt?.Hide();
        }

        private void FinishSpokenPrompt(Action onSatisfied)
        {
            prompt?.ClearHint();
            prompt?.Hide();
            vad.SetGated(true);
            onSatisfied?.Invoke();
        }

        // --- Playthrough / phase lifecycle ---

        /// <summary>Mints a fresh SessionId, clears flags/score/calibration, and asks
        /// the backend to drop any stale history for that id. Called once, from
        /// the menu's Play/Offline demo button (A2), before the consent card ever
        /// shows. `offline` is the Offline demo path — PhaseDialogueController
        /// reads OfflineMode to swap P2/P3 onto the scripted officer instead of
        /// the sidecar; when true this also skips the (doomed) /session/reset
        /// call, since there is no backend to ask.</summary>
        public void StartNewPlaythrough(bool offline = false)
        {
            SessionId = Guid.NewGuid().ToString("N");
            OfflineMode = offline;
            Flags.Clear();
            Score.Reset();

            if (sidecar != null && !offline)
            {
                sidecar.ResetSession(SessionId, ok =>
                {
                    if (!ok)
                    {
                        Debug.LogWarning("[GameFlowDirector] /session/reset failed for the new session id " +
                            "(harmless — the id is new server-side too, so there is nothing to clear).");
                    }
                });
            }
        }

        public void AdvancePhase()
        {
            int index = Array.IndexOf(PhaseOrder, Phase);
            GamePhase next = index < 0
                ? PhaseOrder[0]
                : PhaseOrder[Mathf.Min(index + 1, PhaseOrder.Length - 1)];
            GoToPhase(next);
        }

        public void GoToPhase(GamePhase phase)
        {
            if (_transitioning)
            {
                Debug.LogWarning($"[GameFlowDirector] Ignoring GoToPhase({phase}) — a transition is already in flight.");
                return;
            }
            StartCoroutine(TransitionRoutine(phase));
        }

        public void AbortToMenu() => GoToPhase(GamePhase.Menu);

        /// <summary>Steps out of the interrogation into a memory scene, plays one
        /// cutscene there, and steps back — <b>without</b> a phase change, so
        /// SessionId, the backend's conversation history and ProsodyTracker's
        /// affect baseline all survive (see SessionId for why that matters).
        /// This is P3's memory pair, docs/STORY_SCRIPT.md §4 P3_VERDICT / §5
        /// CS-16A and CS-16B.
        ///
        /// Driven from here rather than from PhaseDialogueController because
        /// SceneRouter.Activate deactivates the non-active scene's roots and
        /// PhaseDialogueController lives in Interrogation — it would disable
        /// itself, and its own coroutine would stop, halfway through. This
        /// component lives in _Persistent and survives the swap.
        ///
        /// No fade: §4 asks for a hard cut in and a hard cut back. The scene is
        /// loaded additively first (invisible — additive loading does not touch
        /// the scene on screen) so that Activate is an instant swap rather than
        /// a hitch. By P3 the memory scene is normally already loaded from M1,
        /// which makes EnsureLoaded a no-op.</summary>
        public void RequestMemoryInterlude(GamePhase memoryPhase, CutsceneId id, Action onComplete)
        {
            StartCoroutine(MemoryInterludeRoutine(memoryPhase, id, onComplete));
        }

        private IEnumerator MemoryInterludeRoutine(GamePhase memoryPhase, CutsceneId id, Action onComplete)
        {
            string memoryScene = SceneNameFor(memoryPhase);

            if (sceneRouter != null && !string.IsNullOrEmpty(memoryScene))
            {
                // Load before cutting, so the cut itself costs nothing.
                yield return sceneRouter.EnsureLoaded(memoryScene);
                yield return sceneRouter.Activate(memoryScene);
            }

            bool finished = false;
            RequestCutscene(id, () => finished = true);
            while (!finished) yield return null;

            if (sceneRouter != null && !string.IsNullOrEmpty(interrogationSceneName))
            {
                yield return sceneRouter.Activate(interrogationSceneName);
            }

            // Invoked only after Interrogation's roots are live again, so a
            // caller that lives in that scene (PhaseDialogueController) is
            // enabled and able to start coroutines by the time it runs.
            onComplete?.Invoke();
        }


        private IEnumerator TransitionRoutine(GamePhase next)
        {
            _transitioning = true;
            PhaseExiting?.Invoke(Phase);
            CancelSpokenPrompt(); // a phase can never leave a spoken prompt pending for the next one

            float fadeDuration = config != null ? config.fadeDurationSeconds : 0.25f;
            if (fader != null) yield return fader.FadeToBlack(fadeDuration);
            yield return null; // one full frame fully black before swapping anything

            // Memory objectives are per-phase: M1NightController and
            // M2MorningController set them, and nothing ever cleared them, so
            // the last one ("Go to the door.", "Bring him to the sofa.") stayed
            // burned on screen through the interrogation that follows. Cleared
            // here rather than on PhaseExiting so it happens under the black
            // frame and never pops, and before PhaseChanged so a handler that
            // sets the next phase's objective still wins.
            objectives?.Clear();

            string targetScene = SceneNameFor(next);
            if (sceneRouter != null && !string.IsNullOrEmpty(targetScene))
            {
                yield return sceneRouter.EnsureLoaded(targetScene);
                yield return sceneRouter.Activate(targetScene);
            }

            Phase = next;

            // Fired after FadeFromBlack completes, not before — a phase
            // handler (e.g. M2MorningController) commonly reacts by
            // requesting a cutscene, which starts its own
            // CutsceneDirector.PlayRoutine fade on the same ScreenFader.
            // Firing PhaseChanged first used to let that second fade start
            // concurrently with this routine's own FadeFromBlack; ScreenFader
            // now tolerates the race via its generation counter, but there's
            // no reason to invite it — waiting here means the screen is
            // fully lit and settled before anything downstream can touch the
            // fader again.
            if (fader != null) yield return fader.FadeFromBlack(fadeDuration);
            PhaseChanged?.Invoke(next);
            _transitioning = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>Verification aid (docs/GAME_COMPLETION_PLAN.md §9): F2 walks
        /// the whole phase order with no natural completion trigger required —
        /// confirms the game reaches Outcome from Menu with zero cutscenes
        /// registered, and that SessionId never changes along the way.</summary>
        private void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                Debug.Log($"[GameFlowDirector] Debug AdvancePhase from {Phase} (SessionId={SessionId}).");
                AdvancePhase();
            }
        }
#endif

        private string SceneNameFor(GamePhase phase)
        {
            switch (phase)
            {
                case GamePhase.Menu: return mainMenuSceneName;
                case GamePhase.P1_Tutorial:
                case GamePhase.P2_Recall:
                case GamePhase.P3_Verdict:
                case GamePhase.P4_Ending:
                case GamePhase.Outcome:
                    return interrogationSceneName;
                case GamePhase.M1_Night: return nightSceneName;
                case GamePhase.M2_Morning: return morningSceneName;
                default: return null;
            }
        }
    }
}
