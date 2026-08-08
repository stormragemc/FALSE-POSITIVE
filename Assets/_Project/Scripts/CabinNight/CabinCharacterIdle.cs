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
        Sleeping,
        Panicked,
        Walking,
        Carrying,
        Kneeling
    }

    /// <summary>
    /// Adds restrained, deterministic movement to the static cabin character poses.
    /// Multiple matching bones remain supported for the o3n player/accessory setup;
    /// the staged named cast uses one shared Avaturn skeleton per character.
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
                CabinIdleProfile.Panicked => 1.4f,
                CabinIdleProfile.Walking => 0.5f,
                CabinIdleProfile.Carrying => 0.9f,
                CabinIdleProfile.Kneeling => 0.6f,
                _ => 0.4f
            };

            float headYaw = profile switch
            {
                CabinIdleProfile.Confrontational => drift * 0.8f,
                CabinIdleProfile.Controlled => drift * 0.35f,
                CabinIdleProfile.Guarded => drift * 1.5f,
                CabinIdleProfile.Sleeping => drift * 0.18f,
                CabinIdleProfile.Panicked => drift * 2.2f,
                CabinIdleProfile.Walking => drift * 0.4f,
                CabinIdleProfile.Carrying => drift * 0.15f,
                CabinIdleProfile.Kneeling => drift * 0.3f,
                _ => 0f
            };

            Apply(_spines, Quaternion.Euler(breath * breathDegrees, 0f, 0f));
            Apply(_necks, Quaternion.Euler(-breath * breathDegrees * 0.25f, headYaw * 0.35f, 0f));
            Apply(_heads, Quaternion.Euler(0f, headYaw, drift * 0.22f));
        }

        private void CacheBones(string boneName, ICollection<BoneState> target)
        {
            // Dedupe by Transform reference — necessary now that Apply live-
            // samples localRotation instead of writing from a cached rest
            // pose (see Apply's doc comment). Body/clothes/hair are separate
            // skeletons with identically-named bones, but AddAccessory
            // (CabinNightCharacterBuilder) remaps every accessory renderer's
            // bones onto the body's own transforms, so the same Transform can
            // legitimately turn up more than once in this search. Applying a
            // live-sampled offset to the same Transform twice in one frame
            // would compound it — the head would slowly rotate off over time
            // instead of settling into a fixed breathing amplitude.
            HashSet<Transform> seen = new();
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                if (child.name == boneName && seen.Add(child))
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
                    // Live-sampled, not RestRotation * offset: CabinCharacterIdle
                    // now runs alongside CabinAnimatorDriver's Animator, which
                    // writes these same bones (Spine1/Neck/Head) every frame
                    // before LateUpdate. RestRotation was a frozen snapshot
                    // from Awake — applying it here would silently replace
                    // whatever pose the Animator just wrote with that frozen
                    // rest pose instead of adding breathing on top of it.
                    bone.Transform.localRotation = bone.Transform.localRotation * offset;
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
