using FalsePositive.Dialogue;
using UnityEngine;

namespace FalsePositive.Cop
{
    /// <summary>
    /// Procedural body motion for the cop rig — breathing, idle head drift,
    /// and a weight shift — plus a "considering" lean while the sidecar is
    /// thinking. Exists because cop.glb ships with zero animation clips (an
    /// Avaturn T1 export) and Mixamo retargeting needs an interactive login
    /// this pipeline doesn't have; see ASSETS_TODO.md.
    ///
    /// Writes in LateUpdate, after any Animator that might be added later
    /// (e.g. real Mixamo clips) would run, so this layers as an additive
    /// offset on top of whatever the rest pose/animation supplies rather
    /// than fighting it. Caches each bone's LOCAL rotation at Awake as the
    /// baseline to offset from — this must run after the Blender-baked
    /// seated rest pose is already in place (i.e. don't call this before
    /// the rig is imported), since that rest pose is exactly what "no
    /// offset" means here.
    /// </summary>
    public sealed class CopIdleAnimator : MonoBehaviour
    {
        [Header("Bones (seated rest pose already baked into the FBX)")]
        [SerializeField] private Transform spine;
        [SerializeField] private Transform spine1;
        [SerializeField] private Transform neck;
        [SerializeField] private Transform head;

        [Header("Breathing (Spine1)")]
        [SerializeField] private float breathingHz = 0.25f;
        [SerializeField] private float breathingDegrees = 1.5f;

        [Header("Head/neck idle drift")]
        [SerializeField] private float driftDegrees = 2.5f;
        [SerializeField] private float driftSpeed = 0.3f;
        [SerializeField] private float glanceIntervalSeconds = 9f;
        [SerializeField] private float glanceDegrees = 6f;
        [SerializeField] private float glanceDurationSeconds = 1.4f;

        [Header("Weight shift (Spine)")]
        [SerializeField] private float weightShiftIntervalSeconds = 8f;
        [SerializeField] private float weightShiftDegrees = 2f;
        [SerializeField] private float weightShiftLerpSeconds = 2f;

        [Header("Dialogue-state reaction")]
        [SerializeField] private float considerLeanDegrees = 5f;
        [SerializeField] private float stateLerpSpeed = 4f;
        [SerializeField] private DialogueManager dialogueManager;

        private Quaternion _spineRest, _spine1Rest, _neckRest, _headRest;
        private float _driftSeedX, _driftSeedY;
        private float _glanceTimer;
        private float _glanceOffset;
        private float _glanceTarget;
        private float _weightTimer;
        private float _weightCurrent;
        private float _weightTarget;
        private float _stateLean;
        private float _stateLeanTarget;

        private void Awake()
        {
            if (spine != null) _spineRest = spine.localRotation;
            if (spine1 != null) _spine1Rest = spine1.localRotation;
            if (neck != null) _neckRest = neck.localRotation;
            if (head != null) _headRest = head.localRotation;

            // Random per-instance seed so multiple cops (if ever) don't
            // breathe/drift in lockstep.
            _driftSeedX = Random.value * 100f;
            _driftSeedY = Random.value * 100f + 50f;
            _glanceTimer = Random.Range(0f, glanceIntervalSeconds);
            _weightTimer = Random.Range(0f, weightShiftIntervalSeconds);
        }

        private void OnEnable()
        {
            if (dialogueManager != null) dialogueManager.StateChanged += OnDialogueStateChanged;
        }

        private void OnDisable()
        {
            if (dialogueManager != null) dialogueManager.StateChanged -= OnDialogueStateChanged;
        }

        private void OnDialogueStateChanged(DialogueState state)
        {
            // Lean forward while the sidecar is thinking (covers turn
            // latency — plan section 0's "considering" beat); settle back
            // once the reply starts playing or while listening.
            _stateLeanTarget = state == DialogueState.Uploading ? 1f : 0f;
        }

        private void LateUpdate()
        {
            float t = Time.time;

            if (spine1 != null)
            {
                float breath = Mathf.Sin(t * breathingHz * Mathf.PI * 2f) * breathingDegrees;
                spine1.localRotation = _spine1Rest * Quaternion.Euler(breath, 0f, 0f);
            }

            UpdateWeightShift();
            if (spine != null)
            {
                float lean = Mathf.Lerp(_stateLean, _stateLeanTarget, Time.deltaTime * stateLerpSpeed);
                _stateLean = lean;
                float leanDegrees = lean * considerLeanDegrees;
                spine.localRotation = _spineRest * Quaternion.Euler(leanDegrees, 0f, _weightCurrent);
            }

            UpdateGlance();
            if (neck != null)
            {
                float driftX = (Mathf.PerlinNoise(_driftSeedX, t * driftSpeed) - 0.5f) * 2f * driftDegrees;
                float driftY = (Mathf.PerlinNoise(_driftSeedY, t * driftSpeed) - 0.5f) * 2f * driftDegrees;
                neck.localRotation = _neckRest * Quaternion.Euler(driftX * 0.4f, driftY * 0.4f + _glanceOffset * 0.5f, 0f);
            }
            if (head != null)
            {
                float driftX = (Mathf.PerlinNoise(_driftSeedX, t * driftSpeed + 7f) - 0.5f) * 2f * driftDegrees;
                float driftY = (Mathf.PerlinNoise(_driftSeedY, t * driftSpeed + 7f) - 0.5f) * 2f * driftDegrees;
                head.localRotation = _headRest * Quaternion.Euler(driftX * 0.6f, driftY * 0.6f + _glanceOffset, 0f);
            }
        }

        private void UpdateWeightShift()
        {
            _weightTimer += Time.deltaTime;
            if (_weightTimer >= weightShiftIntervalSeconds)
            {
                _weightTimer = 0f;
                _weightTarget = Random.Range(-weightShiftDegrees, weightShiftDegrees);
            }
            _weightCurrent = Mathf.Lerp(_weightCurrent, _weightTarget, Time.deltaTime / Mathf.Max(weightShiftLerpSeconds, 0.01f));
        }

        private void UpdateGlance()
        {
            _glanceTimer += Time.deltaTime;
            if (_glanceTimer >= glanceIntervalSeconds)
            {
                _glanceTimer = 0f;
                _glanceTarget = Random.Range(-glanceDegrees, glanceDegrees);
            }
            // Glance eases toward its target then back to 0 within
            // glanceDurationSeconds of triggering, via a simple decay once
            // past that window — cheap approximation, not a real curve.
            float towardTarget = _glanceTimer < glanceDurationSeconds ? _glanceTarget : 0f;
            _glanceOffset = Mathf.Lerp(_glanceOffset, towardTarget, Time.deltaTime * 2f);
        }
    }
}
