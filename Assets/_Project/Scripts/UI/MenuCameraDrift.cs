using UnityEngine;

namespace FalsePositive.UI
{
    /// <summary>
    /// Very slow, never-repeating drift for the main menu's 3D backdrop camera
    /// (docs/TRACK_A_EDITOR_SETUP.md §3: "a still shot or very slow camera
    /// drift"). Caches the base pose in Awake and offsets from it in
    /// LateUpdate on unscaled time, so SceneRouter deactivating/reactivating
    /// MainMenu's root (menu -> gameplay -> back to menu) resumes the drift
    /// from wherever the clock is rather than resetting it — Time.unscaledTime
    /// keeps advancing while this object is inactive, which is exactly the
    /// "resumes, doesn't reset" behaviour the plan calls for.
    ///
    /// Position and rotation each get their own period, chosen mutually
    /// prime-ish so the composite motion never visibly repeats — a single
    /// shared period reads mechanical within about 15 seconds. Amplitudes are
    /// tiny (well under a metre, well under a degree) — "very slow drift", not
    /// a dolly move.
    /// </summary>
    public sealed class MenuCameraDrift : MonoBehaviour
    {
        [SerializeField] private Vector3 positionAmplitude = new Vector3(0.55f, 0.22f, 0.35f);
        [SerializeField] private Vector3 positionPeriod = new Vector3(41f, 27f, 33f);
        [SerializeField] private float pitchAmplitude = 0.5f;
        [SerializeField] private float pitchPeriod = 23f;
        [SerializeField] private float yawAmplitude = 0.9f;
        [SerializeField] private float yawPeriod = 37f;

        private Vector3 _basePosition;
        private Quaternion _baseRotation;

        private void Awake()
        {
            _basePosition = transform.localPosition;
            _baseRotation = transform.localRotation;
        }

        private void LateUpdate()
        {
            float t = Time.unscaledTime;
            const float twoPi = Mathf.PI * 2f;

            Vector3 offset = new Vector3(
                Mathf.Sin(t * twoPi / positionPeriod.x) * positionAmplitude.x,
                Mathf.Sin(t * twoPi / positionPeriod.y) * positionAmplitude.y,
                Mathf.Sin(t * twoPi / positionPeriod.z) * positionAmplitude.z);
            transform.localPosition = _basePosition + offset;

            float pitch = Mathf.Sin(t * twoPi / pitchPeriod) * pitchAmplitude;
            float yaw = Mathf.Sin(t * twoPi / yawPeriod) * yawAmplitude;
            transform.localRotation = _baseRotation * Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
