using System;
using System.Collections.Generic;
using UnityEngine;

namespace FalsePositive.CabinNight
{
    public enum CabinIdleProfile
    {
        Confrontational,
        Controlled,
        Guarded,
        Sleeping
    }

    /// <summary>
    /// Adds restrained, deterministic movement to the static o3n character poses.
    /// Multiple matching bones are supported because the body, clothes, and hair
    /// use separate but identically named skeletons.
    /// </summary>
    public sealed class CabinCharacterIdle : MonoBehaviour
    {
        [SerializeField] private CabinIdleProfile profile;
        [SerializeField] private float seed;

        private readonly List<BoneState> _spines = new();
        private readonly List<BoneState> _necks = new();
        private readonly List<BoneState> _heads = new();

        public void Configure(CabinIdleProfile idleProfile, float animationSeed)
        {
            profile = idleProfile;
            seed = animationSeed;
        }

        private void Awake()
        {
            CacheBones("Spine1", _spines);
            CacheBones("Neck", _necks);
            CacheBones("Head", _heads);
        }

        private void LateUpdate()
        {
            float time = Time.time + seed * 7.31f;
            float breathingSpeed = profile == CabinIdleProfile.Sleeping ? 1.15f : 1.7f;
            float breath = Mathf.Sin(time * breathingSpeed);
            float drift = Mathf.Sin(time * 0.31f + seed * 4.7f);

            float breathDegrees = profile switch
            {
                CabinIdleProfile.Confrontational => 0.65f,
                CabinIdleProfile.Controlled => 0.28f,
                CabinIdleProfile.Guarded => 0.45f,
                CabinIdleProfile.Sleeping => 1.1f,
                _ => 0.4f
            };

            float headYaw = profile switch
            {
                CabinIdleProfile.Confrontational => drift * 0.8f,
                CabinIdleProfile.Controlled => drift * 0.35f,
                CabinIdleProfile.Guarded => drift * 1.5f,
                CabinIdleProfile.Sleeping => drift * 0.18f,
                _ => 0f
            };

            Apply(_spines, Quaternion.Euler(breath * breathDegrees, 0f, 0f));
            Apply(_necks, Quaternion.Euler(-breath * breathDegrees * 0.25f, headYaw * 0.35f, 0f));
            Apply(_heads, Quaternion.Euler(0f, headYaw, drift * 0.22f));
        }

        private void CacheBones(string boneName, ICollection<BoneState> target)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == boneName)
                {
                    target.Add(new BoneState(child, child.localRotation));
                }
            }
        }

        private static void Apply(IEnumerable<BoneState> bones, Quaternion offset)
        {
            foreach (BoneState bone in bones)
            {
                if (bone.Transform != null)
                {
                    bone.Transform.localRotation = bone.RestRotation * offset;
                }
            }
        }

        [Serializable]
        private readonly struct BoneState
        {
            public BoneState(Transform transform, Quaternion restRotation)
            {
                Transform = transform;
                RestRotation = restRotation;
            }

            public Transform Transform { get; }
            public Quaternion RestRotation { get; }
        }
    }
}
