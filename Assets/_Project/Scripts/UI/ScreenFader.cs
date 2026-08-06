using System.Collections;
using UnityEngine;

namespace FalsePositive.UI
{
    /// <summary>
    /// Full-screen black CanvasGroup fade used for the sit/stand camera
    /// handoff and every CutsceneDirector transition. No third-party
    /// transition asset — just an alpha animation.
    ///
    /// GameFlowDirector.TransitionRoutine fades to black, activates the
    /// target scene, and (as of the M2 fix) only invokes PhaseChanged after
    /// its own FadeFromBlack completes — but a phase handler further downstream
    /// can still legitimately request a cutscene while this fader is mid-fade
    /// from something else, and CutsceneDirector.PlayRoutine always starts
    /// its own FadeToBlack regardless of current alpha. So Fade() cannot
    /// assume it owns the CanvasGroup for its whole duration: it samples the
    /// *current* alpha as its start value (never a hardcoded "from") and
    /// tags each fade with a generation counter so a newer Fade() call
    /// supersedes an older one instead of both writing canvasGroup.alpha the
    /// same frame — the two-coroutine race that used to make the M2 opening
    /// beat flicker or stick.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private int _fadeGeneration;

        private void Awake()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        public IEnumerator FadeToBlack(float duration)
        {
            yield return Fade(1f, duration);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        }

        public IEnumerator FadeFromBlack(float duration)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            yield return Fade(0f, duration);
        }

        private IEnumerator Fade(float to, float duration)
        {
            if (canvasGroup == null) yield break;

            int generation = ++_fadeGeneration;
            float from = canvasGroup.alpha;

            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                // A newer Fade() call (e.g. CutsceneDirector starting a
                // cutscene while this transition's own fade is still
                // running) has taken over the CanvasGroup — stop writing
                // instead of fighting it for alpha.
                if (generation != _fadeGeneration) yield break;
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            if (generation == _fadeGeneration) canvasGroup.alpha = to;
        }
    }
}
