using FalsePositive.Core;
using FalsePositive.Menu;
using UnityEngine;

namespace FalsePositive.Player
{
    /// <summary>
    /// Seated-state look. PlayerStateController guarantees the shared
    /// Camera is parented under the current SeatAnchor's CameraMount
    /// whenever this component is enabled, so its localRotation is
    /// inherently relative to seat-forward — no base-rotation math needed.
    ///
    /// Yaw/pitch OFFSETS are accumulated frame to frame and each clamped to
    /// +/-maxAngle independently. Clamping the accumulated total — not the
    /// per-frame delta — is what makes the cap unbreakable regardless of
    /// how hard or how long the mouse is pushed; clamping the delta instead
    /// is the usual way this kind of limit silently creeps past its bound.
    /// Clamping each axis independently gives a square cone (diagonal
    /// reaches ~1.4x maxAngle) rather than a circular one — intentional,
    /// matches "max N degrees on any axis" literally.
    /// </summary>
    public sealed class SeatedCameraRig : MonoBehaviour
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private InterrogationConfig config;

        private float _yawOffset;
        private float _pitchOffset;

        /// <summary>Recenters the look offset — called every time the player sits down, so they always start centered on the cop.</summary>
        public void ResetLook()
        {
            _yawOffset = 0f;
            _pitchOffset = 0f;
            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.identity;
            }
        }

        private void Update()
        {
            if (playerCamera == null) return;

            // SettingsStore.MouseSensitivity is a runtime multiplier, never
            // written into InterrogationConfig — see SettingsStore's own
            // doc-comment for why a play-mode ScriptableObject write would
            // wrongly persist to the shipped .asset.
            Vector2 look = input.LookDelta * config.lookSensitivity * SettingsStore.MouseSensitivity;
            float max = config.seatedMaxLookAngleDegrees;
            float pitchSign = SettingsStore.InvertY ? 1f : -1f;

            _yawOffset = Mathf.Clamp(_yawOffset + look.x, -max, max);
            _pitchOffset = Mathf.Clamp(_pitchOffset + pitchSign * look.y, -max, max);

            playerCamera.localRotation = Quaternion.Euler(_pitchOffset, _yawOffset, 0f);
        }
    }
}
