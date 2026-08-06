using FalsePositive.Interaction;
using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// Centre-screen "[E] &lt;prompt&gt;" line — the on-screen prompt this
    /// project never had (see InteractionRaycaster's doc comment). Removes
    /// the last reason MemorySceneDressing put a floating TextMesh label
    /// over every prop, per the user's explicit request.
    ///
    /// Lives in _Persistent so it works for any memory scene without each
    /// one wiring its own copy. Polls for an InteractionRaycaster the same
    /// way CursorVisibilityController polls Selectable.allSelectablesArray —
    /// only one memory scene's player (and therefore one raycaster) is ever
    /// active at a time, and re-finding it via FindFirstObjectByType only
    /// when the cached one goes null/disabled (scene change) is cheap enough
    /// not to need an event.
    ///
    /// A completed Interactable shows nothing (there is nothing left to do).
    /// A locked DoorInteractable shows its lockedPrompt WITHOUT the "[E]"
    /// bracket — pressing E doesn't accomplish anything while locked, so
    /// bracketing it as a keypress would be misleading.
    /// </summary>
    public sealed class InteractionPromptUI : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text promptText;

        private InteractionRaycaster _raycaster;
        private Interactable _highlighted;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private MaterialPropertyBlock _highlightBlock;

        private void LateUpdate()
        {
            if (_raycaster == null)
            {
                _raycaster = FindAnyObjectByType<InteractionRaycaster>();
            }

            Interactable current = _raycaster != null ? _raycaster.Current : null;
            UpdateHighlight(current);

            if (current == null || current.IsComplete)
            {
                SetVisible(false);
                return;
            }

            bool isLockedDoor = current is DoorInteractable door && door.IsLocked;
            string text = isLockedDoor ? current.LookPrompt : $"[E] {current.LookPrompt}";
            SetVisible(!string.IsNullOrEmpty(current.LookPrompt));
            if (promptText != null) promptText.text = text;
        }

        private void UpdateHighlight(Interactable current)
        {
            if (current == _highlighted) return;

            if (_highlighted != null)
            {
                ApplyHighlight(_highlighted, false);
            }
            if (current != null)
            {
                ApplyHighlight(current, true);
            }
            _highlighted = current;
        }

        private void ApplyHighlight(Interactable interactable, bool on)
        {
            _highlightBlock ??= new MaterialPropertyBlock();
            foreach (Renderer renderer in interactable.GetComponentsInChildren<Renderer>())
            {
                renderer.GetPropertyBlock(_highlightBlock);
                _highlightBlock.SetColor(EmissionColorId, on ? new Color(0.4f, 0.35f, 0.1f) : Color.black);
                renderer.SetPropertyBlock(_highlightBlock);
            }
        }

        private void SetVisible(bool visible)
        {
            if (root != null && root.activeSelf != visible) root.SetActive(visible);
        }
    }
}
