using System;
using System.Collections;
using FalsePositive.Core;
using FalsePositive.UI;
using UnityEngine;

namespace FalsePositive.Player
{
    public enum PlayerState
    {
        Standing,
        Seated,
        Transitioning,
    }

    /// <summary>
    /// Owns the sit/stand FSM and the E-press camera handoff. Re-parents the
    /// single scene Camera between the free-roam rig's mount and the current
    /// seat's mount rather than juggling two active cameras — that avoids
    /// ever having two active AudioListeners (console warning, broken
    /// positional audio) and halves the state to manage in URP.
    ///
    /// Standing up mid-conversation-turn does NOT abort an in-flight
    /// sidecar request — DialogueManager keeps the turn running regardless
    /// of player state; only mic capture and the meter gate off while
    /// seated is false. Standing while the cop is speaking is a legitimate
    /// player action, not an error state, so this controller never touches
    /// dialogue flow directly.
    /// </summary>
    public sealed class PlayerStateController : MonoBehaviour
    {
        [SerializeField] private PlayerInputRouter input;
        [SerializeField] private InterrogationConfig config;
        [SerializeField] private ScreenFader fader;
        [SerializeField] private Transform playerCamera;
        [SerializeField] private CharacterController characterController;
        [SerializeField] private FreeLookCameraRig freeLookRig;
        [SerializeField] private Transform freeLookCameraMount;
        [SerializeField] private SeatedCameraRig seatedRig;
        [SerializeField] private SeatAnchor currentSeat;

        /// <summary>true = the player just sat down (mic UI on, VAD armed); false = just stood up (mic UI off, VAD gated).</summary>
        public event Action<bool> SeatedChanged;

        public PlayerState State { get; private set; } = PlayerState.Standing;

        private bool _nearSeat;

        private void OnEnable()
        {
            input.InteractPressed += OnInteractPressed;
        }

        private void OnDisable()
        {
            input.InteractPressed -= OnInteractPressed;
        }

        private void Update()
        {
            if (State != PlayerState.Standing || currentSeat == null)
            {
                _nearSeat = false;
                return;
            }
            float dist = Vector3.Distance(transform.position, currentSeat.InteractPoint.position);
            _nearSeat = dist <= currentSeat.InteractRadius;
        }

        private void OnInteractPressed()
        {
            switch (State)
            {
                case PlayerState.Standing when _nearSeat:
                    StartCoroutine(SitRoutine());
                    break;
                case PlayerState.Seated:
                    StartCoroutine(StandRoutine());
                    break;
            }
        }

        /// <summary>Called once by GameBootstrap once the sidecar is ready — the game begins seated with no fade.</summary>
        public void BeginSeated()
        {
            State = PlayerState.Seated;
            freeLookRig.enabled = false;
            characterController.enabled = false;

            playerCamera.SetParent(currentSeat.CameraMount, worldPositionStays: false);
            playerCamera.localPosition = Vector3.zero;
            playerCamera.localRotation = Quaternion.identity;

            seatedRig.ResetLook();
            seatedRig.enabled = true;

            SeatedChanged?.Invoke(true);
        }

        private IEnumerator SitRoutine()
        {
            State = PlayerState.Transitioning;

            yield return fader.FadeToBlack(config.fadeDurationSeconds);
            yield return null; // one full frame fully black before swapping anything

            freeLookRig.enabled = false;
            characterController.enabled = false;

            playerCamera.SetParent(currentSeat.CameraMount, worldPositionStays: false);
            playerCamera.localPosition = Vector3.zero;
            playerCamera.localRotation = Quaternion.identity;

            seatedRig.ResetLook();
            seatedRig.enabled = true;

            SeatedChanged?.Invoke(true);

            yield return fader.FadeFromBlack(config.fadeDurationSeconds);
            State = PlayerState.Seated;
        }

        private IEnumerator StandRoutine()
        {
            State = PlayerState.Transitioning;

            yield return fader.FadeToBlack(config.fadeDurationSeconds);
            yield return null;

            seatedRig.enabled = false;

            // Read world yaw BEFORE detaching from the seat mount, then
            // teleport the CharacterController BEFORE re-enabling it (a
            // CharacterController fights position changes made while
            // enabled), and seed the free-look yaw from that captured
            // value so the view doesn't snap when the fade lifts.
            float seatWorldYaw = playerCamera.eulerAngles.y;

            characterController.enabled = false;
            transform.SetPositionAndRotation(currentSeat.ExitPose.position, currentSeat.ExitPose.rotation);

            playerCamera.SetParent(freeLookCameraMount, worldPositionStays: false);
            playerCamera.localPosition = Vector3.zero;
            playerCamera.localRotation = Quaternion.identity;

            freeLookRig.SeedYaw(seatWorldYaw);
            characterController.enabled = true;
            freeLookRig.enabled = true;

            SeatedChanged?.Invoke(false);

            yield return fader.FadeFromBlack(config.fadeDurationSeconds);
            State = PlayerState.Standing;
        }
    }
}
