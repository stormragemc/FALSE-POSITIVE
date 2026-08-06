using System.IO;
using FalsePositive.Dialogue;
using UnityEditor;
using UnityEngine;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Builds Assets/_Project/Config/OfflineDialogueScript.asset — the fixed
    /// Spassky script Offline demo mode plays instead of a live sidecar
    /// turn (see DialogueManager.PlayOfflineTurn). Content mirrors the real
    /// prompts verbatim where docs/GAME_COMPLETION_PLAN.md's prompt files
    /// specify an exact line (Prompts/phase_p2_recall.txt,
    /// phase_p3_verdict.txt), and paraphrases the same topic list
    /// otherwise. Re-runnable, same pattern as CutsceneRecipeBuilder — this
    /// always resolves voClip by name from Art/Audio/VO/, so re-running
    /// after generating a clip just picks it up.
    /// </summary>
    public static class OfflineScriptBuilder
    {
        private const string ConfigPath = "Assets/_Project/Config/OfflineDialogueScript.asset";
        private const string VoRoot = "Assets/_Project/Art/Audio/VO/";

        [MenuItem("Tools/False Positive/Bootstrap/10 - Build Offline Dialogue Script")]
        public static void Build()
        {
            Directory.CreateDirectory("Assets/_Project/Config");

            OfflineDialogueScript script = AssetDatabase.LoadAssetAtPath<OfflineDialogueScript>(ConfigPath);
            if (script == null)
            {
                script = ScriptableObject.CreateInstance<OfflineDialogueScript>();
                AssetDatabase.CreateAsset(script, ConfigPath);
            }

            script.p2Recall = new[]
            {
                Line("spassky_offline_p2_01", "So. What's the last thing you remember?", 4f),
                Line("spassky_offline_p2_02", "Let's start simple. Were you drinking last night? With who, and until when?", 4f),
                Line("spassky_offline_p2_03", "Tell me about the argument with Nick. What was it actually about?", 4f),
                Line("spassky_offline_p2_04", "Did you see Nick go outside?", 3f),
                Line("spassky_offline_p2_05", "Did you go to the door yourself? Did you call out?", 3.5f),
                Line("spassky_offline_p2_06", "Was the door locked or unlocked, the last time you had anything to do with it?", 4f),
                Line("spassky_offline_p2_07", "What did you do afterward? How long were you out for?", 4f),
                Line("spassky_offline_p2_08", "Walk me through what happened when the body was found the next morning.", 4f),
                Line("spassky_offline_p2_09", "What happened to Nick?", 3f),
            };

            script.p3Verdict = new[]
            {
                Line("spassky_offline_p3_01", "Tell me why I should spare your life.", 4f),
                Line("spassky_offline_p3_02", "If it's not you, then tell me who did it?", 4f),
                Line("spassky_offline_p3_03", "That's not an answer. Try again.", 3f),
                Line("spassky_offline_p3_04", "You understand how that sounds, don't you?", 3.5f),
                Line("spassky_offline_p3_05", "Last chance. Who do you think did this?", 3.5f),
            };

            EditorUtility.SetDirty(script);
            AssetDatabase.SaveAssets();

            int missing = 0;
            foreach (OfflineOfficerLine line in script.p2Recall) if (line.voClip == null) missing++;
            foreach (OfflineOfficerLine line in script.p3Verdict) if (line.voClip == null) missing++;
            Debug.Log($"[OfflineScriptBuilder] OfflineDialogueScript.asset written " +
                $"({script.p2Recall.Length + script.p3Verdict.Length} lines, {missing} missing VO clips).");
        }

        private static OfflineOfficerLine Line(string clipName, string text, float holdSecondsIfNoClip) => new OfflineOfficerLine
        {
            line = text,
            holdSecondsIfNoClip = holdSecondsIfNoClip,
            voClip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoRoot + clipName + ".mp3"),
        };
    }
}
