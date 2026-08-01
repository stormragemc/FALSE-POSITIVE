using System.Collections;
using UnityEngine;

namespace FalsePositive.UI
{
    /// <summary>
    /// Full-screen black CanvasGroup fade used for the sit/stand camera
    /// handoff. No third-party transition asset — just an alpha animation.
    /// </summary>
    public sealed class ScreenFader : MonoBehaviour
    {
        [SerializeField] private CanvasGroup canvasGroup;

        private void Awake()
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
        }

        public IEnumerator FadeToBlack(float duration)
        {
            yield return Fade(0f, 1f, duration);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;
        }

        public IEnumerator FadeFromBlack(float duration)
        {
            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            yield return Fade(1f, 0f, duration);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (canvasGroup == null) yield break;

            if (duration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }
            canvasGroup.alpha = to;
        }
    }
}
