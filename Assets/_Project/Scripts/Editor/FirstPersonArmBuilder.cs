using System.Collections.Generic;
using Unity.Collections;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Carves a right-arm-only skinned mesh out of the player's existing body
    /// skin and hangs it on the player rig as a hideable viewmodel.
    ///
    /// Why this exists: Reach_DoorKnob (CabinAnimationBuilder.BuildReachDoorKnob)
    /// has always been authored, and the M2 door Timeline has always played it on
    /// the player's Body Animator — but CabinNightCharacterBuilder.ConfigurePlayer
    /// sets EVERY player renderer to ShadowsOnly, because a first-person player
    /// standing inside their own head is worse than no body at all. The reach
    /// therefore ran perfectly and was completely invisible. The player body is a
    /// single skinned mesh (o3n_male_unified_LOD1), so there was no sub-renderer
    /// to leave visible; the arm had to be extracted.
    ///
    /// The extracted mesh REUSES smr.bones and mesh.bindposes verbatim, so bone
    /// indices in the copied weights stay valid with no remapping and the new
    /// renderer is skinned by the very same skeleton the Animator drives. That is
    /// the whole trick: no second clip, no retarget, no AvatarMask (the project
    /// has none), and the arm cannot drift out of sync with the body it was cut
    /// from.
    ///
    /// Called from the END of ConfigurePlayer — after the ShadowsOnly loop, or
    /// the arm would be shadow-only too and we would be back where we started.
    /// Cutscene.M2DoorOpenSequence switches it on for the duration of the door
    /// beat and off again on handback; there is no authored idle or walk arm
    /// motion, so a permanently visible viewmodel would hang rigid in frame for
    /// the rest of the game.
    /// </summary>
    public static class FirstPersonArmBuilder
    {
        /// <summary>Name of the viewmodel GameObject under the player root.
        /// M2DoorOpenSequence looks it up by this name.</summary>
        public const string ArmObjectName = "FirstPersonArm";

        private const string MeshFolder = "Assets/_Project/CabinNight/Meshes";
        private const string MeshPath = MeshFolder + "/FirstPersonArm_Right.mesh";

        /// <summary>How much of a vertex's skin weight has to land on the arm
        /// chain before it counts as arm. At 0.5 the cut falls in the shoulder
        /// blend where deltoid weight hands over to the chest — high enough that
        /// no torso shell comes along, low enough that the deltoid cap does.</summary>
        private const float ArmWeightThreshold = 0.5f;

        /// <summary>Where the arm is cut off.
        ///
        /// RightUpperArm and not RightShoulder: the o3n rig hangs ClavicleAdjust_R
        /// and TrapeziusAdjust_R off the clavicle, and those two carry the top of
        /// the shoulder and the slab of upper back beside the neck. Rooting at the
        /// clavicle would drag both into the viewmodel; rooting at the upper arm
        /// puts the seam in a ring around the top of the deltoid, which is the
        /// ordinary first-person cut and sits at the very bottom edge of a 66-degree
        /// frame even at the door beat's steepest pitch.</summary>
        private const HumanBodyBones ArmRootBone = HumanBodyBones.RightUpperArm;

        /// <summary>Used only if the Avatar leaves the upper arm unmapped — one
        /// joint further up is still an arm, just a more generous one.</summary>
        private const HumanBodyBones ArmRootFallbackBone = HumanBodyBones.RightShoulder;

        public static void Build(GameObject player)
        {
            if (player == null) return;

            Animator animator = player.GetComponentInChildren<Animator>(true);
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                Debug.LogWarning("[FirstPersonArmBuilder] No humanoid Animator under the player — " +
                                 "no first-person arm built.");
                return;
            }

            Transform armRoot = animator.GetBoneTransform(ArmRootBone)
                                ?? animator.GetBoneTransform(ArmRootFallbackBone);
            if (armRoot == null)
            {
                Debug.LogWarning("[FirstPersonArmBuilder] Avatar maps neither " + ArmRootBone +
                                 " nor " + ArmRootFallbackBone + " — no first-person arm built.");
                return;
            }

            // Pick the source by measurement rather than by name: the body FBX
            // also carries eye/eyelash/mouth skins, and those have exactly zero
            // weight on the arm chain, so "most arm vertices" identifies the body
            // mesh without hardcoding o3n_male_unified_LOD1.
            SkinnedMeshRenderer source = null;
            bool[] sourceMask = null;
            int bestCount = 0;
            foreach (SkinnedMeshRenderer candidate in player.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (candidate.name == ArmObjectName) continue;

                bool[] mask = MarkArmVertices(candidate, armRoot);
                if (mask == null) continue;

                int count = 0;
                foreach (bool isArm in mask)
                {
                    if (isArm) count++;
                }

                if (count <= bestCount) continue;
                bestCount = count;
                source = candidate;
                sourceMask = mask;
            }

            if (source == null)
            {
                Debug.LogWarning("[FirstPersonArmBuilder] No skinned mesh on the player has vertices weighted " +
                                 "to the right arm — no first-person arm built.");
                return;
            }

            Mesh armMesh = Extract(source, sourceMask, out Material[] materials);
            if (armMesh == null)
            {
                Debug.LogWarning("[FirstPersonArmBuilder] Arm extraction produced no triangles — " +
                                 "no first-person arm built.");
                return;
            }

            Attach(player, source, armMesh, materials);
        }

        /// <summary>Flags every vertex whose summed skin weight onto the arm
        /// clears the threshold. Returns null for anything that isn't a usable
        /// skin (no mesh, no bones, nothing under the arm root, unskinned
        /// vertices).
        ///
        /// The keep-set is "every skin bone at or below armRoot in the hierarchy",
        /// NOT "the humanoid arm bones". On the o3n rig the skin is bound to
        /// helper joints only — UpperarmAdjust_R, UpperarmAdjustTwist_R,
        /// LowerarmAdjust_R, LowerarmAdjustTwist_R and the carpal/finger chain —
        /// and Upperarm_R / Lowerarm_R / hand_R themselves appear nowhere in
        /// renderer.bones. Matching Animator.GetBoneTransform results by identity
        /// therefore found exactly zero arm vertices. The helpers are all
        /// descendants of the humanoid joint they follow, so a descendant test
        /// picks them up while staying rig-agnostic.</summary>
        private static bool[] MarkArmVertices(SkinnedMeshRenderer renderer, Transform armRoot)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null) return null;

            Transform[] bones = renderer.bones;
            if (bones == null || bones.Length == 0) return null;

            HashSet<int> chainIndices = new HashSet<int>();
            for (int i = 0; i < bones.Length; i++)
            {
                // IsChildOf is true for the transform itself, so a rig that DOES
                // skin straight to the humanoid joint is covered as well.
                if (bones[i] != null && bones[i].IsChildOf(armRoot)) chainIndices.Add(i);
            }

            if (chainIndices.Count == 0) return null;

            // Read-only views owned by the Mesh — allocated with Allocator.None,
            // so they must NOT be disposed. GetAllBoneWeights is used rather than
            // the legacy BoneWeight[] because that one silently truncates to the
            // four heaviest influences, and a shoulder vertex on this rig can
            // carry more than four.
            NativeArray<byte> influenceCounts = mesh.GetBonesPerVertex();
            NativeArray<BoneWeight1> weights = mesh.GetAllBoneWeights();
            if (influenceCounts.Length != mesh.vertexCount) return null;

            bool[] mask = new bool[mesh.vertexCount];
            int cursor = 0;
            for (int v = 0; v < mask.Length; v++)
            {
                int influences = influenceCounts[v];
                float armWeight = 0f;
                for (int i = 0; i < influences; i++)
                {
                    BoneWeight1 weight = weights[cursor + i];
                    if (chainIndices.Contains(weight.boneIndex)) armWeight += weight.weight;
                }

                cursor += influences;
                mask[v] = armWeight >= ArmWeightThreshold;
            }

            return mask;
        }

        /// <summary>Builds the arm mesh asset from the flagged vertices. A triangle
        /// survives only if all three of its corners are arm — a half-in triangle
        /// stretches from the arm to whatever torso vertex it still references,
        /// which is a spike across the frame, not a ragged edge.</summary>
        private static Mesh Extract(SkinnedMeshRenderer renderer, bool[] mask, out Material[] materials)
        {
            materials = null;
            Mesh mesh = renderer.sharedMesh;
            Material[] sourceMaterials = renderer.sharedMaterials;

            bool[] used = new bool[mesh.vertexCount];
            List<int[]> keptSubmeshes = new List<int[]>();
            List<Material> keptMaterials = new List<Material>();

            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != MeshTopology.Triangles) continue;

                int[] triangles = mesh.GetTriangles(submesh);
                List<int> kept = new List<int>();
                for (int t = 0; t + 2 < triangles.Length; t += 3)
                {
                    int a = triangles[t];
                    int b = triangles[t + 1];
                    int c = triangles[t + 2];
                    if (!mask[a] || !mask[b] || !mask[c]) continue;

                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                    used[a] = true;
                    used[b] = true;
                    used[c] = true;
                }

                if (kept.Count == 0) continue;
                keptSubmeshes.Add(kept.ToArray());
                keptMaterials.Add(submesh < sourceMaterials.Length ? sourceMaterials[submesh] : null);
            }

            if (keptSubmeshes.Count == 0) return null;

            // Compact: keep only the vertices the surviving triangles reference.
            // The face submesh drops out entirely here, which is the point — the
            // whole mesh minus one arm is exactly what must not be on screen.
            int[] remap = new int[mesh.vertexCount];
            List<int> order = new List<int>();
            for (int v = 0; v < used.Length; v++)
            {
                if (!used[v])
                {
                    remap[v] = -1;
                    continue;
                }

                remap[v] = order.Count;
                order.Add(v);
            }

            int count = order.Count;
            Vector3[] sourceVertices = mesh.vertices;
            Vector3[] sourceNormals = mesh.normals;
            Vector4[] sourceTangents = mesh.tangents;
            Vector2[] sourceUv = mesh.uv;
            Vector2[] sourceUv2 = mesh.uv2;
            Color[] sourceColors = mesh.colors;

            Vector3[] vertices = new Vector3[count];
            Vector3[] normals = sourceNormals.Length == mesh.vertexCount ? new Vector3[count] : null;
            Vector4[] tangents = sourceTangents.Length == mesh.vertexCount ? new Vector4[count] : null;
            Vector2[] uv = sourceUv.Length == mesh.vertexCount ? new Vector2[count] : null;
            Vector2[] uv2 = sourceUv2.Length == mesh.vertexCount ? new Vector2[count] : null;
            Color[] colors = sourceColors.Length == mesh.vertexCount ? new Color[count] : null;

            for (int i = 0; i < count; i++)
            {
                int v = order[i];
                vertices[i] = sourceVertices[v];
                if (normals != null) normals[i] = sourceNormals[v];
                if (tangents != null) tangents[i] = sourceTangents[v];
                if (uv != null) uv[i] = sourceUv[v];
                if (uv2 != null) uv2[i] = sourceUv2[v];
                if (colors != null) colors[i] = sourceColors[v];
            }

            NativeArray<byte> influenceCounts = mesh.GetBonesPerVertex();
            NativeArray<BoneWeight1> weights = mesh.GetAllBoneWeights();
            int[] weightOffsets = new int[mesh.vertexCount];
            int cursor = 0;
            for (int v = 0; v < mesh.vertexCount; v++)
            {
                weightOffsets[v] = cursor;
                cursor += influenceCounts[v];
            }

            byte[] armInfluenceCounts = new byte[count];
            List<BoneWeight1> armWeights = new List<BoneWeight1>(count * 4);
            for (int i = 0; i < count; i++)
            {
                int v = order[i];
                int influences = influenceCounts[v];
                armInfluenceCounts[i] = (byte)influences;
                for (int k = 0; k < influences; k++)
                {
                    armWeights.Add(weights[weightOffsets[v] + k]);
                }
            }

            Mesh arm = LoadOrCreateMeshAsset();
            arm.Clear();
            arm.indexFormat = count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            arm.SetVertices(vertices);
            if (normals != null) arm.SetNormals(normals);
            if (tangents != null) arm.SetTangents(tangents);
            if (uv != null) arm.SetUVs(0, uv);
            if (uv2 != null) arm.SetUVs(1, uv2);
            if (colors != null) arm.SetColors(colors);

            // Bindposes are indexed by BONE, not by vertex, so the source array
            // transfers wholesale and every copied boneIndex stays correct.
            arm.bindposes = mesh.bindposes;

            NativeArray<byte> nativeCounts = new NativeArray<byte>(armInfluenceCounts, Allocator.Temp);
            NativeArray<BoneWeight1> nativeWeights = new NativeArray<BoneWeight1>(armWeights.ToArray(), Allocator.Temp);
            arm.SetBoneWeights(nativeCounts, nativeWeights);
            nativeCounts.Dispose();
            nativeWeights.Dispose();

            arm.subMeshCount = keptSubmeshes.Count;
            for (int submesh = 0; submesh < keptSubmeshes.Count; submesh++)
            {
                int[] triangles = keptSubmeshes[submesh];
                for (int i = 0; i < triangles.Length; i++)
                {
                    triangles[i] = remap[triangles[i]];
                }

                arm.SetTriangles(triangles, submesh);
            }

            arm.RecalculateBounds();
            arm.name = "FirstPersonArm_Right";

            if (!AssetDatabase.Contains(arm))
            {
                System.IO.Directory.CreateDirectory(MeshFolder);
                AssetDatabase.CreateAsset(arm, MeshPath);
            }
            else
            {
                EditorUtility.SetDirty(arm);
            }

            AssetDatabase.SaveAssets();

            materials = keptMaterials.ToArray();
            return arm;
        }

        /// <summary>Reuses the existing asset when there is one so its GUID — and
        /// therefore every prefab and scene reference already pointing at it —
        /// survives a rebuild. Only a first run creates a new asset.</summary>
        private static Mesh LoadOrCreateMeshAsset()
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            return existing != null ? existing : new Mesh();
        }

        private static void Attach(GameObject player, SkinnedMeshRenderer source, Mesh armMesh, Material[] materials)
        {
            Transform existing = player.transform.Find(ArmObjectName);
            GameObject arm = existing != null ? existing.gameObject : new GameObject(ArmObjectName);
            arm.transform.SetParent(player.transform, false);
            arm.transform.localPosition = Vector3.zero;
            arm.transform.localRotation = Quaternion.identity;
            arm.transform.localScale = Vector3.one;

            SkinnedMeshRenderer renderer = arm.GetComponent<SkinnedMeshRenderer>();
            if (renderer == null) renderer = arm.AddComponent<SkinnedMeshRenderer>();

            renderer.sharedMesh = armMesh;
            renderer.sharedMaterials = materials;

            // A SkinnedMeshRenderer is posed by its bones and bindposes, not by
            // its own transform, so this can sit beside Body rather than under
            // it — which keeps it clear of the ShadowsOnly sweep, of
            // CabinFootPlanter's Body-local lift, and of M2DoorOpenSequence's
            // per-frame re-pin of Body's local transform.
            renderer.bones = source.bones;
            renderer.rootBone = source.rootBone;

            // The arm swings roughly a metre out of its bind-pose bounds during
            // the reach; without this it culls itself at the exact moment the
            // camera is looking straight at it.
            renderer.updateWhenOffscreen = true;

            // Off, not ShadowsOnly: a shadow-casting viewmodel throws a
            // disembodied forearm across the doorway. The body skin next to it
            // still casts the player's real shadow.
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;

            renderer.enabled = false;
        }
    }
}
