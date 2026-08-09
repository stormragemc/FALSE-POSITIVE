using System;
using System.Collections;
using System.Collections.Generic;
using FalsePositive.Flow;
using FalsePositive.Net;
using FalsePositive.UI;
using UnityEngine;

namespace FalsePositive.Dialogue
{
    /// <summary>
    /// Sits above DialogueManager and owns everything phase-shaped: which
    /// system prompt is active, the memory-flag briefing (A7b), turn caps,
    /// story-mark tracking (A8), and the P4 ending selection (A10) and
    /// outcome card (A11). Lives in Interrogation.unity alongside
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
    /// P3_Verdict reads the accusation through SuspectNameDetector (A9) and
    /// P4 picks the ending through EndingSelector (A10), which applies
    /// docs/STORY_SCRIPT.md §8 in full: credibility, caught unsupported
    /// details, and — for Aaron — whether the witness cited the reasoning.
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

        [Header("P3 memory sequence pacing")]
        /// <summary>Held after the officer's reply finishes, before the folder
        /// opens and the photograph beat starts.</summary>
        [SerializeField, Range(0f, 4f)] private float p3BeforePhotographSeconds = 1.0f;

        /// <summary>Held after "Who, David?" before the microphone reopens and
        /// the live turn is requested.</summary>
        [SerializeField, Range(0f, 4f)] private float p3AfterMemoriesSeconds = 1.2f;

        /// <summary>Ceiling on waiting for the officer to stop speaking, so a
        /// stuck AudioSource cannot strand the sequence and leave P3 unable to
        /// reach its verdict. Generous: real replies run well under this.
        /// </summary>
        [SerializeField, Range(1f, 60f)] private float p3MaxWaitForOfficerSeconds = 30f;

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

        /// <summary>Which of §8's seven supporting clues the witness has raised.
        /// Accumulated across the whole session, not just P3 — see
        /// ClueCitationDetector's scope note.</summary>
        private readonly HashSet<CitedClue> _citedClues = new HashSet<CitedClue>();

        /// <summary>Lines the outcome card will quote back, in the order they
        /// were said. Populated from the turns that carried an unsupported
        /// detail, and from the turn that named someone.</summary>
        private readonly List<QuotedLine> _quotableLines = new List<QuotedLine>();
        private bool _p3MemoriesPlayed;
        // True between EnterLiveDialoguePhase and FinishLiveDialoguePhase. This
        // component can be disabled and re-enabled DURING a phase:
        // RequestMemoryInterlude activates a memory scene, SceneRouter deactivates
        // Interrogation's roots, and this component goes with them. OnDisable
        // dropped the TurnCompleted subscription and OnEnable only restored
        // PhaseChanged — so after a mid-phase interlude the controller stopped
        // counting turns. P3 never reached its cap, the ending was unreachable,
        // and the accusation was never read.
        private bool _liveDialogueActive;

        private DialogueManager Dialogue => binder != null ? binder.Dialogue : null;

        private void Awake()
        {
            Marks = new StoryMarkTracker(storyMarksSource);
        }

        private void OnEnable()
        {
            _flow = GameFlowDirector.Instance;
            if (_flow != null) _flow.PhaseChanged += OnPhaseChanged;

            // Re-entering mid-phase after a memory interlude: no PhaseChanged
            // fires, so nothing else would put the turn subscription back.
            if (_liveDialogueActive) SubscribeDialogue();
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
                    // Session start. Citations span P2 and P3, so they are
                    // cleared here rather than on entry to the verdict.
                    _citedClues.Clear();
                    _quotableLines.Clear();
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
                    EnterP4();
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
            _liveDialogueActive = true;

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
            SubscribeDialogue();
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

            // §7. Deliberately ABOVE the P3 memory-sequence early return below:
            // the turn that return skips is the witness's answer to "Tell me why
            // I should spare your life", which is the most fabrication-dense
            // utterance in the game and must not go unscored. Null is normal —
            // JsonUtility leaves an absent array null, and DialogueManager's
            // offline turns build a response by hand with no fabrications at all.
            if (response.fabrications != null)
            {
                foreach (string trapId in response.fabrications)
                {
                    // RecordFabrication is false for a trap already counted, so
                    // this quotes each unsupported detail once rather than once
                    // per time the officer circled back to it.
                    if (_flow.Score.RecordFabrication(trapId)
                        && !string.IsNullOrWhiteSpace(response.transcript))
                    {
                        _quotableLines.Add(new QuotedLine(TurnsThisSession, response.transcript));
                    }
                }
            }

            // Only P2 covers the seven marks; P3 runs against a tracker that
            // EnterLiveDialoguePhase has already Reset(), so reading it there
            // would score a full P2 as zero coverage.
            if (_currentPhase == GamePhase.P2_Recall)
            {
                _flow.Score.ObserveMarkCoverage(Marks.CoveredCount);
            }
            _flow.Score.UpdateCredibility(StoryMarkTracker.TotalMarks);

            // Reasoning counts wherever it was offered; §8 only asks that the
            // witness said why at some point, not that he said it twice.
            ClueCitationDetector.Observe(response.transcript, _citedClues);

            if (_currentPhase == GamePhase.P3_Verdict && _namedSuspect == Suspect.None)
            {
                _namedSuspect = SuspectNameDetector.Detect(response.transcript);
                if (_namedSuspect != Suspect.None)
                {
                    // Support is the share of §8's seven clues raised, so the
                    // score carries *how well* the accusation was argued and not
                    // only who was named.
                    _flow.Score.SetAccusation(_namedSuspect, _citedClues.Count / 7f);
                    _quotableLines.Add(new QuotedLine(TurnsThisSession, response.transcript));
                }
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
                // Once, not standing. As a standing briefing the server re-applied
                // this every turn, so the officer asked "What happened to Nick?"
                // forever and P2 could never hand over to the verdict.
                Dialogue.QueueSceneInstructionOnce(
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
            _liveDialogueActive = false;
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
            StartCoroutine(P3MemorySequenceRoutine());
        }

        /// <summary>Waits for the officer's live reply to actually finish, then
        /// runs the §4 photograph-and-memories chain.
        ///
        /// The wait is the whole point. DialogueManager raises TurnCompleted the
        /// moment it calls copVoice.Play — playback has *started*, not finished
        /// — so kicking the photograph beat off from that event cut Spassky off
        /// mid-sentence every time the sequence fired. Suspend() only gates the
        /// microphone and leaves his audio running, so the reply plays out and
        /// OnCopFinishedSpeaking drops the state to Idle, which is the signal
        /// waited on here.
        ///
        /// Only the lead-in can be a coroutine on this component: the interludes
        /// deactivate Interrogation's roots, which would kill anything still
        /// running here. Everything past the first cutscene therefore stays a
        /// callback chain, and its pacing lives in GameFlowDirector.</summary>
        private IEnumerator P3MemorySequenceRoutine()
        {
            float waited = 0f;
            while (Dialogue != null && Dialogue.State == DialogueState.Speaking
                   && waited < p3MaxWaitForOfficerSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            // A held beat after the answer, before the folder opens.
            if (p3BeforePhotographSeconds > 0f)
            {
                yield return new WaitForSeconds(p3BeforePhotographSeconds);
            }

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
                                StartCoroutine(ResumeAfterMemoriesRoutine());
                            })))));
        }

        /// <summary>Lets "Who, David?" land before the room is live again.
        ///
        /// Resume() is deliberately after the wait: it re-opens the microphone,
        /// and doing that while the closing line is still sounding invites the
        /// player to answer over it — the same overlap this whole change is
        /// removing, just at the other end of the sequence.</summary>
        private IEnumerator ResumeAfterMemoriesRoutine()
        {
            if (p3AfterMemoriesSeconds > 0f)
            {
                yield return new WaitForSeconds(p3AfterMemoriesSeconds);
            }

            // Mic back up. Resume the same phase rather than re-entering it:
            // re-entering would reset the turn counter and the story marks.
            Dialogue?.Resume();
            Dialogue?.RequestOfficerTurn(null);
        }

        /// <summary>A10 — docs/STORY_SCRIPT.md §8's full ending rule, replacing
        /// the Day-1 stopgap that picked on the name alone.</summary>
        private void EnterP4()
        {
            Dialogue?.Suspend();

            LastEnding = EndingSelector.Select(_flow.Score, _citedClues);
            Debug.Log($"[Ending] {LastEnding.Cutscene} — {LastEnding.Reason} " +
                $"(credibility {_flow.Score.Credibility:0.00}, " +
                $"{_flow.Score.CaughtFabrications} unsupported, " +
                $"{LastEnding.CitedClues} clues)");

            _flow.RequestCutscene(LastEnding.Cutscene, () => _flow.AdvancePhase());
        }

        /// <summary>The decision EnterP4 reached, for the outcome card to
        /// explain itself with. Default is E_DAVID so a session that somehow
        /// reaches Outcome without P4 still reads coherently.</summary>
        public EndingDecision LastEnding { get; private set; } =
            new EndingDecision(CutsceneId.EndingDavid, Suspect.None, 0, "session did not reach a verdict");

        private void EnterOutcome()
        {
            // A11 — the fixed card plus the witness's own lines with turn
            // numbers, per docs/STORY_SCRIPT.md §4 P4_ENDING. The composer is
            // where the "It never says they lied" rule (G6) is enforced.
            _flow.OutcomeScreen?.Show(OutcomeCardComposer.Compose(_quotableLines));
        }

        private void SubscribeDialogue()
        {
            DialogueManager dialogue = Dialogue;
            if (dialogue == null) return;
            // -= before += so a re-entrant call cannot register twice and
            // double-count every turn.
            dialogue.TurnCompleted -= OnTurnCompleted;
            dialogue.TurnCompleted += OnTurnCompleted;
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

            // §7/§8, dev-only. Labelled "unsupported" rather than anything about
            // truth or lying — G6 applies to every visible string, including
            // this one.
            if (_flow.Score != null)
            {
                sb.Append('\n')
                    .Append("unsupported ").Append(_flow.Score.CaughtFabrications)
                    .Append('/').Append(TrapIds.All.Length)
                    .Append("  credibility ").Append(_flow.Score.Credibility.ToString("0.00"));
            }
            debugOverlay.SetMarksStatus(sb.ToString());
        }
    }
}
