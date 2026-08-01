using UnityEngine;

namespace FalsePositive.Cop
{
    /// <summary>
    /// The genuine rig-independent floor tier: swaps between a "closed" and
    /// "open" mouth material on a renderer, driven by live playback
    /// amplitude above a small threshold (so it doesn't flicker on near-
    /// silence). Works on literally any mesh — no blendshapes, no jaw bone
    /// required. Use this if a sourced cop model has neither.
    /// </summary>
    public sealed class TextureSwapCopMouth : MonoBehaviour, ICopMouth
    {
        [SerializeField] private Renderer targetRenderer;
        [SerializeField] private int materialSlot = 0;
        [SerializeField] private Material closedMouthMaterial;
        [SerializeField] private Material openMouthMaterial;
        [SerializeField] private float openThreshold = 0.02f;

        private bool _isOpen;

        public void Begin(AudioSource source)
        {
            _isOpen = true; // force the next SetOpen(false) call to actually apply
            SetOpen(false);
        }

        public void SetAmplitude(float rms)
        {
            SetOpen(rms >= openThreshold);
        }

        public void Stop()
        {
            SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            if (_isOpen == open || targetRenderer == null) return;
            _isOpen = open;

            Material[] materials = targetRenderer.materials; // instances; fine at this call frequency
            if (materialSlot < 0 || materialSlot >= materials.Length) return;
            materials[materialSlot] = open ? openMouthMaterial : closedMouthMaterial;
            targetRenderer.materials = materials;
        }
    }
}
