using System;
using System.Collections.Generic;
using UnityEngine;

namespace FalsePositive.CabinNight
{
    /// <summary>
    /// Per-character muscle deltas layered on top of the shared
    /// CabinIdleProfile pose every frame, via HumanPoseHandler — the same
    /// mechanism CabinPoseTunerWindow previews with in the Editor. Exists
    /// because CabinCast.controller's baked clips are shared by every cast
    /// member (CabinAnimationBuilder bakes one clip set from CabinPoseLibrary
    /// for the whole cast), so a change to CabinPoseLibrary moves all four
    /// seated flashback characters identically. This is the per-instance
    /// escape hatch: Nick can slouch further than Aaron on the same
    /// SeatedForward profile without touching the shared clips.
    ///
    /// Added to a cast member by CabinPoseTunerWindow's Character mode, not
    /// by CabinNightCharacterBuilder — most cast members never need this, so
    /// it isn't baked into every prefab by default. Safe to add by hand too:
    /// enableOverrides defaults false, so an added-but-unconfigured component
    /// changes nothing.
    ///
    /// Must run BEFORE CabinCharacterIdle's LateUpdate, or SetHumanPose here
    /// would overwrite the Spine1/Neck/Head bone tilt CabinCharacterIdle just
    /// applied (muscles vs. direct bone writes both ultimately touch the
    /// skeleton, and whichever runs last wins). DefaultExecutionOrder(-50)
    /// guarantees that without relying on script compilation/instance order,
    /// which Unity does not otherwise define between two default-order
    /// LateUpdate scripts. Mirrors the precedent for the interrogation cop —
    /// ProjectBootstrapBuilder.WireCopModel forces CopTalkGestureAnimator
    /// after CopIdleAnimator for the identical reason, via MonoImporter
    /// instead of this attribute.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class CabinPoseOverride : MonoBehaviour
    {
        [Serializable]
        public struct MuscleOverride
        {
            public string muscleName;
            public float value;
        }

        // False by default so a component added with nothing configured yet
        // (e.g. by the tuner, before the first slider drag) changes nothing —
        // matches the "gate, don't rely on field initializers" rule that also
        // applies to CabinCharacterIdle's new override fields.
        [SerializeField] private bool enableOverrides;
        [SerializeField] private List<MuscleOverride> muscles = new List<MuscleOverride>();

        private Animator _animator;
        private HumanPoseHandler _handler;
        private HumanPose _pose;

        public bool EnableOverrides
        {
            get => enableOverrides;
            set => enableOverrides = value;
        }

        public List<MuscleOverride> Muscles => muscles;

        private void Awake()
        {
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.avatar != null && _animator.avatar.isHuman)
            {
                _handler = new HumanPoseHandler(_animator.avatar, _animator.transform);
                _pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            }
        }

        private void OnDestroy() => _handler?.Dispose();

        private void LateUpdate()
        {
            if (!enableOverrides || _handler == null || muscles.Count == 0) return;

            _handler.GetHumanPose(ref _pose);
            for (int i = 0; i < muscles.Count; i++)
            {
                CabinPoseLibrary.SetMuscle(ref _pose, muscles[i].muscleName, muscles[i].value);
            }
            _handler.SetHumanPose(ref _pose);
        }
    }
}
