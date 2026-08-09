using System.Collections.Generic;
using System.Text;
using FalsePositive.CabinNight;
using UnityEditor;
using UnityEngine;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Lets a human find arm/head/waist muscle and bone values by dragging
    /// sliders and watching the Scene view, instead of guessing numbers to
    /// paste into source files and re-baking to check them.
    ///
    /// Exists because the cast's pose lives in two places that don't respond
    /// to dragging a bone in the Scene view:
    ///  - Arms, wrists, head, neck, spine and chest are muscle floats
    ///    (CabinPoseLibrary.Apply) baked into CabinCast.controller's clips —
    ///    Unity's humanoid solver rewrites every one of those bones from the
    ///    muscle floats every frame the Animator runs.
    ///  - The waist lean does NOT respond to spine/chest muscles on this rig
    ///    (see CabinPoseLibrary.ApplySeated's class doc — the solver holds
    ///    bodyRotation fixed and counter-rotates the hips instead). The real
    ///    waist knob is CabinCharacterIdle's direct Spine1 bone tilt, in
    ///    degrees, applied every LateUpdate.
    ///
    /// Two modes, because "the shared defaults" and "one character" are
    /// genuinely different operations:
    ///  - Profile mode edits CabinPoseLibrary/CabinCharacterIdle's shared
    ///    defaults — affects every cast member using that CabinIdleProfile
    ///    (all four seated flashback characters use SeatedForward). Requires
    ///    pasting the Copy output into source and re-baking.
    ///  - Character mode writes straight into the SELECTED character's
    ///    CabinPoseOverride / CabinCharacterIdle override fields via
    ///    SerializedObject — no code edit, no re-bake, and it only moves that
    ///    one character. This is what makes four people at a table look like
    ///    four different people instead of four copies of one pose.
    ///
    /// Only usable outside Play mode: in Play mode CabinAnimatorDriver has
    /// already cross-faded the Animator into CabinCast.controller, which
    /// re-solves the skeleton every frame and would fight every slider drag.
    /// </summary>
    public sealed class CabinPoseTunerWindow : EditorWindow
    {
        private enum WindowMode { Profile, Character }

        // ---- Muscle vocabulary ----

        // The muscle DOFs CabinPoseLibrary actually uses for arms today, plus
        // Shoulder and Hand (unused by any profile yet, but real clip curves —
        // see docs/POSING_THE_CAST.md's "the wrist is free" note). No fingers:
        // those bind as "LeftHand.Index.1 Stretched", not this
        // HumanTrait.MuscleName form, and CabinAnimationBuilder.WriteMuscle
        // throws on them.
        public static readonly string[] ArmSuffixes =
        {
            "Shoulder Down-Up",
            "Shoulder Front-Back",
            "Arm Down-Up",
            "Arm Front-Back",
            "Arm Twist In-Out",
            "Forearm Stretch",
            "Forearm Twist In-Out",
            "Hand Down-Up",
            "Hand In-Out",
        };

        // Head/neck and spine/chest muscles have no Left/Right prefix — one
        // value each, unlike the paired arm muscles above.
        public static readonly string[] HeadNeckMuscles =
        {
            "Head Nod Down-Up",
            "Head Tilt Left-Right",
            "Head Turn Left-Right",
            "Neck Nod Down-Up",
            "Neck Tilt Left-Right",
            "Neck Turn Left-Right",
        };

        // Real DOFs, but see the in-window warning: on this rig these barely
        // bend the torso forward (that's leanDegrees' job) — they mostly
        // twist and side-bend. CabinPoseLibrary.ApplySeated's doc has the
        // measurement.
        public static readonly string[] SpineChestMuscles =
        {
            "Spine Front-Back", "Spine Left-Right", "Spine Twist Left-Right",
            "Chest Front-Back", "Chest Left-Right", "Chest Twist Left-Right",
            "UpperChest Front-Back", "UpperChest Left-Right", "UpperChest Twist Left-Right",
        };

        private static readonly Dictionary<string, HumanBodyBones> BonePrefixes = new Dictionary<string, HumanBodyBones>
        {
            ["Head"] = HumanBodyBones.Head,
            ["Neck"] = HumanBodyBones.Neck,
            ["UpperChest"] = HumanBodyBones.UpperChest,
            ["Chest"] = HumanBodyBones.Chest,
            ["Spine"] = HumanBodyBones.Spine,
        };

        private const float Epsilon = 0.0005f;

        // ---- Selection ----

        private GameObject _target;
        private Animator _animator;
        private GameObject _root; // the cast member's root — holds CabinCharacterIdle/CabinPoseOverride
        private WindowMode _mode = WindowMode.Profile;
        private CabinIdleProfile _profile = CabinIdleProfile.SeatedForward;
        private bool _mirrorArmsToRight = true;
        private bool _previewRuntimeIdle = true;

        // Character-mode enable flags, mirrored onto the two components.
        private bool _enableOverrides;

        // Keyed by full muscle name. Arms keyed by the LEFT name only (right
        // side derived from _mirrorArmsToRight); head/neck/spine/chest have no
        // side, keyed verbatim.
        private readonly Dictionary<string, float> _leftArmValues = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _singleValues = new Dictionary<string, float>();

        // Waist + idle motion. Profile mode: absolute CabinCharacterIdle
        // switch-table constants. Character mode: leanDegrees is absolute
        // (replaces the profile default), the other two are multipliers on
        // top of the profile default — matching CabinCharacterIdle's
        // leanDegreesOverride / breathScale / headDriftScale semantics.
        private float _leanDegrees;
        private float _breathAmount = 1f;
        private float _headDriftAmount = 1f;

        private Vector2 _scroll;

        [MenuItem("Tools/False Positive/Cabin Pose Tuner")]
        private static void Open() => GetWindow<CabinPoseTunerWindow>("Cabin Pose Tuner");

        private void OnEnable() => LoadFromSelection();
        private void OnFocus() => LoadFromSelection();
        private void OnSelectionChange()
        {
            LoadFromSelection();
            Repaint();
        }

        private void OnGUI()
        {
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Exit Play mode to tune poses — CabinAnimatorDriver re-solves the " +
                    "skeleton from CabinCast.controller every frame while playing, and " +
                    "would overwrite every slider drag.", MessageType.Info);
                return;
            }

            if (_animator == null)
            {
                EditorGUILayout.HelpBox(
                    "Select a cabin cast member in the Hierarchy (the root object, e.g. " +
                    "\"Nick Vlahos (Male)\", or its \"Body\" child).", MessageType.Info);
                return;
            }

            EditorGUILayout.LabelField("Target", _target.name);

            EditorGUI.BeginChangeCheck();
            WindowMode newMode = (WindowMode)EditorGUILayout.EnumPopup("Mode", _mode);
            if (EditorGUI.EndChangeCheck() && newMode != _mode)
            {
                _mode = newMode;
                LoadBaseline();
            }

            if (_mode == WindowMode.Profile)
            {
                EditorGUILayout.HelpBox(
                    "Edits CabinPoseLibrary's shared defaults for this profile — moves " +
                    "EVERY cast member using it. Use Copy + re-bake to make it stick.",
                    MessageType.None);

                EditorGUI.BeginChangeCheck();
                CabinIdleProfile newProfile = (CabinIdleProfile)EditorGUILayout.EnumPopup("Profile", _profile);
                if (EditorGUI.EndChangeCheck() && newProfile != _profile)
                {
                    _profile = newProfile;
                    LoadBaseline();
                }
            }
            else
            {
                EditorGUILayout.HelpBox(
                    "Edits ONLY " + _root.name + " — written straight to CabinPoseOverride / " +
                    "CabinCharacterIdle on this character, no code edit or re-bake needed.",
                    MessageType.None);
                EditorGUILayout.LabelField("Profile (from character)", _profile.ToString());

                EditorGUI.BeginChangeCheck();
                bool enable = EditorGUILayout.ToggleLeft("Enable per-character overrides", _enableOverrides);
                if (EditorGUI.EndChangeCheck())
                {
                    _enableOverrides = enable;
                    WriteCharacterOverrides();
                }
            }

            _previewRuntimeIdle = EditorGUILayout.ToggleLeft(
                "Preview runtime idle (waist lean, at rest phase)", _previewRuntimeIdle);

            EditorGUILayout.Space();
            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            bool changed = false;

            EditorGUILayout.LabelField("Arms", EditorStyles.boldLabel);
            _mirrorArmsToRight = EditorGUILayout.ToggleLeft("Mirror to right arm", _mirrorArmsToRight);
            foreach (string suffix in ArmSuffixes)
            {
                changed |= PairedMuscleSlider(suffix);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Head + Neck", EditorStyles.boldLabel);
            foreach (string name in HeadNeckMuscles)
            {
                if (BoneMapped(name)) changed |= SingleMuscleSlider(name);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Spine + Chest", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "On this rig these barely bend the torso forward — mostly twist and " +
                "side-bend. Use Waist lean below for a forward/back lean.", MessageType.Warning);
            foreach (string name in SpineChestMuscles)
            {
                if (BoneMapped(name)) changed |= SingleMuscleSlider(name);
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Waist + idle motion", EditorStyles.boldLabel);
            changed |= DegreeSlider("Waist lean (degrees)", ref _leanDegrees, -45f, 45f);
            if (_mode == WindowMode.Profile)
            {
                changed |= DegreeSlider("Breath amount (degrees)", ref _breathAmount, 0f, 3f);
                changed |= DegreeSlider("Head drift amount (multiplier)", ref _headDriftAmount, 0f, 3f);
            }
            else
            {
                changed |= DegreeSlider("Breath scale (× profile default)", ref _breathAmount, 0f, 3f);
                changed |= DegreeSlider("Head drift scale (× profile default)", ref _headDriftAmount, 0f, 3f);
            }

            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_mode == WindowMode.Profile)
                {
                    if (GUILayout.Button("Copy SetMuscle + idle lines")) CopyProfileLines();
                    if (GUILayout.Button("Reset to library")) LoadBaseline();
                    if (GUILayout.Button("Re-bake clips (T03c)")) CabinAnimationBuilder.Build();
                }
                else
                {
                    if (GUILayout.Button("Reset to profile default")) LoadBaseline();
                }
            }

            if (changed)
            {
                ApplyLivePose();
                if (_mode == WindowMode.Character) WriteCharacterOverrides();
            }
        }

        // ---- Slider widgets ----

        /// <summary>An arm/wrist muscle: one slider, keyed by the Left name;
        /// the Right value is derived via _mirrorArmsToRight rather than
        /// stored separately, so the two sides can't silently drift apart.</summary>
        private bool PairedMuscleSlider(string suffix)
        {
            string leftMuscle = "Left " + suffix;
            float value = _leftArmValues[leftMuscle];
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(suffix, value, -1f, 1f);
            if (!EditorGUI.EndChangeCheck()) return false;

            RegisterUndo();
            _leftArmValues[leftMuscle] = newValue;
            return true;
        }

        private bool SingleMuscleSlider(string muscleName)
        {
            float value = _singleValues[muscleName];
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(muscleName, value, -1f, 1f);
            if (!EditorGUI.EndChangeCheck()) return false;

            RegisterUndo();
            _singleValues[muscleName] = newValue;
            return true;
        }

        private bool DegreeSlider(string label, ref float value, float min, float max)
        {
            EditorGUI.BeginChangeCheck();
            float newValue = EditorGUILayout.Slider(label, value, min, max);
            if (!EditorGUI.EndChangeCheck()) return false;

            RegisterUndo();
            value = newValue;
            return true;
        }

        private void RegisterUndo() => Undo.RegisterFullObjectHierarchyUndo(_animator.gameObject, "Cabin Pose Tuner");

        private bool BoneMapped(string muscleName)
        {
            foreach (KeyValuePair<string, HumanBodyBones> entry in BonePrefixes)
            {
                if (muscleName.StartsWith(entry.Key, System.StringComparison.Ordinal))
                    return _animator.GetBoneTransform(entry.Value) != null;
            }
            return true;
        }

        // ---- Selection / baseline ----

        private void LoadFromSelection()
        {
            GameObject selected = Selection.activeGameObject;
            Animator animator = selected != null ? selected.GetComponentInChildren<Animator>() : null;
            if (animator == null || animator.avatar == null || !animator.avatar.isHuman)
            {
                // Keep the last valid target rather than clearing it — clicking
                // into the Animation window or Console shouldn't lose your work.
                return;
            }

            if (animator == _animator) return;

            _target = selected;
            _animator = animator;
            _root = FindCastRoot(animator);
            LoadBaseline();
        }

        /// <summary>Walks up from the Animator (on "Body") to the object that
        /// actually owns CabinCharacterIdle/CabinAnimatorDriver, so Character
        /// mode writes to the right GameObject regardless of whether the root
        /// or the Body child was selected in the Hierarchy.</summary>
        private static GameObject FindCastRoot(Animator animator)
        {
            Transform t = animator.transform;
            while (t != null)
            {
                if (t.GetComponent<CabinCharacterIdle>() != null || t.GetComponent<CabinAnimatorDriver>() != null)
                    return t.gameObject;
                t = t.parent;
            }
            return animator.transform.parent != null ? animator.transform.parent.gameObject : animator.gameObject;
        }

        private void LoadBaseline()
        {
            if (_mode == WindowMode.Character)
            {
                CabinCharacterIdle idle = _root.GetComponent<CabinCharacterIdle>();
                if (idle != null) _profile = idle.Profile;
            }

            HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            CabinPoseLibrary.Apply(ref pose, _profile);

            _leftArmValues.Clear();
            foreach (string suffix in ArmSuffixes) _leftArmValues["Left " + suffix] = ReadMuscle(pose, "Left " + suffix);

            _singleValues.Clear();
            foreach (string name in HeadNeckMuscles) _singleValues[name] = ReadMuscle(pose, name);
            foreach (string name in SpineChestMuscles) _singleValues[name] = ReadMuscle(pose, name);

            _leanDegrees = CabinCharacterIdle.LeanDegreesFor(_profile);
            // Profile mode edits the actual switch-table constants, so seed
            // the real per-profile values. Character mode's two fields are
            // MULTIPLIERS on top of those constants (breathScale/headDriftScale),
            // so 1x — "no change" — is the correct baseline there, not the
            // constants themselves.
            _breathAmount = _mode == WindowMode.Profile ? CabinCharacterIdle.BreathDegreesFor(_profile) : 1f;
            _headDriftAmount = _mode == WindowMode.Profile ? CabinCharacterIdle.HeadYawMultiplierFor(_profile) : 1f;
            _enableOverrides = false;

            if (_mode == WindowMode.Character) LoadCharacterOverridesOnTopOfBaseline();

            ApplyLivePose();
        }

        /// <summary>Overwrites the just-loaded profile baseline with whatever
        /// this specific character already has stored, so re-opening the
        /// tuner on an already-tuned character shows their real values
        /// instead of resetting to the shared default.</summary>
        private void LoadCharacterOverridesOnTopOfBaseline()
        {
            CabinPoseOverride poseOverride = _root.GetComponent<CabinPoseOverride>();
            if (poseOverride != null)
            {
                _enableOverrides = poseOverride.EnableOverrides;
                foreach (CabinPoseOverride.MuscleOverride m in poseOverride.Muscles)
                {
                    if (_leftArmValues.ContainsKey(m.muscleName)) _leftArmValues[m.muscleName] = m.value;
                    else if (_singleValues.ContainsKey(m.muscleName)) _singleValues[m.muscleName] = m.value;
                }
            }

            CabinCharacterIdle idle = _root.GetComponent<CabinCharacterIdle>();
            if (idle == null) return;

            SerializedObject so = new SerializedObject(idle);
            bool useIdleOverrides = so.FindProperty("useIdleOverrides").boolValue;
            _enableOverrides |= useIdleOverrides;
            if (useIdleOverrides)
            {
                _leanDegrees = so.FindProperty("leanDegreesOverride").floatValue;
                _breathAmount = so.FindProperty("breathScale").floatValue;
                _headDriftAmount = so.FindProperty("headDriftScale").floatValue;
            }
        }

        // ---- Live preview ----

        /// <summary>Writes the current slider values onto the selected skeleton
        /// via HumanPoseHandler — the same mechanism
        /// CabinNightCharacterBuilder.ApplyPose uses for the edit-time prefab
        /// preview — then, if enabled, layers the waist lean on top exactly as
        /// CabinCharacterIdle.LateUpdate would at rest (breath/drift phase
        /// zero), so the preview matches frame 1 of Play instead of silently
        /// omitting a 16-degree torso lean. Deliberately does NOT touch
        /// Body.localPosition: CabinAnimatorDriver applies the Seated Y offset
        /// only at runtime because a muscle-only clip can't carry
        /// pose.bodyPosition, but HumanPoseHandler.SetHumanPose DOES carry it
        /// (ApplySeated writes pose.bodyPosition directly) — applying both
        /// would double it.</summary>
        private void ApplyLivePose()
        {
            if (_animator == null || _animator.avatar == null || !_animator.avatar.isHuman) return;

            using HumanPoseHandler handler = new HumanPoseHandler(_animator.avatar, _animator.transform);
            HumanPose pose = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            CabinPoseLibrary.Apply(ref pose, _profile);

            foreach (KeyValuePair<string, float> entry in _leftArmValues)
            {
                CabinPoseLibrary.SetMuscle(ref pose, entry.Key, entry.Value);
                if (_mirrorArmsToRight)
                {
                    string rightMuscle = "Right " + entry.Key.Substring("Left ".Length);
                    CabinPoseLibrary.SetMuscle(ref pose, rightMuscle, entry.Value);
                }
            }
            foreach (KeyValuePair<string, float> entry in _singleValues)
            {
                CabinPoseLibrary.SetMuscle(ref pose, entry.Key, entry.Value);
            }

            handler.SetHumanPose(ref pose);

            if (_previewRuntimeIdle)
            {
                Transform spine1 = FindBoneByName(_animator.transform, "Spine1");
                if (spine1 != null) spine1.localRotation = spine1.localRotation * Quaternion.Euler(_leanDegrees, 0f, 0f);
            }

            SceneView.RepaintAll();
        }

        private static Transform FindBoneByName(Transform root, string name)
        {
            foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
            {
                if (child.name == name) return child;
            }
            return null;
        }

        // ---- Character mode: direct write ----

        /// <summary>Writes the current sliders straight onto _root's
        /// CabinPoseOverride and CabinCharacterIdle, adding either component
        /// if this character has never been tuned before. This is what makes
        /// Character mode "no code edit, no re-bake" — Profile mode's Copy
        /// button is the only path that touches source files.</summary>
        private void WriteCharacterOverrides()
        {
            CabinPoseOverride poseOverride = _root.GetComponent<CabinPoseOverride>();
            if (poseOverride == null) poseOverride = Undo.AddComponent<CabinPoseOverride>(_root);

            SerializedObject poseSo = new SerializedObject(poseOverride);
            poseSo.FindProperty("enableOverrides").boolValue = _enableOverrides;
            SerializedProperty musclesProp = poseSo.FindProperty("muscles");
            musclesProp.ClearArray();
            int index = 0;
            foreach (KeyValuePair<string, float> entry in _leftArmValues)
            {
                AppendMuscle(musclesProp, ref index, entry.Key, entry.Value);
                if (_mirrorArmsToRight)
                {
                    string rightMuscle = "Right " + entry.Key.Substring("Left ".Length);
                    AppendMuscle(musclesProp, ref index, rightMuscle, entry.Value);
                }
            }
            foreach (KeyValuePair<string, float> entry in _singleValues)
            {
                AppendMuscle(musclesProp, ref index, entry.Key, entry.Value);
            }
            poseSo.ApplyModifiedProperties();

            CabinCharacterIdle idle = _root.GetComponent<CabinCharacterIdle>();
            if (idle == null) return; // e.g. the player root, which has no CabinCharacterIdle at all

            SerializedObject idleSo = new SerializedObject(idle);
            idleSo.FindProperty("useIdleOverrides").boolValue = _enableOverrides;
            idleSo.FindProperty("leanDegreesOverride").floatValue = _leanDegrees;
            idleSo.FindProperty("breathScale").floatValue = _breathAmount;
            idleSo.FindProperty("headDriftScale").floatValue = _headDriftAmount;
            idleSo.ApplyModifiedProperties();
        }

        private static void AppendMuscle(SerializedProperty musclesProp, ref int index, string muscleName, float value)
        {
            musclesProp.InsertArrayElementAtIndex(index);
            SerializedProperty element = musclesProp.GetArrayElementAtIndex(index);
            element.FindPropertyRelative("muscleName").stringValue = muscleName;
            element.FindPropertyRelative("value").floatValue = value;
            index++;
        }

        // ---- Profile mode: copy-to-source ----

        /// <summary>Emits only the muscles that differ from CabinPoseLibrary's
        /// own baseline for this profile, ready to paste into ApplySeated (or
        /// Apply, for a standing profile) — pasting the whole slider set every
        /// time would bury the profile's existing tuning under redundant
        /// lines. The waist/idle values are a plain read-out, not a paste
        /// target: they live in two different switch statements in
        /// CabinCharacterIdle.cs (one arm per CabinIdleProfile case each), so
        /// there's no single line to paste — the values still tell you exactly
        /// what to type into the matching case.</summary>
        private void CopyProfileLines()
        {
            HumanPose baseline = new HumanPose { muscles = new float[HumanTrait.MuscleCount] };
            CabinPoseLibrary.Apply(ref baseline, _profile);

            StringBuilder sb = new StringBuilder();
            foreach (KeyValuePair<string, float> entry in _leftArmValues)
            {
                EmitIfChanged(sb, baseline, entry.Key, entry.Value);
                if (_mirrorArmsToRight)
                {
                    string rightMuscle = "Right " + entry.Key.Substring("Left ".Length);
                    EmitIfChanged(sb, baseline, rightMuscle, entry.Value);
                }
            }
            foreach (KeyValuePair<string, float> entry in _singleValues)
            {
                EmitIfChanged(sb, baseline, entry.Key, entry.Value);
            }

            sb.AppendLine($"// CabinCharacterIdle.cs — CabinIdleProfile.{_profile} case, in each of the three switches:");
            sb.AppendLine($"//   leanDegrees:  {_leanDegrees:0.00}f");
            sb.AppendLine($"//   breathDegrees: {_breathAmount:0.00}f");
            sb.AppendLine($"//   headYaw multiplier: {_headDriftAmount:0.00}f");

            string result = sb.ToString();
            EditorGUIUtility.systemCopyBuffer = result;
            Debug.Log("[CabinPoseTuner] Copied to clipboard:\n" + result);
        }

        private static void EmitIfChanged(StringBuilder sb, HumanPose baseline, string muscleName, float value)
        {
            if (Mathf.Abs(ReadMuscle(baseline, muscleName) - value) <= Epsilon) return;
            sb.AppendLine($"SetMuscle(ref pose, \"{muscleName}\", {value:0.00}f);");
        }

        private static float ReadMuscle(HumanPose pose, string muscleName)
        {
            for (int i = 0; i < HumanTrait.MuscleName.Length; i++)
            {
                if (string.Equals(HumanTrait.MuscleName[i], muscleName, System.StringComparison.OrdinalIgnoreCase))
                    return pose.muscles[i];
            }
            return 0f;
        }
    }
}
