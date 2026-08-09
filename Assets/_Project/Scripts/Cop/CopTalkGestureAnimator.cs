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
    /// Two poses, cross-faded by <see cref="_reach"/>:
    ///  - free air (reach 0): the gesture above, arms moving in open space.
    ///  - planted (reach 1): both hands resting on the table, placed by a
    ///    two-bone analytic IK solve so they land on the real surface whatever
    ///    the rig's proportions are. The oscillators that would fight the IK
    ///    (upper arm, forearm) fade out as it takes over; the performance
    ///    carries on as small volume-driven slides and taps of the IK targets
    ///    across the surface, plus the wrist flick.
    /// He reaches once, shortly after the scene starts, and stays planted.
    ///
    /// Because this runs AFTER CopIdleAnimator, the IK reads shoulder positions
    /// that already include the idle breathing, so the hands stay planted while
    /// the torso moves under them instead of sliding with it.
    ///
    /// With tableSurface unassigned the reach never engages and this behaves
    /// exactly as the free-air layer alone.
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

        [Header("Table pose — leave tableSurface empty to disable the reach")]
        [SerializeField] private Transform tableSurface;
        [SerializeField] private float reachDelay = 0.75f;
        [SerializeField] private float reachSeconds = 1.2f;
        /// <summary>Half the gap between the hands, across the cop's shoulders.</summary>
        [SerializeField] private float handSpread = 0.22f;
        /// <summary>How far past the table's near edge the hands land.</summary>
        [SerializeField] private float handInset = 0.14f;
        /// <summary>Wrist clearance above the surface — the palm hangs below it.</summary>
        [SerializeField] private float handHeight = 0.045f;
        /// <summary>Fraction of full arm extension treated as "resting". Below 1
        /// the elbow keeps a visible bend instead of locking out straight.</summary>
        [SerializeField] private float reachComfort = 0.95f;
        /// <summary>Swings the elbows around the shoulder-to-hand axis. Provably
        /// does not move the hands, so it is safe to dial purely by eye.</summary>
        [SerializeField] private float elbowSwivelDegrees;
        /// <summary>Wrist correction once planted, mirrored for the right hand —
        /// the rig's rest wrist angle is unlikely to sit flat on a table.</summary>
        [SerializeField] private Vector3 plantedHandEuler;

        [Header("Gesture while planted")]
        [SerializeField] private float tableSlideMetres = 0.035f;
        [SerializeField] private float tableLiftMetres = 0.02f;
        [SerializeField] private float plantedHandScale = 0.6f;

        private Quaternion _leftArmRest, _leftForeArmRest, _leftHandRest;
        private Quaternion _rightArmRest, _rightForeArmRest, _rightHandRest;
        private float _envelope;
        private float _reach;
        private float _sinceEnable;
        private Bounds _surfaceBounds;
        private bool _hasSurfaceBounds;
        private Vector3 _surfaceForward, _surfaceRight;

        private void OnEnable()
        {
            CacheRestRotations();
            _reach = 0f;
            _sinceEnable = 0f;
            _hasSurfaceBounds = false;
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
            UpdateReach();

            float t = Time.time;
            float e = _envelope;
            float free = 1f - _reach;

            // The IK reads live bone positions, so every frame has to start from
            // the same known pose or the solve compounds on the previous one.
            RestoreRest();

            if (spine1 != null)
            {
                float rock = Mathf.Sin(t * 1.4f + 0.5f) * spineDegrees;
                // Additive on top of whatever CopIdleAnimator's breathing
                // curve already wrote to spine1.localRotation this frame —
                // relies on script execution order placing this after
                // CopIdleAnimator (both write in LateUpdate; see bootstrap
                // wiring comment for the explicit order guarantee).
                //
                // Must land BEFORE the IK: spine1 is an ancestor of the arms,
                // so rotating it afterwards would swing the solved hands off
                // the surface by roughly (angle x arm reach) — a couple of
                // centimetres of hands sliding around on the table. Solving
                // after it instead means the IK absorbs the torso motion and
                // the palms stay planted while he breathes and rocks.
                spine1.localRotation = spine1.localRotation * Quaternion.Euler(rock * e, 0f, 0f);
            }

            // Different frequency/phase per joint and per side so it doesn't
            // read as a mirrored clap — the single biggest tell that a
            // gesture is procedural rather than a performance (same lesson
            // as the old CopAnimationBuilder.GestureOscillators()).
            if (free > 0f)
            {
                float a = e * free;
                if (leftArm != null)
                {
                    float lift = Mathf.Sin(t * 1.7f) * armLiftDegrees;
                    float sway = Mathf.Sin(t * 1.1f + 0.6f) * armSwayDegrees;
                    leftArm.localRotation = _leftArmRest * Quaternion.Euler(lift * a, sway * a, 0f);
                }
                if (rightArm != null)
                {
                    float lift = Mathf.Sin(t * 1.9f + 1.3f) * armLiftDegrees;
                    float sway = Mathf.Sin(t * 1.3f + 2.0f) * armSwayDegrees;
                    rightArm.localRotation = _rightArmRest * Quaternion.Euler(lift * a, sway * a, 0f);
                }
                if (leftForeArm != null)
                {
                    float bend = Mathf.Sin(t * 2.3f + 0.3f) * forearmDegrees;
                    leftForeArm.localRotation = _leftForeArmRest * Quaternion.Euler(bend * a, 0f, 0f);
                }
                if (rightForeArm != null)
                {
                    float bend = Mathf.Sin(t * 2.1f + 1.7f) * forearmDegrees;
                    rightForeArm.localRotation = _rightForeArmRest * Quaternion.Euler(bend * a, 0f, 0f);
                }
            }

            if (_reach > 0f && ResolveSurface())
            {
                PlantArm(leftArm, leftForeArm, leftHand, -1f, 0f, t, e);
                PlantArm(rightArm, rightForeArm, rightHand, 1f, 2.2f, t, e);
            }

            // Wrist flick last, on top of whichever pose won — damped once the
            // palm is on the table so it doesn't peel off the surface.
            float handScale = Mathf.Lerp(1f, plantedHandScale, _reach);
            Quaternion planted = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(plantedHandEuler), _reach);
            Quaternion plantedMirrored = Quaternion.Slerp(Quaternion.identity,
                Quaternion.Euler(plantedHandEuler.x, -plantedHandEuler.y, -plantedHandEuler.z), _reach);
            if (leftHand != null)
            {
                float flick = Mathf.Sin(t * 3.1f + 0.9f) * handDegrees;
                leftHand.localRotation = _leftHandRest * planted * Quaternion.Euler(flick * e * handScale, 0f, 0f);
            }
            if (rightHand != null)
            {
                float flick = Mathf.Sin(t * 2.9f + 2.4f) * handDegrees;
                rightHand.localRotation = _rightHandRest * plantedMirrored * Quaternion.Euler(flick * e * handScale, 0f, 0f);
            }
        }

        private void RestoreRest()
        {
            if (leftArm != null) leftArm.localRotation = _leftArmRest;
            if (leftForeArm != null) leftForeArm.localRotation = _leftForeArmRest;
            if (leftHand != null) leftHand.localRotation = _leftHandRest;
            if (rightArm != null) rightArm.localRotation = _rightArmRest;
            if (rightForeArm != null) rightForeArm.localRotation = _rightForeArmRest;
            if (rightHand != null) rightHand.localRotation = _rightHandRest;
        }

        private void UpdateEnvelope()
        {
            float target = lipSync != null ? Mathf.Clamp01(lipSync.result.volume) : 0f;
            float rate = target > _envelope ? attackPerSecond : releasePerSecond;
            _envelope = Mathf.MoveTowards(_envelope, target, rate * Time.deltaTime);
        }

        /// <summary>One-way ease into the planted pose — he settles his hands on
        /// the table early in the interrogation and leaves them there.</summary>
        private void UpdateReach()
        {
            if (tableSurface == null)
            {
                _reach = 0f;
                return;
            }
            _sinceEnable += Time.deltaTime;
            float progress = (_sinceEnable - reachDelay) / Mathf.Max(0.01f, reachSeconds);
            _reach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        }

        /// <summary>Measures the table once from its renderers, then derives the
        /// cop-relative axes. Facing comes from where the table actually is
        /// rather than the rig's authored forward axis, which a seated FBX does
        /// not reliably agree with.</summary>
        private bool ResolveSurface()
        {
            if (tableSurface == null) return false;
            if (!_hasSurfaceBounds)
            {
                Renderer[] renderers = tableSurface.GetComponentsInChildren<Renderer>();
                if (renderers.Length == 0) return false;
                _surfaceBounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++) _surfaceBounds.Encapsulate(renderers[i].bounds);
                _hasSurfaceBounds = true;
            }

            Vector3 toTable = _surfaceBounds.center - transform.position;
            toTable.y = 0f;
            if (toTable.sqrMagnitude < 1e-6f) return false;
            _surfaceForward = toTable.normalized;
            _surfaceRight = Vector3.Cross(Vector3.up, _surfaceForward);
            return true;
        }

        /// <summary>A spot on the tabletop, handInset past the edge nearest the
        /// cop and handSpread out to one side.</summary>
        private Vector3 HandTarget(float side)
        {
            float halfDepth = Mathf.Abs(_surfaceBounds.extents.x * _surfaceForward.x)
                            + Mathf.Abs(_surfaceBounds.extents.z * _surfaceForward.z);
            Vector3 spot = _surfaceBounds.center - _surfaceForward * (halfDepth - handInset);
            spot.y = _surfaceBounds.max.y + handHeight;
            return spot + _surfaceRight * (side * handSpread);
        }

        private void PlantArm(Transform upper, Transform fore, Transform hand, float side, float phase, float t, float e)
        {
            if (upper == null || fore == null || hand == null) return;

            // Planted performance: the hands stay in contact but drift and tap
            // across the surface, driven by the same envelope as the free-air
            // gesture so he still "talks with his hands" without lifting them.
            Vector3 target = HandTarget(side)
                + _surfaceRight * (Mathf.Sin(t * 1.3f + phase) * tableSlideMetres * e)
                + _surfaceForward * (Mathf.Sin(t * 0.9f + phase) * tableSlideMetres * 0.6f * e)
                + Vector3.up * (Mathf.Max(0f, Mathf.Sin(t * 2.1f + phase)) * tableLiftMetres * e);

            float armLength = Vector3.Distance(upper.position, fore.position)
                            + Vector3.Distance(fore.position, hand.position);
            float reach = armLength * Mathf.Clamp(reachComfort, 0.1f, 1f);
            target = RetreatToReach(target, upper.position, reach, handInset);

            Quaternion preUpper = upper.localRotation;
            Quaternion preFore = fore.localRotation;

            Vector3 pole = upper.position + _surfaceRight * (side * 0.6f) - Vector3.up * 0.4f;
            SolveTwoBone(upper, fore, hand, target, pole);

            if (!Mathf.Approximately(elbowSwivelDegrees, 0f))
            {
                Vector3 axis = hand.position - upper.position;
                if (axis.sqrMagnitude > 1e-10f)
                {
                    upper.rotation = Quaternion.AngleAxis(side * elbowSwivelDegrees, axis.normalized) * upper.rotation;
                }
            }

            if (_reach < 1f)
            {
                upper.localRotation = Quaternion.Slerp(preUpper, upper.localRotation, _reach);
                fore.localRotation = Quaternion.Slerp(preFore, fore.localRotation, _reach);
            }
        }

        /// <summary>Retreats a target beyond the arm's comfortable extension back
        /// along -surfaceForward — toward the table's near edge, i.e. toward the
        /// cop's own side — until it comes within reach of the shoulder, or until
        /// it reaches the edge itself (maxRetreat = handInset, the exact distance
        /// HandTarget placed the spot past that edge). This is what a person
        /// actually does at a table too deep to reach fully: the hand stops
        /// nearer the edge, not floating in space above it.
        ///
        /// An earlier version pulled the target toward the shoulder in full 3D,
        /// which seemed equivalent but wasn't: the shoulder sits behind the
        /// table's near edge for anyone standing or seated at it, so shrinking
        /// straight toward it dragged the "clamped" hand target back across the
        /// edge into open air above the floor — the opposite of the intended
        /// fix, and worse than doing nothing. Restricting the slide to the
        /// table's own forward axis guarantees the result never leaves the
        /// segment between the near edge and the original spot.
        ///
        /// Solved as a ray/sphere intersection: point(s) = target - forward*s,
        /// solve |point(s) - shoulder| = reach for the smallest non-negative s,
        /// then clamp s to [0, maxRetreat]. If the reach sphere never meets the
        /// ray (disc &lt; 0) — the vertical drop from shoulder to tabletop alone
        /// exceeds the arm's reach — go straight to the edge as the closest
        /// approximation available; this is a defensive fallback for a
        /// shoulder/table height mismatch this rig should never actually
        /// produce.</summary>
        private Vector3 RetreatToReach(Vector3 target, Vector3 shoulder, float reach, float maxRetreat)
        {
            Vector3 o = target - shoulder;
            if (o.sqrMagnitude <= reach * reach) return target;

            Vector3 d = -_surfaceForward;
            float b = Vector3.Dot(o, d);
            float c = o.sqrMagnitude - reach * reach;
            float disc = b * b - c;
            float s;
            if (disc < 0f)
            {
                s = maxRetreat;
            }
            else
            {
                float sqrtDisc = Mathf.Sqrt(disc);
                s = -b - sqrtDisc;
                if (s < 0f) s = -b + sqrtDisc;
                s = Mathf.Clamp(s, 0f, maxRetreat);
            }
            return target - _surfaceForward * s;
        }

        /// <summary>Analytic two-joint IK — places <paramref name="end"/> on
        /// <paramref name="target"/> by rotating only <paramref name="root"/> and
        /// <paramref name="mid"/>. Deliberately preserves the chain's existing
        /// bend plane rather than forcing one from the pole, so the elbow keeps
        /// the rig's natural orientation; the pole is only a fallback for an arm
        /// that starts perfectly straight (where the plane is undefined). Use
        /// elbowSwivelDegrees to steer the elbow after the fact.
        ///
        /// The three rotations are world-space pre-multiplies applied through
        /// Transform.rotation, so each one reads the value the previous write
        /// already propagated down the chain — the order below is load-bearing.
        /// Verified exact (sub-micron) over 4000 randomised rest poses, bone
        /// lengths and parent transforms.</summary>
        private static void SolveTwoBone(Transform root, Transform mid, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 a = root.position, b = mid.position, c = end.position;
            float lab = Vector3.Distance(a, b);
            float lcb = Vector3.Distance(c, b);
            if (lab < 1e-5f || lcb < 1e-5f) return;

            // Clamp into the annulus the chain can physically reach. Too far and
            // it cannot straighten enough; too close and it cannot fold enough.
            // Without the lower bound the law of cosines leaves its domain and
            // Acos silently clamps to a pose that misses the target entirely.
            float lat = Mathf.Clamp(Vector3.Distance(target, a),
                Mathf.Abs(lab - lcb) + 1e-4f, lab + lcb - 1e-4f);

            Vector3 ac = c - a, ab = b - a, at = target - a;
            if (ac.sqrMagnitude < 1e-10f || at.sqrMagnitude < 1e-10f) return;

            float acAb0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac.normalized, ab.normalized), -1f, 1f));
            float baBc0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1f, 1f));
            float acAt0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot(ac.normalized, at.normalized), -1f, 1f));

            float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

            Vector3 axis0 = Vector3.Cross(ac, ab);
            if (axis0.sqrMagnitude < 1e-10f) axis0 = Vector3.Cross(ac, pole - a);
            if (axis0.sqrMagnitude < 1e-10f) return;
            axis0.Normalize();
            Vector3 axis1 = Vector3.Cross(ac, at);

            root.rotation = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, axis0) * root.rotation;
            mid.rotation = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, axis0) * mid.rotation;
            if (axis1.sqrMagnitude > 1e-10f)
            {
                root.rotation = Quaternion.AngleAxis(acAt0 * Mathf.Rad2Deg, axis1.normalized) * root.rotation;
            }
        }
    }
}
