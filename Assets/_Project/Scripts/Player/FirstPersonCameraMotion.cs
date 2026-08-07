using FalsePositive.Cutscene;
using UnityEngine;

namespace FalsePositive.Player
{
    /// <summary>
    /// Adds subtle, procedural head motion after the active look rig has
    /// written its camera pose. The position offset is additive, so scripted
    /// changes to the camera's base height remain authoritative.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FirstPersonCameraMotion : MonoBehaviour
    {
        [Header("Walking")]
        [SerializeField] private float referenceWalkSpeed = 3.2f;
        [SerializeField] private float walkFrequency = 1.7f;
        [SerializeField] private float sprintFrequency = 2.35f;
        [SerializeField] private float verticalAmplitude = 0.032f;
        [SerializeField] private float lateralAmplitude = 0.012f;
        [SerializeField] private float pitchAmplitude = 0.18f;
        [SerializeField] private float rollAmplitude = 0.4f;
        [SerializeField] private float startSmoothTime = 0.12f;
        [SerializeField] private float stopSmoothTime = 0.28f;

        [Header("Standing Idle")]
        [SerializeField] private float idleFrequency = 0.085f;
        [SerializeField] private float idleVerticalAmplitude = 0.003f;
        [SerializeField] private float idleLateralAmplitude = 0.004f;
        [SerializeField] private float idlePitchAmplitude = 0.1f;
        [SerializeField] private float idleRollAmplitude = 0.14f;
        [SerializeField] private float idleSmoothTime = 0.8f;

        private CharacterController _controller;
        private FreeLookCameraRig _standingRig;
        private Transform _motionSource;
        private Transform _lastParent;
        private CutsceneDirector _cutsceneDirector;
        private Vector3 _baseLocalPosition;
        private Vector3 _lastAppliedLocalPosition;
        private Quaternion _lastAppliedLocalRotation;
        private Quaternion _lastRotationOffset = Quaternion.identity;
        private Vector3 _lastSourcePosition;
        private float _walkPhase;
        private float _idlePhase;
        private float _walkBlend;
        private float _walkBlendVelocity;
        private float _idleBlend;
        private float _idleBlendVelocity;
        private bool _hasAppliedPosition;

        private void Awake()
        {
            _controller = GetComponentInParent<CharacterController>();
            _standingRig = GetComponentInParent<FreeLookCameraRig>();
            _motionSource = _controller != null ? _controller.transform : transform.parent;
            CaptureBasePose();
            if (_motionSource != null) _lastSourcePosition = _motionSource.position;
        }

        private void OnEnable()
        {
            CaptureBasePose();
            if (_motionSource != null) _lastSourcePosition = _motionSource.position;
        }

        private void OnDisable()
        {
            transform.localPosition = _baseLocalPosition;
            _hasAppliedPosition = false;
            _walkBlend = 0f;
            _idleBlend = 0f;
        }

        private void LateUpdate()
        {
            if (transform.parent != _lastParent)
            {
                CaptureBasePose();
            }
            else if (_hasAppliedPosition &&
                     (transform.localPosition - _lastAppliedLocalPosition).sqrMagnitude > 0.00000001f)
            {
                // A cutscene or another camera system supplied a new base pose
                // since our previous LateUpdate. Follow it instead of fighting it.
                _baseLocalPosition = transform.localPosition;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f) return;

            bool standing = _standingRig == null || _standingRig.enabled;
            bool grounded = _controller == null || (_controller.enabled && _controller.isGrounded);
            float horizontalSpeed = GetHorizontalSpeed(deltaTime);
            bool walking = standing && grounded && horizontalSpeed > 0.08f;

            float speedRatio = Mathf.Clamp(horizontalSpeed / Mathf.Max(0.01f, referenceWalkSpeed), 0f, 1.6f);
            float walkTarget = walking ? Mathf.Clamp01(speedRatio) : 0f;
            float walkSmoothTime = walkTarget > _walkBlend ? startSmoothTime : stopSmoothTime;
            _walkBlend = Mathf.SmoothDamp(_walkBlend, walkTarget, ref _walkBlendVelocity,
                Mathf.Max(0.01f, walkSmoothTime), Mathf.Infinity, deltaTime);

            bool scripted = IsCutscenePlaying();
            float idleTarget = standing && !walking && !scripted ? 1f : 0f;
            _idleBlend = Mathf.SmoothDamp(_idleBlend, idleTarget, ref _idleBlendVelocity,
                Mathf.Max(0.01f, idleSmoothTime), Mathf.Infinity, deltaTime);

            float cadenceT = Mathf.InverseLerp(1f, 1.6f, speedRatio);
            float cadence = Mathf.Lerp(walkFrequency, sprintFrequency, cadenceT);
            if (walking || _walkBlend > 0.001f) _walkPhase += deltaTime * cadence * Mathf.PI * 2f;
            _idlePhase += deltaTime * idleFrequency * Mathf.PI * 2f;

            float stride = Mathf.Sin(_walkPhase);
            float step = Mathf.Sin(_walkPhase * 2f);
            Vector3 walkPosition = new Vector3(stride * lateralAmplitude, step * verticalAmplitude, 0f) * _walkBlend;
            Vector3 walkRotation = new Vector3(step * pitchAmplitude, 0f, -stride * rollAmplitude) * _walkBlend;

            float idleWave = Mathf.Sin(_idlePhase);
            float idleCounterWave = Mathf.Cos(_idlePhase * 0.73f);
            Vector3 idlePosition = new Vector3(idleCounterWave * idleLateralAmplitude,
                idleWave * idleVerticalAmplitude, 0f) * _idleBlend;
            Vector3 idleRotation = new Vector3(idleWave * idlePitchAmplitude, 0f,
                idleCounterWave * idleRollAmplitude) * _idleBlend;

            Vector3 positionOffset = walkPosition + idlePosition;
            Quaternion rotationOffset = Quaternion.Euler(walkRotation + idleRotation);
            Quaternion baseRotation = Quaternion.Angle(transform.localRotation, _lastAppliedLocalRotation) < 0.001f
                ? transform.localRotation * Quaternion.Inverse(_lastRotationOffset)
                : transform.localRotation;
            transform.localPosition = _baseLocalPosition + positionOffset;
            transform.localRotation = baseRotation * rotationOffset;
            _lastAppliedLocalPosition = transform.localPosition;
            _lastAppliedLocalRotation = transform.localRotation;
            _lastRotationOffset = rotationOffset;
            _hasAppliedPosition = true;
        }

        private float GetHorizontalSpeed(float deltaTime)
        {
            Vector3 sourcePosition = _motionSource != null ? _motionSource.position : transform.position;
            Vector3 displacement = sourcePosition - _lastSourcePosition;
            _lastSourcePosition = sourcePosition;
            displacement.y = 0f;

            float measuredSpeed = displacement.magnitude / deltaTime;
            // Ignore teleports and scene/cutscene placement jumps.
            if (measuredSpeed > referenceWalkSpeed * 3f) measuredSpeed = 0f;

            if (_controller != null && _controller.enabled)
            {
                Vector3 velocity = _controller.velocity;
                velocity.y = 0f;
                measuredSpeed = Mathf.Max(measuredSpeed, velocity.magnitude);
            }
            return measuredSpeed;
        }

        private bool IsCutscenePlaying()
        {
            if (_cutsceneDirector == null) _cutsceneDirector = FindAnyObjectByType<CutsceneDirector>();
            return _cutsceneDirector != null && _cutsceneDirector.IsPlaying;
        }

        private void CaptureBasePose()
        {
            _lastParent = transform.parent;
            _baseLocalPosition = transform.localPosition;
            _lastAppliedLocalPosition = transform.localPosition;
            _lastAppliedLocalRotation = transform.localRotation;
            _lastRotationOffset = Quaternion.identity;
            _hasAppliedPosition = false;
            _walkBlend = 0f;
            _walkBlendVelocity = 0f;
            _idleBlend = 0f;
            _idleBlendVelocity = 0f;
        }
    }
}
