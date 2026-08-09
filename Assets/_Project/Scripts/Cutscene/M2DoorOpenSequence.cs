using System;
using FalsePositive.CabinNight;
using FalsePositive.Player;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Runs M2_DoorOpen.playable — the arm-reaches / door-swings / camera-walks-
    /// out-and-orbits-the-body cinematic — and mirrors its result onto the real
    /// player rig.
    ///
    /// Why a proxy instead of animating the player directly: a Timeline
    /// AnimationTrack binds to an *Animator*, and CabinNight.CabinAnimatorDriver
    /// resolves its target with GetComponentInChildren&lt;Animator&gt;(), which
    /// includes self — so an Animator added to the player root would be silently
    /// hijacked and every player pose in the game would break. Instead the
    /// camera track drives a bare three-node proxy and this component copies it
    /// onto the player each LateUpdate:
    ///
    ///   M2_DoorCam            Animator lives here, parked at the world origin
    ///   +- M2_DoorCam_Root    animated localPosition + localEulerAnglesRaw.y
    ///      +- M2_DoorCam_Pitch animated localEulerAnglesRaw.x
    ///
    /// Nothing animates the Animator's *own* transform, so AnimationTrack's
    /// trackOffset modes never come into it; and because M2_DoorCam sits at the
    /// origin with identity rotation, the baked local values are literally world
    /// values. The arm track still binds to the player's own Body Animator,
    /// which is a child and therefore safe.
    ///
    /// Owns the suppress/restore of everything that would otherwise fight the
    /// baked keys, and hands control back seeded from the final proxy pose so
    /// there is no snap on the frame the player gets the camera again.
    ///
    /// Staged into Memory_CabinMorning by Editor.M2DoorTimelineBuilder; driven by
    /// CutsceneStage.OutIntoTheSnow.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class M2DoorOpenSequence : MonoBehaviour
    {
        [SerializeField] private PlayableDirector director;

        /// <summary>M2_DoorCam_Root — world position + yaw the player root is
        /// copied from. Its parent sits at the origin, so its local keys read
        /// straight out as world values.</summary>
        [SerializeField] private Transform proxyRoot;

        /// <summary>M2_DoorCam_Pitch — localRotation.x the FirstPersonView is copied from.</summary>
        [SerializeField] private Transform proxyPitch;

        /// <summary>Where the player is snapped before the first frame plays.
        /// This — not where the player happened to be standing when they used
        /// the key — is what makes the framing identical every playthrough, and
        /// it is what puts the knob under the authored reach pose. Roughly
        /// 0.6 m inside DoorwayCentre (-3.75, 0, -3.75) along the inward normal
        /// of the 45-degree chamfered wall the front door sits in.</summary>
        [SerializeField] private Vector3 doorMark = new Vector3(-3.33f, 0f, -3.33f);

        /// <summary>Yaw the player faces on the door mark: straight out through
        /// the chamfer, i.e. along (-1, 0, -1). The camera track takes over from
        /// here, but seeding it avoids a visible spin on frame one.</summary>
        [SerializeField] private float doorMarkYaw = 225f;

        /// <summary>Door rotation the swing track ends on. Duplicated from
        /// CutsceneStage's DoorClosedRotation/DoorOpenYawDegrees (both of which
        /// come from CabinV2Builder) so the door can be pinned open on handback
        /// rather than trusting a stopped PlayableDirector to leave an Animator's
        /// last evaluated pose in place.</summary>
        private static readonly Quaternion DoorOpenRotation =
            Quaternion.Euler(0f, 100f, 0f) * Quaternion.Euler(270f, 0f, 0f);

        public bool IsPlaying { get; private set; }

        private Action _onDone;
        private Transform _player;
        private Transform _view;
        private FreeLookCameraRig _rig;
        private PlayerInputRouter _input;
        private CharacterController _controller;
        private FirstPersonCameraMotion _headBob;
        private CabinFallRecovery _fallRecovery;
        private CabinAnimatorDriver _animatorDriver;
        private Transform _body;
        private Vector3 _bodyLocalPosition;
        private Quaternion _bodyLocalRotation;
        private Renderer _arm;

        /// <summary>Name of the viewmodel GameObject Editor.FirstPersonArmBuilder
        /// hangs off the player root — matched by string because Scripts/Editor is
        /// editor-only and unreachable from this assembly (the same constraint
        /// CabinNight.CabinAnimatorDriver documents for BodyYOffsetFor). Keep in
        /// sync with FirstPersonArmBuilder.ArmObjectName.</summary>
        private const string ArmObjectName = "FirstPersonArm";

        private void OnDisable()
        {
            // Scene teardown mid-sequence must not leave the player with no
            // camera rig and gated input.
            if (IsPlaying) Restore();
        }

        /// <summary>Starts the cinematic. onDone fires once the director has
        /// stopped and control is fully back with the player. Fires immediately
        /// if the sequence can't run, so a missing binding degrades to "no
        /// cinematic" rather than a soft-lock.</summary>
        public void Play(Action onDone)
        {
            if (IsPlaying)
            {
                Debug.LogWarning("[M2DoorOpenSequence] Play called while already playing — ignored.");
                return;
            }

            _onDone = onDone;

            if (director == null || director.playableAsset == null || proxyRoot == null || proxyPitch == null)
            {
                Debug.LogError("[M2DoorOpenSequence] Not wired (director/proxy missing) — run " +
                               "'Tools/False Positive/Bootstrap/T05 - Build M2 Door Timeline'. Skipping the cinematic.");
                Finish();
                return;
            }

            if (!BindingsResolve())
            {
                Finish();
                return;
            }

            GameObject playerGo = GameObject.Find("Player (Male - First Person)");
            if (playerGo == null)
            {
                Debug.LogError("[M2DoorOpenSequence] No 'Player (Male - First Person)' in the scene. Skipping the cinematic.");
                Finish();
                return;
            }

            _player = playerGo.transform;
            _view = _player.Find("FirstPersonView");
            _rig = playerGo.GetComponent<FreeLookCameraRig>();
            _input = playerGo.GetComponent<PlayerInputRouter>();
            _controller = playerGo.GetComponent<CharacterController>();
            _headBob = _view != null ? _view.GetComponent<FirstPersonCameraMotion>() : null;
            _fallRecovery = playerGo.GetComponent<CabinFallRecovery>();
            _animatorDriver = playerGo.GetComponentInChildren<CabinAnimatorDriver>(true);

            // The arm track binds to the Animator on this child. Its clip has no
            // root curves (CabinAnimationBuilder authors muscles only) and the
            // track is set to ApplySceneOffsets, so nothing should write this
            // transform — snapshot it anyway and re-pin it every frame, because
            // if Timeline ever did decide the root belonged at a track offset,
            // the player's body would silently detach and stand in the doorway
            // while the camera walked off without it.
            _body = _player.Find("Body");
            if (_body != null)
            {
                _bodyLocalPosition = _body.localPosition;
                _bodyLocalRotation = _body.localRotation;
            }

            // The only beat in the game with a visible player limb. Every player
            // renderer is ShadowsOnly (CabinNightCharacterBuilder.ConfigurePlayer)
            // because a first-person player standing inside their own head is
            // worse than no body; this arm is a separate skin carved off the same
            // skeleton, so the arm track drives it for free. Null is fine — a
            // missing mesh asset degrades to the old invisible reach, not a
            // soft-lock.
            Transform arm = _player.Find(ArmObjectName);
            _arm = arm != null ? arm.GetComponent<Renderer>() : null;
            if (_arm != null) _arm.enabled = true;

            Suppress();

            // Snap to the mark, then prime the proxy at t=0 so the very first
            // mirrored frame is already the authored pose — playing without
            // this shows one frame of the proxy's editor placement.
            _player.SetPositionAndRotation(doorMark, Quaternion.Euler(0f, doorMarkYaw, 0f));

            // Hold would leave the director playing past the end and `stopped`
            // would never fire, so the handback below would never run.
            director.extrapolationMode = DirectorWrapMode.None;
            director.time = 0d;
            director.Evaluate();

            director.stopped += HandleStopped;
            IsPlaying = true;
            director.Play();

            MirrorProxyToPlayer();
        }

        /// <summary>Guards the exact failure that retired this project's first
        /// Timeline attempt (see Assets/_Project/ASSETS_TODO.md section 1):
        /// PlayableDirector bindings are keyed to a specific TrackAsset object,
        /// so a builder that deletes and recreates its tracks orphans every
        /// binding, and the cutscene then plays perfectly while driving nothing.
        /// M2DoorTimelineBuilder reuses tracks by name and re-binds in the same
        /// run specifically to avoid that — this fails loud if it ever regresses
        /// anyway, instead of silently playing 13 seconds of nothing.</summary>
        private bool BindingsResolve()
        {
            if (!(director.playableAsset is TimelineAsset timeline)) return true;

            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (!(track is AnimationTrack)) continue;
                if (director.GetGenericBinding(track) == null)
                {
                    Debug.LogError(
                        $"[M2DoorOpenSequence] Timeline track '{track.name}' has no bound Animator — the binding was " +
                        "orphaned. Re-run 'Tools/False Positive/Bootstrap/T05 - Build M2 Door Timeline'. " +
                        "Skipping the cinematic.");
                    return false;
                }
            }

            return true;
        }

        private void LateUpdate()
        {
            // LateUpdate so this wins over anything still writing in Update.
            if (IsPlaying) MirrorProxyToPlayer();
        }

        private void MirrorProxyToPlayer()
        {
            if (_player == null || proxyRoot == null) return;

            _player.SetPositionAndRotation(proxyRoot.position,
                Quaternion.Euler(0f, proxyRoot.eulerAngles.y, 0f));

            if (_view != null && proxyPitch != null)
            {
                _view.localRotation = proxyPitch.localRotation;
            }

            if (_body != null)
            {
                _body.localPosition = _bodyLocalPosition;
                _body.localRotation = _bodyLocalRotation;
            }
        }

        private void HandleStopped(PlayableDirector stopped)
        {
            if (stopped != director) return;
            Restore();
            Finish();
        }

        private void Suppress()
        {
            // Move and Look only — SetMovementGated deliberately leaves Interact
            // live, which the lift interlude straight after this beat needs.
            _input?.SetMovementGated(true);

            // The rig polls input each Update rather than subscribing to it, so
            // disabling it is enough to stop both the rotation writes and the
            // SimpleMove call (see FreeLookCameraRig's class doc).
            if (_rig != null) _rig.enabled = false;

            // Direct transform writes and an enabled CharacterController fight
            // each other; the capsule is re-synced on the way out.
            if (_controller != null) _controller.enabled = false;

            // Procedural head motion is additive on top of the rig's pose and
            // would jitter the baked camera path.
            if (_headBob != null) _headBob.enabled = false;

            // Would push controller params over the arm track's Animator.
            if (_animatorDriver != null) _animatorDriver.enabled = false;

            // Insurance: no camera key goes near y = -4, but a teleport to the
            // cabin spawn mid-cinematic would be unrecoverable.
            if (_fallRecovery != null) _fallRecovery.enabled = false;
        }

        private void Restore()
        {
            IsPlaying = false;
            if (director != null) director.stopped -= HandleStopped;

            // Back to invisible before the player has the camera again — there is
            // no authored idle or walk arm motion, so a viewmodel left on would
            // hang rigid in frame for the rest of the game.
            if (_arm != null) _arm.enabled = false;

            GameObject door = GameObject.Find("Prop_FrontDoor_Locked");
            if (door != null) door.transform.rotation = DoorOpenRotation;

            // Seeded from where the cinematic actually ended rather than from a
            // hardcoded pair, so the two can never drift apart. SeedYaw resets
            // pitch to zero, so it must come first.
            if (_rig != null)
            {
                _rig.enabled = true;
                if (proxyRoot != null) _rig.SeedYaw(proxyRoot.eulerAngles.y);
                if (proxyPitch != null) _rig.SeedPitch(NormalizeSigned(proxyPitch.localEulerAngles.x));
            }

            if (_animatorDriver != null) _animatorDriver.enabled = true;
            if (_headBob != null) _headBob.enabled = true;
            if (_fallRecovery != null) _fallRecovery.enabled = true;

            if (_controller != null)
            {
                _controller.enabled = true;
                // The capsule's physics pose is stale after a cinematic's worth
                // of direct transform writes; without this the first frame of
                // player control can resolve a penetration that isn't there.
                Physics.SyncTransforms();
            }

            // Look only, deliberately — movement stays gated. The lift interlude
            // that runs straight after this beat needs the player standing
            // still, and CutsceneStage releases movement two beats later (see
            // its SetMovementGated(false) calls in TheCarry/TheSofa). Releasing
            // look here means a framing that ends slightly off is recoverable
            // with the mouse rather than a soft-lock, which is what the beat
            // this replaces did.
            _input?.SetLookGated(false);
        }

        /// <summary>Euler angles come back in [0, 360); SeedPitch clamps against
        /// a symmetric +/- limit, so 332 degrees has to arrive as -28.</summary>
        private static float NormalizeSigned(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            return degrees;
        }

        private void Finish()
        {
            Action done = _onDone;
            _onDone = null;
            done?.Invoke();
        }
    }
}
