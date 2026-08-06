using System.Collections;
using FalsePositive.CabinNight;
using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// Procedural cutscene motion for a cast member — no .anim assets, no
    /// Animator Controller, per the plan's explicit "procedural only" choice.
    /// Drives the same HumanPose muscle vocabulary CabinNightCharacterBuilder
    /// bakes at build time (via the shared CabinPoseLibrary), plus a simple
    /// procedural leg/arm counter-swing layered on top while actually moving.
    /// CutsceneStage owns *when* each of these runs per beat; this only knows
    /// how to move/turn/pose one actor.
    ///
    /// CabinCharacterIdle (breathing/sway) keeps running on the same object
    /// underneath — that component only touches Spine1/Neck/Head bones, this
    /// one touches the full-body HumanPose plus root transform, so they don't
    /// fight each other.
    /// </summary>
    public sealed class ScriptedActor : MonoBehaviour
    {
        private Animator _animator;
        private HumanPoseHandler _poseHandler;
        private HumanPose _pose;
        private bool _hasPose;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.avatar != null && _animator.avatar.isHuman)
            {
                _poseHandler = new HumanPoseHandler(_animator.avatar, _animator.transform);
                _poseHandler.GetHumanPose(ref _pose);
                _hasPose = true;
            }
        }

        private void OnDestroy()
        {
            _poseHandler?.Dispose();
        }

        /// <summary>Applies a static profile pose immediately (no walk cycle).</summary>
        public void PlayPose(CabinIdleProfile profile)
        {
            if (!_hasPose) return;
            CabinPoseLibrary.Apply(ref _pose, profile);
            _poseHandler.SetHumanPose(ref _pose);
        }

        /// <summary>Instant yaw snap to face a point (e.g. the door, on a forced head-turn beat).</summary>
        public void TurnTo(Vector3 worldPoint)
        {
            Vector3 flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        }

        /// <summary>
        /// Walks to a world position over time, applying the Walking (or
        /// Carrying) pose with a procedural counter-swing layered on top so
        /// the walk doesn't read as a statue sliding across the floor.
        /// </summary>
        public IEnumerator MoveTo(Vector3 worldPosition, float speed, CabinIdleProfile walkProfile = CabinIdleProfile.Walking)
        {
            if (!_hasPose)
            {
                // No humanoid avatar (shouldn't happen for cast members, but
                // don't hard-fail a cutscene over it) — just translate.
                while (Vector3.Distance(transform.position, worldPosition) > 0.05f)
                {
                    transform.position = Vector3.MoveTowards(transform.position, worldPosition, speed * Time.deltaTime);
                    TurnTo(worldPosition);
                    yield return null;
                }
                yield break;
            }

            CabinPoseLibrary.Apply(ref _pose, walkProfile);
            float strideRate = walkProfile == CabinIdleProfile.Carrying ? 3.2f : 5.5f;
            float strideDegrees = walkProfile == CabinIdleProfile.Carrying ? 6f : 14f;

            while (Vector3.Distance(transform.position, worldPosition) > 0.05f)
            {
                TurnTo(worldPosition);
                transform.position = Vector3.MoveTowards(transform.position, worldPosition, speed * Time.deltaTime);

                float swing = Mathf.Sin(Time.time * strideRate) * strideDegrees;
                CabinPoseLibrary.SetMuscle(ref _pose, "Left Upper Leg Front-Back", swing / 30f + (walkProfile == CabinIdleProfile.Carrying ? 0f : 0.12f));
                CabinPoseLibrary.SetMuscle(ref _pose, "Right Upper Leg Front-Back", -swing / 30f - (walkProfile == CabinIdleProfile.Carrying ? 0f : 0.12f));
                if (walkProfile != CabinIdleProfile.Carrying)
                {
                    CabinPoseLibrary.SetMuscle(ref _pose, "Left Arm Front-Back", -swing / 20f);
                    CabinPoseLibrary.SetMuscle(ref _pose, "Right Arm Front-Back", swing / 20f);
                }
                _poseHandler.SetHumanPose(ref _pose);
                yield return null;
            }

            transform.position = worldPosition;
            CabinPoseLibrary.Apply(ref _pose, walkProfile == CabinIdleProfile.Carrying ? CabinIdleProfile.Carrying : CabinIdleProfile.Controlled);
            _poseHandler.SetHumanPose(ref _pose);
        }
    }
}
