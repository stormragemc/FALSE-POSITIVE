using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Reports the state of the things the bootstrap builders are supposed to have
    /// produced in the two memory scenes, so a rebuild can be checked without
    /// clicking through the Inspector — and, more to the point, so it can be
    /// checked in batch mode.
    ///
    /// This exists because FirstPersonArmBuilder failed silently once already: it
    /// logged a warning, the build still reported success, and nothing downstream
    /// noticed the arm was missing. Everything here is a measurement, not an
    /// assertion — read the numbers, they are the point.
    /// </summary>
    public static class CabinFixVerifier
    {
        private const string NightScene = "Assets/_Project/Scenes/Memory_CabinNight.unity";
        private const string MorningScene = "Assets/_Project/Scenes/Memory_CabinMorning.unity";

        [MenuItem("Tools/False Positive/Diagnostics/Verify Cabin Fixes")]
        public static void Verify()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("[CabinFixVerifier]");
            Report(report, NightScene);
            Report(report, MorningScene);
            Debug.Log(report.ToString());
        }

        private static void Report(StringBuilder report, string scenePath)
        {
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            report.AppendLine("=== " + System.IO.Path.GetFileNameWithoutExtension(scenePath));

            report.AppendLine("  fog=" + RenderSettings.fog + " mode=" + RenderSettings.fogMode
                + " density=" + RenderSettings.fogDensity.ToString("0.0000")
                + " color=" + RenderSettings.fogColor
                + " ambient=" + RenderSettings.ambientMode
                + " skybox=" + (RenderSettings.skybox == null ? "NONE" : RenderSettings.skybox.name));

            GameObject ring = GameObject.Find("Environment/Distance Fog Ring");
            report.AppendLine("  fogRing=" + (ring == null ? "ABSENT" : Describe(ring)));

            GameObject characters = GameObject.Find("Characters");
            if (characters == null)
            {
                report.AppendLine("  Characters root ABSENT");
                return;
            }

            foreach (Transform child in characters.transform)
            {
                report.AppendLine("  " + DescribeCharacter(child.gameObject));
            }
        }

        private static string Describe(GameObject go)
        {
            MeshFilter filter = go.GetComponent<MeshFilter>();
            MeshRenderer renderer = go.GetComponent<MeshRenderer>();
            return "active=" + go.activeInHierarchy
                + " mesh=" + (filter == null || filter.sharedMesh == null ? "NONE" : filter.sharedMesh.name)
                + " material=" + (renderer == null || renderer.sharedMaterial == null
                    ? "NONE" : renderer.sharedMaterial.shader.name);
        }

        private static string DescribeCharacter(GameObject character)
        {
            StringBuilder line = new StringBuilder();
            line.Append(character.name)
                .Append(" active=").Append(character.activeSelf)
                .Append(" y=").Append(character.transform.position.y.ToString("0.000"));

            Animator animator = character.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                Transform left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Transform right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                float sole = Mathf.Min(
                    left == null ? float.MaxValue : left.position.y,
                    right == null ? float.MaxValue : right.position.y);
                if (sole < float.MaxValue) line.Append(" ankleY=").Append(sole.ToString("0.000"));
                line.Append(" bodyLocalY=").Append(animator.transform.localPosition.y.ToString("0.000"));
            }

            CapsuleCollider capsule = character.GetComponent<CapsuleCollider>();
            BoxCollider box = character.GetComponent<BoxCollider>();
            if (capsule != null)
            {
                line.Append(" capsule h=").Append(capsule.height.ToString("0.00"))
                    .Append(" r=").Append(capsule.radius.ToString("0.00"))
                    .Append(" c=").Append(capsule.center.ToString("F2"));
            }
            else if (box != null)
            {
                line.Append(" box size=").Append(box.size.ToString("F2"))
                    .Append(" c=").Append(box.center.ToString("F2"));
            }
            else
            {
                line.Append(" collider=NONE");
            }

            Transform arm = character.transform.Find(FirstPersonArmBuilder.ArmObjectName);
            if (arm != null)
            {
                SkinnedMeshRenderer renderer = arm.GetComponent<SkinnedMeshRenderer>();
                Mesh mesh = renderer == null ? null : renderer.sharedMesh;
                line.Append(" | arm verts=").Append(mesh == null ? -1 : mesh.vertexCount)
                    .Append(" tris=").Append(mesh == null ? -1 : mesh.triangles.Length / 3)
                    .Append(" submeshes=").Append(mesh == null ? -1 : mesh.subMeshCount)
                    .Append(" bounds=").Append(mesh == null ? "-" : mesh.bounds.size.ToString("F2"))
                    .Append(" enabled=").Append(renderer != null && renderer.enabled);
            }
            else if (character.name.StartsWith("Player"))
            {
                line.Append(" | arm=ABSENT");
            }

            line.Append(" | under: ").Append(ProbeUnder(character.transform));
            return line.ToString();
        }

        /// <summary>Everything a downward probe from head height finds under this
        /// character, nearest first — the same query CabinNight.CabinFootPlanter
        /// runs, spelled out so a surprising ground Y can be traced to the
        /// collider that produced it.</summary>
        private static string ProbeUnder(Transform root)
        {
            RaycastHit[] hits = Physics.RaycastAll(root.position + Vector3.up * 2f, Vector3.down,
                8f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            StringBuilder found = new StringBuilder();
            int shown = 0;
            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null || hit.collider.transform.IsChildOf(root)) continue;
                found.Append(hit.collider.name).Append('(').Append(hit.collider.GetType().Name)
                    .Append(")@").Append(hit.point.y.ToString("0.000")).Append(' ');
                if (++shown >= 4) break;
            }

            return found.Length == 0 ? "NOTHING" : found.ToString();
        }
    }
}
