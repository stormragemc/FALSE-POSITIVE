using System;
using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// The M2 front door — locked until a KeyPickup unlocks it, at which point
    /// interacting opens it (out into the snow, CS-12). Also usable in M1 for
    /// the "It's locked" beat by simply never calling Unlock().
    /// </summary>
    public sealed class DoorInteractable : Interactable
    {
        [SerializeField] private bool startsLocked;
        [SerializeField] private string lockedPrompt = "It's locked.";
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip openClip;
        [SerializeField] private AudioClip lockedClip;
        [SerializeField] private AudioClip unlockClip;

        public bool IsLocked { get; private set; }
        public event Action Opened;

        private bool _reportedLocked;

        private void Awake()
        {
            IsLocked = startsLocked;
        }

        /// <summary>Called by KeyPickup.OnInteract the moment the key is taken —
        /// the "door unlocked" beat the user explicitly asked for a sound on,
        /// previously completely silent (the key's own pickup SFX plays on the
        /// key, not the door, and doesn't imply the lock itself turned).</summary>
        public void Unlock()
        {
            IsLocked = false;
            lookPrompt = "Open the door";
            if (audioSource != null && unlockClip != null) audioSource.PlayOneShot(unlockClip);
        }

        public override void OnInteract()
        {
            if (IsLocked)
            {
                lookPrompt = lockedPrompt;

                // Rattle SFX + subtitle every attempt — used to be
                // completely silent, which read as "nothing happens" when
                // the player couldn't yet reach the key. This must NOT call
                // MarkComplete(): InteractionRaycaster gates further
                // interaction on IsComplete (Interactable.cs), so marking
                // complete here would permanently brick the door — the
                // player could take the key and still never open it. The
                // memory flag is written directly instead, on first attempt
                // only, independent of the door ever being marked complete
                // (which now only happens once it actually opens).
                if (audioSource != null && lockedClip != null) audioSource.PlayOneShot(lockedClip);
                GameFlowDirector.Instance?.Subtitles?.Show(string.Empty, lockedPrompt, 1.5f);
                if (!_reportedLocked)
                {
                    _reportedLocked = true;
                    GameFlowDirector.Instance?.Flags.Set(MemoryFlagIds.FoundDoorLocked);
                }
                return;
            }

            if (audioSource != null && openClip != null) audioSource.PlayOneShot(openClip);
            MarkComplete();
            Opened?.Invoke();
        }
    }
}
