using FalsePositive.Player;
using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// Camera-forward raycast against whatever Interactable is in front of the
    /// player. Lives on the player in each memory scene. Subscribes to
    /// PlayerInputRouter.InteractPressed (.started, not .performed — see that
    /// file for why) rather than polling a key directly, matching every other
    /// input consumer in the project. No on-screen prompt UI: each prop
    /// already carries a floating name label (see MemorySceneDressing), which
    /// is the identification a prompt line would otherwise provide.
    /// </summary>
    public sealed class InteractionRaycaster : MonoBehaviour
    {
        [SerializeField] private Camera raycastCamera;
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private float maxDistance = 3f;

        private Interactable _current;

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
