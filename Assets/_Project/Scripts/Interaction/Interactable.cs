using System;
using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Interaction
{
    /// <summary>
    /// Frozen contract (docs/GAME_COMPLETION_PLAN.md §4.1) — Person B owns this
    /// file. <see cref="OnInteract"/> and <see cref="Completed"/> are the whole
    /// public surface; do not add to it without updating the plan. Subclasses
    /// call <see cref="MarkComplete"/> once their interaction is satisfied,
    /// which writes memoryFlag (a MemoryFlagIds constant, never a raw string
    /// literal) to GameFlowDirector.Flags exactly once and raises Completed.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        [SerializeField] protected string lookPrompt;    // "Hold E to tune"
        [SerializeField] protected string memoryFlag;    // written to GameFlowDirector.Flags on use

        public string LookPrompt => lookPrompt;
        public bool IsComplete { get; private set; }

        public abstract void OnInteract();

        public event Action<Interactable> Completed;

        protected void MarkComplete()
        {
            if (IsComplete) return;
            IsComplete = true;

            if (!string.IsNullOrEmpty(memoryFlag))
            {
                GameFlowDirector.Instance?.Flags.Set(memoryFlag);
            }

            Completed?.Invoke(this);
        }
    }
}
