using UnityEngine;

namespace FalsePositive.Cop
{
    /// <summary>
    /// Middle fidelity tier: rotates a jaw bone based on live playback
    /// amplitude. Not universal — many rigs (Mixamo, Synty) don't ship a
    /// jaw bone at all, in which case use TextureSwapCopMouth instead.
    ///
    /// closedLocalEuler/openLocalEuler are offsets relative to the bone's
    /// own rest rotation captured at Awake, not absolute local rotations.
    /// This matters because an FBX-imported bone's local rotation at rest
    /// is very rarely identity — this rig's Jaw bone rests at roughly
    /// (56, 180, 180) Euler (an artifact of Blender's bone-roll convention
    /// surviving the FBX export), not zero. Treating closedLocalEuler as
    /// an absolute Quaternion.Euler(0,0,0) would have snapped the jaw to a
    /// completely wrong orientation on every Update() the component is
    /// enabled, not just while the officer speaks.
    /// </summary>
    public sealed class JawBoneCopMouth : MonoBehaviour, ICopMouth
    {
        [SerializeField] private Transform jawBone;
        [SerializeField] private Vector3 closedLocalEuler = Vector3.zero;
        // Sign is a best guess, not empirically confirmed in Unity: Blender's
        // pose-bone test (Tools/blender/rig_cop.py, same rest-relative
        // convention) used -10 deg on local X and rendered a clean opening
        // with no mesh tearing, but FBX export can flip per-bone axis
        // handedness, so Unity's correct sign might be the opposite of
        // Blender's. Could not verify live in-Editor: Time.frameCount never
        // advances past 1 in Play mode in this environment, which SkinnedMeshRenderer's
        // bone-matrix cache depends on, so no scripted test (render capture or
        // BakeMesh) reflects a live jawBone rotation change outside a real
        // ticking player loop. Watch the very first real Play session: if the
        // jaw rotates up into the skull instead of down/open, negate this.
        [SerializeField] private Vector3 openLocalEuler = new Vector3(10f, 0f, 0f);
        [SerializeField] private float amplitudeGain = 6f;
        [SerializeField] private float smoothing = 12f;

        private float _targetOpen;
        private float _currentOpen;
        private Quaternion _restRotation;
        private bool _haveRest;

        private void Awake()
        {
            CaptureRestRotation();
        }

        private void CaptureRestRotation()
        {
            if (jawBone == null || _haveRest) return;
            _restRotation = jawBone.localRotation;
            _haveRest = true;
        }

        public void Begin(AudioSource source)
        {
            CaptureRestRotation(); // in case Begin() fires before Awake (e.g. Add + call same frame)
            _targetOpen = 0f;
        }

        public void SetAmplitude(float rms)
        {
            _targetOpen = Mathf.Clamp01(rms * amplitudeGain);
        }

        public void Stop()
        {
            _targetOpen = 0f;
        }

        private void Update()
        {
            if (jawBone == null) return;
            CaptureRestRotation();
            _currentOpen = Mathf.Lerp(_currentOpen, _targetOpen, Time.deltaTime * smoothing);
            Quaternion offset = Quaternion.Euler(Vector3.Lerp(closedLocalEuler, openLocalEuler, _currentOpen));
            jawBone.localRotation = _restRotation * offset;
        }
    }
}
