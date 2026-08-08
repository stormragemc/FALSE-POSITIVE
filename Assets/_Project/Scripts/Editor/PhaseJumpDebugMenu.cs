using FalsePositive.Flow;
using FalsePositive.Voice;
using UnityEditor;
using UnityEngine;

namespace FalsePositive.EditorTools
{
    /// <summary>
    /// Play-mode shortcuts that drop straight into any interrogation phase,
    /// so iterating on P2's dialogue or P4's endings doesn't require talking
    /// through every earlier phase first. Companion to
    /// P3MemoryPairDebugMenu.cs (same Debug/ root, memory-pair-specific
    /// items) -- that file used to own the P3 jump alone; this one covers
    /// all four so "first/second/third/last interrogation" are all one
    /// click away.
    ///
    /// Editor-only and play-mode-only: every item validates on
    /// Application.isPlaying, so none of this can fire in a build or against
    /// a scene that has no live GameFlowDirector.
    /// </summary>
    public static class PhaseJumpDebugMenu
    {
        private const string Root = "Tools/False Positive/Debug/";

        private static GameFlowDirector Flow()
        {
            GameFlowDirector flow = Object.FindAnyObjectByType<GameFlowDirector>();
            if (flow == null)
            {
                Debug.LogError("[PhaseDebug] No GameFlowDirector in the loaded scenes -- " +
                    "press Play from _Persistent first.");
            }
            return flow;
        }

        [MenuItem(Root + "Jump to P1_Tutorial (first interrogation)", true)]
        [MenuItem(Root + "Jump to P2_Recall (second interrogation)", true)]
        [MenuItem(Root + "Jump to P3_Verdict (third interrogation)", true)]
        [MenuItem(Root + "Jump to P4_Ending (last beat)", true)]
        private static bool RequiresPlayMode() => Application.isPlaying;

        /// <summary>Shared by every jump item. GameFlowDirector.SessionId is
        /// only ever minted by StartNewPlaythrough, which normally runs from
        /// MainMenuController.HandlePlay -- jumping straight into a live-
        /// dialogue phase from a fresh Play (never clicked Play on the menu)
        /// would otherwise bind a null session id into DialogueManager and
        /// every backend turn would fail. Mint one here if none exists yet,
        /// same as the old P3-only jump should have but didn't.
        ///
        /// Also ensures the mic pipeline is actually live -- every phase past
        /// Boot/Menu gates progress on real speech (P1's "Who are you? Where
        /// am I?" spoken prompt, P2/P3's live turns), and normally that mic
        /// only opens via MicConsentFlow after the player clicks through
        /// consent. A jump that skips straight past that leaves
        /// VoiceActivityDetector uncalibrated, so speaking into the mic does
        /// nothing and the phase looks silently stuck -- which is exactly
        /// what "the Spassky cutscene never played" turned out to be: P1
        /// never got a qualifying utterance to finish its spoken-prompt gate
        /// on, so CutsceneId.SpasskyAnswer never got requested.</summary>
        private static void Jump(GamePhase phase)
        {
            GameFlowDirector flow = Flow();
            if (flow == null) return;

            if (string.IsNullOrEmpty(flow.SessionId))
            {
                Debug.Log("[PhaseDebug] No session yet -- minting one via StartNewPlaythrough.");
                flow.StartNewPlaythrough();
            }

            EnsureMicReady(flow);

            Debug.Log($"[PhaseDebug] GoToPhase({phase}) -- memory flags and score are empty on " +
                "this path; use the F1 debug overlay to toggle flags if the officer's questions need them.");
            flow.GoToPhase(phase);
        }

        /// <summary>Editor-only, play-mode-only debug tooling never ships, so
        /// auto-granting consent here (rather than silently failing to open
        /// the mic, or making every jump item wait on a manual consent
        /// click) is a reasonable convenience -- same spirit as
        /// UtteranceRecorder.SimulateUtterance/MicCalibration.SimulateVoice,
        /// which already exist to bypass real mic interaction for testing.
        /// Calibration runs over the next few seconds via VAD's own Update
        /// loop rather than blocking here, so speaking won't register
        /// immediately after a jump -- logged so that isn't mistaken for
        /// still being broken.</summary>
        private static void EnsureMicReady(GameFlowDirector flow)
        {
            if (flow.Mic == null || flow.Vad == null) return;

            if (!flow.Mic.IsCapturing)
            {
                if (!MicConsentGate.Granted)
                {
                    Debug.Log("[PhaseDebug] Auto-granting mic consent for this debug jump " +
                        "(editor/play-mode only -- never happens in a build).");
                    MicConsentGate.Grant();
                }

                if (flow.Mic.TryBeginCapture(null, out string error))
                {
                    Debug.Log("[PhaseDebug] Mic capture started for the debug jump.");
                }
                else
                {
                    Debug.LogWarning($"[PhaseDebug] Could not start mic capture ({error}) -- " +
                        "spoken prompts and live dialogue turns will not register.");
                    return;
                }
            }

            if (!flow.Vad.IsCalibrated)
            {
                float seconds = flow.Config != null ? flow.Config.calibrationSilenceSeconds : 3f;
                flow.Vad.BeginCalibration(seconds);
                Debug.Log($"[PhaseDebug] VAD not calibrated -- calibrating now ({seconds}s of quiet " +
                    "room tone). Speaking won't register until that finishes.");
            }
        }

        /// <summary>Scripted, never touches the backend -- CutsceneId.SpasskyAnswer
        /// then FuzzyToNight then straight into M1_Night.</summary>
        [MenuItem(Root + "Jump to P1_Tutorial (first interrogation)", false, 90)]
        private static void JumpToP1() => Jump(GamePhase.P1_Tutorial);

        /// <summary>First live LLM phase (p2TurnCap = 14). Ends on the forced
        /// closing question "What happened to Nick?" once every story mark is
        /// covered or the cap is hit.</summary>
        [MenuItem(Root + "Jump to P2_Recall (second interrogation)", false, 91)]
        private static void JumpToP2() => Jump(GamePhase.P2_Recall);

        /// <summary>p3TurnCap = 8. The CS-16 memory pair only plays after two
        /// completed turns in this phase (PhaseDialogueController's own
        /// TurnsThisPhase >= 2 gate) -- use "Play CS-16A/CS-16B" below for an
        /// immediate look at that blocking instead of talking twice.</summary>
        [MenuItem(Root + "Jump to P3_Verdict (third interrogation)", false, 92)]
        private static void JumpToP3() => Jump(GamePhase.P3_Verdict);

        /// <summary>Cutscene only -- picks one of the four endings from
        /// whichever suspect was last named, then advances straight to
        /// Outcome. With no suspect named on a fresh jump, expect the
        /// fallback ending rather than a meaningful choice.</summary>
        [MenuItem(Root + "Jump to P4_Ending (last beat)", false, 93)]
        private static void JumpToP4() => Jump(GamePhase.P4_Ending);
    }
}
