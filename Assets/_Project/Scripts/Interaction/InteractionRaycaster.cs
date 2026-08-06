using FalsePositive.Player;
using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// Camera-forward raycast against whatever Interactable is in front of the
    /// player. Lives on the player in each memory scene. Subscribes to
    /// PlayerInputRouter.InteractPressed (.started, not .performed — see that
    /// file for why) rather than polling a key directly, matching every other
    /// input consumer in the project.
    ///
    /// <see cref="Current"/> is the hook UI.InteractionPromptUI polls (Phase 3
    /// of the Cabin_v2 pass) to show "[E] &lt;LookPrompt&gt;" on screen — this
    /// used to have no prompt UI at all, relying on a floating TextMesh label
    /// over every prop instead (see MemorySceneDressing's old AddProp), which
    /// is exactly the "placeholder text above objects" the user asked to
    /// remove. Now the raycast target IS the identification.
    /// </summary>
    public sealed class InteractionRaycaster : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private float maxDistance = 3f;

        private Interactable _current;

        /// <summary>The Interactable currently under the crosshair, or null. Read by UI.InteractionPromptUI.</summary>
        public Interactable Current => _current;

        private void OnEnable()
        {
            if (input != null) input.InteractPressed += HandleInteractPressed;
        }

        private void OnDisable()
        {
            if (input != null) input.InteractPressed -= HandleInteractPressed;
        }

        private void Update()
        {
            _current = null;
            if (raycastCamera == null) return;
            if (!Physics.Raycast(raycastCamera.transform.position, raycastCamera.transform.forward, out RaycastHit info, maxDistance)) return;
            _current = info.collider.GetComponentInParent<Interactable>();
        }

        private void HandleInteractPressed()
        {
            if (_current != null && !_current.IsComplete) _current.OnInteract();
        }
    }
}
