using System.Collections;
using FalsePositive.Cutscene;
using FalsePositive.Flow;
using UnityEngine;

namespace FalsePositive.Player
{
    /// <summary>
    /// Adds a woozy head-sway on top of SeatedCameraRig's look rotation while
    /// CutsceneId.Wake plays -- the player waking up disoriented at the
    /// interrogation table. Same Started/Finished ramp shape as
    /// Cutscene/DrunkCutsceneBinder (which drives the colour/trail post-process
    /// and the slow/echoed VO for the same cutscene), but this lives on the
    /// Player rig in Interrogation instead of _Persistent: it has to touch the
    /// scene-local camera transform SeatedCameraRig also drives, which a
    /// _Persistent singleton can't hold a reference to. Interrogation's Player
    /// rig (this GameObject, SeatedCameraRig, etc.) is hand-authored in the
    /// scene rather than built by Editor/ProjectBootstrapBuilder.cs -- add this
    /// component the same way if it's ever missing after a scene rebuild.
    ///
    /// Runs in LateUpdate, strictly after SeatedCameraRig.Update() has already
    /// written playerCamera.localRotation from mouse look this frame -- this
    /// multiplies an additional sway rotation on top rather than fighting
    /// SeatedCameraRig for ownership of the field.
    /// </summary>
    public sealed class DrunkCameraSway : MonoBehaviour
    {
        [SerializeField] private CutsceneId cutsceneId = CutsceneId.Wake;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private float rampInSeconds = 0.4f;
        [SerializeField] private float rampOutSeconds = 1.5f;

        [SerializeField] private float yawDegrees = 3.5f;
        [SerializeField] private float pitchDegrees = 2f;
        [SerializeField] private float rollDegrees = 2.5f;

        // Deliberately mismatched, non-integer frequencies (and a phase offset
        // per axis) so the three axes never line up on a shared cycle -- equal
        // frequencies would read as a metronome swinging back and forth, not
        // disorientation.
        [SerializeField] private float yawHz = 0.22f;
        [SerializeField] private float pitchHz = 0.31f;
        [SerializeField] private float rollHz = 0.17f;

        private CutsceneDirector _cutscenes;
        private Coroutine _rampRoutine;
        private float _intensity;
        private float _clock;

        private void OnEnable()
        {
            _cutscenes = FindAnyObjectByType<CutsceneDirector>();
            if (_cutscenes != null)
            {
                _cutscenes.Started += HandleStarted;
                _cutscenes.Finished += HandleFinished;
            }
        }

        private void OnDisable()
        {
            if (_cutscenes != null)
            {
                _cutscenes.Started -= HandleStarted;
                _cutscenes.Finished -= HandleFinished;
            }
            // Never leave the camera swaying if this component (or the scene it
            // lives in) goes away mid-cutscene.
            _intensity = 0f;
        }

        private void HandleStarted(CutsceneId id)
        {
            if (id != cutsceneId) return;
            StartRamp(1f, rampInSeconds);
        }

        private void HandleFinished(CutsceneId id)
        {
            if (id != cutsceneId) return;
            StartRamp(0f, rampOutSeconds);
        }

        private void StartRamp(float target, float seconds)
        {
            if (_rampRoutine != null) StopCoroutine(_rampRoutine);
            _rampRoutine = StartCoroutine(RampRoutine(target, seconds));
        }

        private IEnumerator RampRoutine(float target, float seconds)
        {
            float start = _intensity;
            if (seconds <= 0f)
            {
                _intensity = target;
                yield break;
            }

            float t = 0f;
            while (t < seconds)
            {
                t += Time.deltaTime;
                _intensity = Mathf.Lerp(start, target, t / seconds);
                yield return null;
            }
            _intensity = target;
        }

        private void LateUpdate()
        {
            if (playerCamera == null || _intensity <= 0.0001f) return;
            _clock += Time.deltaTime;

            float turns = _clock * Mathf.PI * 2f;
            float yaw = Mathf.Sin(turns * yawHz) * yawDegrees * _intensity;
            float pitch = Mathf.Sin(turns * pitchHz + 1.3f) * pitchDegrees * _intensity;
            float roll = Mathf.Sin(turns * rollHz + 2.7f) * rollDegrees * _intensity;

            playerCamera.localRotation *= Quaternion.Euler(pitch, yaw, roll);
        }
    }
}
