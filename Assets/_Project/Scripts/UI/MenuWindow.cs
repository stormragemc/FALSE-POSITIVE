using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Generic modal window used by MainMenu's Quit/Credits/How-to-play popups
    /// and the restyled Settings panel. <see cref="root"/>.SetActive() is the
    /// outer visibility gate — non-negotiable, see CursorVisibilityController.cs:
    /// it counts Selectable.allSelectablesArray, and Selectable registers
    /// itself in OnEnable, so a window hidden only via CanvasGroup.alpha would
    /// keep its buttons "enabled" forever and permanently unlock the cursor the
    /// first time this scene's Selectables get counted during gameplay.
    ///
    /// This component itself must live on an always-active GameObject *above*
    /// root (see CreateWindow in ProjectBootstrapBuilder.cs: WindowHost -> Root)
    /// — StartCoroutine on a disabled GameObject throws, and a coroutine hosted
    /// on root itself would die the instant Close() deactivates it mid-fade.
    ///
    /// Fade coroutine structurally mirrors ScreenFader.cs (same
    /// _fadeGeneration counter, same "sample current alpha as from" rule) with
    /// two deliberate differences: Time.unscaledDeltaTime, because Settings
    /// doubles as the Day-2 pause menu at Time.timeScale = 0 (A15), and
    /// Mathf.SmoothStep easing instead of linear.
    /// </summary>
    public sealed class MenuWindow : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Button backdropButton;
        [SerializeField] private Button closeButton;
        [SerializeField] private Selectable defaultSelection;
        [SerializeField] private bool closeOnBackdropClick = true;
        [SerializeField] private float fadeDuration = 0.14f;

        private int _fadeGeneration;
        private GameObject _previousSelection;

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            if (backdropButton != null && closeOnBackdropClick) backdropButton.onClick.AddListener(Close);
            if (closeButton != null) closeButton.onClick.AddListener(Close);
        }

        public void Open()
        {
            if (IsOpen) return;
            IsOpen = true;

            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;

            if (root != null) root.SetActive(true);
            if (canvasGroup != null) canvasGroup.blocksRaycasts = true;

            MenuWindowStack.Push(this);
            StartCoroutine(FadeInAndFocus());
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;

            MenuWindowStack.Pop(this);
            StartCoroutine(FadeOutAndDeactivate());
        }

        public void Toggle()
        {
            if (IsOpen) Close();
            else Open();
        }

        private IEnumerator FadeInAndFocus()
        {
            yield return Fade(1f);

            // Selecting during the fade would flash the "selected" tint at
            // alpha 0 — only assign focus once the window is fully visible.
            if (EventSystem.current != null && defaultSelection != null)
            {
                EventSystem.current.SetSelectedGameObject(defaultSelection.gameObject);
            }
        }

        private IEnumerator FadeOutAndDeactivate()
        {
            yield return Fade(0f);

            if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
            if (root != null) root.SetActive(false);

            if (EventSystem.current != null && _previousSelection != null && _previousSelection.activeInHierarchy)
            {
                EventSystem.current.SetSelectedGameObject(_previousSelection);
            }
        }

        private IEnumerator Fade(float to)
        {
            if (canvasGroup == null) yield break;

            int generation = ++_fadeGeneration;
            float from = canvasGroup.alpha;

            if (fadeDuration <= 0f)
            {
                canvasGroup.alpha = to;
                yield break;
            }

            float t = 0f;
            while (t < fadeDuration)
            {
                // A newer Open()/Close() call has taken over this CanvasGroup —
                // stop writing instead of fighting it for alpha (same race this
                // guards against in ScreenFader.cs).
                if (generation != _fadeGeneration) yield break;
                t += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.SmoothStep(from, to, t / fadeDuration);
                yield return null;
            }
            if (generation == _fadeGeneration) canvasGroup.alpha = to;
        }
    }
}
