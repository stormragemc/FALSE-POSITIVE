using FalsePositive.Core;
using UnityEngine;

namespace FalsePositive.Player
{
    /// <summary>
    /// Standing-state movement and look: CharacterController WASD from
    /// Move, yaw applied to the player body, pitch applied to the shared
    /// Camera (which PlayerStateController guarantees is parented under
    /// this rig's mount point whenever this component is enabled), clamped
    /// to +/-standingPitchClampDegrees. Yaw itself is free/unclamped.
    ///
    /// Uses per-frame polling of PlayerInputRouter's exposed values rather
    /// than InputAction event subscriptions, so simply setting `enabled =
    /// false` fully stops this rig from moving the player or the camera —
    /// no separate "unsubscribe input handlers" step needed here (that
    /// gotcha applies to event-based subscriptions, which this rig doesn't
    /// use).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class FreeLookCameraRig : MonoBehaviour
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private InterrogationConfig config;
        [SerializeField] private float moveSpeed = 3.2f;
        [SerializeField] private float sprintMultiplier = 1.6f;

        private CharacterController _controller;
        private float _pitch;
        private float _yaw;

        /// <summary>Seeds yaw (and immediately applies it to the body) — used when standing up so the view doesn't snap.</summary>
        public void SeedYaw(float worldYawDegrees)
        {
            _yaw = worldYawDegrees;
            _pitch = 0f;
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
        }

        private void Update()
        {
            Vector2 look = input.LookDelta * config.lookSensitivity;
            _yaw += look.x;
            _pitch = Mathf.Clamp(_pitch - look.y, -config.standingPitchClampDegrees, config.standingPitchClampDegrees);

            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (playerCamera != null)
            {
                playerCamera.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }

            Vector2 move = input.MoveValue;
            float speed = moveSpeed * (input.SprintHeld ? sprintMultiplier : 1f);
            Vector3 worldMove = (transform.right * move.x + transform.forward * move.y) * speed;
            _controller.SimpleMove(worldMove);
        }
    }
}
