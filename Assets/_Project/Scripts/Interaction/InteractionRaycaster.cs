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

        // Non-alloc buffer, scanned for the nearest hit that actually has an
        // Interactable. A plain Physics.Raycast (single nearest hit) used to
        // silently kill the prompt whenever any non-interactable collider
        // sat in front of a prop — e.g. Prop_DoorKey/Prop_CoatOnChair are
        // fully enclosed inside BO_CoatHanger's BoxCollider, and
        // M1NightController's trigger box sits directly in front of the M1
        // door on a straight-on approach. 16, not 8: an overflowing buffer
        // fills with an arbitrary subset of hits, not the nearest ones, and
        // could silently drop exactly the prop near a collider cluster.
        private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];

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

            int hitCount = Physics.RaycastNonAlloc(
                raycastCamera.transform.position, raycastCamera.transform.forward,
                _hitBuffer, maxDistance);

            float nearestDistance = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (hit.distance >= nearestDistance) continue;

                Interactable candidate = hit.collider.GetComponentInParent<Interactable>();
                if (candidate == null || candidate.IsComplete) continue;

                nearestDistance = hit.distance;
                _current = candidate;
            }
        }

        private void HandleInteractPressed()
        {
            if (_current != null && !_current.IsComplete) _current.OnInteract();
        }
    }
}
