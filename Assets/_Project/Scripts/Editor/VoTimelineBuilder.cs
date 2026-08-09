using System.Collections.Generic;
using System.IO;
using System.Linq;
using FalsePositive.Cutscene;
using FalsePositive.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Generates one Timeline and PlayableDirector per authored cutscene.
    /// Dialogue and SFX are placed at the same cumulative beat times used by
    /// CutsceneDirector, so Timeline owns audio while the existing stage code
    /// continues to own scene-specific movement and poses.
    /// </summary>
    public static class VoTimelineBuilder
    {
        private const string PersistentScenePath = "Assets/_Project/Scenes/_Persistent.unity";
        private const string TimelineRoot = "Assets/_Project/Art/Timelines/Voice/";
        private const string DirectorRootName = "CutsceneTimelines";

        [MenuItem("Tools/False Positive/Bootstrap/11 - Build Cutscene VO Timelines")]
        public static void BuildAll()
        {
            AssetDatabase.Refresh();
            OfflineScriptBuilder.Build();
            MemorySceneWiring.WireBoth();
            CutsceneRecipeBuilder.PopulateRecipes();
            CutsceneRecipeBuilder.AttachVoClips();

            Directory.CreateDirectory(TimelineRoot);
            Scene scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
            GameObject cutsceneGo = GameObject.Find("CutsceneDirector");
            if (cutsceneGo == null)
            {
                throw new System.InvalidOperationException(
                    "[VoTimelineBuilder] No CutsceneDirector in _Persistent.unity — run bootstrap step 1 first.");
            }

            CutsceneDirector cutscenes = cutsceneGo.GetComponent<CutsceneDirector>();
            SerializedObject serialized = new SerializedObject(cutscenes);
            AudioSource voSource = serialized.FindProperty("voSource").objectReferenceValue as AudioSource;
            AudioSource sfxSource = EnsureSfxSource(cutsceneGo.transform, serialized);
            if (voSource == null)
            {
                throw new System.InvalidOperationException(
                    "[VoTimelineBuilder] CutsceneDirector.voSource is not wired.");
            }

            Transform directorRoot = cutsceneGo.transform.Find(DirectorRootName);
            if (directorRoot == null)
            {
                GameObject root = new GameObject(DirectorRootName);
                root.transform.SetParent(cutsceneGo.transform, false);
                directorRoot = root.transform;
            }

            SerializedProperty recipes = serialized.FindProperty("recipes");
            List<CutsceneTimelineBinding> bindings = new List<CutsceneTimelineBinding>(recipes.arraySize);
            int audioClipCount = 0;
            int animationClipCount = 0;

            for (int i = 0; i < recipes.arraySize; i++)
            {
                SerializedProperty recipe = recipes.GetArrayElementAtIndex(i);
                CutsceneId id = (CutsceneId)recipe.FindPropertyRelative("id").enumValueIndex;
                TimelineAsset timeline = BuildTimeline(
                    id,
                    recipe.FindPropertyRelative("beats"),
                    ref audioClipCount,
                    ref animationClipCount);
                PlayableDirector director = EnsureDirector(directorRoot, id, timeline);

                foreach (TrackAsset track in timeline.GetOutputTracks())
                {
                    if (track is not AudioTrack) continue;
                    director.SetGenericBinding(track, track.name == "SFX" ? sfxSource : voSource);
                }

                bindings.Add(new CutsceneTimelineBinding { id = id, director = director });
            }

            SerializedProperty timelineBindings = serialized.FindProperty("timelineDirectors");
            timelineBindings.arraySize = bindings.Count;
            for (int i = 0; i < bindings.Count; i++)
            {
                SerializedProperty binding = timelineBindings.GetArrayElementAtIndex(i);
                binding.FindPropertyRelative("id").enumValueIndex = (int)bindings[i].id;
                binding.FindPropertyRelative("director").objectReferenceValue = bindings[i].director;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PersistentScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[VoTimelineBuilder] Built {bindings.Count} cutscene Timelines with " +
                $"{audioClipCount} timed audio clips and {animationClipCount} animation clips.");
        }

        private static TimelineAsset BuildTimeline(
            CutsceneId id,
            SerializedProperty beats,
            ref int audioClipCount,
            ref int animationClipCount)
        {
            string path = TimelineRoot + id + ".playable";
            TimelineAsset timeline = AssetDatabase.LoadAssetAtPath<TimelineAsset>(path);
            if (timeline == null)
            {
                timeline = ScriptableObject.CreateInstance<TimelineAsset>();
                timeline.name = id.ToString();
                AssetDatabase.CreateAsset(timeline, path);
            }

            foreach (TrackAsset track in timeline.GetRootTracks().ToArray())
            {
                timeline.DeleteTrack(track);
            }

            AudioTrack voTrack = null;
            AudioTrack sfxTrack = null;
            Dictionary<string, AnimationTrack> animationTracks = new Dictionary<string, AnimationTrack>();
            double cursor = 0d;

            for (int i = 0; i < beats.arraySize; i++)
            {
                SerializedProperty beat = beats.GetArrayElementAtIndex(i);
                string speaker = beat.FindPropertyRelative("speaker").stringValue;
                AudioClip audio = beat.FindPropertyRelative("voClip").objectReferenceValue as AudioClip;
                float fallback = beat.FindPropertyRelative("holdSecondsIfNoClip").floatValue;
                double duration = audio != null ? audio.length : fallback;

                if (audio != null)
                {
                    AudioTrack track;
                    if (string.IsNullOrEmpty(speaker))
                    {
                        sfxTrack ??= timeline.CreateTrack<AudioTrack>(null, "SFX");
                        track = sfxTrack;
                    }
                    else
                    {
                        voTrack ??= timeline.CreateTrack<AudioTrack>(null, "VO");
                        track = voTrack;
                    }

                    TimelineClip clip = track.CreateClip<AudioPlayableAsset>();
                    AudioPlayableAsset playable = (AudioPlayableAsset)clip.asset;
                    playable.clip = audio;
                    playable.loop = false;
                    clip.displayName = audio.name;
                    clip.start = cursor;
                    clip.duration = audio.length;
                    audioClipCount++;
                }

                string animationSpeaker = speaker == "???" ? "SPASSKY" : speaker;
                AnimationClip animation = ResolveAnimation(id, animationSpeaker);
                if (animation != null && !string.IsNullOrEmpty(beat.FindPropertyRelative("line").stringValue))
                {
                    if (!animationTracks.TryGetValue(animationSpeaker, out AnimationTrack animationTrack))
                    {
                        animationTrack = timeline.CreateTrack<AnimationTrack>(null, "ANIM_" + animationSpeaker);
                        animationTracks.Add(animationSpeaker, animationTrack);
                    }

                    TimelineClip clip = animationTrack.CreateClip<AnimationPlayableAsset>();
                    AnimationPlayableAsset playable = (AnimationPlayableAsset)clip.asset;
                    playable.clip = animation;
                    playable.loop = AnimationPlayableAsset.LoopMode.On;
                    clip.displayName = animation.name;
                    clip.start = cursor;
                    clip.duration = duration;
                    animationClipCount++;
                }

                // Space the next beat off this one. Clips used to butt end to
                // end, which is what made the flashbacks unreadable — seven
                // lines from five people the player has never met, delivered
                // with no air between them. CutsceneDirector waits this same
                // value between beats, so the subtitles track the voices.
                cursor += duration + CutscenePacing.BeatGapFor(id);
            }

            EditorUtility.SetDirty(timeline);
            return timeline;
        }

        private static AnimationClip ResolveAnimation(CutsceneId id, string speaker)
        {
            if (speaker == "SPASSKY")
            {
                return AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/_Project/Art/Animations/Cop/Cop_Talk.anim");
            }

            string filename;
            if (id == CutsceneId.GoodYears)
            {
                filename = "Pose_SeatedForward";
            }
            else
            {
                filename = speaker switch
                {
                    "PRIYA" => "Idle_Panicked",
                    "IVY" => "Idle_Guarded",
                    "AARON" => "Idle_Controlled",
                    "NICK" => "Idle_Confrontational",
                    _ => null,
                };
            }

            return string.IsNullOrEmpty(filename)
                ? null
                : AssetDatabase.LoadAssetAtPath<AnimationClip>(
                    "Assets/_Project/CabinNight/Animations/" + filename + ".anim");
        }

        private static PlayableDirector EnsureDirector(
            Transform root,
            CutsceneId id,
            TimelineAsset timeline)
        {
            Transform child = root.Find(id.ToString());
            GameObject go;
            if (child == null)
            {
                go = new GameObject(id.ToString());
                go.transform.SetParent(root, false);
            }
            else
            {
                go = child.gameObject;
            }

            PlayableDirector director = go.GetComponent<PlayableDirector>();
            if (director == null) director = go.AddComponent<PlayableDirector>();
            director.playOnAwake = false;
            director.timeUpdateMode = DirectorUpdateMode.GameTime;
            director.extrapolationMode = DirectorWrapMode.None;
            director.playableAsset = timeline;
            return director;
        }

        private static AudioSource EnsureSfxSource(Transform cutsceneRoot, SerializedObject serialized)
        {
            SerializedProperty property = serialized.FindProperty("sfxSource");
            AudioSource source = property.objectReferenceValue as AudioSource;
            if (source != null) return source;

            Transform child = cutsceneRoot.Find("CutsceneSfxSource");
            GameObject go;
            if (child == null)
            {
                go = new GameObject("CutsceneSfxSource");
                go.transform.SetParent(cutsceneRoot, false);
            }
            else
            {
                go = child.gameObject;
            }

            source = go.GetComponent<AudioSource>();
            if (source == null) source = go.AddComponent<AudioSource>();
            property.objectReferenceValue = source;
            return source;
        }
    }
}
