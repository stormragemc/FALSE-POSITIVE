using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace FalsePositive.Player
{
    /// <summary>
    /// Owns the single generated InputSystem_Actions instance for the whole
    /// project. Every other script subscribes to the events/values exposed
    /// here — nothing else should touch the input asset directly.
    ///
    /// Two gotchas found in the shipped InputSystem_Actions asset, handled
    /// here rather than by editing the asset:
    ///  - "Interact" has a Hold interaction at the action level, so
    ///    `performed` doesn't fire until ~0.4s of hold. We subscribe to
    ///    `started` instead, which fires immediately on press.
    ///  - "Look" binds &lt;Pointer&gt;/delta with no processors — the delta is
    ///    already a per-frame value. Never multiply it by Time.deltaTime,
    ///    which would make aiming framerate-dependent.
    /// </summary>
    public sealed class PlayerInputRouter : MonoBehaviour
    {
        public event Action InteractPressed;

        private InputSystem_Actions _actions;

        public Vector2 MoveValue => _actions != null ? _actions.Player.Move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 LookDelta => _actions != null ? _actions.Player.Look.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintHeld => _actions != null && _actions.Player.Sprint.IsPressed();

        private void Awake()
        {
            _actions = new InputSystem_Actions();
            _actions.Player.Interact.started += OnInteractStarted;
        }

        private void OnEnable()
        {
            _actions?.Player.Enable();
        }

        private void OnDisable()
        {
            _actions?.Player.Disable();
        }

        private void OnDestroy()
        {
            if (_actions == null) return;
            _actions.Player.Interact.started -= OnInteractStarted;
            _actions.Dispose();
        }

        private void OnInteractStarted(InputAction.CallbackContext ctx)
        {
            InteractPressed?.Invoke();
        }
    }
}
