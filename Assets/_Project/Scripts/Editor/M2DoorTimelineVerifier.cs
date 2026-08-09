using System.Linq;
using System.Text;
using FalsePositive.Cutscene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Scrubs M2_DoorOpen.playable in edit mode and asserts the things that are
    /// hard to see by eye but ruin the beat when wrong.
    ///
    /// This is the automated half of "scrub the timeline and check it": it drives
    /// the same PlayableDirector the game does, one frame at a time, and measures
    /// the proxy rig's actual evaluated pose rather than the curve values the
    /// builder thinks it wrote. That catches a bad track offset, an orphaned
    /// binding, a 360-degree yaw step, a camera key sunk under the snow, or an
    /// aim that drifts off the body — none of which are visible in the asset.
    ///
    /// Read-only: opens the scene, evaluates, and never saves.
    /// </summary>
    public static class M2DoorTimelineVerifier
    {
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";

        /// <summary>All mirrored from M2DoorTimelineBuilder. Duplicated on purpose:
        /// a verifier that imports the builder's own numbers can only prove the
        /// builder agrees with itself.</summary>
        private static readonly Vector3 AimPoint = new Vector3(2.3f, 0.25f, -6.3f);
        private const float EyeHeight = 1.64f;
        private const float TEnd = 13.40f;
        private const float HoldStart = 5.90f;
        private const float HoldEnd = 6.50f;
        private const float Fps = 30f;

        private const float DoorSwingStart = 1.85f;
        private const float DoorSwingEnd = 2.95f;
        private const float DoorOpenYawDegrees = 100f;

        /// <summary>FreeLookCameraRig clamps handback pitch to
        /// InterrogationConfig.standingPitchClampDegrees; a final pose past this
        /// would be silently flattened on the frame control returns.</summary>
        private const float PitchClamp = 85f;

        [MenuItem("Tools/False Positive/Diagnostics/Verify M2 Door Timeline")]
        public static void Verify()
        {
            EditorSceneManager.OpenScene(MorningScenePath, OpenSceneMode.Single);

            var report = new StringBuilder();
            int failures = 0;

            GameObject sequencing = GameObject.Find("Sequencing");
            PlayableDirector director = sequencing != null ? sequencing.GetComponent<PlayableDirector>() : null;
            if (director == null || !(director.playableAsset is TimelineAsset timeline))
            {
                Debug.LogError("[M2Verify] No PlayableDirector with a TimelineAsset on 'Sequencing' — " +
                               "run 'Tools/False Positive/Bootstrap/T05 - Build M2 Door Timeline' first.");
                return;
            }

            report.AppendLine($"[M2Verify] {timeline.name}: duration {timeline.duration:F2}s, " +
                              $"{timeline.GetOutputTracks().Count()} tracks, wrap={director.extrapolationMode}.");

            // The component's serialized wiring. Play() degrades to "no cinematic"
            // rather than soft-locking if this is null, which is the right runtime
            // behaviour and exactly the wrong thing to discover at runtime.
            M2DoorOpenSequence sequence = sequencing.GetComponent<M2DoorOpenSequence>();
            Vector3 doorMark = Vector3.zero;
            bool hasMark = false;
            if (sequence == null)
            {
                failures++;
                report.AppendLine("  *** no M2DoorOpenSequence on 'Sequencing' — CutsceneStage falls back to the old coroutine beat");
            }
            else
            {
                var serialized = new SerializedObject(sequence);
                foreach (string field in new[] { "director", "proxyRoot", "proxyPitch" })
                {
                    SerializedProperty property = serialized.FindProperty(field);
                    bool wired = property != null && property.objectReferenceValue != null;
                    report.AppendLine($"  sequence.{field} -> {(wired ? property.objectReferenceValue.name : "*** NULL ***")}");
                    if (!wired) failures++;
                }

                doorMark = serialized.FindProperty("doorMark").vector3Value;
                hasMark = true;
                report.AppendLine($"  sequence.doorMark {doorMark.ToString("F3")} " +
                                  $"yaw {serialized.FindProperty("doorMarkYaw").floatValue:F1}");
            }

            // 1. Bindings. An orphaned binding plays a perfect cinematic that
            //    drives nothing, which is exactly how the project's first
            //    Timeline attempt died (ASSETS_TODO.md section 1).
            Transform proxyRoot = null;
            Transform proxyPitch = null;
            Transform door = null;
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                Object bound = director.GetGenericBinding(track);
                report.AppendLine($"  track '{track.name}' -> {(bound == null ? "*** NOT BOUND ***" : bound.name)}");
                if (bound == null)
                {
                    failures++;
                    continue;
                }

                if (track.name == "Camera" && bound is Animator camAnimator)
                {
                    proxyRoot = camAnimator.transform.Find("M2_DoorCam_Root");
                    proxyPitch = proxyRoot != null ? proxyRoot.Find("M2_DoorCam_Pitch") : null;
                }

                if (track.name == "Door" && bound is Animator doorAnimator) door = doorAnimator.transform;
            }

            if (proxyRoot == null || proxyPitch == null)
            {
                Debug.LogError(report + "\n[M2Verify] Camera proxy nodes not found under the bound Animator.");
                return;
            }

            // 2. Scrub. Same call pattern the editor's own preview uses.
            director.extrapolationMode = DirectorWrapMode.None;
            director.RebuildGraph();

            int samples = Mathf.RoundToInt(TEnd * Fps) + 1;
            float maxYawStep = 0f;
            float worstYawStepAt = 0f;
            float minPitch = float.MaxValue;
            float maxPitch = float.MinValue;
            float maxAimError = 0f;
            float worstAimAt = 0f;
            float maxSpeed = 0f;
            float maxSink = 0f;
            float worstSinkAt = 0f;
            float previousYaw = 0f;
            Vector3 previousPosition = Vector3.zero;
            Vector3 startPosition = Vector3.zero;
            Vector3 holdStartPosition = Vector3.zero;
            float holdStartYaw = 0f;
            float holdStartPitch = 0f;
            Vector3 holdEndPosition = Vector3.zero;
            float holdEndYaw = 0f;
            float holdEndPitch = 0f;
            Vector3 endPosition = Vector3.zero;
            float endPitch = 0f;

            Physics.SyncTransforms();

            for (int i = 0; i < samples; i++)
            {
                float t = i / Fps;
                director.time = t;
                director.Evaluate();

                Vector3 position = proxyRoot.position;
                float yaw = proxyRoot.eulerAngles.y;
                float pitch = Signed(proxyPitch.localEulerAngles.x);

                minPitch = Mathf.Min(minPitch, pitch);
                maxPitch = Mathf.Max(maxPitch, pitch);

                if (i == 0) startPosition = position;

                if (i > 0)
                {
                    // DeltaAngle, so a genuine 360-degree wrap in the baked curve
                    // reads as ~0 here and only a real discontinuity shows up.
                    float step = Mathf.Abs(Mathf.DeltaAngle(previousYaw, yaw));
                    if (step > maxYawStep) { maxYawStep = step; worstYawStepAt = t; }
                    maxSpeed = Mathf.Max(maxSpeed, Vector3.Distance(previousPosition, position) * Fps);
                }

                // Aim: from the fixed hold onward the body must stay centred.
                if (t >= HoldStart - 0.001f)
                {
                    Vector3 eye = position + Vector3.up * EyeHeight;
                    Vector3 want = (AimPoint - eye).normalized;
                    Vector3 have = Quaternion.Euler(pitch, yaw, 0f) * Vector3.forward;
                    float error = Vector3.Angle(want, have);
                    if (error > maxAimError) { maxAimError = error; worstAimAt = t; }
                }

                // Ground clearance: the proxy root is the player's feet, so a
                // negative clearance is the camera walking through the snow.
                if (t >= HoldStart &&
                    Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 6f, ~0,
                        QueryTriggerInteraction.Ignore))
                {
                    float sink = hit.point.y - position.y;
                    if (sink > maxSink) { maxSink = sink; worstSinkAt = t; }
                }

                if (Mathf.Abs(t - HoldStart) < 0.5f / Fps)
                {
                    holdStartPosition = position; holdStartYaw = yaw; holdStartPitch = pitch;
                }

                if (Mathf.Abs(t - HoldEnd) < 0.5f / Fps)
                {
                    holdEndPosition = position; holdEndYaw = yaw; holdEndPitch = pitch;
                }

                previousYaw = yaw;
                previousPosition = position;
                endPosition = position;
                endPitch = pitch;
            }

            // The mark and the path's first key are separate constants in separate
            // files (M2DoorOpenSequence.doorMark and M2DoorTimelineBuilder.DoorMark).
            // If they drift, the player is snapped somewhere the camera path does
            // not start and frame one of the cinematic is a jump.
            if (hasMark)
            {
                float drift = Vector3.Distance(doorMark, startPosition);
                report.AppendLine($"  door mark vs camera path t=0: {startPosition.ToString("F3")}, {drift:F4} m apart");
                if (drift > 0.01f)
                {
                    failures++;
                    report.AppendLine("    *** the snap target and the path's first key have drifted apart");
                }
            }

            report.AppendLine($"  yaw: largest single-frame step {maxYawStep:F2} deg at t={worstYawStepAt:F2}s " +
                              $"({maxYawStep * Fps:F0} deg/s)");
            report.AppendLine($"  pitch range: {minPitch:F2} .. {maxPitch:F2} deg (clamp +-{PitchClamp})");
            report.AppendLine($"  aim error from t={HoldStart}s: max {maxAimError:F3} deg at t={worstAimAt:F2}s");
            report.AppendLine($"  peak travel speed: {maxSpeed:F2} m/s");
            report.AppendLine($"  deepest the feet sit below the ground under them: {maxSink:F3} m at t={worstSinkAt:F2}s");
            report.AppendLine($"  fixed hold {HoldStart}-{HoldEnd}s: " +
                              $"moved {Vector3.Distance(holdStartPosition, holdEndPosition):F4} m, " +
                              $"yaw {Mathf.Abs(Mathf.DeltaAngle(holdStartYaw, holdEndYaw)):F3} deg, " +
                              $"pitch {Mathf.Abs(holdStartPitch - holdEndPitch):F3} deg " +
                              $"-> pose {holdStartPosition.ToString("F3")} yaw {holdStartYaw:F2} pitch {holdStartPitch:F2}");
            report.AppendLine($"  ends at {endPosition.ToString("F3")}, " +
                              $"{Vector3.Distance(endPosition, new Vector3(AimPoint.x, endPosition.y, AimPoint.z)):F2} m from the body, " +
                              $"handback pitch {endPitch:F2} deg");

            // 3. Door swing, measured off the transform rather than the curve.
            if (door != null)
            {
                // Hinge position as well as angle: a root-motion track that
                // re-bases its position curve parks the door at the world origin
                // for the whole pre-swing hold, which the angle alone won't show.
                director.time = 0d;
                director.Evaluate();
                Vector3 restPosition = door.position;

                Quaternion closed = Quaternion.Euler(270f, 0f, 0f);
                float before = SampleDoorAngle(director, door, closed, DoorSwingStart - 0.1f);
                Vector3 beforePosition = door.position;
                float after = SampleDoorAngle(director, door, closed, DoorSwingEnd + 0.1f);
                Vector3 afterPosition = door.position;
                float atEnd = SampleDoorAngle(director, door, closed, TEnd);

                report.AppendLine($"  door: {before:F2} deg before the swing, {after:F2} deg after, {atEnd:F2} deg at the end " +
                                  $"(target {DoorOpenYawDegrees})");
                report.AppendLine($"  door hinge: {restPosition.ToString("F3")} at t=0, {beforePosition.ToString("F3")} " +
                                  $"pre-swing, {afterPosition.ToString("F3")} post-swing");

                if (before > 0.5f) { failures++; report.AppendLine("    *** door is not closed when the swing starts"); }
                if (Mathf.Abs(after - DoorOpenYawDegrees) > 1f) { failures++; report.AppendLine("    *** door did not reach the open angle"); }
                if (Vector3.Distance(restPosition, beforePosition) > 0.001f ||
                    Vector3.Distance(restPosition, afterPosition) > 0.001f)
                {
                    failures++;
                    report.AppendLine("    *** the door hinge moves — the swing is translating the door, not rotating it");
                }
            }
            else
            {
                failures++;
                report.AppendLine("  *** no Door track binding — the door will not swing");
            }

            // 4. Verdicts.
            if (maxYawStep > 12f)
            {
                failures++;
                report.AppendLine($"    *** yaw jumps {maxYawStep:F1} deg in one frame — a wrap or a bad key, not a pan");
            }

            if (Mathf.Abs(minPitch) > PitchClamp || Mathf.Abs(maxPitch) > PitchClamp)
            {
                failures++;
                report.AppendLine("    *** pitch leaves the rig's clamp range");
            }

            if (maxAimError > 1.5f)
            {
                failures++;
                report.AppendLine($"    *** camera drifts {maxAimError:F2} deg off the body");
            }

            if (maxSink > 0.06f)
            {
                failures++;
                report.AppendLine($"    *** camera sits {maxSink:F2} m under the ground — the snow height moved");
            }

            if (Vector3.Distance(holdStartPosition, holdEndPosition) > 0.001f ||
                Mathf.Abs(Mathf.DeltaAngle(holdStartYaw, holdEndYaw)) > 0.01f ||
                Mathf.Abs(holdStartPitch - holdEndPitch) > 0.01f)
            {
                failures++;
                report.AppendLine("    *** the 'fixed rotation and position' hold is not actually still");
            }

            director.time = 0d;
            director.Evaluate();
            director.Stop();

            report.AppendLine(failures == 0
                ? "[M2Verify] PASS — every check clean."
                : $"[M2Verify] {failures} FAILURE(S) — see the *** lines above.");

            if (failures == 0) Debug.Log(report.ToString());
            else Debug.LogError(report.ToString());
        }

        private static float SampleDoorAngle(PlayableDirector director, Transform door, Quaternion closed, float t)
        {
            director.time = t;
            director.Evaluate();
            return Quaternion.Angle(closed, door.rotation);
        }

        private static float Signed(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            return degrees;
        }
    }
}
