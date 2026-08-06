using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Subtitles for both officer-VO paths — live TTS from DialogueManager
    /// and Person B's pre-rendered Timeline VO — through the one component,
    /// so the two never look different. Show(...) auto-hides after
    /// holdSeconds (the live-TTS caller, timed to the clip); ShowUntilHidden
    /// stays up until an explicit Hide() (the Timeline-signal caller). See
    /// the frozen Show(speaker, line, holdSeconds) signature in
    /// docs/GAME_COMPLETION_PLAN.md A0.5.
    /// </summary>
    public sealed class SubtitleUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text speakerText;
        [SerializeField] private Text lineText;

        public bool Enabled { get; set; } = true;

        public void Show(string speaker, string line, float holdSeconds)
        {
            if (!Enabled) return;
            StopAllCoroutines();
            SetText(speaker, line);
            if (root != null) root.SetActive(true);
            if (holdSeconds > 0f) StartCoroutine(HideAfter(holdSeconds));
        }

        public void ShowUntilHidden(string speaker, string line)
        {
            if (!Enabled) return;
            StopAllCoroutines();
            SetText(speaker, line);
            if (root != null) root.SetActive(true);
        }

        public void Hide()
        {
            StopAllCoroutines();
            if (root != null) root.SetActive(false);
        }

        private void SetText(string speaker, string line)
        {
            if (speakerText != null) speakerText.text = speaker ?? string.Empty;
            if (lineText != null) lineText.text = line ?? string.Empty;
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (root != null) root.SetActive(false);
        }
    }
}
