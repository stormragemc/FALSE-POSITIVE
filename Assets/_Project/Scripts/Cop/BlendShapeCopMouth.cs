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
    ///
    /// Also drives jawOpen directly, off the same lipSync.result.volume signal
    /// CopTalkGestureAnimator already uses for the arm gesture (same
    /// attack/release envelope shape) — the 7-phoneme uLipSync profile only
    /// covers the lip shapes (viseme_aa/I/U/E/O/sil/SS), so without this the
    /// jaw never opens and the performance reads as lips-only. jawOpen is
    /// this class's alone to write: it must never be added to any
    /// uLipSyncBlendShape table (head/teeth/tongue), since each of those
    /// clears-then-writes its own table every LateUpdate and would fight an
    /// envelope entry placed there.
    /// </summary>
    [RequireComponent(typeof(ULS.uLipSync))]
    public sealed class BlendShapeCopMouth : MonoBehaviour, ICopMouth
    {
        [SerializeField] private ULS.uLipSync lipSync;
        [SerializeField] private ULS.uLipSyncBlendShape blendShape;

        [Header("Jaw (jawOpen), one writer for the whole rig")]
        [SerializeField] private SkinnedMeshRenderer[] jawRenderers;
        [SerializeField] private int[] jawBlendShapeIndices;
        [SerializeField] private float jawAttackPerSecond = 15f;
        [SerializeField] private float jawReleasePerSecond = 6f;
        [SerializeField] private float maxJawBlendShapeValue = 60f;

        private bool _listenerAttached;
        private float _jawEnvelope;

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

        private void LateUpdate()
        {
            float target = lipSync != null ? Mathf.Clamp01(lipSync.result.volume) : 0f;
            float rate = target > _jawEnvelope ? jawAttackPerSecond : jawReleasePerSecond;
            _jawEnvelope = Mathf.MoveTowards(_jawEnvelope, target, rate * Time.deltaTime);

            if (jawRenderers == null || jawBlendShapeIndices == null) return;
            float weight = _jawEnvelope * maxJawBlendShapeValue;
            int count = Mathf.Min(jawRenderers.Length, jawBlendShapeIndices.Length);
            for (int i = 0; i < count; i++)
            {
                if (jawRenderers[i] == null || jawBlendShapeIndices[i] < 0) continue;
                jawRenderers[i].SetBlendShapeWeight(jawBlendShapeIndices[i], weight);
            }
        }
    }
}
