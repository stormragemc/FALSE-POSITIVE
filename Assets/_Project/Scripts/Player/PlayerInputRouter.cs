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
        private bool _moveGated;
        private bool _lookGated;

        // Zeroed out while gated, never disabled — OnDisable/Disable() would
        // kill the whole Player action map including Interact, and cutscene-
        // adjacent gameplay interludes need Move/Look suppressed while E is
        // still live (Scripts/Cutscene/CutsceneStage.cs).
        public Vector2 MoveValue => !_moveGated && _actions != null ? _actions.Player.Move.ReadValue<Vector2>() : Vector2.zero;
        public Vector2 LookDelta => !_lookGated && _actions != null ? _actions.Player.Look.ReadValue<Vector2>() : Vector2.zero;
        public bool SprintHeld => !_moveGated && _actions != null && _actions.Player.Sprint.IsPressed();

        /// <summary>Suppresses Move/Sprint (FreeLookCameraRig reads these every
        /// Update via MoveValue/SprintHeld) while leaving Look and Interact
        /// live. Used for the M2 carry interlude, where the player must be
        /// able to look around and press E but shouldn't wander off.</summary>
        public void SetMoveGated(bool gated) => _moveGated = gated;

        /// <summary>Suppresses Look (FreeLookCameraRig.LookDelta) while leaving
        /// Move/Sprint/Interact live. Used for the M2 lift interlude — the
        /// camera is seeded onto Nick's body by the cutscene, then look is
        /// released so a bad aim is recoverable by mouse alone while movement
        /// stays gated.</summary>
        public void SetLookGated(bool gated) => _lookGated = gated;

        /// <summary>Gates both Move and Look together — the common case for a
        /// fully scripted beat (e.g. the M1/M2 walk-outs) where the player
        /// should neither walk away nor look away from the staged approach.</summary>
        public void SetMovementGated(bool gated)
        {
            _moveGated = gated;
            _lookGated = gated;
        }

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
