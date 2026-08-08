using FalsePositive.Flow;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEngine;

namespace FalsePositive.EditorTools
{
    /// <summary>
    /// Play-mode shortcuts for iterating on P3's memory pair (CS-16A/CS-16B,
    /// docs/STORY_SCRIPT.md §4 P3_VERDICT). Reaching P3 normally means talking
    /// through P1, M1, P2 and M2 first — several minutes per attempt, which is
    /// not a sane edit/test loop for blocking that will need nudging.
    ///
    /// Editor-only and play-mode-only: every item validates on
    /// Application.isPlaying, so none of this can fire in a build or against a
    /// scene that has no live GameFlowDirector.
    /// </summary>
    public static class P3MemoryPairDebugMenu
    {
        private const string Root = "Tools/False Positive/Debug/";

        private static GameFlowDirector Flow()
        {
            GameFlowDirector flow = Object.FindAnyObjectByType<GameFlowDirector>();
            if (flow == null)
            {
                Debug.LogError("[P3Debug] No GameFlowDirector in the loaded scenes — " +
                    "press Play from _Persistent first.");
            }
            return flow;
        }

        [MenuItem(Root + "Jump to P3_Verdict", true)]
        [MenuItem(Root + "Play CS-16A (Good Years)", true)]
        [MenuItem(Root + "Play CS-16B (When It Went Wrong)", true)]
        [MenuItem(Root + "Check M1 staging was restored", true)]
        private static bool RequiresPlayMode() => Application.isPlaying;

        /// <summary>Drops straight into P3, which runs the memory pair as its
        /// opening beat. Everything earlier in the playthrough is skipped, so
        /// MemoryFlags and SessionScore will be empty — fine for looking at
        /// blocking, misleading if you are judging what the officer says.</summary>
        [MenuItem(Root + "Jump to P3_Verdict", false, 100)]
        private static void JumpToP3()
        {
            GameFlowDirector flow = Flow();
            if (flow == null) return;
            Debug.Log("[P3Debug] GoToPhase(P3_Verdict) — memory flags/score are empty on this path.");
            flow.GoToPhase(GamePhase.P3_Verdict);
        }

        /// <summary>Plays one memory in isolation: cut to the cabin, run the
        /// cutscene, cut back. No dialogue phase, so this is the cheapest way
        /// to look at staging and at whether the cast is put back afterwards.</summary>
        [MenuItem(Root + "Play CS-16A (Good Years)", false, 101)]
        private static void PlayGoodYears()
        {
            GameFlowDirector flow = Flow();
            if (flow == null) return;
            flow.RequestMemoryInterlude(GamePhase.M1_Night, CutsceneId.GoodYears,
                () => Debug.Log("[P3Debug] CS-16A returned. Run 'Check M1 staging was restored'."));
        }

        [MenuItem(Root + "Play CS-16B (When It Went Wrong)", false, 102)]
        private static void PlayWhenItWentWrong()
        {
            GameFlowDirector flow = Flow();
            if (flow == null) return;
            flow.RequestMemoryInterlude(GamePhase.M1_Night, CutsceneId.WhenItWentWrong,
                () => Debug.Log("[P3Debug] CS-16B returned. Run 'Check M1 staging was restored'."));
        }

        /// <summary>The highest-risk assertion in the whole feature. CutsceneStage
        /// borrows Nick and the Teagues for the flashbacks and must hand them
        /// back disabled — M1_Night has Nick outside and the Teagues upstairs,
        /// and if any of them is left standing in the cabin then §7's "who went
        /// through the door?" trap is silently broken, with nothing in the game
        /// to tell you.</summary>
        [MenuItem(Root + "Check M1 staging was restored", false, 120)]
        private static void CheckStagingRestored()
        {
            // GameObject.Find only searches the ACTIVE scene, and by the time
            // an interlude has returned, Interrogation is active again and the
            // cabin's roots are deactivated — the first version of this check
            // therefore always reported "no Characters root" and told you
            // nothing. Search every loaded scene's roots instead, inactive
            // included, so it works after the flashback rather than only during.
            Transform root = null;
            for (int i = 0; i < SceneManager.sceneCount && root == null; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || scene.name != "Memory_CabinNight") continue;
                foreach (GameObject go in scene.GetRootGameObjects())
                {
                    if (go.name == "Characters") { root = go.transform; break; }
                    Transform nested = go.transform.Find("Characters");
                    if (nested != null) { root = nested; break; }
                }
            }

            if (root == null)
            {
                Debug.LogWarning("[P3Debug] Memory_CabinNight is not loaded, so there is " +
                    "nothing to check. Play a memory interlude first.");
                return;
            }

            bool ok = true;
            foreach (string name in new[] { "Nick Vlahos (Male)", "Aaron Teague (Male)", "Ivy Teague (Female)" })
            {
                Transform t = root.Find(name);
                if (t == null)
                {
                    Debug.LogWarning($"[P3Debug] {name} not found under Characters.");
                    continue;
                }
                if (t.gameObject.activeSelf)
                {
                    ok = false;
                    Debug.LogError($"[P3Debug] {name} is STILL ACTIVE after the flashback — " +
                        "ReturnBorrowed did not restore it. §7's door trap is broken in this run.");
                }
            }

            if (ok) Debug.Log("[P3Debug] M1 staging restored: Nick and the Teagues are all disabled again.");
        }
    }
}
