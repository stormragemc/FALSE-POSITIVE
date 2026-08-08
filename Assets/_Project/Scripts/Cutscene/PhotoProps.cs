using UnityEngine;

namespace FalsePositive.Cutscene
{
    /// <summary>
    /// The three photographs in docs/STORY_SCRIPT.md §4 P3_VERDICT: the printed
    /// group photo Spassky slides across the table, and the school and wedding
    /// photos Priya holds up inside CS-16A.
    ///
    /// Built procedurally rather than authored as prefabs, for the same reason
    /// MemorySceneDressing builds its props in code — a photograph is a textured
    /// quad, and a prefab per image would be three assets to keep in sync with
    /// three textures.
    ///
    /// Nothing here is ever left in the scene: every photo is parented to the
    /// surface or bone it belongs to and destroyed with the beat that made it.
    /// </summary>
    public static class PhotoProps
    {
        private const string ResourceRoot = "Photos/";

        /// <summary>Standard 6x4 print, in metres. The school photo is the same
        /// shape; the wedding photo is shown on a phone and gets scaled down by
        /// its caller.</summary>
        private const float PrintWidth = 0.15f;
        private const float PrintHeight = 0.11f;

        /// <summary>Creates an unlit, double-sided quad carrying
        /// <paramref name="textureName"/> from Resources/Photos.</summary>
        public static GameObject Create(string textureName, float width, float height)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourceRoot + textureName);
            if (texture == null)
            {
                Debug.LogWarning($"[PhotoProps] No texture at Resources/{ResourceRoot}{textureName} — " +
                    "the beat will play without its photograph.");
                return null;
            }

            GameObject photo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            photo.name = "Photo_" + textureName;
            // A prop, not an obstacle: the player must never collide with it and
            // InteractionRaycaster must never pick it up as an Interactable.
            Object.Destroy(photo.GetComponent<Collider>());
            photo.transform.localScale = new Vector3(width, height, 1f);

            // Unlit so the print stays readable in the interrogation room's hard
            // key light and in the cabin's very low firelight, which is the whole
            // point of showing it.
            Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
            Renderer renderer = photo.GetComponent<Renderer>();
            renderer.material = new Material(shader) { mainTexture = texture };
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return photo;
        }

        /// <summary>Lays a photograph flat on whatever solid surface is under
        /// <paramref name="above"/>, facing up and turned to be read from
        /// <paramref name="readFrom"/>.
        ///
        /// Raycast rather than a hardcoded height: the interrogation Table is a
        /// bare Transform with no renderer, so its surface height cannot be read
        /// off the object, and guessing is what leaves props floating.</summary>
        public static GameObject LayOnSurface(string textureName, Vector3 above, Vector3 readFrom,
            float width = PrintWidth, float height = PrintHeight)
        {
            GameObject photo = Create(textureName, width, height);
            if (photo == null) return null;

            Vector3 restPoint = above;
            if (Physics.Raycast(above + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 3f))
            {
                restPoint = hit.point;
            }
            else
            {
                Debug.LogWarning("[PhotoProps] Nothing under the photograph to rest it on — " +
                    "falling back to the requested height.");
            }

            // Quad faces +Z by default; +90 on X lays it face-up.
            Vector3 toReader = readFrom - restPoint;
            toReader.y = 0f;
            float yaw = toReader.sqrMagnitude < 0.0001f
                ? 0f
                : Quaternion.LookRotation(toReader.normalized, Vector3.up).eulerAngles.y;

            photo.transform.SetPositionAndRotation(
                restPoint + Vector3.up * 0.002f,          // a paper's thickness off the surface
                Quaternion.Euler(90f, yaw, 0f));
            return photo;
        }

        /// <summary>Puts a photograph in an actor's hand, so it moves with them
        /// instead of hanging in the air beside them. Falls back to the actor's
        /// chest height if the rig has no hand bone.</summary>
        public static GameObject PutInHand(GameObject actor, Vector3 faceToward,
            string textureName, float width = PrintWidth, float height = PrintHeight)
        {
            GameObject photo = Create(textureName, width, height);
            if (photo == null || actor == null) return photo;

            Animator animator = actor.GetComponentInChildren<Animator>();
            Transform hand = animator != null && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;

            if (hand != null)
            {
                photo.transform.SetParent(hand, false);
                // Held out from the palm, roughly where a raised photo sits.
                photo.transform.localPosition = new Vector3(0.06f, 0.02f, 0f);
            }
            else
            {
                photo.transform.SetParent(actor.transform, false);
                photo.transform.localPosition = new Vector3(0f, 1.25f, 0.22f);
            }

            // Turned to be read by the viewer rather than by the character.
            Vector3 toViewer = faceToward - photo.transform.position;
            toViewer.y = 0f;
            if (toViewer.sqrMagnitude > 0.0001f)
            {
                photo.transform.rotation = Quaternion.LookRotation(-toViewer.normalized, Vector3.up);
            }
            return photo;
        }

        public static void Discard(GameObject photo)
        {
            if (photo != null) Object.Destroy(photo);
        }
    }
}
