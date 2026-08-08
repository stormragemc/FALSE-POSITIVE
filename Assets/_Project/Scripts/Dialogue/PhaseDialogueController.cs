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
        [SerializeField] private OfflineDialogueScript offlineScript;
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
        private Suspect _namedSuspect = Suspect.None;
        private bool _p3MemoriesPlayed;

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
                    _namedSuspect = Suspect.None;
                    EnterP3();
                    break;
                case GamePhase.P4_Ending:
                    EnterP4Placeholder();
                    break;
                case GamePhase.Outcome:
                    EnterOutcome();
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
                // The prompt is already answered and hidden at this point —
                // stop the nudge here rather than waiting for ExitPhase(),
                // so it can never fire mid-cutscene against a hidden prompt.
                if (_noSpeechNudgeRoutine != null)
                {
                    StopCoroutine(_noSpeechNudgeRoutine);
                    _noSpeechNudgeRoutine = null;
                }
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

            dialogue.OfflineMode = _flow.OfflineMode;
            if (_flow.OfflineMode)
            {
                OfflineOfficerLine[] lines = offlineScript != null ? offlineScript.LinesFor(_currentPhase) : null;
                dialogue.BeginOfflinePhase(lines);
                if (lines != null && lines.Length >= 2)
                {
                    // The opening RequestOfficerTurn call plays line[0] and
                    // itself fires TurnCompleted (same as a live opening
                    // turn), so TurnsThisPhase reaches N right after line[N-1]
                    // plays. P2's cap must land one line before the closing
                    // question so OnTurnCompleted's _awaitingClosingAnswer
                    // hand-off (see below) lets exactly one more line — the
                    // closing question, the script's last line — play before
                    // finishing: cap = Length - 1. P3 has no such hand-off
                    // and finishes the instant capReached is true, so its
                    // cap must equal the full length so that line plays
                    // first: cap = Length. Verified against the live turn
                    // counter, not just derived on paper — see the offline
                    // playthrough check in the plan.
                    _currentTurnCap = _currentPhase == GamePhase.P2_Recall
                        ? lines.Length - 1
                        : lines.Length;
                }
                else if (offlineScript == null)
                {
                    Debug.LogError("[PhaseDialogueController] OfflineMode is on but no OfflineDialogueScript " +
                        "is assigned — the officer will have nothing to say.");
                }
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

            if (_currentPhase == GamePhase.P3_Verdict && _namedSuspect == Suspect.None)
            {
                _namedSuspect = DetectNamedSuspect(response.transcript);
            }

            // One completed turn = the witness has defended themselves, which is
            // the cue for the photograph (§4). Guarded so it fires exactly once.
            if (_currentPhase == GamePhase.P3_Verdict && !_p3MemoriesPlayed && TurnsThisPhase >= 2)
            {
                RunP3MemorySequence();
                return;
            }

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

        /// <summary>P3 opens LIVE, not with the memories (docs/STORY_SCRIPT.md
        /// §4 P3_VERDICT): the officer asks why he should spare the witness and
        /// the witness defends themselves first. Only after that does he slide
        /// the photograph across and the memory pair run. An earlier version
        /// played both memories at the top of the phase, before the player had
        /// said anything, which inverted the scene — the memories are his answer
        /// to a question that has already been put to him.</summary>
        private void EnterP3()
        {
            _p3MemoriesPlayed = false;
            EnterLiveDialoguePhase(
                prompts != null ? prompts.TextFor(GamePhase.P3_Verdict) : string.Empty,
                p3TurnCap);
        }

        /// <summary>The photograph beat and the memory pair, fired once the
        /// witness has answered the opening question. The mic is down for the
        /// whole sequence — §4 has Spassky ask who killed Nick twice before it
        /// reopens, and the memories are the answer David cannot give him.
        ///
        /// Both interludes run on GameFlowDirector rather than here: they swap
        /// the active scene, which deactivates this component, so a coroutine
        /// started here would not survive to see them finish.</summary>
        private void RunP3MemorySequence()
        {
            _p3MemoriesPlayed = true;
            Dialogue?.Suspend();

            // §4: Spassky slides a printed group photograph across the table.
            // Laid on the desk by raycast between the two seats — the
            // interrogation Table is a bare Transform with no renderer, so its
            // surface height cannot be read off the object, and a guessed height
            // is exactly how a prop ends up floating.
            Vector3 seat = binder != null && binder.PlayerState != null
                ? binder.PlayerState.transform.position
                : new Vector3(0f, 0f, -0.8f);
            GameObject groupPhoto = FalsePositive.Cutscene.PhotoProps.LayOnSurface(
                "photo_group_that_night",
                above: new Vector3(0f, 0.9f, 0.1f),
                readFrom: seat,
                width: 0.18f, height: 0.135f);

            _flow.RequestCutscene(CutsceneId.P3Photograph, () =>
                _flow.RequestMemoryInterlude(GamePhase.M1_Night, CutsceneId.GoodYears, () =>
                    _flow.RequestCutscene(CutsceneId.P3AfterGoodYears, () =>
                        _flow.RequestMemoryInterlude(GamePhase.M1_Night, CutsceneId.WhenItWentWrong, () =>
                            _flow.RequestCutscene(CutsceneId.P3WhoDavid, () =>
                            {
                                // Mic back up. Resume the same phase rather than
                                // re-entering it: re-entering would reset the
                                // turn counter and the story marks.
                                // "He pulls the photograph back into the folder"
                                // — it goes before the name is asked for.
                                FalsePositive.Cutscene.PhotoProps.Discard(groupPhoto);
                                Dialogue?.Resume();
                                Dialogue?.RequestOfficerTurn(null);
                            })))));
        }

        private void EnterP4Placeholder()
        {
            // Day-1 stopgap ending pick, per docs/STORY_SCRIPT.md §8's rule
            // that naming nobody (or nobody unambiguously) falls to E_DAVID.
            // This is deliberately cruder than the real rule — it skips the
            // credibility/fabrication-count/cited-clues conditions entirely
            // and picks on the name alone. A10 (Day 2) replaces it with the
            // full SessionScore-driven EndingSelector. Never claim the full
            // rule is live while this is — GAME_COMPLETION_PLAN.md §10's
            // "never fake" rule.
            Dialogue?.Suspend();
            CutsceneId ending = _namedSuspect switch
            {
                Suspect.Aaron => CutsceneId.EndingAaron,
                Suspect.Ivy => CutsceneId.EndingIvy,
                Suspect.Priya => CutsceneId.EndingPriya,
                _ => CutsceneId.EndingDavid,
            };
            _flow.RequestCutscene(ending, () => _flow.AdvancePhase());
        }

        private void EnterOutcome()
        {
            const string card = "14:20 — Ivy has asked to make a second statement.\n" +
                "She is still waiting.\n\nFALSE POSITIVE";
            // A11 (Day 2) replaces this fixed card with 2-3 verbatim player
            // lines quoted back with turn numbers, per docs/STORY_SCRIPT.md
            // §4 P4_ENDING — "It never says they lied" (G6) applies to that
            // version too.
            _flow.OutcomeScreen?.Show(card);
        }

        /// <summary>Day-1 client-side stopgap for A9's SuspectNameDetector —
        /// a single unambiguous name from {Aaron, Ivy, Priya} in one turn's
        /// transcript. Deliberately crude: no possessive handling, no
        /// mid-sentence disambiguation, and "maybe Aaron or Ivy" is rejected
        /// only because both names appear in the same transcript, not because
        /// "maybe" was understood as hedging.</summary>
        private static Suspect DetectNamedSuspect(string transcript)
        {
            if (string.IsNullOrEmpty(transcript)) return Suspect.None;
            string lower = transcript.ToLowerInvariant();
            bool aaron = lower.Contains("aaron");
            bool ivy = lower.Contains("ivy");
            bool priya = lower.Contains("priya");
            int count = (aaron ? 1 : 0) + (ivy ? 1 : 0) + (priya ? 1 : 0);
            if (count != 1) return Suspect.None;
            if (aaron) return Suspect.Aaron;
            if (ivy) return Suspect.Ivy;
            return Suspect.Priya;
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
