using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// The small caption in the top-right that names when and where a beat is
    /// happening — "00:50 / Alone by the fire".
    ///
    /// docs/STORY_SCRIPT.md §4 stages CS-16B as a series of hard cuts forward in
    /// time: 23:40 by the table, then roughly an hour later by the fireplace.
    /// On the page the jump is obvious because the script says so. On screen it
    /// was the same room, the same firelight and no marker of any kind, so the
    /// two halves read as one continuous scene and the second one appeared to
    /// start mid-thought.
    ///
    /// Deliberately not a subtitle. Subtitles are what people are saying;
    /// this is the film telling you where you are, so it sits in a different
    /// corner, stays up across several beats, and is not affected by the
    /// subtitle toggle in settings.
    /// </summary>
    public sealed class TimeCardUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text timeText;
        [SerializeField] private Text captionText;

        [SerializeField, Range(0.1f, 2f)] private float fadeSeconds = 0.5f;

        private Coroutine _routine;

        /// <summary>Fades the card in, holds, and fades it out.
        ///
        /// <paramref name="holdSeconds"/> is the visible time at full opacity;
        /// the fades are on top of it, so a card outlives the beat that raised
        /// it and carries over the first line of the new scene — which is the
        /// point, since that line is the one needing the context.</summary>
        public void Show(string time, string caption, float holdSeconds)
        {
            if (root == null) return;
            if (_routine != null) StopCoroutine(_routine);
            _routine = StartCoroutine(ShowRoutine(time, caption, holdSeconds));
        }

        public void Hide()
        {
            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }
            if (group != null) group.alpha = 0f;
            if (root != null) root.SetActive(false);
        }

        private IEnumerator ShowRoutine(string time, string caption, float holdSeconds)
        {
            if (timeText != null) timeText.text = time ?? string.Empty;
            if (captionText != null)
            {
                captionText.text = caption ?? string.Empty;
                captionText.gameObject.SetActive(!string.IsNullOrEmpty(caption));
            }

            root.SetActive(true);
            yield return Fade(0f, 1f);

            // Unscaled: a cutscene is not gameplay and must keep its timing if
            // anything ever pauses the game underneath it.
            float elapsed = 0f;
            while (elapsed < holdSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            yield return Fade(1f, 0f);
            root.SetActive(false);
            _routine = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            if (group == null) yield break;

            float elapsed = 0f;
            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeSeconds));
                yield return null;
            }
            group.alpha = to;
        }
    }
}
