using UnityEngine;

namespace FalsePositive.Cop
{
    /// <summary>
    /// Picks up whichever ICopMouth implementation is attached to the
    /// current cop prefab and forwards playback lifecycle plus a per-frame
    /// amplitude reading to it. Swapping cop models later means reassigning
    /// the <see cref="mouthImplementation"/> reference here — DialogueManager
    /// and the sidecar client never see a phoneme or care which fidelity
    /// tier is active.
    /// </summary>
    public sealed class CopMouthController : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour mouthImplementation; // must implement ICopMouth
        [SerializeField] private int amplitudeSampleWindow = 256;

        private ICopMouth _mouth;
        private AudioSource _source;
        private float[] _sampleScratch;
        private bool _active;

        private void Awake()
        {
            _mouth = mouthImplementation as ICopMouth;
            if (_mouth == null)
            {
                Debug.LogError($"[CopMouth] {mouthImplementation} does not implement ICopMouth.", this);
            }
            _sampleScratch = new float[Mathf.Max(amplitudeSampleWindow, 16)];
        }

        public void Begin(AudioSource playbackSource)
        {
            _source = playbackSource;
            _active = true;
            _mouth?.Begin(playbackSource);
        }

        public void Stop()
        {
            _active = false;
            _mouth?.Stop();
        }

        private void Update()
        {
            if (!_active || _source == null || !_source.isPlaying) return;

            _source.GetOutputData(_sampleScratch, 0);
            double sumSq = 0;
            for (int i = 0; i < _sampleScratch.Length; i++)
            {
                sumSq += _sampleScratch[i] * _sampleScratch[i];
            }
            float rms = Mathf.Sqrt((float)(sumSq / _sampleScratch.Length));
            _mouth?.SetAmplitude(rms);
        }
    }
}
