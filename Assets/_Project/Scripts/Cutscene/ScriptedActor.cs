using System.Collections;
using FalsePositive.CabinNight;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Procedural cutscene MOVEMENT for a cast member — root Transform
    /// position/rotation only. Pose/animation is CabinAnimatorDriver's job
    /// (CabinCast.controller, built by Editor.CabinAnimationBuilder); this
    /// class delegates to it rather than writing muscles itself, which is
    /// what it did before real AnimationClips existed in the project (see
    /// git history / the plan doc this superseded — "no .anim assets, no
    /// Animator Controller" was a Day-1 scope decision that a later pass
    /// reversed once HumanoidClipAuthoringTests confirmed clip authoring
    /// actually works for this rig).
    ///
    /// CabinCharacterIdle (breathing/sway) keeps running on the same object
    /// underneath — that component only touches Spine1/Neck/Head bones as a
    /// live additive offset on top of whatever the Animator wrote this frame,
    /// so it doesn't fight the controller.
    ///
    /// Public signatures are unchanged from the pre-clip version, so
    /// CutsceneStage's call sites (PlayPose/MoveTo) needed zero edits when
    /// this was rewritten.
    /// </summary>
    public sealed class ScriptedActor : MonoBehaviour
    {
        private CabinAnimatorDriver _driver;

        private void Awake()
        {
            _driver = GetComponent<CabinAnimatorDriver>();
        }

        /// <summary>Applies a static profile pose immediately (no walk cycle).</summary>
        public void PlayPose(CabinIdleProfile profile) => _driver?.PlayProfile(profile);

        /// <summary>Instant yaw snap to face a point (e.g. the door, on a forced head-turn beat).</summary>
        public void TurnTo(Vector3 worldPoint)
        {
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        /// <summary>
        /// Walks to a world position over time, cross-fading into the Walk (or
        /// Walk_Carry) animator state for the duration of the move.
        /// </summary>
        public IEnumerator MoveTo(Vector3 worldPosition, float speed, CabinIdleProfile walkProfile = CabinIdleProfile.Walking)
        {
            if (_driver == null)
            {
                // No driver (shouldn't happen for cast members — every
                // BuildCharacter call adds one — but don't hard-fail a
                // cutscene over it), just translate.
                while (Vector3.Distance(transform.position, worldPosition) > 0.05f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, worldPosition, speed * Time.deltaTime);
                    TurnTo(worldPosition);
                    yield return null;
                }
                yield break;
            }

            _driver.PlayState(walkProfile == CabinIdleProfile.Carrying ? "Walk_Carry" : "Walk", 0.2f);
            SetSolid(false);

            while (Vector3.Distance(transform.position, worldPosition) > 0.05f)
            {
                TurnTo(worldPosition);
                transform.position = Vector3.MoveTowards(transform.position, worldPosition, speed * Time.deltaTime);
                yield return null;
            }

            transform.position = worldPosition;
            SetSolid(true);
            _driver.PlayProfile(walkProfile == CabinIdleProfile.Carrying ? CabinIdleProfile.Carrying : CabinIdleProfile.Controlled);
        }

        /// <summary>Turns this actor's body collider off for the duration of a
        /// scripted walk.
        ///
        /// Cast members are solid so the player can bump into them
        /// (CabinNight.CabinFootPlanter fits the volume to the pose). A solid
        /// body being teleported along a waypoint path each frame is a
        /// different thing though — it has no Rigidbody, so it does not push,
        /// it interpenetrates, and the player's CharacterController then
        /// resolves that overlap by shoving the player out of the way on their
        /// next Move. Nick walking out in "OutIntoTheSnow" would barge straight
        /// through whoever is standing in the doorway.
        ///
        /// Fetched fresh rather than cached: CabinFootPlanter.FitCollider can
        /// swap a capsule for a box (or back) when the pose changes, so a
        /// cached reference here would be destroyed out from under us.</summary>
        private void SetSolid(bool solid)
        {
            foreach (Collider collider in GetComponents<Collider>())
            {
                if (collider is CharacterController) continue;
                collider.enabled = solid;
            }
        }

        /// <summary>
        /// Walks a bent path (e.g. through a doorway rather than straight
        /// through a wall) as ONE continuous walk-cycle, not a walk/idle
        /// stutter per leg. MoveTo cross-fades into Walk/Walk_Carry before
        /// its loop and back to Controlled/Carrying after — calling MoveTo
        /// once per waypoint would replay both transitions at every corner.
        /// This instead cross-fades once at the start and once at the end,
        /// looping MoveTowards/TurnTo across every leg in between.
        /// </summary>
        public IEnumerator MoveAlong(Vector3[] waypoints, float speed, CabinIdleProfile walkProfile = CabinIdleProfile.Walking)
        {
            if (waypoints == null || waypoints.Length == 0) yield break;

            if (_driver == null)
            {
                foreach (Vector3 waypoint in waypoints)
                {
                    while (Vector3.Distance(transform.position, waypoint) > 0.05f)
                    {
                        transform.position = Vector3.MoveTowards(transform.position, waypoint, speed * Time.deltaTime);
                        TurnTo(waypoint);
                        yield return null;
                    }
                }
                yield break;
            }

            _driver.PlayState(walkProfile == CabinIdleProfile.Carrying ? "Walk_Carry" : "Walk", 0.2f);
            SetSolid(false);

            foreach (Vector3 waypoint in waypoints)
            {
                while (Vector3.Distance(transform.position, waypoint) > 0.05f)
                {
                    TurnTo(waypoint);
                    transform.position = Vector3.MoveTowards(transform.position, waypoint, speed * Time.deltaTime);
                    yield return null;
                }
                transform.position = waypoint;
            }

            SetSolid(true);
            _driver.PlayProfile(walkProfile == CabinIdleProfile.Carrying ? CabinIdleProfile.Carrying : CabinIdleProfile.Controlled);
        }
    }
}
