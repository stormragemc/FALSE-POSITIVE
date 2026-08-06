using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// The M2 door key, on the hook immediately left of the frame. Unlocks its
    /// paired door and disappears.
    /// </summary>
    public sealed class KeyPickup : Interactable
    {
        [SerializeField] private DoorInteractable doorToUnlock;

        public override void OnInteract()
        {
            MarkComplete();
            doorToUnlock?.Unlock();
            gameObject.SetActive(false);
        }
    }
}
