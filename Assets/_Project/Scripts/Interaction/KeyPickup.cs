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
        [SerializeField] private AudioClip pickupClip;

        public override void OnInteract()
        {
            // PlayClipAtPoint, not a PlayOneShot on this object's own
            // AudioSource — this object deactivates itself immediately
            // below, which would cut off a same-object source mid-clip.
            if (pickupClip != null) AudioSource.PlayClipAtPoint(pickupClip, transform.position);

            MarkComplete();
            doorToUnlock?.Unlock();
            gameObject.SetActive(false);
        }
    }
}
