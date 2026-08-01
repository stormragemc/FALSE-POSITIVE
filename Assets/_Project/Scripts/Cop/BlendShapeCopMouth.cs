using UnityEngine;
using ULS = uLipSync;

namespace FalsePositive.Cop
{
    /// <summary>
    /// Primary fidelity tier: wraps uLipSync's own uLipSync + uLipSyncBlendShape
    /// components (github.com/hecomi/uLipSync, MIT). The Inspector-configured
    /// Phoneme -> BlendShape table on uLipSyncBlendShape stays the source of
    /// truth (plan section 6) — this class just wires uLipSync's phoneme
    /// analysis into it at runtime and satisfies ICopMouth's lifecycle, so
    /// the voice pipeline never has to know a phoneme exists.
    ///
    /// uLipSync analyzes audio via OnAudioFilterRead, which requires its
    /// `uLipSync` component to live on the SAME GameObject as the AudioSource
    /// it should listen to (standard Unity audio-filter behavior) — that's
    /// why this is a [RequireComponent], not just a serialized reference.
    ///
    /// Note: the namespace and the core component class are both literally
    /// named "uLipSync" — the `ULS` alias below sidesteps that ambiguity
    /// rather than fully-qualifying every reference.
    /// </summary>
    [RequireComponent(typeof(ULS.uLipSync))]
    public sealed class BlendShapeCopMouth : MonoBehaviour, ICopMouth
    {
        [SerializeField] private ULS.uLipSync lipSync;
        [SerializeField] private ULS.uLipSyncBlendShape blendShape;

        private bool _listenerAttached;

        private void Awake()
        {
            if (lipSync == null)
            {
                lipSync = GetComponent<ULS.uLipSync>();
            }
        }

        public void Begin(AudioSource source)
        {
            // uLipSync reads whatever AudioSource lives on this same
            // GameObject automatically — `source` is accepted only to
            // satisfy ICopMouth's shared signature with the other tiers.
            if (_listenerAttached || lipSync == null || blendShape == null) return;
            lipSync.onLipSyncUpdate.AddListener(blendShape.OnLipSyncUpdate);
            _listenerAttached = true;
        }

        public void SetAmplitude(float rms)
        {
            // Ignored on purpose — uLipSync derives its own volume directly
            // from the audio it's listening to; the jaw-bone and
            // texture-swap tiers are the ones that need an externally
            // supplied amplitude.
        }

        public void Stop()
        {
            // Nothing to do — uLipSync naturally settles to silence/neutral
            // once the AudioSource stops producing output.
        }
    }
}
