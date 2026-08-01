using UnityEngine;

namespace FalsePositive.Cop
{
    /// <summary>
    /// A lifecycle seam, not a viseme API — deliberately doesn't mention
    /// phonemes, so the voice pipeline (DialogueManager, CopVoicePlayback)
    /// never has to know which fidelity tier is active. The blendshape tier
    /// (BlendShapeCopMouth) subscribes to uLipSync's own callback
    /// internally; the jaw-bone and texture-swap tiers instead read
    /// SetAmplitude, which the blendshape tier ignores.
    /// </summary>
    public interface ICopMouth
    {
        void Begin(AudioSource source);
        void SetAmplitude(float rms);
        void Stop();
    }
}
