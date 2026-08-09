using System;
using System.IO;
using System.Linq;
using FalsePositive.CabinNight;
using FalsePositive.Cutscene;
using FalsePositive.Flow;
using FalsePositive.Interaction;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Builds the hand-editable Timeline beat for the radio-tuning cutscene
    /// (CutsceneId.RadioClears): a Player Body animation clip (muscle-space,
    /// baked the same way CabinAnimationBuilder bakes cast idles/poses), a
    /// Camera animation clip (Transform curves, pushing the first-person view
    /// down toward the radio), and the Cutscene_RadioTune.playable Timeline
    /// asset that plays them alongside the existing tuning-sweep/lock-on SFX.
    ///
    /// Idempotent by construction, the specific failure that retired
    /// CopAnimationBuilder's Timeline: existing .anim assets are left
    /// completely alone unless force is set, and the .playable's tracks are
    /// resolved by name and never deleted/recreated, so a scene binding set
    /// on them survives every re-run. See RadioTuneTimelineBuilder.WireNightScene
    /// for the scene-side half (director setup, track bindings, CutsceneStage
    /// wiring) — this class only ever authors the two assets below.
    /// </summary>
    public static class RadioTuneTimelineBuilder
    {
        private const string AnimRoot = "Assets/_Project/Art/Animations/RadioTune/";
        private const string TimelineRoot = "Assets/_Project/Art/Timelines/";
        private const string TimelinePath = TimelineRoot + "Cutscene_RadioTune.playable";
        private const string SfxRoot = "Assets/_Project/Art/Audio/SFX/";
        private const string NightScenePath = "Assets/_Project/Scenes/Memory_CabinNight.unity";
        private const string PersistentScenePath = "Assets/_Project/Scenes/_Persistent.unity";
        private const string PlayerPrefabPath = "Assets/_Project/CabinNight/Prefabs/Player_FirstPerson.prefab";
        private const string ShirtMaterialPath = "Assets/_Project/CabinNight/Materials/MaleBodyJeansShirt.mat";
        private const string PlayerName = "Player (Male - First Person)";

        public const string BodyClipName = "RadioTune_Body";
        public const string CameraClipName = "RadioTune_Camera";

        public const string TrackBody = "Player Body";
        public const string TrackCamera = "Camera";
        public const string TrackStatic = "Radio Static";
        public const string TrackTune = "Radio Tune";

        // 7.40s @ 60fps — see the plan's beat table (reach 0-1.10, knob turn
        // 1.55-3.05, hold to 6.30 through the storm-warning VO, return by 7.40).
        public const float Duration = 7.40f;
        private const float ReachTime = 1.10f;
        private const float HoldEnd = 6.30f;

        // Camera framing — see BuildCameraClip. CameraRestY/CameraRestZ must
        // stay equal to the prefab's FirstPersonView localPosition and to
        // CutsceneStage.RadioTune's viewRestPos, or the view snaps when the
        // Timeline takes over at t=0 and again when it hands back at Duration.
        private const float CameraRestY = 1.64f;
        private const float CameraRestZ = 0.04f;
        private const float CameraPushZ = 0.10f;
        private const float CameraHoldPitch = 35.6f;
        private const float CameraSettleTime = 0.90f;

        /// <summary>Builds (or repairs) both .anim assets and the .playable's
        /// track/clip structure. Existing .anim curves are left untouched
        /// unless force is set — that is what makes hand-tuning them in the
        /// Animation window stick across re-runs.</summary>
        public static void EnsureAssetsBuilt(bool force)
        {
            Directory.CreateDirectory(AnimRoot);
            Directory.CreateDirectory(TimelineRoot);
            AssetDatabase.Refresh();

            AnimationClip bodyClip = BuildBodyClip(force);
            AnimationClip cameraClip = BuildCameraClip(force);
            BuildTimelineAsset(bodyClip, cameraClip);

            AssetDatabase.SaveAssets();
            Debug.Log("[RadioTuneTimelineBuilder] RadioTune clips + Cutscene_RadioTune.playable built.");
        }

        // ---- Menu entries ----

        [MenuItem("Tools/False Positive/Bootstrap/9c - Build Radio Tune Timeline")]
        public static void BuildAndWire()
        {
            EnsureAssetsBuilt(false);
            WireNightScene();
        }

        [MenuItem("Tools/False Positive/Bootstrap/9c! - Rebake Radio Tune Animation")]
        public static void RebakeAndWire()
        {
            EnsureAssetsBuilt(true);
            WireNightScene();
        }

        /// <summary>Headless-runnable check for the specific regression that
        /// retired CopAnimationBuilder's Timeline (a binding gone null on
        /// re-run) plus the other failure modes called out in the plan. Uses
        /// real Unity-API resolution (GetGenericBinding etc.), not YAML text
        /// matching, so a hand-broken binding is caught the same way a
        /// stale one from a bad re-run would be. Logs one error per problem
        /// and a final PASS/FAIL summary; never throws, so a batch run can
        /// check all of them in one pass and grep the log for LogError.</summary>
        [MenuItem("Tools/False Positive/Bootstrap/9c? - Verify Radio Tune Wiring")]
        public static void VerifyNightScene()
        {
            bool ok = true;

            EditorSceneManager.OpenScene(NightScenePath, OpenSceneMode.Single);

            M1NightController controller = UnityEngine.Object.FindAnyObjectByType<M1NightController>();
            if (controller == null)
            {
                Debug.LogError("[RadioTuneTimelineBuilder] Verify: no M1NightController in Memory_CabinNight.");
                ok = false;
            }
            else if (new SerializedObject(controller).FindProperty("radio").objectReferenceValue == null)
            {
                Debug.LogError("[RadioTuneTimelineBuilder] Verify: M1NightController.radio is null.");
                ok = false;
            }

            CutsceneStage stage = UnityEngine.Object.FindAnyObjectByType<CutsceneStage>();
            PlayableDirector director = null;
            if (stage == null)
            {
                Debug.LogError("[RadioTuneTimelineBuilder] Verify: no CutsceneStage in Memory_CabinNight.");
                ok = false;
            }
            else
            {
                SerializedObject stageSo = new SerializedObject(stage);
                director = stageSo.FindProperty("radioTuneDirector").objectReferenceValue as PlayableDirector;
                Transform anchor = stageSo.FindProperty("radioTuneAnchor").objectReferenceValue as Transform;
                if (director == null)
                {
                    Debug.LogError("[RadioTuneTimelineBuilder] Verify: CutsceneStage.radioTuneDirector is null.");
                    ok = false;
                }
                if (anchor == null)
                {
                    Debug.LogError("[RadioTuneTimelineBuilder] Verify: CutsceneStage.radioTuneAnchor is null.");
                    ok = false;
                }
            }

            if (director != null)
            {
                if (director.playableAsset == null)
                {
                    Debug.LogError("[RadioTuneTimelineBuilder] Verify: director.playableAsset is null.");
                    ok = false;
                }
                if (director.playOnAwake)
                {
                    Debug.LogError("[RadioTuneTimelineBuilder] Verify: director.playOnAwake is true (would fire on scene load).");
                    ok = false;
                }

                if (director.playableAsset is TimelineAsset timeline)
                {
                    foreach (TrackAsset track in timeline.GetOutputTracks())
                    {
                        bool isKnown = track.name == TrackBody || track.name == TrackCamera
                            || track.name == TrackStatic || track.name == TrackTune;
                        if (!isKnown) continue;

                        if (director.GetGenericBinding(track) == null)
                        {
                            Debug.LogError($"[RadioTuneTimelineBuilder] Verify: track '{track.name}' has no binding.");
                            ok = false;
                        }

                        if (track.name == TrackCamera && track is AnimationTrack cameraTrack)
                        {
                            foreach (TimelineClip clip in cameraTrack.GetClips())
                            {
                                if (clip.asset is AnimationPlayableAsset asset && asset.removeStartOffset)
                                {
                                    Debug.LogError(
                                        "[RadioTuneTimelineBuilder] Verify: Camera clip has removeStartOffset " +
                                        "enabled — Timeline will re-base the frame-0 pose onto the track's zero " +
                                        "offset and drop the camera from head height to the floor.");
                                    ok = false;
                                }
                            }
                        }
                    }

                    ok &= VerifyRecipeCoversTimeline(timeline.duration);
                }
            }

            Debug.Log(ok
                ? "[RadioTuneTimelineBuilder] Verify: PASS."
                : "[RadioTuneTimelineBuilder] Verify: FAIL — see errors above.");
        }

        /// <summary>The restore in CutsceneStage.RadioTune() runs before
        /// CutsceneDirector.Finished fires, but only if the recipe (which
        /// gates Finished) is at least as long as the Timeline it hosts —
        /// otherwise SomeoneLeft's door-yaw seed could land before RadioTune
        /// has handed control back. See the plan's restore-ordering note.</summary>
        private static bool VerifyRecipeCoversTimeline(double timelineDuration)
        {
            Scene persistentScene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Additive);
            try
            {
                CutsceneDirector director = null;
                foreach (GameObject root in persistentScene.GetRootGameObjects())
                {
                    director = root.GetComponentInChildren<CutsceneDirector>(true);
                    if (director != null) break;
                }
                if (director == null)
                {
                    Debug.LogError("[RadioTuneTimelineBuilder] Verify: no CutsceneDirector in _Persistent.unity.");
                    return false;
                }

                SerializedProperty recipesProp = new SerializedObject(director).FindProperty("recipes");
                for (int i = 0; i < recipesProp.arraySize; i++)
                {
                    SerializedProperty recipeProp = recipesProp.GetArrayElementAtIndex(i);
                    if ((CutsceneId)recipeProp.FindPropertyRelative("id").enumValueIndex != CutsceneId.RadioClears) continue;

                    bool ok = true;
                    if (!recipeProp.FindPropertyRelative("keepScreenLit").boolValue)
                    {
                        Debug.LogError("[RadioTuneTimelineBuilder] Verify: RadioClears recipe.keepScreenLit is false.");
                        ok = false;
                    }

                    SerializedProperty beatsProp = recipeProp.FindPropertyRelative("beats");
                    double total = 0;
                    for (int b = 0; b < beatsProp.arraySize; b++)
                    {
                        SerializedProperty beat = beatsProp.GetArrayElementAtIndex(b);
                        AudioClip voClip = beat.FindPropertyRelative("voClip").objectReferenceValue as AudioClip;
                        total += voClip != null ? voClip.length : beat.FindPropertyRelative("holdSecondsIfNoClip").floatValue;
                    }
                    if (total < timelineDuration)
                    {
                        Debug.LogError(
                            $"[RadioTuneTimelineBuilder] Verify: RadioClears recipe ({total:F2}s) is shorter than the Timeline ({timelineDuration:F2}s).");
                        ok = false;
                    }
                    return ok;
                }

                Debug.LogError("[RadioTuneTimelineBuilder] Verify: no RadioClears recipe in _Persistent.unity.");
                return false;
            }
            finally
            {
                EditorSceneManager.CloseScene(persistentScene, true);
            }
        }

        // ---- Scene-side wiring ----

        /// <summary>Director setup, track bindings, RadioTuneAnchor placement
        /// and CutsceneStage field wiring for Memory_CabinNight, plus the
        /// player prefab material patch. Re-asserts every binding on every
        /// run — self-heals a hand-broken one, costs nothing when already
        /// correct. RadioTuneAnchor is a child of Prop_Radio, which dressing
        /// destroys and recreates on every run — fine, this recreates it
        /// too.</summary>
        public static void WireNightScene()
        {
            PatchPlayerPrefabMaterial();

            Scene scene = EditorSceneManager.OpenScene(NightScenePath, OpenSceneMode.Single);

            GameObject player = GameObject.Find(PlayerName);
            if (player == null)
            {
                throw new InvalidOperationException("[RadioTuneTimelineBuilder] Player not found in Memory_CabinNight.");
            }

            Transform view = player.transform.Find("FirstPersonView");
            if (view == null)
            {
                throw new InvalidOperationException("[RadioTuneTimelineBuilder] FirstPersonView not found on Player.");
            }

            // Inert with no controller outside Timeline — Timeline drives it
            // directly while the track has weight.
            Animator cameraAnimator = view.GetComponent<Animator>();
            if (cameraAnimator == null) cameraAnimator = view.gameObject.AddComponent<Animator>();
            cameraAnimator.applyRootMotion = false;
            cameraAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            Transform body = player.transform.Find("Body");
            Animator bodyAnimator = body != null ? body.GetComponent<Animator>() : null;
            if (bodyAnimator == null)
            {
                throw new InvalidOperationException(
                    "[RadioTuneTimelineBuilder] Player has no Body Animator — run the character bootstrap first.");
            }

            GameObject radioGo = GameObject.Find("Prop_Radio");
            if (radioGo == null)
            {
                throw new InvalidOperationException("[RadioTuneTimelineBuilder] Prop_Radio not found — run dressing/wiring first.");
            }

            RadioTuner radio = radioGo.GetComponent<RadioTuner>();
            SerializedObject radioSo = radio != null ? new SerializedObject(radio) : null;
            AudioSource staticSource = radioSo?.FindProperty("staticLoopSource").objectReferenceValue as AudioSource;
            AudioSource sfxSource = radioSo?.FindProperty("sfxSource").objectReferenceValue as AudioSource;
            if (staticSource == null || sfxSource == null)
            {
                throw new InvalidOperationException(
                    "[RadioTuneTimelineBuilder] Prop_Radio's AudioSources are unwired — run dressing first.");
            }

            Transform anchor = radioGo.transform.Find("RadioTuneAnchor");
            if (anchor == null)
            {
                GameObject anchorGo = new GameObject("RadioTuneAnchor");
                anchorGo.transform.SetParent(radioGo.transform, false);
                anchor = anchorGo.transform;
            }
            // World-space, not local — facing +Z, matching Prop_Radio's own
            // (unrotated) facing. See the plan's geometry check.
            anchor.position = new Vector3(-0.35f, 0f, 2.83f);
            anchor.rotation = Quaternion.identity;

            GameObject sequencing = GameObject.Find("Sequencing");
            if (sequencing == null)
            {
                throw new InvalidOperationException("[RadioTuneTimelineBuilder] Sequencing root not found.");
            }

            Transform directorTf = sequencing.transform.Find("RadioTuneDirector");
            GameObject directorGo = directorTf != null ? directorTf.gameObject : new GameObject("RadioTuneDirector");
            if (directorTf == null) directorGo.transform.SetParent(sequencing.transform, false);

            PlayableDirector director = directorGo.GetComponent<PlayableDirector>();
            if (director == null) director = directorGo.AddComponent<PlayableDirector>();

            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                throw new InvalidOperationException(
                    "[RadioTuneTimelineBuilder] Cutscene_RadioTune.playable missing — run EnsureAssetsBuilt first.");
            }

            director.playableAsset = timeline;
            director.playOnAwake = false; // default true would fire the beat on scene load
            director.extrapolationMode = DirectorWrapMode.None; // Hold never reaches Stopped; CutsceneStage.RadioTune waits on that

            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                switch (track.name)
                {
                    case TrackBody: director.SetGenericBinding(track, bodyAnimator); break;
                    case TrackCamera: director.SetGenericBinding(track, cameraAnimator); break;
                    case TrackStatic: director.SetGenericBinding(track, staticSource); break;
                    case TrackTune: director.SetGenericBinding(track, sfxSource); break;
                }
            }

            CutsceneStage stage = UnityEngine.Object.FindAnyObjectByType<CutsceneStage>();
            if (stage == null)
            {
                throw new InvalidOperationException("[RadioTuneTimelineBuilder] CutsceneStage not found in Memory_CabinNight.");
            }
            SetField(stage, "radioTuneDirector", director);
            SetField(stage, "radioTuneAnchor", anchor);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, NightScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("[RadioTuneTimelineBuilder] Memory_CabinNight wired for RadioTune.");
        }

        /// <summary>Surgical fix so the current baked prefab picks up the
        /// shirt material without a full CabinNightCharacterBuilder rebuild —
        /// see that class's ConfigurePlayer/AssignBodyMaterials for why slot
        /// [1] (not [0], the face) is the body slot, and why both the base
        /// and _LOD1 "unified" renderers need it, not just the one CutsceneStage
        /// makes visible.</summary>
        private static void PatchPlayerPrefabMaterial()
        {
            Material shirt = AssetDatabase.LoadAssetAtPath<Material>(ShirtMaterialPath);
            if (shirt == null)
            {
                Debug.LogWarning($"[RadioTuneTimelineBuilder] {ShirtMaterialPath} not found — player prefab material left unchanged.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabPath);
            try
            {
                bool changed = false;
                foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    if (!renderer.name.ToLowerInvariant().Contains("unified")) continue;

                    Material[] materials = renderer.sharedMaterials;
                    if (materials.Length < 2 || materials[1] == shirt) continue;

                    materials[1] = shirt;
                    renderer.sharedMaterials = materials;
                    changed = true;
                }

                if (changed) PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SetField(object target, string fieldName, object value)
        {
            var field = target.GetType().GetField(fieldName,
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
            if (field == null)
            {
                throw new InvalidOperationException($"[RadioTuneTimelineBuilder] {target.GetType().Name} has no field '{fieldName}'.");
            }
            field.SetValue(target, value);
        }

        // ---- Player Body clip (muscle-space, CabinAnimationBuilder's pattern) ----

        // Reach-pose targets from Controlled base — see the plan's "Right
        // arm (the knob hand) reach values" section. Frame 0 and the last
        // frame use the real Controlled base value for every muscle here
        // (read via BaseMuscles, never assumed), so the eases in/out of the
        // pose are invisible against the player's resting stance.
        private static AnimationClip BuildBodyClip(bool force)
        {
            AnimationClip clip = LoadOrCreateClip(AnimRoot + BodyClipName + ".anim", BodyClipName, out bool isNew);
            if (!isNew && !force) return clip;

            ClearAllCurves(clip);
            float[] baseMuscles = BaseMuscles(CabinIdleProfile.Controlled);

            WriteReachMuscle(clip, baseMuscles, "Right Arm Down-Up", -0.15f);
            WriteReachMuscle(clip, baseMuscles, "Right Arm Front-Back", 0.62f);
            WriteReachMuscle(clip, baseMuscles, "Right Forearm Stretch", -0.05f);
            WriteReachMuscle(clip, baseMuscles, "Right Shoulder Front-Back", 0.30f);
            WriteReachMuscle(clip, baseMuscles, "Left Arm Front-Back", 0.52f);
            WriteReachMuscle(clip, baseMuscles, "Left Arm Down-Up", -0.22f);
            WriteReachMuscle(clip, baseMuscles, "Head Nod Down-Up", -0.30f);
            WriteKnobTwist(clip, baseMuscles);
            WriteHeldMuscles(clip, baseMuscles);

            SetClipSettings(clip);
            return clip;
        }

        private static readonly string[] AnimatedMuscleNames =
        {
            "Right Arm Down-Up", "Right Arm Front-Back", "Right Forearm Stretch",
            "Right Shoulder Front-Back", "Right Forearm Twist In-Out",
            "Left Arm Front-Back", "Left Arm Down-Up", "Head Nod Down-Up",
        };

        /// <summary>base -> reach (at ReachTime) -> held to HoldEnd -> base
        /// (at Duration). The knob-hand muscles that also turn mid-hold use
        /// WriteKnobTwist instead.</summary>
        private static void WriteReachMuscle(AnimationClip clip, float[] baseMuscles, string muscleName, float reachValue)
        {
            float baseValue = BaseValue(baseMuscles, muscleName);
            Keyframe[] keys =
            {
                new Keyframe(0f, baseValue),
                new Keyframe(ReachTime, reachValue),
                new Keyframe(HoldEnd, reachValue),
                new Keyframe(Duration, baseValue),
            };
            WriteMuscle(clip, muscleName, SmoothCurve(keys));
        }

        /// <summary>The mimed knob turn: base while the arm reaches (0-1.10),
        /// then sweeps back and forth in time with the Timeline's audio track
        /// (sweep SFX at 1.45, lock-on at 3.00) before settling and holding
        /// through the VO, then returning to base.</summary>
        private static void WriteKnobTwist(AnimationClip clip, float[] baseMuscles)
        {
            const string muscle = "Right Forearm Twist In-Out";
            float baseValue = BaseValue(baseMuscles, muscle);
            Keyframe[] keys =
            {
                new Keyframe(0f, baseValue),
                new Keyframe(ReachTime, baseValue),
                new Keyframe(1.55f, -0.45f),
                new Keyframe(2.05f, 0.45f),
                new Keyframe(2.55f, -0.20f),
                new Keyframe(3.05f, 0.30f),
                new Keyframe(HoldEnd, 0.30f),
                new Keyframe(Duration, baseValue),
            };
            WriteMuscle(clip, muscle, SmoothCurve(keys));
        }

        private static void WriteHeldMuscles(AnimationClip clip, float[] baseMuscles)
        {
            for (int m = 0; m < 55; m++)
            {
                string name = HumanTrait.MuscleName[m];
                if (Array.IndexOf(AnimatedMuscleNames, name) >= 0) continue;
                WriteMuscle(clip, name, AnimationCurve.Constant(0f, Duration, baseMuscles[m]));
            }
        }

        // ---- Camera clip (Transform curves, first-person push toward the radio) ----

        private static AnimationClip BuildCameraClip(bool force)
        {
            AnimationClip clip = LoadOrCreateClip(AnimRoot + CameraClipName + ".anim", CameraClipName, out bool isNew);
            if (!isNew && !force) return clip;

            ClearAllCurves(clip);

            // The view holds the player's real standing eye height for the whole
            // beat and only pitches down — dipping it read as crouching over the
            // radio rather than looking at it. CameraHoldPitch is atan(dy/dz)
            // from the held camera to the centre of the radio's front face:
            // anchor (-0.35, 0, 2.83) + (0, CameraRestY, CameraRestZ + CameraPushZ)
            // sighting (-0.35, 1.35, 3.375), i.e. dy 0.29 over dz 0.405.
            //
            // Push and pitch trade off — pushing closer needs a steeper look-down
            // for the same target: 0.00 -> 29.9, 0.10 -> 35.6, 0.20 -> 43.6.
            // Some push is required because the body renderer is visible during
            // this beat (CutsceneStage.RadioTune step 3) and the pitch tips the
            // skull toward the 0.045 near-plane; raise both together if it clips.
            float holdZ = CameraRestZ + CameraPushZ;
            WriteTransformCurve(clip, "m_LocalPosition.x", (0f, 0f), (CameraSettleTime, 0f), (HoldEnd, 0f), (Duration, 0f));
            WriteTransformCurve(clip, "m_LocalPosition.y", (0f, CameraRestY), (CameraSettleTime, CameraRestY), (HoldEnd, CameraRestY), (Duration, CameraRestY));
            WriteTransformCurve(clip, "m_LocalPosition.z", (0f, CameraRestZ), (CameraSettleTime, holdZ), (HoldEnd, holdZ), (Duration, CameraRestZ));
            WriteTransformCurve(clip, "localEulerAnglesRaw.x", (0f, 0f), (CameraSettleTime, CameraHoldPitch), (HoldEnd, CameraHoldPitch), (Duration, 0f));
            WriteTransformCurve(clip, "localEulerAnglesRaw.y", (0f, 0f), (CameraSettleTime, 0f), (HoldEnd, 0f), (Duration, 0f));
            WriteTransformCurve(clip, "localEulerAnglesRaw.z", (0f, 0f), (CameraSettleTime, 0f), (HoldEnd, 0f), (Duration, 0f));

            SetClipSettings(clip);
            return clip;
        }

        private static void WriteTransformCurve(AnimationClip clip, string propertyName, params (float time, float value)[] points)
        {
            Keyframe[] keys = points.Select(p => new Keyframe(p.time, p.value)).ToArray();
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), propertyName), SmoothCurve(keys));
        }

        // ---- Timeline asset ----

        /// <summary>Never deletes a track — CopAnimationBuilder.BuildTimeline's
        /// DeleteTrack/recreate loop is the documented reason a Timeline
        /// scene binding went null on every re-run (docs/UNITY_CLIENT.md).
        /// Tracks are resolved by name and created only on a miss; a track
        /// that already has clips is left alone so a user's own edits (clip
        /// timing, added tracks) survive re-running EnsureAssetsBuilt.</summary>
        private static void BuildTimelineAsset(AnimationClip bodyClip, AnimationClip cameraClip)
        {
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(TimelinePath);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                AssetDatabase.CreateAsset(timeline, TimelinePath);
            }

            AnimationTrack bodyTrack = GetOrCreateTrack<AnimationTrack>(timeline, TrackBody);
            AnimationTrack cameraTrack = GetOrCreateTrack<AnimationTrack>(timeline, TrackCamera);
            AudioTrack staticTrack = GetOrCreateTrack<AudioTrack>(timeline, TrackStatic);
            AudioTrack tuneTrack = GetOrCreateTrack<AudioTrack>(timeline, TrackTune);

            EnsureAnimationClip(bodyTrack, bodyClip, easeIn: 0.35, easeOut: 0.45);
            EnsureAnimationClip(cameraTrack, cameraClip, easeIn: 0.0, easeOut: 0.0);
            EnsureStaticClip(staticTrack);
            EnsureTuneClips(tuneTrack);

            timeline.durationMode = TimelineAsset.DurationMode.FixedLength;
            timeline.fixedDuration = Duration;

            EditorUtility.SetDirty(timeline);
        }

        private static T GetOrCreateTrack<T>(TimelineAsset timeline, string trackName) where T : TrackAsset, new()
        {
            foreach (TrackAsset track in timeline.GetOutputTracks())
            {
                if (track is T typed && track.name == trackName) return typed;
            }
            return timeline.CreateTrack<T>(null, trackName);
        }

        private static void EnsureAnimationClip(AnimationTrack track, AnimationClip sourceClip, double easeIn, double easeOut)
        {
            TimelineClip clip = track.GetClips().FirstOrDefault();
            if (clip == null)
            {
                clip = track.CreateClip(sourceClip);
                clip.start = 0;
                clip.easeInDuration = easeIn;
                clip.easeOutDuration = easeOut;
            }

            if (clip.asset is AnimationPlayableAsset asset)
            {
                asset.loop = AnimationPlayableAsset.LoopMode.Off;
                asset.applyFootIK = false;
                // The camera clip animates its own Animator's transform, so Timeline treats it as a
                // root transform and, with removeStartOffset on, re-bases the frame-0 pose onto the
                // track's zero offset - dropping the eye from y=1.64 to the player's feet. Keep the
                // pose absolute.
                asset.removeStartOffset = false;
            }
        }

        private static void EnsureStaticClip(AudioTrack track)
        {
            if (track.GetClips().Any()) return;

            TimelineClip clip = track.CreateClip(LoadSfx("radio_static_loop"));
            clip.start = 0;
            clip.duration = 1.90;
        }

        private static void EnsureTuneClips(AudioTrack track)
        {
            if (track.GetClips().Any()) return;

            TimelineClip sweep = track.CreateClip(LoadSfx("radio_tuning_sweep"));
            sweep.start = 1.45;

            TimelineClip lockOn = track.CreateClip(LoadSfx("radio_lock_on"));
            lockOn.start = 3.00;
        }

        private static AudioClip LoadSfx(string name)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + name + ".mp3");
            if (clip == null) throw new InvalidOperationException($"[RadioTuneTimelineBuilder] Missing SFX clip '{name}'.mp3.");
            return clip;
        }

        // ---- Shared low-level helpers (CabinAnimationBuilder's pattern) ----

        /// <summary>A HumanPose's muscle array, independent of any specific
        /// avatar instance — see CabinAnimationBuilder.BaseMuscles, which
        /// this duplicates rather than exposes, matching this codebase's
        /// preference for small independent Editor builders over a shared
        /// base class.</summary>
        private static float[] BaseMuscles(CabinIdleProfile profile)
        {
            HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            CabinPoseLibrary.Apply(ref pose, profile);
            return pose.muscles;
        }

        private static float BaseValue(float[] baseMuscles, string muscleName) => baseMuscles[MuscleIndex(muscleName)];

        private static int MuscleIndex(string muscleName)
        {
            int index = Array.FindIndex(HumanTrait.MuscleName,
                n => string.Equals(n, muscleName, StringComparison.OrdinalIgnoreCase));
            if (index < 0) throw new InvalidOperationException($"[RadioTuneTimelineBuilder] Unknown muscle '{muscleName}'.");
            return index;
        }

        private static void WriteMuscle(AnimationClip clip, string muscleName, AnimationCurve curve)
        {
            int index = MuscleIndex(muscleName);
            if (index >= 55)
            {
                throw new InvalidOperationException(
                    $"[RadioTuneTimelineBuilder] '{muscleName}' is a finger muscle (index {index}); not supported here.");
            }

            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Animator), muscleName), curve);
        }

        private static AnimationCurve SmoothCurve(Keyframe[] keys)
        {
            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < curve.length; i++) curve.SmoothTangents(i, 0f);
            return curve;
        }

        private static void ClearAllCurves(AnimationClip clip)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        private static void SetClipSettings(AnimationClip clip)
        {
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = false;
            settings.loopBlendOrientation = true;
            settings.loopBlendPositionY = true;
            settings.loopBlendPositionXZ = true;
            settings.keepOriginalOrientation = true;
            settings.keepOriginalPositionY = true;
            settings.keepOriginalPositionXZ = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        private static AnimationClip LoadOrCreateClip(string path, string name, out bool isNew)
        {
            AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            if (existing != null)
            {
                isNew = false;
                return existing;
            }

            AnimationClip clip = new AnimationClip { name = name };
            AssetDatabase.CreateAsset(clip, path);
            isNew = true;
            return clip;
        }
    }
}
