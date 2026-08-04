using UnityEngine;
using UnityEngine.InputSystem;

namespace FalsePositive.Player
{
    /// <summary>
    /// Lightweight controller used only by the standalone Cabin Aftermath level.
    /// It deliberately has no dependency on interrogation state or the sidecar.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class CabinFirstPersonController : MonoBehaviour
    {
        [SerializeField] private Transform view;
        [SerializeField] private float walkSpeed = 3.2f;
        [SerializeField] private float sprintMultiplier = 1.5f;
        [SerializeField] private float mouseSensitivity = 0.12f;

        private CharacterController _controller;
        private float _pitch;
        private float _verticalVelocity;

        public void SetView(Transform cameraView)
        {
            view = cameraView;
        }

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void Update()
        {
            Vector2 look = Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
            transform.Rotate(0f, look.x * mouseSensitivity, 0f);
            _pitch = Mathf.Clamp(_pitch - look.y * mouseSensitivity, -80f, 80f);
            if (view != null) view.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            Vector2 input = Vector2.zero;
            if (Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed) input.y -= 1f;
                if (Keyboard.current.dKey.isPressed) input.x += 1f;
                if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            }

            float speed = walkSpeed;
            if (Keyboard.current != null && Keyboard.current.leftShiftKey.isPressed) speed *= sprintMultiplier;
            Vector3 movement = (transform.forward * input.y + transform.right * input.x).normalized * speed;
            _verticalVelocity = _controller.isGrounded ? -1f : _verticalVelocity + Physics.gravity.y * Time.deltaTime;
            movement.y = _verticalVelocity;
            _controller.Move(movement * Time.deltaTime);
        }
    }
}
