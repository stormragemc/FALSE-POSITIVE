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

        private Animator _animator;
        private Vector3 _bodyRestLocalPosition;
        private Coroutine _plant;
        private bool _plantsFeet;

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

        private void Start() => PlayProfile(defaultProfile);

        /// <summary>Cross-fades to the idle/pose state for a CabinIdleProfile and
        /// applies (or clears) the Kneeling/Sleeping body-height offset.</summary>
        public void PlayProfile(CabinIdleProfile profile)
        {
            PlayState(StateName(profile), 0.25f);
            ApplyBodyYOffset(profile);
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

        private void OnDisable() => _plant = null;

        private void ApplyBodyYOffset(CabinIdleProfile profile)
        {
            if (_animator == null) return;
            float offset = Editor_BodyYOffsetFor(profile);
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
                default: return "Idle_Controlled";
            }
        }
    }
}
