using System.Collections.Generic;
using FalsePositive.Cutscene;
using FalsePositive.Flow;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Populates CutsceneDirector's recipes with dialogue verbatim from
    /// docs/STORY_SCRIPT.md §4/§5. Re-runnable and safe to call again once
    /// Step 5 generates VO — VoClipAttacher (a separate pass) fills in the
    /// AudioClip references this leaves null; this method only ever resets
    /// the text/flag data, never clobbers clips, because it is meant to run
    /// again if a line's wording changes.
    /// </summary>
    public static class CutsceneRecipeBuilder
    {
        private const string PersistentScenePath = "Assets/_Project/Scenes/_Persistent.unity";

        private const string VoRoot = "Assets/_Project/Art/Audio/VO/";

        private static readonly Dictionary<CutsceneId, string[]> VoClipNames = new Dictionary<CutsceneId, string[]>
        {
            { CutsceneId.Wake, new[] { "wake_call_1", "wake_call_2", "wake_call_3" } },
            { CutsceneId.SpasskyAnswer, new[] { "spassky_answer" } },
            { CutsceneId.RadioClears, new[] { "radio_storm_warning" } },
            { CutsceneId.PriyaScreams, new[] { "priya_screams" } },
            { CutsceneId.OutIntoTheSnow, new[] { "priya_what_do_we_do", "ivy_oh_my_god", "aaron_bring_him_in" } },
            { CutsceneId.TheCarry, new[]
                {
                    "priya_what_could_have_happened", "ivy_i_dont_know", "priya_all_night",
                    "ivy_yes_all_night", "aaron_priya_not_now", "priya_door_locked", "aaron_lift_on_three",
                }
            },
            { CutsceneId.EndingDavid, new[] { "spassky_ending_david" } },
            { CutsceneId.EndingAaron, new[] { "spassky_ending_aaron" } },
            { CutsceneId.EndingIvy, new[] { "spassky_ending_ivy" } },
            { CutsceneId.EndingPriya, new[] { "spassky_ending_priya" } },
        };

        [MenuItem("Tools/False Positive/Bootstrap/7 - Attach VO Clips")]
        public static void AttachVoClips()
        {
            Scene scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
            GameObject cutsceneGo = GameObject.Find("CutsceneDirector");
            CutsceneDirector director = cutsceneGo.GetComponent<CutsceneDirector>();
            SerializedObject so = new SerializedObject(director);
            SerializedProperty recipesProp = so.FindProperty("recipes");

            int attached = 0;
            for (int i = 0; i < recipesProp.arraySize; i++)
            {
                SerializedProperty recipeProp = recipesProp.GetArrayElementAtIndex(i);
                CutsceneId id = (CutsceneId)recipeProp.FindPropertyRelative("id").enumValueIndex;
                if (!VoClipNames.TryGetValue(id, out string[] clipNames)) continue;

                SerializedProperty beatsProp = recipeProp.FindPropertyRelative("beats");
                for (int b = 0; b < beatsProp.arraySize && b < clipNames.Length; b++)
                {
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoRoot + clipNames[b] + ".mp3");
                    if (clip == null)
                    {
                        Debug.LogWarning($"[CutsceneRecipeBuilder] Missing VO clip {clipNames[b]}.mp3 for {id} beat {b}.");
                        continue;
                    }
                    beatsProp.GetArrayElementAtIndex(b).FindPropertyRelative("voClip").objectReferenceValue = clip;
                    attached++;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PersistentScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CutsceneRecipeBuilder] Attached {attached} VO clips.");
        }

        [MenuItem("Tools/False Positive/Bootstrap/6 - Populate Cutscene Recipes")]
        public static void PopulateRecipes()
        {
            Scene scene = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
            GameObject cutsceneGo = GameObject.Find("CutsceneDirector");
            if (cutsceneGo == null)
            {
                throw new System.InvalidOperationException(
                    "[CutsceneRecipeBuilder] No CutsceneDirector in _Persistent.unity — run step 1 first.");
            }

            CutsceneDirector director = cutsceneGo.GetComponent<CutsceneDirector>();
            SerializedObject so = new SerializedObject(director);
            SerializedProperty recipesProp = so.FindProperty("recipes");

            List<CutsceneRecipe> existing = new List<CutsceneRecipe>();
            for (int i = 0; i < recipesProp.arraySize; i++)
            {
                existing.Add(ExtractRecipe(recipesProp.GetArrayElementAtIndex(i)));
            }

            CutsceneRecipe[] recipes = BuildRecipes(existing);

            recipesProp.arraySize = recipes.Length;
            for (int i = 0; i < recipes.Length; i++)
            {
                WriteRecipe(recipesProp.GetArrayElementAtIndex(i), recipes[i]);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, PersistentScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[CutsceneRecipeBuilder] {recipes.Length} cutscene recipes written.");
        }

        private static CutsceneRecipe ExtractRecipe(SerializedProperty element)
        {
            CutsceneRecipe recipe = new CutsceneRecipe
            {
                id = (CutsceneId)element.FindPropertyRelative("id").enumValueIndex,
            };
            SerializedProperty beatsProp = element.FindPropertyRelative("beats");
            recipe.beats = new CutsceneBeat[beatsProp.arraySize];
            for (int i = 0; i < beatsProp.arraySize; i++)
            {
                SerializedProperty beatProp = beatsProp.GetArrayElementAtIndex(i);
                recipe.beats[i] = new CutsceneBeat
                {
                    voClip = beatProp.FindPropertyRelative("voClip").objectReferenceValue as AudioClip,
                };
            }
            return recipe;
        }

        private static void WriteRecipe(SerializedProperty element, CutsceneRecipe recipe)
        {
            element.FindPropertyRelative("id").enumValueIndex = (int)recipe.id;
            element.FindPropertyRelative("fadeOutSeconds").floatValue = recipe.fadeOutSeconds;
            element.FindPropertyRelative("fadeInSeconds").floatValue = recipe.fadeInSeconds;

            SerializedProperty beatsProp = element.FindPropertyRelative("beats");
            beatsProp.arraySize = recipe.beats.Length;
            for (int i = 0; i < recipe.beats.Length; i++)
            {
                SerializedProperty beatProp = beatsProp.GetArrayElementAtIndex(i);
                CutsceneBeat beat = recipe.beats[i];
                beatProp.FindPropertyRelative("speaker").stringValue = beat.speaker ?? string.Empty;
                beatProp.FindPropertyRelative("line").stringValue = beat.line ?? string.Empty;
                beatProp.FindPropertyRelative("holdSecondsIfNoClip").floatValue = beat.holdSecondsIfNoClip;
                beatProp.FindPropertyRelative("memoryFlagToSet").stringValue = beat.memoryFlagToSet ?? string.Empty;
                // voClip intentionally preserved from `existing` by BuildRecipes, not overwritten here.
                if (beatProp.FindPropertyRelative("voClip").objectReferenceValue == null && beat.voClip != null)
                {
                    beatProp.FindPropertyRelative("voClip").objectReferenceValue = beat.voClip;
                }
            }
        }

        private static CutsceneBeat Beat(string speaker, string line, float hold, string flag = null) => new CutsceneBeat
        {
            speaker = speaker,
            line = line,
            holdSecondsIfNoClip = hold,
            memoryFlagToSet = flag,
        };

        private static CutsceneRecipe Recipe(CutsceneId id, float fadeOut, float fadeIn, params CutsceneBeat[] beats) => new CutsceneRecipe
        {
            id = id,
            fadeOutSeconds = fadeOut,
            fadeInSeconds = fadeIn,
            beats = beats,
        };

        /// <summary>Carries forward any voClip already assigned to a matching
        /// (id, beat index) pair from a previous run, so re-running this after
        /// Step 5/7 attaches VO never discards it.</summary>
        private static CutsceneRecipe[] BuildRecipes(List<CutsceneRecipe> existing)
        {
            CutsceneRecipe[] recipes =
            {
                Recipe(CutsceneId.Wake, 0f, 0.6f,
                    Beat("???", "David.", 1.2f),
                    Beat("???", "David.", 1.0f),
                    Beat("???", "David!", 1.0f)),

                Recipe(CutsceneId.SpasskyAnswer, 0.3f, 0.5f,
                    Beat("SPASSKY",
                        "I'm Officer Spassky. You're one of the suspects involved in the death of Nick. " +
                        "We have just finished interrogating the rest of your friends. So here we are. " +
                        "Please — try to recall everything that happened last night.", 8f)),

                Recipe(CutsceneId.FuzzyToNight, 1.2f, 1.2f),
                Recipe(CutsceneId.StandFromChair, 0.2f, 0.4f),

                Recipe(CutsceneId.RadioClears, 0.2f, 0.3f,
                    Beat("RADIO", "…a snow storm. Please stay indoors during these times.", 3f,
                        MemoryFlagIds.HeardRadioWarning)),

                Recipe(CutsceneId.SomeoneLeft, 0.3f, 0.3f,
                    Beat(null, null, 1.5f, MemoryFlagIds.SawDoorClose)),

                Recipe(CutsceneId.CallForNick, 0.2f, 0.3f),
                Recipe(CutsceneId.FuzzyToInterrogation, 1.2f, 1.2f),
                Recipe(CutsceneId.FuzzyToMorning, 1.2f, 1.2f),

                Recipe(CutsceneId.PriyaScreams, 0.3f, 0.5f,
                    Beat("PRIYA", "GUYS! GUYS! HELP! WHAT HAPPENED TO NICK? IVY! AARON! DAVID! GUYS, COME HERE PLEASE!", 4f,
                        MemoryFlagIds.SawBody)),

                Recipe(CutsceneId.TheyComeDown, 0.2f, 0.3f),

                Recipe(CutsceneId.OutIntoTheSnow, 0.3f, 0.3f,
                    Beat("PRIYA", "What do we do?? What do we do??", 2f),
                    Beat("IVY", "Oh my god, what happened to him? What do we do now?", 2.5f),
                    Beat("AARON", "He looks cold. Let's bring him in — to the sofa, near the fireplace.", 3f)),

                Recipe(CutsceneId.TheCarry, 0.3f, 0.3f,
                    Beat("PRIYA", "What could have happened here?", 2f),
                    Beat("IVY", "I don't know! I was with Aaron upstairs!!", 2f),
                    Beat("PRIYA", "…All night?", 1.2f),
                    Beat("IVY", "…Yes. All night.", 1.5f, MemoryFlagIds.HeardIvyAlibi),
                    Beat("AARON", "Priya. Not now.", 1.2f),
                    Beat("PRIYA", "The door was locked. Who locked the door?", 2f),
                    Beat("AARON", "Lift on three.", 1.5f, MemoryFlagIds.HeardAaronDeflect)),

                Recipe(CutsceneId.TheSofa, 0.3f, 0.4f,
                    Beat(null, null, 2f)),

                Recipe(CutsceneId.FuzzyToVerdict, 1.2f, 1.2f),

                Recipe(CutsceneId.FlashbackAaron, 0.5f, 0.5f, Beat(null, null, 5f)),
                Recipe(CutsceneId.FlashbackIvy, 0.5f, 0.5f, Beat(null, null, 5f)),
                Recipe(CutsceneId.FlashbackPriya, 0.5f, 0.5f, Beat(null, null, 5f)),

                Recipe(CutsceneId.EndingDavid, 0.4f, 0.6f,
                    Beat("SPASSKY", "— you were the only one who couldn't tell me where you were.", 4f)),
                Recipe(CutsceneId.EndingAaron, 0.4f, 0.6f,
                    Beat("SPASSKY", "He locked it. You unlocked it. One of those took a decision.", 4f)),
                Recipe(CutsceneId.EndingIvy, 0.4f, 0.6f,
                    Beat("SPASSKY", "She agreed with you. That's not the same as it being true.", 4f)),
                Recipe(CutsceneId.EndingPriya, 0.4f, 0.6f,
                    Beat("SPASSKY", "She's the one who called us. Sit with that.", 4f)),
            };

            foreach (CutsceneRecipe recipe in recipes)
            {
                CutsceneRecipe match = existing.Find(r => r.id == recipe.id);
                if (match == null) continue;
                for (int i = 0; i < recipe.beats.Length && i < match.beats.Length; i++)
                {
                    recipe.beats[i].voClip = match.beats[i].voClip;
                }
            }

            return recipes;
        }
    }
}
