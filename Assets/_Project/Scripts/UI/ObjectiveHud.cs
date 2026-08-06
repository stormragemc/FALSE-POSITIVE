using UnityEngine;
using UnityEngine.UI;

namespace FalsePositive.UI
{
    /// <summary>
    /// The bottom-left "Fix the radio." / "Go to the door." objective line
    /// shown during the memory scenes. Exposed on GameFlowDirector so
    /// Person B's Interactables (in the other scenes) can drive it without
    /// a direct reference — see the frozen contract in
    /// docs/GAME_COMPLETION_PLAN.md A0.5.
    /// </summary>
    public sealed class ObjectiveHud : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text label;

        public void Set(string text)
        {
            if (label != null) label.text = text;
            if (root != null) root.SetActive(!string.IsNullOrEmpty(text));
        }

        public void Clear() => Set(string.Empty);
    }
}
