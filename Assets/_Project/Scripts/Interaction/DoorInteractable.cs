using System;
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

        public bool IsLocked { get; private set; }
        public event Action Opened;

        private void Awake()
        {
            IsLocked = startsLocked;
        }

        public void Unlock()
        {
            IsLocked = false;
        }

        public override void OnInteract()
        {
            if (IsLocked)
            {
                lookPrompt = lockedPrompt;
                return;
            }

            MarkComplete();
            Opened?.Invoke();
        }
    }
}
