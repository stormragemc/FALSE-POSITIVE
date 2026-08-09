using UnityEngine;

namespace FalsePositive.CabinNight
{
    /// <summary>
    /// Puts a cast member's feet on the floor and sizes their collider to the
    /// pose they are actually in.
    ///
    /// Two separate defects made the cast sink, and this fixes both:
    ///
    /// 1. NOTHING IN THE PROJECT EVER QUERIED THE GROUND. Every character Y was
    ///    a hand-authored literal — 0 for the cabin floor, CabinV2Builder.
    ///    StairHeight(2.7) for the two upstairs, and a hand-patched -0.38 on
    ///    the sleeping Priya. Those literals do not track the room-height raise
    ///    (the ceiling slab's top face, which is what Aaron and Ivy stand on,
    ///    is translated by CeilingRise to 3.25 while their authored y is
    ///    3.026) and they know nothing about the snow outside, which sits at
    ///    roughly -0.19 .. -0.34 rather than 0.
    /// 2. NO FOOT IK. CabinPoseLibrary deliberately drops the hips for the
    ///    Kneeling (-0.28), Sleeping (-0.12) and Seated (-0.31) poses, and
    ///    Unity's humanoid solver has no foot IK, so the legs follow the hips
    ///    straight down through the floor. The drops are correct — a kneeling
    ///    person's hips ARE lower — they just need the body re-grounded
    ///    afterwards. That is the layering here: the pose lowers the hips, then
    ///    this lifts the feet back to the ground.
    ///
    /// Runtime assembly, not Editor, on purpose: the same code has to run at
    /// build time (CabinNightCharacterBuilder, so the saved prefab and scene
    /// are already correct and nothing pops on the first frame) and in play
    /// mode (CabinAnimatorDriver, after a pose crossfade settles). One
    /// implementation, one set of numbers.
    /// </summary>
    public static class CabinFootPlanter
    {
        /// <summary>The humanoid foot bone is the ANKLE, not the sole — leave a
        /// boot's worth of clearance under it or the character sinks to the
        /// shins. Measured value carried over from the original
        /// Cutscene.CutsceneStage.PlantFeet, which this replaced.</summary>
        public const float SoleToAnkle = 0.09f;

        /// <summary>Probe start height above the character's authored root.
        /// Has to clear the tallest thing anyone legitimately stands ON (the
        /// ceiling slab's top face is 0.22 above Aaron and Ivy's authored y)
        /// without starting above the cabin ceiling for someone on the ground
        /// floor — the ceiling's underside is at 3.05, so 2 is safe.</summary>
        private const float ProbeUp = 2f;

        private const float ProbeDistance = 8f;

        /// <summary>Sub-half-centimetre corrections are below the noise floor of
        /// a crossfaded pose and are not worth a transform write every time a
        /// profile changes.</summary>
        private const float MinLift = 0.005f;

        /// <summary>Limb thickness the bone skeleton does not carry — bones are
        /// centre-lines, so the solid volume has to be grown out from them.</summary>
        private const float LimbPadding = 0.12f;

        private const float MinCapsuleRadius = 0.2f;

        /// <summary>Grounds the character and re-fits its collider to the pose.
        /// Safe to call repeatedly: everything below is measured absolutely
        /// from the current pose, so a second call on an already-planted
        /// character is a no-op.</summary>
        public static void PlantAndFit(GameObject character, CabinIdleProfile profile)
        {
            float groundY = Plant(character, profile);
            if (!float.IsNaN(groundY)) FitCollider(character, groundY);
        }

        /// <summary>Drops the character onto the ground under it and lifts its
        /// feet onto that ground. Returns the ground Y it settled on, or NaN if
        /// the character has no humanoid rig to measure.</summary>
        public static float Plant(GameObject character, CabinIdleProfile profile)
        {
            if (character == null) return float.NaN;

            // Transforms written this frame (the pose bake, or a root move by
            // ScriptedActor) are not in the physics scene until it syncs, and
            // the probe below is a physics query. Cheap, and legal in edit mode.
            Physics.SyncTransforms();

            Transform root = character.transform;
            float groundY = FindGroundY(root, SupportsBodyWeight(profile));

            Vector3 position = root.position;
            position.y = groundY;
            root.position = position;

            PlantFeetOnto(character, groundY);
            return groundY;
        }

        /// <summary>Finds the surface the character is standing on with an
        /// actual collider query, as opposed to trusting an authored literal.
        /// Falls back to the terrain heightmap (for the case where the exterior
        /// TerrainCollider is missing — see MemoryExteriorBounds' class doc for
        /// how that has happened before) and finally to the authored Y, which
        /// is always safe because it is what the project did before this
        /// existed.</summary>
        public static float FindGroundY(Transform root, bool allowFurnitureSupport)
        {
            Vector3 origin = root.position + Vector3.up * ProbeUp;
            RaycastHit[] hits = Physics.RaycastAll(
                origin, Vector3.down, ProbeDistance, ~0, QueryTriggerInteraction.Ignore);

            float best = float.NegativeInfinity;
            bool anyHit = false;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null) continue;

                // The character's own capsule is directly under the probe.
                // Same self-exclusion Cutscene.CutsceneStage already does when
                // it drops Prop_NickBody onto the snow.
                if (hit.collider.transform.IsChildOf(root)) continue;

                anyHit = true;
                if (!allowFurnitureSupport && !IsGroundSurface(hit.collider)) continue;
                if (hit.point.y > best) best = hit.point.y;
            }

            if (!float.IsNegativeInfinity(best)) return best;

            if (!anyHit)
            {
                Terrain terrain = Terrain.activeTerrain;
                if (terrain != null)
                {
                    return terrain.transform.position.y + terrain.SampleHeight(root.position);
                }
            }

            return root.position.y;
        }

        /// <summary>Which colliders count as ground you can stand on.
        ///
        /// CabinV2Builder splits the cabin exactly along this line already: the
        /// structural shell it can be legitimate to stand on — floor, ceiling
        /// slab (which is the upper storey's floor), stairs, railing, hearth —
        /// gets MeshColliders, and the furniture gets BoxColliders. The snow
        /// outside is a TerrainCollider.
        ///
        /// The distinction matters because a downward probe cannot otherwise
        /// tell "the floor I am standing on" from "the table my knees are
        /// under". Every swapped-in stool and the table carry a full-height
        /// BoxCollider from y=0 to their top face (SwapOneFurnitureObject), so
        /// a seated actor at the table would otherwise be planted on the
        /// tabletop. Poses that genuinely rest ON furniture opt back in via
        /// SupportsBodyWeight.</summary>
        private static bool IsGroundSurface(Collider collider)
        {
            return collider is TerrainCollider || collider is MeshCollider;
        }

        /// <summary>True for poses whose body weight is carried by furniture
        /// rather than by the floor. Only Sleeping qualifies: the night staging
        /// has Priya asleep on BO_Sofa, whose collider is a BoxCollider and so
        /// is invisible to the structural probe above. Seated does NOT qualify
        /// — a seated person's hips are on the stool but their FEET are on the
        /// floor, and the feet are what this plants.</summary>
        private static bool SupportsBodyWeight(CabinIdleProfile profile)
        {
            return profile == CabinIdleProfile.Sleeping;
        }

        /// <summary>Lifts the skeleton so the lower foot's sole rests on
        /// groundY.
        ///
        /// Uses the humanoid foot BONES, not renderer bounds. A
        /// SkinnedMeshRenderer's bounds are a conservative box that does not
        /// hug the animated pose: measuring those over-lifted the whole cast by
        /// ~0.35 m and left them visibly hovering with their feet at y = 0.40.
        /// (Kept from the original CutsceneStage.PlantFeet, where it was found
        /// the hard way.)</summary>
        public static void PlantFeetOnto(GameObject character, float groundY)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            Transform leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Transform rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            if (leftFoot == null && rightFoot == null) return;

            float lowest = leftFoot == null ? rightFoot.position.y
                : rightFoot == null ? leftFoot.position.y
                : Mathf.Min(leftFoot.position.y, rightFoot.position.y);

            float scale = Mathf.Approximately(character.transform.lossyScale.y, 0f)
                ? 1f : character.transform.lossyScale.y;

            float lift = groundY + SoleToAnkle * scale - lowest;
            if (Mathf.Abs(lift) < MinLift) return;

            // The Animator lives on the "Body" child; moving that rather than
            // the root leaves the root — which owns the collider and is what
            // cutscene movement drives — exactly where it was placed.
            animator.transform.localPosition += new Vector3(0f, lift / scale, 0f);
        }

        /// <summary>Re-sizes the character's collider around the pose it is
        /// actually in.
        ///
        /// Every cast member shipped with the same hardcoded 1.72 x 0.27
        /// upright capsule centred at y 0.86, which is the right shape for
        /// exactly one of the poses in the room. For a seated, kneeling or
        /// sleeping character that capsule is mostly empty air above them
        /// while the visible body sticks out of its side — you walk through the
        /// person and bump into nothing. Measured from bones for the same
        /// reason PlantFeetOnto measures from bones.</summary>
        public static void FitCollider(GameObject character, float groundY)
        {
            Animator animator = character.GetComponentInChildren<Animator>();
            if (animator == null || !animator.isHuman) return;

            Transform root = character.transform;
            bool any = false;
            Bounds local = new Bounds();
            for (HumanBodyBones bone = 0; bone < HumanBodyBones.LastBone; bone++)
            {
                Transform t = animator.GetBoneTransform(bone);
                if (t == null) continue;

                Vector3 point = root.InverseTransformPoint(t.position);
                if (!any)
                {
                    local = new Bounds(point, Vector3.zero);
                    any = true;
                }
                else
                {
                    local.Encapsulate(point);
                }
            }

            if (!any) return;

            float scale = Mathf.Approximately(root.lossyScale.y, 0f) ? 1f : root.lossyScale.y;
            local.Expand(2f * LimbPadding / scale);

            // The root now sits on the ground (Plant moved it there), so local
            // y 0 IS the ground. Clamping the underside to it stops the padded
            // box from burying a lip of collider below the floor, which reads
            // as a snag when the player walks up to someone.
            float bottom = Mathf.Max(local.min.y, 0f);
            float top = Mathf.Max(local.max.y, bottom + 0.05f);
            local.SetMinMax(new Vector3(local.min.x, bottom, local.min.z),
                new Vector3(local.max.x, top, local.max.z));

            float height = local.size.y;
            float width = Mathf.Max(local.size.x, local.size.z);

            if (height > width)
            {
                CapsuleCollider capsule = Replace<CapsuleCollider>(character);
                capsule.direction = 1;
                capsule.radius = Mathf.Max(width * 0.5f, MinCapsuleRadius);
                capsule.height = Mathf.Max(height, capsule.radius * 2f);
                capsule.center = local.center;
            }
            else
            {
                // Lying down (Priya asleep on the sofa). A capsule standing on
                // end here would be a pillar through a horizontal body.
                BoxCollider box = Replace<BoxCollider>(character);
                box.center = local.center;
                box.size = local.size;
            }
        }

        /// <summary>Returns the character's collider as T, swapping the
        /// component out if it is currently the other shape. Characters change
        /// pose at runtime (CabinAnimatorDriver.PlayProfile), so the collider
        /// has to be able to change shape with them.</summary>
        private static T Replace<T>(GameObject character) where T : Collider
        {
            T wanted = character.GetComponent<T>();
            bool wasEnabled = true;
            bool sawOne = false;

            foreach (Collider existing in character.GetComponents<Collider>())
            {
                // CharacterController derives from Collider. It belongs to the
                // player's movement, is never a pose-fitted body volume, and
                // destroying it would take the player's floor away.
                if (existing is CharacterController) continue;

                if (!sawOne)
                {
                    wasEnabled = existing.enabled;
                    sawOne = true;
                }

                if (existing == wanted) continue;
                if (Application.isPlaying) Object.Destroy(existing);
                else Object.DestroyImmediate(existing);
            }

            if (wanted != null) return wanted;

            wanted = character.AddComponent<T>();
            // A cutscene may have switched collision off for the walk that is
            // still in progress (Cutscene.ScriptedActor.MoveTo) — a swapped-in
            // collider must not quietly switch it back on.
            wanted.enabled = wasEnabled;
            return wanted;
        }
    }
}
