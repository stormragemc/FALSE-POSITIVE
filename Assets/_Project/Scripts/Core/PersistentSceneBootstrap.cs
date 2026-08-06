using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Core
{
    /// <summary>
    /// Guarantees the game is playable no matter which scene is open when
    /// Play is pressed. Before this existed, pressing Play from anything
    /// other than `_Persistent.unity` produced a scene with no
    /// `GameFlowDirector`, no `EventSystem`, and no fader — MainMenu would
    /// render but every button and every phase transition would silently
    /// do nothing (see docs/GAME_COMPLETION_PLAN.md's Day-1 exit criterion
    /// and the `AskUserQuestion` findings this file resolves).
    ///
    /// Runs once, before any scene's own Start() methods (AfterSceneLoad —
    /// scene objects exist, so GetSceneByName works, but nothing has run
    /// Start yet). If `_Persistent` is already loaded (the normal path,
    /// where the user pressed Play from `_Persistent.unity` itself, or the
    /// build's first scene already loaded it) this is a no-op.
    /// </summary>
    internal static class PersistentSceneBootstrap
    {
        private const string PersistentSceneName = "_Persistent";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsurePersistentLoaded()
        {
            Scene persistent = SceneManager.GetSceneByName(PersistentSceneName);
            if (persistent.IsValid() && persistent.isLoaded)
            {
                return;
            }

            SceneManager.LoadScene(PersistentSceneName, LoadSceneMode.Additive);
        }
    }
}
