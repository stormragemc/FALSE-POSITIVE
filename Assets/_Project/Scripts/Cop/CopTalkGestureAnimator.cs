using UnityEngine;

namespace FalsePositive.Cop
{
    /// <summary>
    /// Procedural "talking with hands" body layer — asymmetric shoulder/arm/
    /// forearm/hand sway plus a small torso accent, amplitude driven directly
    /// by uLipSync's own analyzed volume rather than a baked clip. Writes in
    /// LateUpdate, same as CopIdleAnimator, and composes additively on top of
    /// whatever CopIdleAnimator wrote this frame (bones are disjoint —
    /// CopIdleAnimator drives spine/spine1/neck/head, this drives the arms
    /// plus a spine1 accent applied on top of CopIdleAnimator's own breathing
    /// via the SAME "* Quaternion.Euler(...)" additive convention, applied
    /// after CopIdleAnimator in script execution order — see bootstrap
    /// wiring comment).
    ///
    /// Superseded design: a previous pass drove this via a baked Timeline
    /// AnimationClip (Cop_Talk / Cutscene_SpasskyAnswer.playable, still on
    /// disk under Scripts/Editor/CopAnimationBuilder.cs, unreferenced) played
    /// only during CutsceneId.SpasskyAnswer. That covered one cutscene out of
    /// the whole game — every live dialogue turn (the bulk of play) had a
    /// static body — and its scene binding silently went null on every
    /// bootstrap re-run (PlayableDirector.SetGenericBinding is keyed to a
    /// specific track object, and CopAnimationBuilder.BuildTimeline deletes
    /// and recreates the track on every build). Driving straight off
    /// uLipSync's volume instead covers both cases uniformly with the same
    /// signal, and never touches Hips/legs, so it can't reintroduce the
    /// root-motion/sink class of bug a full-body clip did.
    /// </summary>
    public sealed class CopTalkGestureAnimator : MonoBehaviour
    {
        [Header("Bones")]
        [SerializeField] private Transform leftArm;
        [SerializeField] private Transform leftForeArm;
        [SerializeField] private Transform leftHand;
        [SerializeField] private Transform rightArm;
        [SerializeField] private Transform rightForeArm;
        [SerializeField] private Transform rightHand;
        [SerializeField] private Transform spine1;

        [Header("Volume source")]
        [SerializeField] private uLipSync.uLipSync lipSync;

        [Header("Envelope (volume -> gesture amplitude 0..1)")]
        [SerializeField] private float attackPerSecond = 15f;
        [SerializeField] private float releasePerSecond = 3f;

        [Header("Gesture shape — degrees at full envelope, applied additively")]
        [SerializeField] private float armLiftDegrees = 18f;
        [SerializeField] private float armSwayDegrees = 10f;
        [SerializeField] private float forearmDegrees = 14f;
        [SerializeField] private float handDegrees = 12f;
        [SerializeField] private float spineDegrees = 2f;

        private Quaternion _leftArmRest, _leftForeArmRest, _leftHandRest;
        private Quaternion _rightArmRest, _rightForeArmRest, _rightHandRest;
        private float _envelope;

        private void OnEnable()
        {
            CacheRestRotations();
        }

        /// <summary>Same reasoning as CopIdleAnimator.CacheRestRotations: caches
        /// from the bones' CURRENT local rotation (the baked seated FBX rest
        /// pose, since nothing else drives these bones) so re-enabling this
        /// component never snaps to a stale rotation.</summary>
        private void CacheRestRotations()
        {
            if (leftArm != null) _leftArmRest = leftArm.localRotation;
            if (leftForeArm != null) _leftForeArmRest = leftForeArm.localRotation;
            if (leftHand != null) _leftHandRest = leftHand.localRotation;
            if (rightArm != null) _rightArmRest = rightArm.localRotation;
            if (rightForeArm != null) _rightForeArmRest = rightForeArm.localRotation;
            if (rightHand != null) _rightHandRest = rightHand.localRotation;
        }

        private void LateUpdate()
        {
            UpdateEnvelope();

            float t = Time.time;
            float e = _envelope;

            // Different frequency/phase per joint and per side so it doesn't
            // read as a mirrored clap — the single biggest tell that a
            // gesture is procedural rather than a performance (same lesson
            // as the old CopAnimationBuilder.GestureOscillators()).
            if (leftArm != null)
            {
                float lift = Mathf.Sin(t * 1.7f) * armLiftDegrees;
                float sway = Mathf.Sin(t * 1.1f + 0.6f) * armSwayDegrees;
                leftArm.localRotation = _leftArmRest * Quaternion.Euler(lift * e, sway * e, 0f);
            }
            if (rightArm != null)
            {
                float lift = Mathf.Sin(t * 1.9f + 1.3f) * armLiftDegrees;
                float sway = Mathf.Sin(t * 1.3f + 2.0f) * armSwayDegrees;
                rightArm.localRotation = _rightArmRest * Quaternion.Euler(lift * e, sway * e, 0f);
            }
            if (leftForeArm != null)
            {
                float bend = Mathf.Sin(t * 2.3f + 0.3f) * forearmDegrees;
                leftForeArm.localRotation = _leftForeArmRest * Quaternion.Euler(bend * e, 0f, 0f);
            }
            if (rightForeArm != null)
            {
                float bend = Mathf.Sin(t * 2.1f + 1.7f) * forearmDegrees;
                rightForeArm.localRotation = _rightForeArmRest * Quaternion.Euler(bend * e, 0f, 0f);
            }
            if (leftHand != null)
            {
                float flick = Mathf.Sin(t * 3.1f + 0.9f) * handDegrees;
                leftHand.localRotation = _leftHandRest * Quaternion.Euler(flick * e, 0f, 0f);
            }
            if (rightHand != null)
            {
                float flick = Mathf.Sin(t * 2.9f + 2.4f) * handDegrees;
                rightHand.localRotation = _rightHandRest * Quaternion.Euler(flick * e, 0f, 0f);
            }
            if (spine1 != null)
            {
                float rock = Mathf.Sin(t * 1.4f + 0.5f) * spineDegrees;
                // Additive on top of whatever CopIdleAnimator's breathing
                // curve already wrote to spine1.localRotation this frame —
                // relies on script execution order placing this after
                // CopIdleAnimator (both write in LateUpdate; see bootstrap
                // wiring comment for the explicit order guarantee).
                spine1.localRotation = spine1.localRotation * Quaternion.Euler(rock * e, 0f, 0f);
            }
        }

        private void UpdateEnvelope()
        {
            float target = lipSync != null ? Mathf.Clamp01(lipSync.result.volume) : 0f;
            float rate = target > _envelope ? attackPerSecond : releasePerSecond;
            _envelope = Mathf.MoveTowards(_envelope, target, rate * Time.deltaTime);
        }
    }
}
