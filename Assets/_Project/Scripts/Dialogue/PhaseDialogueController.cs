using System;
using System.Collections;
using FalsePositive.Flow;
using FalsePositive.Net;
using FalsePositive.UI;
using UnityEngine;

namespace FalsePositive.Dialogue
{
    /// <summary>
    /// Sits above DialogueManager and owns everything phase-shaped: which
    /// system prompt is active, the memory-flag briefing (A7b), turn caps,
    /// story-mark tracking (A8), and — for Day 1 — the placeholder P4
    /// hand-off to Outcome. Lives in Interrogation.unity alongside
    /// DialogueManager; reacts to GameFlowDirector.PhaseChanged rather than
    /// being called directly, so it works whether Interrogation is being
    /// activated for the first time or the fourth.
    ///
    /// P1_Tutorial never touches the backend at all — see
    /// docs/STORY_SCRIPT.md §4: the "who are you" prompt is a local,
    /// requireLoud:false spoken-prompt gate, and Spassky's answer is
    /// pre-rendered VO played through GameFlowDirector.RequestCutscene.
    /// The first real turn is P2_Recall's opening line.
    ///
    /// P3_Verdict's suspect-naming and P4's real ending selection are A9/A10
    /// (Day 2) — until those land, P3 ends purely on its turn cap and P4 is
    /// a single placeholder cutscene straight through to Outcome, which is
    /// deliberate: it is what keeps the whole game walkable end to end on
    /// Day 1 (docs/GAME_COMPLETION_PLAN.md, exit criterion #12).
    /// </summary>
    public sealed class PhaseDialogueController : MonoBehaviour
    {
        [SerializeField] private PhasePromptSet prompts;
        [SerializeField] private TextAsset storyMarksSource;
        [SerializeField] private InterrogationSceneBinder binder;
        [SerializeField] private int p1NoSpeechNudgeSeconds = 15;
        [SerializeField] private int p2TurnCap = 14;
        [SerializeField] private int p3TurnCap = 8;
        [SerializeField] private int sessionTurnCap = 30;

        public int TurnsThisPhase { get; private set; }
        public int TurnsThisSession { get; private set; }
        public bool PhaseComplete { get; private set; }
        public StoryMarkTracker Marks { get; private set; }

        public event Action<GamePhase> PhaseDialogueFinished;

        private GameFlowDirector _flow;
        private GamePhase _currentPhase;
        private int _currentTurnCap;
        private bool _awaitingClosingAnswer;
        private bool _hasSeatedOnce;
        private Coroutine _noSpeechNudgeRoutine;

        private DialogueManager Dialogue => binder != null ? binder.Dialogue : null;

        private void Awake()
        {
            Marks = new StoryMarkTracker(storyMarksSource);
        }

        private void OnEnable()
        {
            _flow = GameFlowDirector.Instance;
            if (_flow != null) _flow.PhaseChanged += OnPhaseChanged;
        }

        private void OnDisable()
        {
            if (_flow != null) _flow.PhaseChanged -= OnPhaseChanged;
            ExitPhase();
        }

        private void OnPhaseChanged(GamePhase phase) => EnterPhase(phase);

        public void EnterPhase(GamePhase phase)
        {
            ExitPhase();
            TurnsThisPhase = 0;
            PhaseComplete = false;
            _awaitingClosingAnswer = false;
            _currentPhase = phase;

            switch (phase)
            {
                case GamePhase.P1_Tutorial:
                    EnterP1();
                    break;
                case GamePhase.M1_Night:
                case GamePhase.M2_Morning:
                    // Track B's scene owns this phase's gameplay and calls
                    // GameFlowDirector.AdvancePhase() itself when it's done.
                    // Dialogue stays suspended for the whole phase.
                    Dialogue?.Suspend();
                    break;
                case GamePhase.P2_Recall:
                    EnterLiveDialoguePhase(prompts != null ? prompts.TextFor(phase) : string.Empty, p2TurnCap);
                    break;
                case GamePhase.P3_Verdict:
                    EnterLiveDialoguePhase(prompts != null ? prompts.TextFor(phase) : string.Empty, p3TurnCap);
                    break;
                case GamePhase.P4_Ending:
                    EnterP4Placeholder();
                    break;
            }
        }

        public void ExitPhase()
        {
            UnsubscribeDialogue();
            if (_noSpeechNudgeRoutine != null)
            {
                StopCoroutine(_noSpeechNudgeRoutine);
                _noSpeechNudgeRoutine = null;
            }
        }

        private void EnterP1()
        {
            if (!_hasSeatedOnce && binder != null && binder.PlayerState != null)
            {
                binder.PlayerState.BeginSeated();
                _hasSeatedOnce = true;
            }

            Dialogue?.Suspend();
            _flow.RequestSpokenPrompt("Who are you? Where am I?", requireLoud: false, onSatisfied: () =>
            {
                _flow.RequestCutscene(CutsceneId.SpasskyAnswer, () =>
                    _flow.RequestCutscene(CutsceneId.FuzzyToNight, () => _flow.AdvancePhase()));
            });
            _noSpeechNudgeRoutine = StartCoroutine(NoSpeechNudge());
        }

        private IEnumerator NoSpeechNudge()
        {
            yield return new WaitForSeconds(p1NoSpeechNudgeSeconds);
            _flow.Prompt?.Pulse();
        }

        private void EnterLiveDialoguePhase(string phasePrompt, int turnCap)
        {
            Marks.Reset();
            _currentTurnCap = turnCap;

            DialogueManager dialogue = Dialogue;
            if (dialogue == null || _flow == null)
            {
                Debug.LogError("[PhaseDialogueController] No bound DialogueManager/GameFlowDirector — " +
                    "cannot start a live dialogue phase.");
                return;
            }

            dialogue.Resume();
            dialogue.TurnCompleted += OnTurnCompleted;
            dialogue.QueueSceneInstruction(BuildSceneInstruction(phasePrompt));
            dialogue.RequestOfficerTurn(null);

            UpdateDebugReadout();
        }

        /// <summary>A7b: folds MemoryFlags.Describe() into the same scene
        /// instruction as the phase prompt — without this, the story marks,
        /// traps, and clue ledger in docs/STORY_SCRIPT.md are inert.</summary>
        private string BuildSceneInstruction(string phasePrompt)
        {
            string briefing = _flow.Flags != null ? _flow.Flags.Describe() : string.Empty;
            if (string.IsNullOrEmpty(briefing)) return phasePrompt;

            string header = "WITNESS KNOWLEDGE — what this witness did and did not observe " +
                "in the memory sequences:";
            return $"{phasePrompt}\n\n{header}\n{briefing}";
        }

        private void OnTurnCompleted(SidecarTurnResponse response)
        {
            TurnsThisPhase++;
            TurnsThisSession++;
            Marks.Observe(response.transcript);

            _flow.Score.RecordTurn(new TurnRecord(
                TurnsThisSession,
                _currentPhase,
                response.transcript,
                response.reply_text,
                response.prosody != null ? response.prosody.tension : 0f,
                response.prosody != null ? response.prosody.arousal : 0f,
                response.prosody != null ? response.prosody.confidence_in_signal : 0f,
                response.prosody != null && response.prosody.reliable));

            UpdateDebugReadout();

            bool capReached = TurnsThisPhase >= _currentTurnCap || TurnsThisSession >= sessionTurnCap;

            if (_currentPhase == GamePhase.P2_Recall && !_awaitingClosingAnswer && (Marks.AllCovered || capReached))
            {
                _awaitingClosingAnswer = true;
                Dialogue.QueueSceneInstruction(
                    "The witness has covered enough ground for this phase. Ask exactly: " +
                    "\"What happened to Nick?\" as your next question, then wait for their answer.");
                return; // one more turn — the closing question and its answer — before advancing
            }

            if (_awaitingClosingAnswer || (_currentPhase == GamePhase.P3_Verdict && capReached))
            {
                FinishLiveDialoguePhase();
            }
        }

        private void FinishLiveDialoguePhase()
        {
            UnsubscribeDialogue();
            PhaseComplete = true;
            GamePhase finishedPhase = _currentPhase;
            PhaseDialogueFinished?.Invoke(finishedPhase);
            _flow.AdvancePhase();
        }

        private void EnterP4Placeholder()
        {
            // A10 (Day 2) replaces this with real ending selection
            // (docs/STORY_SCRIPT.md §8). Never claim otherwise in the deck
            // while this placeholder is active — GAME_COMPLETION_PLAN.md
            // §10's "never fake" rule.
            Dialogue?.Suspend();
            _flow.RequestCutscene(CutsceneId.EndingDavid, () => _flow.AdvancePhase());
        }

        private void UnsubscribeDialogue()
        {
            DialogueManager dialogue = Dialogue;
            if (dialogue != null) dialogue.TurnCompleted -= OnTurnCompleted;
        }

        private void UpdateDebugReadout()
        {
            DebugOverlayUI debugOverlay = _flow != null ? _flow.DebugOverlay : null;
            if (debugOverlay == null || Marks == null) return;

            var sb = new System.Text.StringBuilder();
            sb.Append(_currentPhase).Append(" turn ").Append(TurnsThisPhase).Append('/').Append(_currentTurnCap)
                .Append(" (session ").Append(TurnsThisSession).Append(')').Append('\n');
            foreach (StoryMarkId id in (StoryMarkId[])Enum.GetValues(typeof(StoryMarkId)))
            {
                sb.Append(Marks.IsCovered(id) ? "[x] " : "[ ] ").Append(id).Append("  ");
            }
            debugOverlay.SetMarksStatus(sb.ToString());
        }
    }
}
