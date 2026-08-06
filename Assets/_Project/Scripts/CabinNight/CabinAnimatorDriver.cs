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

        private Animator _animator;
        private Vector3 _bodyRestLocalPosition;

        public void Configure(CabinIdleProfile profile) => defaultProfile = profile;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null) _bodyRestLocalPosition = _animator.transform.localPosition;
        }

        private void Start() => PlayProfile(defaultProfile);

        /// <summary>Cross-fades to the idle/pose state for a CabinIdleProfile and
        /// applies (or clears) the Kneeling/Sleeping body-height offset.</summary>
        public void PlayProfile(CabinIdleProfile profile)
        {
            PlayState(StateName(profile), 0.25f);
            ApplyBodyYOffset(profile);
        }

        /// <summary>Cross-fades directly to a named state in CabinCast.controller —
        /// used for the Walk/Walk_Carry/Lift_Crouch states that aren't tied to a
        /// single CabinIdleProfile (Cutscene.ScriptedActor.MoveTo calls this).</summary>
        public void PlayState(string stateName, float fadeSeconds)
        {
            if (_animator == null || string.IsNullOrEmpty(stateName)) return;
            _animator.CrossFadeInFixedTime(stateName, fadeSeconds, 0);
        }

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
                default: return "Idle_Controlled";
            }
        }
    }
}
