using System.Collections;
using UnityEngine;

namespace FalsePositive.CabinNight
{
    /// <summary>
    /// Owns CrossFade state selection into CabinCast.controller (built by
    /// Editor.CabinAnimationBuilder) for one cast member. Lives on the cast
    /// ROOT; the Animator it drives is on the "Body" child
    /// (CabinNightCharacterBuilder.BuildCharacter). Added to every cast
    /// member including the player — the player has no ScriptedActor but
    /// still needs the Lift_Crouch state for the sofa-carry beat, and the
    /// player's renderers are shadow-only, so the lift reads as a cast
    /// shadow (CabinNightCharacterBuilder.ConfigurePlayer).
    ///
    /// Root-transform ownership is unaffected by any of this: the Animator
    /// writes muscles only (applyRootMotion = false, no root curves in any
    /// clip — see CabinAnimationBuilder's class doc), Cutscene.ScriptedActor
    /// and CabinFirstPersonController/FreeLookCameraRig own the root
    /// Transform's position/rotation exactly as before.
    ///
    /// Kneeling and Sleeping need a body-height drop that a muscle-only clip
    /// cannot carry (CabinAnimationBuilder.BodyYOffsetFor's doc explains why
    /// — confirmed empirically, not just documented). This driver applies
    /// that offset itself, directly on the Body child's local Y, restoring
    /// it for every other state.
    /// </summary>
    public sealed class CabinAnimatorDriver : MonoBehaviour
    {
        [SerializeField] private CabinIdleProfile defaultProfile;

        /// <summary>How long to wait after a profile change before the feet are
        /// measured against it. PlayState below crossfades the muscle pose over
        /// 0.25s while ApplyBodyYOffset moves the hips instantly, so measuring
        /// one frame later catches a transitional shape — hips already dropped,
        /// legs still in the old pose — and bakes a correction for a pose that
        /// no longer exists by the time the blend finishes. Same wait, and the
        /// same reason, as Cutscene.CutsceneStage.PoseBorrowed.</summary>
        private const float PoseSettleSeconds = 0.3f;

        // Matches PlayState's own 0.25f crossfade duration -- the whole point of
        // easing this is to stay in step with the muscle blend, not run longer
        // or shorter than it. Also short enough to be fully settled before
        // Cutscene.CutsceneStage.PlantFeet measures foot height 0.3s after a
        // pose starts (PoseBorrowed) -- an ease still running at that mark would
        // bake a wrong one-time foot correction that nothing re-runs.
        private const float BodyYOffsetEaseSeconds = 0.25f;

        private Animator _animator;
        private Vector3 _bodyRestLocalPosition;
        private Coroutine _plant;
        private bool _plantsFeet;

        // Timed lerp state for the body-Y offset ease -- not a coroutine. A
        // coroutine started here would be silently orphaned if this component's
        // GameObject is deactivated mid-ease (Cutscene.CutsceneStage.Borrow /
        // ReturnBorrowed toggle borrowed cast members' SetActive), stranding a
        // partial offset that would then persist into the actor's NEXT Borrow.
        // Driving the ease from Update means it simply stops advancing when
        // disabled, and OnDisable below snaps to the final target so nothing
        // partial survives regardless.
        private float _offsetFrom;
        private float _offsetTo;
        private float _offsetT = 1f; // 1 = fully arrived at _offsetTo

        public void Configure(CabinIdleProfile profile) => defaultProfile = profile;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null) _bodyRestLocalPosition = _animator.transform.localPosition;

            // The player carries this component too (for the Lift_Crouch beat)
            // but is moved by a CharacterController with gravity — the floor
            // already finds them, and re-seating their root would fight it.
            _plantsFeet = GetComponent<CharacterController>() == null;
        }

        // Spawn pose is instant -- characters should not visibly slide into
        // place the moment a scene loads. Every PlayProfile call AFTER this one
        // eases (see the public overload below).
        private void Start() => PlayProfile(defaultProfile, instant: true);

        private void Update()
        {
            if (_animator == null || _offsetT >= 1f) return;
            _offsetT = Mathf.Clamp01(_offsetT + Time.deltaTime / BodyYOffsetEaseSeconds);
            WriteBodyYOffset(Mathf.Lerp(_offsetFrom, _offsetTo, _offsetT));
        }

        private void OnDisable()
        {
            _plant = null;

            // Never leave a mid-ease offset for the next activation to inherit.
            _offsetFrom = _offsetTo;
            _offsetT = 1f;
            if (_animator != null) WriteBodyYOffset(_offsetTo);
        }

        /// <summary>Cross-fades to the idle/pose state for a CabinIdleProfile and
        /// eases the Kneeling/Sleeping/Seated* body-height offset in over
        /// BodyYOffsetEaseSeconds, matching the muscle crossfade instead of
        /// popping the whole body up to 0.31m in one frame while the pose is
        /// still mid-blend.</summary>
        public void PlayProfile(CabinIdleProfile profile) => PlayProfile(profile, instant: false);

        private void PlayProfile(CabinIdleProfile profile, bool instant)
        {
            PlayState(StateName(profile), 0.25f);
            ApplyBodyYOffset(profile, instant);
            SchedulePlant(profile);
        }

        /// <summary>Cross-fades directly to a named state in CabinCast.controller —
        /// used for the Walk/Walk_Carry/Lift_Crouch states that aren't tied to a
        /// single CabinIdleProfile (Cutscene.ScriptedActor.MoveTo calls this).</summary>
        public void PlayState(string stateName, float fadeSeconds)
        {
            // A walk or a lift is a MOVE, not a settled pose. Cancelling here
            // stops a re-plant queued by the profile that preceded it from
            // firing mid-stride and re-seating a character who is halfway
            // across the room — and, since a re-plant re-fits the collider,
            // from switching collision back on under a walk that deliberately
            // turned it off (Cutscene.ScriptedActor.MoveTo).
            CancelPlant();
            if (_animator == null || string.IsNullOrEmpty(stateName)) return;
            _animator.CrossFadeInFixedTime(stateName, fadeSeconds, 0);
        }

        /// <summary>Re-grounds the character and re-fits its collider once the
        /// new pose has settled. Every pose in CabinPoseLibrary that drops the
        /// hips drops the feet with them (no foot IK on this rig), and the
        /// collider that has to match a kneeling body is not the one that
        /// matches a standing one — see CabinFootPlanter.</summary>
        private void SchedulePlant(CabinIdleProfile profile)
        {
            if (!_plantsFeet || !isActiveAndEnabled) return;
            CancelPlant();
            _plant = StartCoroutine(PlantAfterBlend(profile));
        }

        private void CancelPlant()
        {
            if (_plant == null) return;
            StopCoroutine(_plant);
            _plant = null;
        }

        private IEnumerator PlantAfterBlend(CabinIdleProfile profile)
        {
            yield return new WaitForSeconds(PoseSettleSeconds);
            _plant = null;
            CabinFootPlanter.PlantAndFit(gameObject, profile);
        }

        private void ApplyBodyYOffset(CabinIdleProfile profile, bool instant)
        {
            if (_animator == null) return;
            float target = Editor_BodyYOffsetFor(profile);
            if (instant)
            {
                _offsetFrom = target;
                _offsetTo = target;
                _offsetT = 1f;
                WriteBodyYOffset(target);
                return;
            }
            _offsetFrom = Mathf.Lerp(_offsetFrom, _offsetTo, _offsetT); // current, mid-ease-safe
            _offsetTo = target;
            _offsetT = 0f;
        }

        private void WriteBodyYOffset(float offset)
        {
            Vector3 position = _bodyRestLocalPosition;
            position.y += offset;
            _animator.transform.localPosition = position;
        }

        /// <summary>Mirrors Editor.CabinAnimationBuilder.BodyYOffsetFor without a
        /// runtime-assembly dependency on the Editor assembly (Scripts/Editor is
        /// editor-only and unreachable from here, same constraint documented on
        /// Cutscene.ScriptedActor for CabinPoseLibrary). Keep in sync with that
        /// method and with CabinPoseLibrary.Apply's Kneeling/Sleeping
        /// pose.bodyPosition offsets, which this is standing in for.</summary>
        private static float Editor_BodyYOffsetFor(CabinIdleProfile profile)
        {
            switch (profile)
            {
                case CabinIdleProfile.Kneeling: return -0.28f;
                case CabinIdleProfile.Sleeping: return -0.12f;
                // All three seated variants share one drop — the lean changes
                // the torso only, never how far the hips sit onto the pad.
                case CabinIdleProfile.Seated:
                case CabinIdleProfile.SeatedBack:
                case CabinIdleProfile.SeatedForward: return -0.31f;
                default: return 0f;
            }
        }

        private static string StateName(CabinIdleProfile profile)
        {
            switch (profile)
            {
                case CabinIdleProfile.Confrontational: return "Idle_Confrontational";
                case CabinIdleProfile.Controlled: return "Idle_Controlled";
                case CabinIdleProfile.Guarded: return "Idle_Guarded";
                case CabinIdleProfile.Sleeping: return "Idle_Sleeping";
                case CabinIdleProfile.Panicked: return "Idle_Panicked";
                case CabinIdleProfile.Walking: return "Idle_Walking";
                case CabinIdleProfile.Carrying: return "Pose_Carrying";
                case CabinIdleProfile.Kneeling: return "Pose_Kneeling";
                case CabinIdleProfile.Seated: return "Pose_Seated";
                case CabinIdleProfile.SeatedBack: return "Pose_SeatedBack";
                case CabinIdleProfile.SeatedForward: return "Pose_SeatedForward";
                case CabinIdleProfile.HoldingCup: return "Pose_HoldingCup";
                default: return "Idle_Controlled";
            }
        }
    }
}
