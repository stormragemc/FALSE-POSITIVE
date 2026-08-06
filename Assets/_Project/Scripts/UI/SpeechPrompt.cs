using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// The "Say: ..." / "Call out for Nick." prompt shown while a scripted
    /// (non-conversational) utterance is expected — see
    /// GameFlowDirector.RequestSpokenPrompt. Lives in _Persistent.
    /// </summary>
    public sealed class SpeechPrompt : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text promptText;
        [SerializeField] private Text hintText;
        [SerializeField] private float pulseScale = 1.15f;
        [SerializeField] private float pulseDurationSeconds = 0.3f;

        public bool IsVisible => root != null && root.activeSelf;

        public void Show(string text)
        {
            if (promptText != null) promptText.text = text;
            if (root != null) root.SetActive(true);
            ClearHint();
        }

        public void SetHint(string hint)
        {
            if (hintText == null) return;
            hintText.text = hint;
            hintText.gameObject.SetActive(!string.IsNullOrEmpty(hint));
        }

        public void ClearHint() => SetHint(null);

        public void Pulse()
        {
            // Guards a real race: PhaseDialogueController's no-speech nudge
            // timer can still be in flight after the player already answered
            // and Hide() deactivated this GameObject (StartCoroutine on an
            // inactive object throws, logged as "Coroutine couldn't be
            // started"). Nothing to pulse if it isn't showing anyway.
            if (promptText == null || !gameObject.activeInHierarchy) return;
            StopAllCoroutines();
            StartCoroutine(PulseRoutine());
        }

        public void Hide()
        {
            ClearHint();
            if (root != null) root.SetActive(false);
        }

        private System.Collections.IEnumerator PulseRoutine()
        {
            Transform t = promptText.transform;
            Vector3 baseScale = Vector3.one;
            float half = pulseDurationSeconds * 0.5f;

            float elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(baseScale, baseScale * pulseScale, elapsed / half);
                yield return null;
            }
            elapsed = 0f;
            while (elapsed < half)
            {
                elapsed += Time.deltaTime;
                t.localScale = Vector3.Lerp(baseScale * pulseScale, baseScale, elapsed / half);
                yield return null;
            }
            t.localScale = baseScale;
        }
    }
}
