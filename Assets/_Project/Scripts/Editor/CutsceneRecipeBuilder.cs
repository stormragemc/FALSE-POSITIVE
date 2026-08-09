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
    /// Populates CutsceneDirector's recipes with canonical dialogue from
    /// docs/HUMAN_SCRIPT.md. Re-runnable: the stable-ID mapping below replaces
    /// legacy descriptive stems without changing story timing or flags.
    /// </summary>
    public static class CutsceneRecipeBuilder
    {
        private const string PersistentScenePath = "Assets/_Project/Scenes/_Persistent.unity";

        private const string VoRoot = "Assets/_Project/Art/Audio/VO/Production/";
        private const string SfxRoot = "Assets/_Project/Art/Audio/SFX/";

        private static readonly Dictionary<CutsceneId, string[]> VoClipNames = new Dictionary<CutsceneId, string[]>
        {
            { CutsceneId.Wake, new[] { "SPASSKY-001", "SPASSKY-002", "SPASSKY-003" } },
            { CutsceneId.SpasskyAnswer, new[] { "SPASSKY-005" } },
            { CutsceneId.NightArgument, new[] { null, "NICK-001" } },
            { CutsceneId.RadioClears, new[] { "RADIO-001" } },
            { CutsceneId.PriyaScreams, new[] { "PRIYA-001" } },
            { CutsceneId.OutIntoTheSnow, new[] { "PRIYA-002", "IVY-001", "AARON-001" } },
            { CutsceneId.TheCarry, new[]
                {
                    "PRIYA-003", "IVY-002", "PRIYA-004", "IVY-003",
                    "AARON-002", "PRIYA-005", "AARON-003",
                }
            },
            { CutsceneId.TheSofa, new[] { null, "PRIYA-006", "PRIYA-007" } },
            { CutsceneId.P3Photograph, new[] { "SPASSKY-057", "SPASSKY-058", "SPASSKY-059" } },
            { CutsceneId.P3AfterGoodYears, new[] { "SPASSKY-060", "SPASSKY-061" } },
            { CutsceneId.P3WhoDavid, new[] { "SPASSKY-062" } },
            { CutsceneId.GoodYears, new[]
                {
                    "PRIYA-014", "NICK-002", "PRIYA-015", "AARON-004",
                    "PRIYA-016", "NICK-003", "NICK-004",
                }
            },
            { CutsceneId.WhenItWentWrong, new[]
                {
                    "RADIO-002", "NICK-005", "AARON-005", "RADIO-003", null,
                    "NICK-006", null, "NICK-007", "RADIO-004", null,
                }
            },
            { CutsceneId.EndingDavid, new[] { "SPASSKY-053" } },
            { CutsceneId.EndingAaron, new[] { "SPASSKY-054" } },
            { CutsceneId.EndingIvy, new[] { "SPASSKY-055" } },
            { CutsceneId.EndingPriya, new[] { "PRIYA-008", "SPASSKY-056" } },
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
                    if (string.IsNullOrEmpty(clipNames[b])) continue;
                    AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoRoot + clipNames[b] + ".wav");
                    if (clip == null)
                    {
                        Debug.LogWarning($"[CutsceneRecipeBuilder] Missing VO clip {clipNames[b]}.wav for {id} beat {b}.");
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

            CutsceneRecipe[] recipes = BuildRecipes();

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

        private static void WriteRecipe(SerializedProperty element, CutsceneRecipe recipe)
        {
            element.FindPropertyRelative("id").enumValueIndex = (int)recipe.id;
            element.FindPropertyRelative("fadeOutSeconds").floatValue = recipe.fadeOutSeconds;
            element.FindPropertyRelative("fadeInSeconds").floatValue = recipe.fadeInSeconds;
            element.FindPropertyRelative("keepScreenLit").boolValue = recipe.keepScreenLit;

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
                // Always write the generated result so inserting or reordering
                // recipes cannot leak a clip from the old serialized array slot.
                beatProp.FindPropertyRelative("voClip").objectReferenceValue = beat.voClip;

                // Same reasoning as voClip above: written unconditionally, so a
                // card cannot survive on a beat that no longer defines one.
                beatProp.FindPropertyRelative("timeCard").stringValue = beat.timeCard ?? string.Empty;
                beatProp.FindPropertyRelative("timeCardCaption").stringValue = beat.timeCardCaption ?? string.Empty;
                beatProp.FindPropertyRelative("timeCardHoldSeconds").floatValue = beat.timeCardHoldSeconds;
                beatProp.FindPropertyRelative("dipToBlackBefore").boolValue = beat.dipToBlackBefore;
            }
        }

        private static CutsceneBeat Beat(string speaker, string line, float hold, string flag = null) => new CutsceneBeat
        {
            speaker = speaker,
            line = line,
            holdSecondsIfNoClip = hold,
            memoryFlagToSet = flag,
        };

        /// <summary>A wordless beat carrying only SFX + hold (+ optional flag) —
        /// for the cheap-form transitions and stubs that have no dialogue in
        /// docs/STORY_SCRIPT.md §4/§5. `sfxName` is a file under
        /// Assets/_Project/Art/Audio/SFX/ (no extension); missing files log a
        /// warning and fall back to a silent hold rather than throwing, same
        /// as the VO-attach path.</summary>
        private static CutsceneBeat SfxBeat(string sfxName, float hold, string flag = null)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxRoot + sfxName + ".mp3");
            if (clip == null)
            {
                Debug.LogWarning($"[CutsceneRecipeBuilder] Missing SFX {sfxName}.mp3 under {SfxRoot} — beat will be a silent hold.");
            }
            return new CutsceneBeat
            {
                holdSecondsIfNoClip = hold,
                memoryFlagToSet = flag,
                voClip = clip,
            };
        }

        /// <summary>A dialogue beat whose voClip is loaded directly from
        /// Art/Audio/VO by filename, for lines added after the AttachVoClips
        /// VoClipNames table was written (VoClipNames maps by beat index, which
        /// gets fragile to extend for a single inserted line) — subtitle text
        /// still comes from `line`, same as Beat().</summary>
        /// <summary>Attaches a top-right time card to a beat, and optionally a
        /// dip to black before it for a jump forward in time.
        ///
        /// Used only where the script itself jumps: §4 stages CS-16B at 23:40
        /// and then "hard jump forward to roughly 00:50" in the same room. With
        /// no marker those halves played as one scene, so the fireplace
        /// conversation appeared to begin mid-thought.</summary>
        private static CutsceneBeat Card(CutsceneBeat beat, string time, string caption = null,
            bool dipToBlack = false, float hold = 3.5f)
        {
            beat.timeCard = time;
            beat.timeCardCaption = caption;
            beat.timeCardHoldSeconds = hold;
            beat.dipToBlackBefore = dipToBlack;
            return beat;
        }

        private static CutsceneBeat VoBeat(string speaker, string line, string voName, float holdIfMissing, string flag = null)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(VoRoot + voName + ".wav");
            if (clip == null)
            {
                Debug.LogWarning($"[CutsceneRecipeBuilder] Missing VO {voName}.wav under {VoRoot} — beat will be a silent hold.");
            }
            return new CutsceneBeat
            {
                speaker = speaker,
                line = line,
                holdSecondsIfNoClip = holdIfMissing,
                memoryFlagToSet = flag,
                voClip = clip,
            };
        }

        private static CutsceneRecipe Recipe(CutsceneId id, float fadeOut, float fadeIn, params CutsceneBeat[] beats) => new CutsceneRecipe
        {
            id = id,
            fadeOutSeconds = fadeOut,
            fadeInSeconds = fadeIn,
            beats = beats,
        };

        /// <summary>Same as Recipe(), but the screen stays lit for the whole
        /// cutscene instead of fading to black — for the M2 beats staged to be
        /// watched (Cutscene.CutsceneStage's player-walks-out/carries-Nick-in
        /// staging). fadeOut/fadeIn are kept at 0 here only for clarity in the
        /// recipe list; CutsceneDirector.PlayRoutine skips both fade calls
        /// entirely when keepScreenLit is set, so these values are never read.</summary>
        private static CutsceneRecipe VisibleRecipe(CutsceneId id, params CutsceneBeat[] beats) => new CutsceneRecipe
        {
            id = id,
            fadeOutSeconds = 0f,
            fadeInSeconds = 0f,
            keepScreenLit = true,
            beats = beats,
        };

        private static CutsceneRecipe[] BuildRecipes()
        {
            CutsceneRecipe[] recipes =
            {
                Recipe(CutsceneId.Wake, 0f, 0.6f,
                    Beat("???", "David.", 1.2f),
                    Beat("???", "David.", 1.0f),
                    Beat("???", "David!", 1.0f)),

                // Kept lit (not the fade-to-black cheap form every other
                // beat here uses) so the player watches Spassky deliver the
                // line and see CutsceneAnimationDirector's Cop_Talk clip
                // play, instead of hearing 8s of VO over a black screen.
                // This is the game's first cutscene.
                VisibleRecipe(CutsceneId.SpasskyAnswer,
                    Beat("SPASSKY",
                        "I'm Officer Spassky. Nick is dead, and right now you're one of the suspects. " +
                        "I've already spoken to the others. Take your time and tell me everything you " +
                        "remember from last night.", 8f)),

                VisibleRecipe(CutsceneId.NightArgument,
                    Beat("DAVID", "You have to tell him, Nick. Say it out loud before Aaron works it out for himself.", 4f),
                    Beat("NICK", "Not tonight, David. I can't do this with you right now. I need some air.", 4f)),

                // The four "fuzzy" transitions (§10: "the same asset, parameterised")
                // share one rewind-whoosh SFX, distinguished only by fade timing —
                // FuzzyToNight is the reverse/rewind (long, disorienting), the
                // other three are the forward return (shorter, snappier).
                Recipe(CutsceneId.FuzzyToNight, 1.2f, 1.2f,
                    SfxBeat("fuzzy_whoosh", 1.4f)),
                Recipe(CutsceneId.StandFromChair, 0.2f, 0.4f,
                    SfxBeat("chair_creak", 0.6f)),

                Recipe(CutsceneId.RadioClears, 0.2f, 0.3f,
                    Beat("RADIO", "A snowstorm is moving through the area. Please stay indoors until conditions improve.", 3f,
                        MemoryFlagIds.HeardRadioWarning)),

                Recipe(CutsceneId.SomeoneLeft, 0.3f, 0.3f,
                    SfxBeat("door_latch_close", 1.5f, MemoryFlagIds.SawDoorClose)),

                // Never actually raised by M1NightController.cs (the call-for-Nick
                // beat is a RequestSpokenPrompt, not a cutscene) — filled anyway so
                // a future direct call never hits an empty stub.
                Recipe(CutsceneId.CallForNick, 0.2f, 0.3f,
                    SfxBeat("wind_gust_roar", 1.8f)),
                Recipe(CutsceneId.FuzzyToInterrogation, 1.2f, 1.2f,
                    SfxBeat("fuzzy_whoosh", 0.9f)),
                Recipe(CutsceneId.FuzzyToMorning, 1.2f, 1.2f,
                    SfxBeat("fuzzy_whoosh", 0.9f)),

                Recipe(CutsceneId.PriyaScreams, 0.3f, 0.5f,
                    Beat("PRIYA", "Guys! Help! Something's happened to Nick! Ivy! Aaron! David! Please, come here!", 4f,
                        MemoryFlagIds.SawBody)),

                Recipe(CutsceneId.TheyComeDown, 0.2f, 0.3f,
                    SfxBeat("footsteps_stairs", 1.6f)),

                // These three beats used to be fade-to-black+VO like everything
                // else — the M2 fix (docs/GAME_COMPLETION_PLAN.md follow-up)
                // keeps the screen lit for them specifically, so the player
                // actually watches the door open, the walk out, the lift, and
                // the carry back rather than hearing it narrated over black.
                VisibleRecipe(CutsceneId.OutIntoTheSnow,
                    Beat("PRIYA", "What do we do? What do we do?", 2f),
                    Beat("IVY", "Oh my God. What happened to him? What do we do now?", 2.5f),
                    Beat("AARON", "He's freezing. Let's get him inside, onto the sofa by the fire.", 3f)),

                VisibleRecipe(CutsceneId.TheCarry,
                    Beat("PRIYA", "How did this happen?", 2f),
                    Beat("IVY", "I don't know. I was upstairs with Aaron.", 2f),
                    Beat("PRIYA", "All night?", 1.2f),
                    Beat("IVY", "Yes. All night.", 1.5f, MemoryFlagIds.HeardIvyAlibi),
                    Beat("AARON", "Priya. Not now.", 1.2f),
                    Beat("PRIYA", "The door was locked. Who locked it?", 2f),
                    Beat("AARON", "Lift on three. One, two, three.", 1.5f, MemoryFlagIds.HeardAaronDeflect)),

                VisibleRecipe(CutsceneId.TheSofa,
                    SfxBeat("body_settle_thud", 2f),
                    VoBeat("PRIYA", "Nick? Nick, can you hear me?", "PRIYA-006", 2.5f),
                    VoBeat("PRIYA", "Police? Our friend is hurt. We found him outside in the snow. Please send someone. Please hurry.", "PRIYA-007", 5f)),

                Recipe(CutsceneId.FuzzyToVerdict, 1.2f, 1.2f,
                    SfxBeat("fuzzy_whoosh", 0.9f)),

                // Spassky's scripted P3 beats. These play in the interrogation
                // room with the mic down, so they are pre-rendered rather than
                // live TTS — same treatment as spassky_answer, and it keeps the
                // longest lines off the live turn budget. VisibleRecipe because
                // §4 cuts hard between the room and the memories rather than
                // fading. No CutsceneStage staging: nothing moves in the room,
                // and CutsceneStage only lives in the memory scenes anyway.
                VisibleRecipe(CutsceneId.P3Photograph,
                    VoBeat("SPASSKY", "That's what I don't understand.", "SPASSKY-057", 2f),
                    VoBeat("SPASSKY", "Old friends. An anniversary. Drinks. From the way they tell it, things were going well.", "SPASSKY-058", 5f),
                    VoBeat("SPASSKY", "So when did it all go wrong?", "SPASSKY-059", 2.4f)),

                VisibleRecipe(CutsceneId.P3AfterGoodYears,
                    VoBeat("SPASSKY", "And somewhere between that photograph and sunrise, Nick ended up dead.", "SPASSKY-060", 4.4f),
                    VoBeat("SPASSKY", "If it wasn't you, David — who killed Nick?", "SPASSKY-061", 3f)),

                VisibleRecipe(CutsceneId.P3WhoDavid,
                    VoBeat("SPASSKY", "Who, David?", "SPASSKY-062", 1.6f)),

                // The P3 memory pair (docs/STORY_SCRIPT.md §4 P3_VERDICT, §5
                // CS-16A/CS-16B). VisibleRecipe, not Recipe: the script calls
                // for a hard cut into the photograph and a hard cut back on the
                // glasses touching / the door slamming, so these must not fade
                // to black at either end the way the fuzzy transitions do.
                //
                // Cutscene.CutsceneStage.GoodYears/WhenItWentWrong stage the
                // cabin underneath these beats and hand the scene back exactly
                // as M1_Night left it.
                //
                // Priya's three lines and Aaron's two are not rendered yet —
                // VoBeat falls back to a silent hold and logs which file is
                // missing, so the pair is playable now and completes itself
                // when the clips land under Art/Audio/VO with these names.
                VisibleRecipe(CutsceneId.GoodYears,
                    Card(VoBeat("PRIYA", "Fifteen years and you two still act exactly the same.", "PRIYA-014", 2.4f),
                        "21:00", "Earlier that night"),
                    VoBeat("NICK", "He was worse at seventeen.", "NICK-002", 1.6f),
                    VoBeat("PRIYA", "And two years for these two.", "PRIYA-015", 1.8f),
                    VoBeat("AARON", "Barely survived it.", "AARON-004", 1.4f),
                    VoBeat("PRIYA", "To us. Somehow.", "PRIYA-016", 1.4f),
                    VoBeat("NICK", "Unfortunately.", "NICK-003", 1.2f),
                    // The coat swap is now witnessed rather than inferred from
                    // the coat on the chair, which is why §9's clue 3 lists the
                    // good-years memory as a source alongside M1 and M2.
                    VoBeat("NICK", "Here. You look fucking freezing.", "NICK-004", 2f, MemoryFlagIds.SawCoatSwap)),

                VisibleRecipe(CutsceneId.WhenItWentWrong,
                    // The radio warning from M1 bleeds under the fragment in
                    // three broken pieces. These are cuts of radio_storm_warning
                    // rather than new lines; until they are cut, each is a
                    // subtitled silent hold.
                    Card(VoBeat("RADIO", "…snow storm…", "RADIO-002", 1.2f),
                        "23:40", "Later. The fire is dying."),
                    VoBeat("NICK", "You've been saying \"after this trip\" for two years.", "NICK-005", 2.6f),
                    // Aaron does not shout and does not approach. This one quiet
                    // question is his entire visible reaction, and the moment the
                    // player is being handed motive.
                    VoBeat("AARON", "…Two years?", "AARON-005", 1.6f, MemoryFlagIds.SawAaronLearn),
                    VoBeat("RADIO", "…please stay indoors…", "RADIO-003", 1.2f),
                    // David is heard only through the player's microphone, so
                    // his half of the argument is subtitle-only by design — a
                    // plain Beat with no VO name, never a missing-clip warning.
                    Card(Beat("DAVID", "You need to tell him.", 1.8f),
                        "00:50", "About an hour later — David and Nick, alone"),
                    // No dipToBlack here: CutsceneStage.WhenItWentWrong already
                    // blinks across this jump, and the cast swap has to happen
                    // inside that fade. Two dips would read as a stutter.
                    VoBeat("NICK", "He already knows.", "NICK-006", 1.6f),
                    Beat("DAVID", "Then say it to his face.", 1.8f),
                    VoBeat("NICK", "I need some air.", "NICK-007", 1.6f),
                    VoBeat("RADIO", "…during these times.", "RADIO-004", 1.2f),
                    SfxBeat("door_latch_close", 1.2f)),

                // Accusation flashbacks (docs/STORY_SCRIPT.md §4 P3_VERDICT):
                // "heavily degraded, no dialogue" — the cheap form is a held
                // black frame with a single diegetic sound doing the work.
                // Aaron's bolt-click and Priya's glass-clink are named in the
                // script; Ivy's beat is described as pure stillness/silence,
                // so it deliberately carries no SFX.
                Recipe(CutsceneId.FlashbackAaron, 0.5f, 0.5f,
                    SfxBeat("flashback_bolt_click", 5f)),
                Recipe(CutsceneId.FlashbackIvy, 0.5f, 0.5f,
                    Beat(null, null, 5f)),
                Recipe(CutsceneId.FlashbackPriya, 0.5f, 0.5f,
                    SfxBeat("flashback_glass_clink", 5f)),

                Recipe(CutsceneId.EndingDavid, 0.4f, 0.6f,
                    Beat("SPASSKY", "You were the only one who couldn't tell me where you were.", 4f)),
                Recipe(CutsceneId.EndingAaron, 0.4f, 0.6f,
                    Beat("SPASSKY", "He locked it. You unlocked it. Only one of those was a decision.", 4f)),
                Recipe(CutsceneId.EndingIvy, 0.4f, 0.6f,
                    Beat("SPASSKY", "She agreed with you. That's not the same as it being true.", 4f)),
                Recipe(CutsceneId.EndingPriya, 0.4f, 0.6f,
                    Beat("PRIYA", "What happened? Why won't anyone tell me what happened?", 4f),
                    Beat("SPASSKY", "She's the one who called us. Sit with that.", 4f)),
            };

            return recipes;
        }
    }
}
