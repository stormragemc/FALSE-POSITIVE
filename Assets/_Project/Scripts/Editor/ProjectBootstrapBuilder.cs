using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FalsePositive.Audio;
using FalsePositive.Cop;
using FalsePositive.Core;
using FalsePositive.Cutscene;
using FalsePositive.Dialogue;
using FalsePositive.Flow;
using FalsePositive.Menu;
using FalsePositive.Net;
using FalsePositive.Player;
using FalsePositive.UI;
using FalsePositive.Voice;
using TMPro;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using UnityEngine.UI;
using ULS = uLipSync;

namespace FalsePositive.Editor
{
    /// <summary>
    /// Executable form of docs/TRACK_A_EDITOR_SETUP.md. Builds _Persistent.unity
    /// and MainMenu.unity from scratch, creates the two config ScriptableObjects,
    /// fixes up Interrogation.unity for the A0 service split, and rewrites Build
    /// Settings. Re-runnable — each step wipes and rebuilds its own target so it
    /// never accumulates duplicate objects across runs.
    /// </summary>
    public static class ProjectBootstrapBuilder
    {
        private const string ConfigPath = "Assets/_Project/Config/InterrogationConfig.asset";
        private const string PersistentScenePath = "Assets/_Project/Scenes/_Persistent.unity";
        private const string MainMenuScenePath = "Assets/_Project/Scenes/MainMenu.unity";
        private const string InterrogationScenePath = "Assets/_Project/Scenes/Interrogation.unity";
        private const string NightScenePath = "Assets/_Project/Scenes/Memory_CabinNight.unity";
        private const string MorningScenePath = "Assets/_Project/Scenes/Memory_CabinMorning.unity";
        private const string PromptsRoot = "Assets/_Project/Prompts/";
        private const string ConfigRoot = "Assets/_Project/Config/";

        private static readonly DefaultControls.Resources UIResources = BuildUiResources();

        private static DefaultControls.Resources BuildUiResources()
        {
            // A fresh DefaultControls.Resources has every Sprite field null,
            // so every DefaultControls.Create* control (every button, toggle,
            // dropdown, slider in this file) rendered as a flat, borderless
            // white rectangle. Populate it with the engine's built-in UI
            // skin so buttons actually look like buttons.
            return new DefaultControls.Resources
            {
                standard = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd"),
                background = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd"),
                inputField = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/InputFieldBackground.psd"),
                knob = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd"),
                checkmark = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd"),
                dropdown = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/DropdownArrow.psd"),
                mask = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UIMask.psd"),
            };
        }

        // ------------------------------------------------------------------
        // Menu visual system — docs/TRACK_A_EDITOR_SETUP.md §3 / the menu
        // overhaul plan. "An evidence folder left open on a desk, lit by one
        // window, during a storm": flat dark plates, 2px hairlines, generous
        // negative space, tracked-out caps labels, one warm-red accent
        // reserved for the destructive action. Linear colour space is on and
        // Graphic.color converts from sRGB, so every value here is authored
        // from the design's hex swatch via the float constructor rather than
        // typed as a hex literal.
        // ------------------------------------------------------------------
        private static class MenuPalette
        {
            public static readonly Color Ink = new Color(0.020f, 0.027f, 0.043f);
            public static readonly Color Panel = new Color(0.043f, 0.059f, 0.086f);
            public static readonly Color PanelRaised = new Color(0.075f, 0.102f, 0.141f);
            public static readonly Color Rule = new Color(0.224f, 0.263f, 0.310f);
            public static readonly Color RuleDim = new Color(0.137f, 0.165f, 0.200f);
            public static readonly Color TextPrimary = new Color(0.894f, 0.914f, 0.941f);
            public static readonly Color TextSecondary = new Color(0.553f, 0.596f, 0.659f);
            public static readonly Color TextMuted = new Color(0.361f, 0.400f, 0.459f);
            public static readonly Color Accent = new Color(0.698f, 0.227f, 0.180f);
            public static readonly Color Focus = new Color(0.431f, 0.565f, 0.722f);

            public static Color BackdropScrim => new Color(Ink.r, Ink.g, Ink.b, 0.72f);
            public static Color PanelFill => new Color(Panel.r, Panel.g, Panel.b, 0.96f);
        }

        private const string CabinPrefabPath = "Assets/_Project/Art/Cabin_v2/Prefabs/Cabin_v2.prefab";
        private const string StormSkyMaterialPath = "Assets/_Project/CabinNight/Data/StormSky.mat";
        private const string SnowflakeTexturePath = "Assets/_Project/CabinNight/Data/Snowflake.png";
        private const string SnowMaterialPath = "Assets/_Project/CabinNight/Materials/SnowParticle_Transparent.mat";
        private const string MenuBackdropLayerName = "MenuBackdrop";

        [MenuItem("Tools/False Positive/Bootstrap/Build Everything (0-9)")]
        public static void BuildEverything()
        {
            TmpEssentialsBootstrap.ImportTmpEssentials(); // first — the menu's TMP text needs the default font before BuildMainMenuScene runs
            CabinV2Builder.BuildAll();
            BuildConfigAssets();
            BuildPersistentScene();
            BuildMainMenuScene();
            FixInterrogationScene();
            MemorySceneBuilderV2.BuildBoth();
            MemorySceneDressing.DressBothScenes();
            MemorySceneWiring.WireBoth();
            RewriteBuildSettings();
            CutsceneRecipeBuilder.PopulateRecipes();
            CutsceneRecipeBuilder.AttachVoClips();
            Debug.Log("[ProjectBootstrapBuilder] Full scaffold rebuilt.");
        }

        // ------------------------------------------------------------------
        // 1. _Persistent.unity
        // ------------------------------------------------------------------

        [MenuItem("Tools/False Positive/Bootstrap/1 - Build _Persistent Scene")]
        public static void BuildPersistentScene()
        {
            InterrogationConfig config = RequireConfig();
            Scene scene = OpenOrCreateEmptyScene(PersistentScenePath);

            // --- Flow ---
            GameObject flowRoot = new GameObject("Flow");
            GameObject gfdGo = NewChild(flowRoot.transform, "GameFlowDirector");
            GameFlowDirector gfd = gfdGo.AddComponent<GameFlowDirector>();
            SceneRouter router = NewChild(flowRoot.transform, "SceneRouter").AddComponent<SceneRouter>();
            NewChild(flowRoot.transform, "CursorVisibility").AddComponent<CursorVisibilityController>();
            SetField(gfd, "config", config);

            // --- BackendHealthProbe ---
            GameObject backendGo = new GameObject("BackendHealthProbe");
            InterrogationSidecarClient sidecarClient = backendGo.AddComponent<InterrogationSidecarClient>();
            SidecarProcessLauncher launcher = backendGo.AddComponent<SidecarProcessLauncher>();
            BackendHealthProbe probe = backendGo.AddComponent<BackendHealthProbe>();
            SetField(sidecarClient, "config", config);
            SetField(launcher, "config", config);
            SetField(launcher, "client", sidecarClient);
            SetField(probe, "sidecarLauncher", launcher);

            // --- VoiceSystem ---
            GameObject voiceRoot = new GameObject("VoiceSystem");
            MicrophoneService mic = NewChild(voiceRoot.transform, "MicrophoneService").AddComponent<MicrophoneService>();
            SetField(mic, "config", config);

            VoiceActivityDetector vad = NewChild(voiceRoot.transform, "VoiceActivityDetector").AddComponent<VoiceActivityDetector>();
            SetField(vad, "mic", mic);
            SetField(vad, "config", config);

            UtteranceRecorder recorder = NewChild(voiceRoot.transform, "UtteranceRecorder").AddComponent<UtteranceRecorder>();
            SetField(recorder, "vad", vad);
            SetField(recorder, "config", config);

            MicCalibration calibration = NewChild(voiceRoot.transform, "MicCalibration").AddComponent<MicCalibration>();
            SetField(calibration, "mic", mic);
            SetField(calibration, "vad", vad);
            SetField(calibration, "config", config);

            LoudnessGate loudnessGate = NewChild(voiceRoot.transform, "LoudnessGate").AddComponent<LoudnessGate>();
            SetField(loudnessGate, "recorder", recorder);

            // --- HUD Canvas (Screen Space Overlay, sort order 100) ---
            GameObject hud = CreateCanvasRoot("HUD", 100);

            // ScreenFader — its own canvas, sort order 200, above everything.
            GameObject faderCanvas = CreateCanvasRoot("ScreenFaderCanvas", 200);
            GameObject faderGo = new GameObject("ScreenFader", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            faderGo.transform.SetParent(faderCanvas.transform, false);
            StretchFill(faderGo);
            Image faderImage = faderGo.GetComponent<Image>();
            faderImage.color = Color.black;
            CanvasGroup faderCg = faderGo.GetComponent<CanvasGroup>();
            faderCg.alpha = 0f;
            faderCg.blocksRaycasts = false;
            faderCg.interactable = false;
            ScreenFader fader = faderGo.AddComponent<ScreenFader>();
            SetField(fader, "canvasGroup", faderCg);

            // MicIndicator
            GameObject micIndGo = new GameObject("MicIndicator", typeof(RectTransform));
            micIndGo.transform.SetParent(hud.transform, false);
            AnchorTopRight(micIndGo, new Vector2(-160f, -30f), new Vector2(300f, 64f));
            Text micIndText = CreateText(micIndGo.transform, "Label", "Microphone inactive", 14);
            micIndText.color = MenuPalette.TextMuted; // risk #11: dial back from stock white/20 now that the menu has a real palette
            micIndText.raycastTarget = false; // this indicator must never swallow a backdrop-dismiss click from the panels drawn above it
            AnchorTopStretch(micIndText.gameObject, 0f, 40f);
            Image micIndDot = CreateDotImage(micIndGo.transform, "Dot", new Vector2(-150f, 12f));
            micIndDot.raycastTarget = false;

            GameObject meterBg = new GameObject("LevelMeterBackground", typeof(RectTransform), typeof(Image));
            meterBg.transform.SetParent(micIndGo.transform, false);
            meterBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            meterBg.GetComponent<Image>().raycastTarget = false;
            // AnchorBottomStretch sets pivot (0.5,0) — the hand-rolled version this
            // replaced left the default (0.5,0.5) pivot, so the bar straddled the
            // rect's bottom edge instead of sitting flush above it.
            AnchorBottomStretch(meterBg, 4f, 12f);
            RectTransform meterBgRt = meterBg.GetComponent<RectTransform>();
            meterBgRt.sizeDelta = new Vector2(-8f, meterBgRt.sizeDelta.y);

            GameObject meterFillGo = new GameObject("LevelMeterFill", typeof(RectTransform), typeof(Image));
            meterFillGo.transform.SetParent(meterBg.transform, false);
            RectTransform meterFillRt = meterFillGo.GetComponent<RectTransform>();
            meterFillRt.anchorMin = Vector2.zero;
            meterFillRt.anchorMax = new Vector2(0f, 1f);
            meterFillRt.offsetMin = Vector2.zero;
            meterFillRt.offsetMax = Vector2.zero;
            Image meterFill = meterFillGo.GetComponent<Image>();
            meterFill.color = new Color(0.6f, 0.6f, 0.6f);
            meterFill.raycastTarget = false;

            MicIndicator micIndicator = micIndGo.AddComponent<MicIndicator>();
            SetField(micIndicator, "mic", mic);
            SetField(micIndicator, "vad", vad);
            SetField(micIndicator, "recorder", recorder);
            SetField(micIndicator, "config", config);
            SetField(micIndicator, "label", micIndText);
            SetField(micIndicator, "dot", micIndDot);
            SetField(micIndicator, "levelMeterRoot", meterBg);
            SetField(micIndicator, "levelMeterFill", meterFill);

            // SpeechPrompt
            GameObject speechGo = new GameObject("SpeechPrompt", typeof(RectTransform));
            speechGo.transform.SetParent(hud.transform, false);
            AnchorBottomCenter(speechGo, new Vector2(0f, 140f), new Vector2(900f, 110f));
            Text speechPromptText = CreateText(speechGo.transform, "PromptText", string.Empty, 30);
            AnchorTopStretch(speechPromptText.gameObject, 0f, 60f);
            Text speechHintText = CreateText(speechGo.transform, "HintText", string.Empty, 20);
            speechHintText.color = new Color(1f, 0.85f, 0.4f);
            AnchorBottomStretch(speechHintText.gameObject, 0f, 40f);
            SpeechPrompt speechPrompt = speechGo.AddComponent<SpeechPrompt>();
            SetField(speechPrompt, "root", speechGo);
            SetField(speechPrompt, "promptText", speechPromptText);
            SetField(speechPrompt, "hintText", speechHintText);
            speechGo.SetActive(false);

            // SubtitleUI
            GameObject subtitleGo = new GameObject("SubtitleUI", typeof(RectTransform));
            subtitleGo.transform.SetParent(hud.transform, false);
            AnchorBottomCenter(subtitleGo, new Vector2(0f, 60f), new Vector2(1100f, 100f));
            Text speakerText = CreateText(subtitleGo.transform, "SpeakerText", string.Empty, 22);
            speakerText.fontStyle = FontStyle.Bold;
            AnchorTopStretch(speakerText.gameObject, 0f, 32f);
            Text lineText = CreateText(subtitleGo.transform, "LineText", string.Empty, 24);
            AnchorBottomStretch(lineText.gameObject, 0f, 60f);
            SubtitleUI subtitleUi = subtitleGo.AddComponent<SubtitleUI>();
            SetField(subtitleUi, "root", subtitleGo);
            SetField(subtitleUi, "speakerText", speakerText);
            SetField(subtitleUi, "lineText", lineText);
            subtitleGo.SetActive(false);

            // InteractionPromptUI — centre-screen "[E] <prompt>", the on-
            // screen prompt InteractionRaycaster never had (see its doc
            // comment). Replaces the floating TextMesh labels
            // MemorySceneDressing used to put over every prop. Poller/root
            // split matching OfflineModeLabel: the poller (this GameObject)
            // must stay active for LateUpdate to ever run, so the toggled
            // visibility lives on a separate child.
            GameObject interactionPromptPoller = new GameObject("InteractionPrompt", typeof(RectTransform));
            interactionPromptPoller.transform.SetParent(hud.transform, false);
            StretchFill(interactionPromptPoller);
            GameObject interactionPromptRoot = new GameObject("Root", typeof(RectTransform));
            interactionPromptRoot.transform.SetParent(interactionPromptPoller.transform, false);
            AnchorBottomCenter(interactionPromptRoot, new Vector2(0f, 90f), new Vector2(600f, 50f));
            Text interactionPromptText = CreateText(interactionPromptRoot.transform, "PromptText", string.Empty, 26);
            interactionPromptText.color = new Color(1f, 0.92f, 0.6f);
            StretchFill(interactionPromptText.gameObject);
            InteractionPromptUI interactionPrompt = interactionPromptPoller.AddComponent<InteractionPromptUI>();
            SetField(interactionPrompt, "root", interactionPromptRoot);
            SetField(interactionPrompt, "promptText", interactionPromptText);
            interactionPromptRoot.SetActive(false);

            // ObjectiveHud
            GameObject objectiveGo = new GameObject("ObjectiveHud", typeof(RectTransform));
            objectiveGo.transform.SetParent(hud.transform, false);
            AnchorTopLeft(objectiveGo, new Vector2(40f, -40f), new Vector2(500f, 40f));
            Text objectiveText = CreateText(objectiveGo.transform, "Label", string.Empty, 22);
            objectiveText.alignment = TextAnchor.MiddleLeft;
            StretchFill(objectiveText.gameObject);
            ObjectiveHud objectiveHud = objectiveGo.AddComponent<ObjectiveHud>();
            SetField(objectiveHud, "root", objectiveGo);
            SetField(objectiveHud, "label", objectiveText);
            objectiveGo.SetActive(false);

            // OfflineModeLabel — see docs/GAME_COMPLETION_PLAN.md §10, "never fake".
            // The poller (OfflineModeLabel) lives on an always-active object —
            // it must keep receiving Update() to ever turn "root" back on —
            // and "root" is the separate child it shows/hides.
            GameObject offlineLabelPoller = new GameObject("OfflineModeLabel", typeof(RectTransform));
            offlineLabelPoller.transform.SetParent(hud.transform, false);
            StretchFill(offlineLabelPoller);
            GameObject offlineLabelRoot = new GameObject("Root", typeof(RectTransform));
            offlineLabelRoot.transform.SetParent(offlineLabelPoller.transform, false);
            AnchorTopLeft(offlineLabelRoot, new Vector2(40f, -90f), new Vector2(420f, 30f));
            Text offlineLabelText = CreateText(offlineLabelRoot.transform, "Label", "OFFLINE — scripted interrogation", 18);
            offlineLabelText.alignment = TextAnchor.MiddleLeft;
            offlineLabelText.color = new Color(1f, 0.75f, 0.3f);
            StretchFill(offlineLabelText.gameObject);
            OfflineModeLabel offlineLabel = offlineLabelPoller.AddComponent<OfflineModeLabel>();
            SetField(offlineLabel, "root", offlineLabelRoot);
            offlineLabelRoot.SetActive(false);

            // ConsentPanel (MicConsentFlow)
            GameObject consentGo = BuildConsentPanel(hud.transform, mic, out MicConsentFlow consentFlow);

            // CalibrationPanel
            GameObject calibrationGo = BuildCalibrationPanel(hud.transform, calibration, mic, vad, config, out CalibrationPanelUI calibrationPanelUi);

            // SettingsPanelRoot
            GameObject settingsGo = BuildSettingsPanel(hud.transform, mic, out SettingsPanel settingsPanel);

            // DebugOverlayPanel
            GameObject debugGo = BuildDebugOverlayPanel(hud.transform, out DebugOverlayUI debugOverlay);

            // FaultCard / OutcomeScreen — A13/A11 stubs, Day 2.
            GameObject faultCard = new GameObject("FaultCard", typeof(RectTransform));
            faultCard.transform.SetParent(hud.transform, false);
            StretchFill(faultCard);
            faultCard.SetActive(false);

            GameObject outcomeGo = BuildOutcomePanel(hud.transform, out OutcomeScreen outcomeScreen);

            // SimulatedSpeechButton — offline-demo test hook only, see its
            // own class doc comment. Poller/root split matches
            // OfflineModeLabel above; deliberately NOT a Button (see that
            // doc comment for why) so it never affects CursorVisibilityController.
            GameObject simSpeechPoller = new GameObject("SimulatedSpeechButton", typeof(RectTransform));
            simSpeechPoller.transform.SetParent(hud.transform, false);
            StretchFill(simSpeechPoller);
            GameObject simSpeechRoot = new GameObject("Root", typeof(RectTransform));
            simSpeechRoot.transform.SetParent(simSpeechPoller.transform, false);
            AnchorBottomRight(simSpeechRoot, new Vector2(-40f, 40f), new Vector2(300f, 52f));
            Image simSpeechPlate = CreateFlatImage(simSpeechRoot.transform, "Plate", MenuPalette.PanelRaised);
            simSpeechPlate.raycastTarget = true; // CreateFlatImage defaults this off; this plate must catch clicks/hover
            StretchFill(simSpeechPlate.gameObject);
            TextMeshProUGUI simSpeechLabel = CreateTmpText(simSpeechPlate.transform, "Label", "SIMULATE SPEECH   [F3]", 15f,
                MenuPalette.TextPrimary, TextAlignmentOptions.Center, tracking: 4f, style: FontStyles.UpperCase);
            simSpeechLabel.raycastTarget = false;
            StretchFill(simSpeechLabel.gameObject);
            SimulatedSpeechButton simSpeechButton = simSpeechPoller.AddComponent<SimulatedSpeechButton>();
            SetField(simSpeechButton, "root", simSpeechRoot);
            SetField(simSpeechButton, "label", simSpeechLabel);
            SetField(simSpeechButton, "plate", simSpeechPlate);
            SetField(simSpeechButton, "recorder", recorder);
            SetField(simSpeechButton, "calibration", calibration);
            SetField(simSpeechButton, "normalColor", MenuPalette.PanelRaised);
            SetField(simSpeechButton, "hoverColor", MenuPalette.Focus);
            simSpeechRoot.SetActive(false);

            // Guardrail #2 (docs/GAME_COMPLETION_PLAN.md §8): the mic-state
            // indicator must stay visible and honest through every panel this
            // method builds — consent, calibration, settings, outcome — not
            // dimmed underneath their backdrop scrims. All the panels above
            // are built after MicIndicator, so re-assert last-sibling now
            // that they all exist; its own graphics are raycastTarget=false
            // so it never blocks a backdrop-dismiss click either. The debug
            // test button re-asserts after it, so it can still be clicked/
            // read even though MicIndicator itself never blocks raycasts.
            micIndGo.transform.SetAsLastSibling();
            simSpeechPoller.transform.SetAsLastSibling();

            // CutsceneDirector (B1) — same-scene wiring only, per its own
            // doc-comment: never a camera/object from whichever memory or
            // interrogation scene happens to be active.
            GameObject cutsceneGo = new GameObject("CutsceneDirector");
            GameObject voSourceGo = new GameObject("CutsceneVoSource", typeof(AudioSource));
            voSourceGo.transform.SetParent(cutsceneGo.transform, false);
            AudioSource cutsceneVoSource = voSourceGo.GetComponent<AudioSource>();
            // uLipSyncAudioSource ([RequireComponent(typeof(AudioSource))]) lets
            // uLipSync.audioSourceProxy analyze THIS AudioSource's playback
            // instead of only the GameObject its own uLipSync component lives
            // on — see Cutscene.CutsceneAnimationDirector, which points the
            // Cop's uLipSync at this during CutsceneId.SpasskyAnswer so the
            // mouth syncs to the real cutscene VO, not just live dialogue turns.
            ULS.uLipSyncAudioSource cutsceneVoLipSync = voSourceGo.AddComponent<ULS.uLipSyncAudioSource>();
            CutsceneDirector cutsceneDirector = cutsceneGo.AddComponent<CutsceneDirector>();
            SetField(cutsceneDirector, "fader", fader);
            SetField(cutsceneDirector, "subtitles", subtitleUi);
            SetField(cutsceneDirector, "voSource", cutsceneVoSource);
            SetField(cutsceneDirector, "voSourceLipSync", cutsceneVoLipSync);

            // EventSystem — the project uses the new Input System exclusively
            // (Active Input Handling = Input System Package), so this must be
            // InputSystemUIInputModule; the legacy StandaloneInputModule throws
            // InvalidOperationException on every EventSystem.Update() tick.
            GameObject eventSystemGo = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));

            // --- Wire GameFlowDirector ---
            SetField(gfd, "mic", mic);
            SetField(gfd, "vad", vad);
            SetField(gfd, "recorder", recorder);
            SetField(gfd, "sidecar", sidecarClient);
            SetField(gfd, "loudnessGate", loudnessGate);
            SetField(gfd, "fader", fader);
            SetField(gfd, "subtitles", subtitleUi);
            SetField(gfd, "prompt", speechPrompt);
            SetField(gfd, "objectives", objectiveHud);
            SetField(gfd, "sceneRouter", router);
            SetField(gfd, "consentFlow", consentFlow);
            SetField(gfd, "calibration", calibration);
            SetField(gfd, "calibrationPanel", calibrationPanelUi);
            SetField(gfd, "debugOverlay", debugOverlay);
            SetField(gfd, "settingsPanel", settingsPanel);
            SetField(gfd, "outcomeScreen", outcomeScreen);

            MemoryFlagCatalog catalog = AssetDatabase.LoadAssetAtPath<MemoryFlagCatalog>(ConfigRoot + "MemoryFlagCatalog.asset");
            if (catalog != null) SetField(gfd, "memoryFlagCatalog", catalog);
            else Debug.LogWarning("[ProjectBootstrapBuilder] MemoryFlagCatalog.asset not found yet — run step 3 (Build Config Assets) and re-run step 1, or it will bind on the next pass.");

            SaveScene(scene, PersistentScenePath);
            Debug.Log("[ProjectBootstrapBuilder] _Persistent.unity rebuilt.");
        }

        private static GameObject BuildOutcomePanel(Transform hud, out OutcomeScreen outcomeScreen)
        {
            GameObject root = new GameObject("OutcomeScreen", typeof(RectTransform));
            root.transform.SetParent(hud, false);
            StretchFill(root);
            Image backing = root.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.92f);

            Text card = CreateText(root.transform, "CardText", string.Empty, 30);
            card.alignment = TextAnchor.MiddleCenter;
            AnchorCenter(card.gameObject, new Vector2(0f, 40f), new Vector2(1100f, 400f));

            Button quitButton = CreateButton(root.transform, "QuitToMenuButton", "Return to menu");
            AnchorCenter(quitButton.gameObject, new Vector2(0f, -260f), new Vector2(260f, 56f));

            outcomeScreen = root.AddComponent<OutcomeScreen>();
            SetField(outcomeScreen, "root", root);
            SetField(outcomeScreen, "cardText", card);
            SetField(outcomeScreen, "quitToMenuButton", quitButton);
            root.SetActive(false);
            return root;
        }

        /// <summary>RC3 fix 3.1: the consent gate rebuilt on CreateWindow.
        /// closeOnBackdropClick is deliberately left unwired — window.BackdropButton
        /// has no listener here — because this is a consent gate, not a dismissible
        /// popup; Back is the only way out. Consent copy is verbatim from
        /// docs/STORY_SCRIPT.md §4 (S0), guardrail #1 — MicConsentFlow.Awake sets
        /// it from its own const, so CopyText starts empty here.</summary>
        private static GameObject BuildConsentPanel(Transform hud, MicrophoneService mic, out MicConsentFlow flow)
        {
            WindowRefs window = CreateWindow(hud, "ConsentPanel", "Microphone access", new Vector2(880f, 460f));

            VerticalLayoutGroup contentLayout = window.Content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 24f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            // No SetLayoutHeight — fix 2.2: a wrapping paragraph's height comes
            // from TMP's own preferred size now that childControlHeight is true.
            TextMeshProUGUI copy = CreateTmpText(window.Content, "CopyText", string.Empty, 17f,
                MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft, lineSpacing: 6f);

            SettingsRowRefs deviceRow = CreateSettingsRow(window.Content, "Input Device", withReadout: false, height: 52f);
            Dropdown deviceDropdown = CreateDropdown(deviceRow.ControlSlot, "DeviceDropdown");
            StretchFill(deviceDropdown.gameObject);
            RestyleDropdown(deviceDropdown);

            Button backButton = CreateTmpButton(window.Footer, "BackButton", "Back", 19f);
            RectTransform backRt = backButton.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(0f, 0.5f);
            backRt.anchorMax = new Vector2(0f, 0.5f);
            backRt.pivot = new Vector2(0f, 0.5f);
            backRt.anchoredPosition = new Vector2(28f, 0f);
            backRt.sizeDelta = new Vector2(200f, 48f);

            Button enableButton = CreateTmpButton(window.Footer, "EnableButton", "Enable microphone", 19f, accent: true);
            RectTransform enableRt = enableButton.GetComponent<RectTransform>();
            enableRt.anchorMin = new Vector2(1f, 0.5f);
            enableRt.anchorMax = new Vector2(1f, 0.5f);
            enableRt.pivot = new Vector2(1f, 0.5f);
            enableRt.anchoredPosition = new Vector2(-28f, 0f);
            enableRt.sizeDelta = new Vector2(300f, 48f);

            // Lives on the WindowHost, not Root — same fade-must-outlive-
            // deactivation constraint as MenuWindow/SettingsPanel (see
            // CreateWindow's doc comment).
            flow = window.Host.AddComponent<MicConsentFlow>();
            SetField(flow, "root", window.Root);
            SetField(flow, "canvasGroup", window.CanvasGroup);
            SetField(flow, "copyText", copy);
            SetField(flow, "deviceDropdown", deviceDropdown);
            SetField(flow, "enableButton", enableButton);
            SetField(flow, "backButton", backButton);
            SetField(flow, "mic", mic);

            window.Root.SetActive(false);
            return window.Root;
        }

        /// <summary>RC3 fix 3.1/3.4/3.5: the calibration card rebuilt on
        /// CreateWindow, with a working level meter (fix 3.4 — driven via
        /// anchorMax.x through MicIndicator.RmsToMeter, not the no-op
        /// Image.fillAmount the legacy version used) and a calibration
        /// progress bar (fix 3.5, fed by MicCalibration.Progress01). The
        /// legacy FailureText is not recreated (fix 3.6) — CalibrationPanelUI
        /// already writes the failure reason into StatusText, so a second
        /// copy of the same message would be redundant.</summary>
        private static GameObject BuildCalibrationPanel(
            Transform hud, MicCalibration calibration, MicrophoneService mic, VoiceActivityDetector vad,
            InterrogationConfig config, out CalibrationPanelUI panelUi)
        {
            WindowRefs window = CreateWindow(hud, "CalibrationPanel", "Microphone check", new Vector2(880f, 460f));

            VerticalLayoutGroup contentLayout = window.Content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 20f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            TextMeshProUGUI status = CreateTmpText(window.Content, "StatusText", string.Empty, 17f,
                MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft, lineSpacing: 6f);

            GameObject meterRow = new GameObject("LevelMeterRow", typeof(RectTransform));
            meterRow.transform.SetParent(window.Content, false);
            SetLayoutHeight(meterRow, 32f);

            TextMeshProUGUI meterLabel = CreateTmpText(meterRow.transform, "Label", "LEVEL", 14f, MenuPalette.TextMuted,
                TextAlignmentOptions.MidlineLeft, tracking: 4f, style: FontStyles.UpperCase);
            RectTransform meterLabelRt = meterLabel.rectTransform;
            meterLabelRt.anchorMin = new Vector2(0f, 0f);
            meterLabelRt.anchorMax = new Vector2(0f, 1f);
            meterLabelRt.pivot = new Vector2(0f, 0.5f);
            meterLabelRt.sizeDelta = new Vector2(90f, 0f);
            meterLabelRt.anchoredPosition = Vector2.zero;

            GameObject meterBg = new GameObject("LevelMeterBackground", typeof(RectTransform), typeof(Image));
            meterBg.transform.SetParent(meterRow.transform, false);
            meterBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            meterBg.GetComponent<Image>().raycastTarget = false;
            RectTransform meterBgRt = meterBg.GetComponent<RectTransform>();
            meterBgRt.anchorMin = new Vector2(0f, 0.5f);
            meterBgRt.anchorMax = new Vector2(1f, 0.5f);
            meterBgRt.pivot = new Vector2(0.5f, 0.5f);
            meterBgRt.offsetMin = new Vector2(104f, -8f);
            meterBgRt.offsetMax = new Vector2(0f, 8f);

            GameObject meterFillGo = new GameObject("LevelMeterFill", typeof(RectTransform), typeof(Image));
            meterFillGo.transform.SetParent(meterBg.transform, false);
            RectTransform meterFillRt = meterFillGo.GetComponent<RectTransform>();
            meterFillRt.anchorMin = Vector2.zero;
            meterFillRt.anchorMax = new Vector2(0f, 1f);
            meterFillRt.offsetMin = Vector2.zero;
            meterFillRt.offsetMax = Vector2.zero;
            Image meterFill = meterFillGo.GetComponent<Image>();
            meterFill.color = MenuPalette.TextMuted;
            meterFill.raycastTarget = false;

            Image divider = CreateFlatImage(window.Content, "Divider", MenuPalette.RuleDim);
            SetLayoutHeight(divider.gameObject, 1f);

            GameObject progressRow = new GameObject("ProgressRow", typeof(RectTransform));
            progressRow.transform.SetParent(window.Content, false);
            SetLayoutHeight(progressRow, 28f);

            TextMeshProUGUI progressLabel = CreateTmpText(progressRow.transform, "Label", "CALIBRATING", 14f,
                MenuPalette.TextMuted, TextAlignmentOptions.MidlineLeft, tracking: 4f, style: FontStyles.UpperCase);
            RectTransform progressLabelRt = progressLabel.rectTransform;
            progressLabelRt.anchorMin = new Vector2(0f, 0f);
            progressLabelRt.anchorMax = new Vector2(0f, 1f);
            progressLabelRt.pivot = new Vector2(0f, 0.5f);
            progressLabelRt.sizeDelta = new Vector2(120f, 0f);
            progressLabelRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI progressReadout = CreateTmpText(progressRow.transform, "Readout", "0%", 14f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineRight);
            RectTransform progressReadoutRt = progressReadout.rectTransform;
            progressReadoutRt.anchorMin = new Vector2(1f, 0f);
            progressReadoutRt.anchorMax = new Vector2(1f, 1f);
            progressReadoutRt.pivot = new Vector2(1f, 0.5f);
            progressReadoutRt.sizeDelta = new Vector2(56f, 0f);
            progressReadoutRt.anchoredPosition = Vector2.zero;

            GameObject progressBarBg = new GameObject("ProgressBarBackground", typeof(RectTransform), typeof(Image));
            progressBarBg.transform.SetParent(progressRow.transform, false);
            progressBarBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            progressBarBg.GetComponent<Image>().raycastTarget = false;
            RectTransform progressBarBgRt = progressBarBg.GetComponent<RectTransform>();
            progressBarBgRt.anchorMin = new Vector2(0f, 0.5f);
            progressBarBgRt.anchorMax = new Vector2(1f, 0.5f);
            progressBarBgRt.pivot = new Vector2(0.5f, 0.5f);
            progressBarBgRt.offsetMin = new Vector2(132f, -6f);
            progressBarBgRt.offsetMax = new Vector2(-64f, 6f);

            GameObject progressFillGo = new GameObject("ProgressBarFill", typeof(RectTransform), typeof(Image));
            progressFillGo.transform.SetParent(progressBarBg.transform, false);
            RectTransform progressFillRt = progressFillGo.GetComponent<RectTransform>();
            progressFillRt.anchorMin = Vector2.zero;
            progressFillRt.anchorMax = new Vector2(0f, 1f);
            progressFillRt.offsetMin = Vector2.zero;
            progressFillRt.offsetMax = Vector2.zero;
            Image progressFill = progressFillGo.GetComponent<Image>();
            progressFill.color = MenuPalette.Focus;
            progressFill.raycastTarget = false;

            // Only the device dropdown lives here — the failure reason itself
            // is written into StatusText by CalibrationPanelUI.OnFailed.
            GameObject failurePanel = new GameObject("FailurePanel", typeof(RectTransform));
            failurePanel.transform.SetParent(window.Content, false);
            SetLayoutHeight(failurePanel, 52f);
            Dropdown deviceDropdown = CreateDropdown(failurePanel.transform, "DeviceDropdown");
            StretchFill(deviceDropdown.gameObject);
            RestyleDropdown(deviceDropdown);
            failurePanel.SetActive(false);

            Button cancelButton = CreateTmpButton(window.Footer, "CancelButton", "Cancel", 19f);
            RectTransform cancelRt = cancelButton.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(0f, 0.5f);
            cancelRt.anchorMax = new Vector2(0f, 0.5f);
            cancelRt.pivot = new Vector2(0f, 0.5f);
            cancelRt.anchoredPosition = new Vector2(28f, 0f);
            cancelRt.sizeDelta = new Vector2(180f, 48f);

            Button retryButton = CreateTmpButton(window.Footer, "RetryButton", "Retry", 19f, accent: true);
            RectTransform retryRt = retryButton.GetComponent<RectTransform>();
            retryRt.anchorMin = new Vector2(1f, 0.5f);
            retryRt.anchorMax = new Vector2(1f, 0.5f);
            retryRt.pivot = new Vector2(1f, 0.5f);
            retryRt.anchoredPosition = new Vector2(-28f, 0f);
            retryRt.sizeDelta = new Vector2(180f, 48f);

            // Both start hidden — CalibrationPanelUI.Show() calls
            // SetFailureVisible(false) too, this just matches that default
            // before Show() ever runs once.
            cancelButton.gameObject.SetActive(false);
            retryButton.gameObject.SetActive(false);

            // Lives on the WindowHost, not Root — same fade-must-outlive-
            // deactivation constraint as MenuWindow/SettingsPanel. No
            // MenuWindow here and closeOnBackdropClick is deliberately
            // unwired — this is a consent gate, not a dismissible popup.
            panelUi = window.Host.AddComponent<CalibrationPanelUI>();
            SetField(panelUi, "root", window.Root);
            SetField(panelUi, "canvasGroup", window.CanvasGroup);
            SetField(panelUi, "statusText", status);
            SetField(panelUi, "levelMeterFill", meterFill);
            SetField(panelUi, "progressBarFill", progressFill);
            SetField(panelUi, "progressReadout", progressReadout);
            SetField(panelUi, "failurePanel", failurePanel);
            SetField(panelUi, "deviceDropdown", deviceDropdown);
            SetField(panelUi, "retryButton", retryButton);
            SetField(panelUi, "cancelButton", cancelButton);
            SetField(panelUi, "calibration", calibration);
            SetField(panelUi, "mic", mic);
            SetField(panelUi, "vad", vad);
            SetField(panelUi, "config", config);

            window.Root.SetActive(false);
            return window.Root;
        }

        private static GameObject BuildSettingsPanel(Transform hud, MicrophoneService mic, out SettingsPanel settingsPanel)
        {
            WindowRefs window = CreateWindow(hud, "SettingsPanelRoot", "Settings", new Vector2(880f, 780f));

            // Tighter than CreateWindow's default 20px header/footer gap and
            // CreateSettingsRow's default 56px row height — Settings packs
            // four sections into one non-scrolling window, unlike Credits
            // (scrollable) or How-to-play (three sections only).
            RectTransform contentRt = (RectTransform)window.Content;
            contentRt.offsetMin = new Vector2(contentRt.offsetMin.x, 84f + 12f);
            contentRt.offsetMax = new Vector2(contentRt.offsetMax.x, -(64f + 12f));

            VerticalLayoutGroup contentLayout = window.Content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 4f;
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            CreateSectionHeader(window.Content, "Audio", 22f);

            SettingsRowRefs masterRow = CreateSettingsRow(window.Content, "Master Volume", height: 52f);
            Slider masterSlider = CreateSlider(masterRow.ControlSlot, "MasterVolumeSlider", 0f, 1f, 1f);
            StretchFill(masterSlider.gameObject);
            RestyleSlider(masterSlider);

            SettingsRowRefs voiceRow = CreateSettingsRow(window.Content, "Voice", height: 52f);
            Slider voiceSlider = CreateSlider(voiceRow.ControlSlot, "VoiceVolumeSlider", 0f, 1f, 1f);
            StretchFill(voiceSlider.gameObject);
            RestyleSlider(voiceSlider);

            SettingsRowRefs sfxRow = CreateSettingsRow(window.Content, "Effects", height: 52f);
            Slider sfxSlider = CreateSlider(sfxRow.ControlSlot, "SfxVolumeSlider", 0f, 1f, 1f);
            StretchFill(sfxSlider.gameObject);
            RestyleSlider(sfxSlider);

            CreateSectionHeader(window.Content, "Microphone", 22f);

            SettingsRowRefs micRow = CreateSettingsRow(window.Content, "Input Device", withReadout: false, height: 52f);
            Dropdown micDropdown = CreateDropdown(micRow.ControlSlot, "MicDeviceDropdown");
            StretchFill(micDropdown.gameObject);
            RestyleDropdown(micDropdown);

            TextMeshProUGUI micCaption = CreateTmpText(window.Content, "MicCaption",
                "Processed to transcribe speech and read tone, then discarded — never written to disk.",
                14f, MenuPalette.TextMuted, TextAlignmentOptions.TopLeft);
            SetLayoutHeight(micCaption.gameObject, 20f);

            CreateSectionHeader(window.Content, "Controls", 22f);

            SettingsRowRefs sensitivityRow = CreateSettingsRow(window.Content, "Look Sensitivity", height: 52f);
            Slider sensitivitySlider = CreateSlider(sensitivityRow.ControlSlot, "MouseSensitivitySlider", 0.1f, 3f, 1f);
            StretchFill(sensitivitySlider.gameObject);
            RestyleSlider(sensitivitySlider);

            SettingsRowRefs invertRow = CreateSettingsRow(window.Content, "Invert Vertical Look", withReadout: false, height: 52f);
            Toggle invertYToggle = CreateToggle(invertRow.ControlSlot, "InvertYToggle", false);
            RestyleToggle(invertYToggle);

            CreateSectionHeader(window.Content, "Accessibility", 22f);

            SettingsRowRefs subtitlesRow = CreateSettingsRow(window.Content, "Subtitles", withReadout: false, height: 52f);
            Toggle subtitlesToggle = CreateToggle(subtitlesRow.ControlSlot, "SubtitlesToggle", true);
            RestyleToggle(subtitlesToggle);

            Button resetButton = CreateTmpButton(window.Footer, "ResetButton", "Reset to Defaults", 17f);
            RectTransform resetRt = resetButton.GetComponent<RectTransform>();
            resetRt.anchorMin = new Vector2(0f, 0.5f);
            resetRt.anchorMax = new Vector2(0f, 0.5f);
            resetRt.pivot = new Vector2(0f, 0.5f);
            resetRt.anchoredPosition = new Vector2(28f, 0f);
            resetRt.sizeDelta = new Vector2(240f, 48f);

            Button doneButton = CreateTmpButton(window.Footer, "DoneButton", "Done", 19f, accent: true);
            RectTransform doneRt = doneButton.GetComponent<RectTransform>();
            doneRt.anchorMin = new Vector2(1f, 0.5f);
            doneRt.anchorMax = new Vector2(1f, 0.5f);
            doneRt.pivot = new Vector2(1f, 0.5f);
            doneRt.anchoredPosition = new Vector2(-28f, 0f);
            doneRt.sizeDelta = new Vector2(180f, 48f);

            // SettingsPanel lives on the WindowHost, not Root — same
            // constraint as MenuWindow (see CreateWindow's doc comment and
            // SettingsPanel.cs's own): Hide() fades on a coroutine before
            // root.SetActive(false), and a coroutine hosted on root itself
            // would die the instant that call runs. There is no MenuWindow
            // component here — SettingsPanel owns its own fade — but the
            // backdrop click still needs manual wiring since MenuWindow isn't
            // present to do it via CreateWindow's usual path.
            settingsPanel = window.Host.AddComponent<SettingsPanel>();
            SetField(settingsPanel, "root", window.Root);
            SetField(settingsPanel, "canvasGroup", window.CanvasGroup);
            SetField(settingsPanel, "micDeviceDropdown", micDropdown);
            SetField(settingsPanel, "masterVolumeSlider", masterSlider);
            SetField(settingsPanel, "voiceVolumeSlider", voiceSlider);
            SetField(settingsPanel, "sfxVolumeSlider", sfxSlider);
            SetField(settingsPanel, "mouseSensitivitySlider", sensitivitySlider);
            SetField(settingsPanel, "subtitlesToggle", subtitlesToggle);
            SetField(settingsPanel, "invertYToggle", invertYToggle);
            SetField(settingsPanel, "backButton", doneButton);
            SetField(settingsPanel, "resetButton", resetButton);
            SetField(settingsPanel, "backdropButton", window.BackdropButton);
            SetField(settingsPanel, "mic", mic);
            SetField(settingsPanel, "masterVolumeReadout", masterRow.Readout);
            SetField(settingsPanel, "voiceVolumeReadout", voiceRow.Readout);
            SetField(settingsPanel, "sfxVolumeReadout", sfxRow.Readout);
            SetField(settingsPanel, "mouseSensitivityReadout", sensitivityRow.Readout);

            window.Root.SetActive(false);
            return window.Root;
        }

        private static GameObject BuildDebugOverlayPanel(Transform hud, out DebugOverlayUI overlay)
        {
            GameObject root = new GameObject("DebugOverlayPanel", typeof(RectTransform));
            root.transform.SetParent(hud, false);
            AnchorTopLeft(root, new Vector2(20f, -20f), new Vector2(560f, 300f));
            Image backing = root.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.55f);

            Text bootStatus = CreateText(root.transform, "BootStatusText", string.Empty, 16);
            bootStatus.alignment = TextAnchor.UpperLeft;
            AnchorTopStretch(bootStatus.gameObject, -8f, 24f);

            Text state = CreateText(root.transform, "StateText", string.Empty, 16);
            state.alignment = TextAnchor.UpperLeft;
            AnchorTopStretch(state.gameObject, -32f, 24f);

            Text vadText = CreateText(root.transform, "VadText", string.Empty, 16);
            vadText.alignment = TextAnchor.UpperLeft;
            AnchorTopStretch(vadText.gameObject, -56f, 24f);

            Text lastTurn = CreateText(root.transform, "LastTurnText", string.Empty, 14);
            lastTurn.alignment = TextAnchor.UpperLeft;
            AnchorTopStretch(lastTurn.gameObject, -80f, 100f);

            Text marks = CreateText(root.transform, "MarksText", string.Empty, 14);
            marks.alignment = TextAnchor.UpperLeft;
            AnchorTopStretch(marks.gameObject, -184f, 100f);

            overlay = root.AddComponent<DebugOverlayUI>();
            SetField(overlay, "panel", root);
            SetField(overlay, "bootStatusText", bootStatus);
            SetField(overlay, "stateText", state);
            SetField(overlay, "vadText", vadText);
            SetField(overlay, "lastTurnText", lastTurn);
            SetField(overlay, "marksText", marks);
            root.SetActive(false);
            return root;
        }

        // ------------------------------------------------------------------
        // 2. MainMenu.unity
        // ------------------------------------------------------------------

        [MenuItem("Tools/False Positive/Bootstrap/2 - Build MainMenu Scene")]
        public static void BuildMainMenuScene()
        {
            Scene scene = OpenOrCreateEmptyScene(MainMenuScenePath);

            // OpenOrCreateEmptyScene only destroys root GameObjects on a
            // re-run — RenderSettings/LightmapSettings are scene-level state
            // that survives that, so they must be set explicitly every time
            // rather than assumed clean (risk 7 in the menu overhaul plan).
            SetRenderSettingsForMenu();
            int menuBackdropLayer = EnsureMenuBackdropLayer();

            // --- Backdrop: perspective camera + 3D cabin exterior, storm sky,
            // falling snow (docs/TRACK_A_EDITOR_SETUP.md §3). Everything below
            // is forced onto its own dedicated layer and the camera's culling
            // mask is exactly that layer — with additive scenes never
            // unloaded, this is what stops the menu camera from ever
            // rendering whatever Interrogation/Memory content happens to
            // still be active underneath it.
            GameObject backdropRoot = new GameObject("Backdrop");

            GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(MenuCameraDrift));
            cameraGo.transform.SetParent(backdropRoot.transform, false);
            cameraGo.tag = "MainCamera";
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = false;
            camera.fieldOfView = 38f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 220f;
            camera.allowHDR = true;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.cullingMask = 1 << menuBackdropLayer;
            camera.depth = -1f;
            // Starting estimate — puts the cabin silhouette in the right
            // two-thirds, leaving the left third for the title column. The
            // "log once, then hardcode" convention this repo already uses for
            // CabinV2Builder's door-hinge constants: EncapsulateRendererBounds
            // below logs the cabin's actual bounds on every run; read that
            // line out of the batch log and replace this literal if the
            // framing is off once seen in-Editor. Do not guess twice.
            cameraGo.transform.position = new Vector3(-11.2f, 2.35f, -12.6f);
            cameraGo.transform.rotation = Quaternion.Euler(4.5f, 41f, 0f);

            GameObject storm = new GameObject("StormAmbience", typeof(AudioSource));
            storm.transform.SetParent(backdropRoot.transform, false);
            AudioSource stormSource = storm.GetComponent<AudioSource>();
            stormSource.loop = true;
            stormSource.playOnAwake = true;
            stormSource.volume = 0.3f;
            stormSource.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                "Assets/_Project/Art/Audio/SFX/menu_storm_bed.mp3");
            if (stormSource.clip == null)
            {
                Debug.LogWarning("[ProjectBootstrapBuilder] menu_storm_bed.mp3 not found under " +
                    "Assets/_Project/Art/Audio/SFX/ — StormAmbience will have no clip until it is generated.");
            }

            // Verbatim from MemorySceneBuilderV2.cs's night branch — proven
            // values, not re-derived.
            GameObject moonlight = NewChild(backdropRoot.transform, "Cold Moonlight");
            Light moonLight = moonlight.AddComponent<Light>();
            moonLight.type = LightType.Directional;
            moonLight.shadows = LightShadows.Soft;
            moonlight.transform.rotation = Quaternion.Euler(38f, 328f, 0f);
            moonLight.color = new Color(0.48f, 0.6f, 1f);
            moonLight.intensity = 0.52f;

            GameObject windowGlow = NewChild(backdropRoot.transform, "WindowGlow");
            Light glowLight = windowGlow.AddComponent<Light>();
            glowLight.type = LightType.Point;
            glowLight.color = new Color(1f, 0.62f, 0.28f);
            glowLight.intensity = 2.4f;
            glowLight.range = 6f;
            glowLight.shadows = LightShadows.None;
            windowGlow.transform.position = new Vector3(0f, 1.8f, 0f); // overwritten below once the cabin's real bounds are known

            GameObject cabinPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(CabinPrefabPath);
            if (cabinPrefab != null)
            {
                GameObject cabin = (GameObject)PrefabUtility.InstantiatePrefab(cabinPrefab, backdropRoot.transform);
                cabin.name = "Cabin";
                cabin.transform.localPosition = Vector3.zero;

                // Pure waste in a menu that is never walked through.
                foreach (Collider collider in cabin.GetComponentsInChildren<Collider>(true))
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }

                Bounds bounds = EncapsulateRendererBounds(cabin);
                Debug.Log($"[ProjectBootstrapBuilder] Cabin bounds: center={bounds.center} size={bounds.size} " +
                    "— re-check the hardcoded camera transform above against this once seen in-Editor.");

                // WindowGlow has no authored placement in the source prefab,
                // so aim it at the cabin's silhouette from the camera's side
                // rather than a fixed offset that would break if the cabin's
                // geometry ever changes.
                Vector3 towardCamera = (cameraGo.transform.position - bounds.center).normalized;
                windowGlow.transform.position = bounds.center + Vector3.up * 0.4f + towardCamera * (bounds.extents.magnitude * 0.4f);
            }
            else
            {
                Debug.LogWarning("[ProjectBootstrapBuilder] Cabin_v2.prefab not found — menu backdrop will have no cabin.");
            }

            GameObject windZoneGo = NewChild(backdropRoot.transform, "WindZone");
            windZoneGo.transform.rotation = Quaternion.Euler(4f, 68f, 0f);
            WindZone windZone = windZoneGo.AddComponent<WindZone>();
            windZone.mode = WindZoneMode.Directional;
            windZone.windMain = 0.72f;
            windZone.windTurbulence = 0.5f;
            windZone.radius = 20f;

            // Base values from MemorySceneBuilderV2.cs, retuned for the
            // menu's tighter framing. prewarm matters — without it the first
            // frame of the menu is a snowless sky, which is the frame people
            // screenshot.
            GameObject snowGo = NewChild(backdropRoot.transform, "Snow");
            snowGo.transform.position = new Vector3(-9f, 10f, -11f);
            ParticleSystem snowPs = snowGo.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule snowMain = snowPs.main;
            snowMain.startLifetime = 8f;
            snowMain.startSpeed = 3.1f;
            snowMain.maxParticles = 900;
            snowMain.startSize = 0.035f;
            snowMain.startColor = new Color(1f, 1f, 1f, 0.85f);
            snowMain.simulationSpace = ParticleSystemSimulationSpace.World;
            snowMain.prewarm = true;
            ParticleSystem.EmissionModule snowEmission = snowPs.emission;
            snowEmission.rateOverTime = 90f;
            ParticleSystem.ShapeModule snowShape = snowPs.shape;
            snowShape.shapeType = ParticleSystemShapeType.Box;
            snowShape.scale = new Vector3(22f, 1f, 22f);
            ParticleSystem.ExternalForcesModule snowExternalForces = snowPs.externalForces;
            snowExternalForces.enabled = true; // couples to the WindZone above
            ParticleSystemRenderer snowRenderer = snowGo.GetComponent<ParticleSystemRenderer>();
            snowRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            snowRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            snowRenderer.sharedMaterial = LoadOrCreateSnowMaterial();

            // Missing this recursive pass is the #1 way this ships as a
            // black screen — the camera's culling mask only sees this layer.
            SetLayerRecursively(backdropRoot, menuBackdropLayer);

            // --- Canvas: vignette, title, menu (sortOrder 0) ---
            GameObject canvasGo = CreateCanvasRoot("Canvas", 0); // matchWidthOrHeight is set inside CreateCanvasRoot

            CreateVignette(canvasGo.transform);

            TextMeshProUGUI caseStamp = CreateTmpText(canvasGo.transform, "CaseStamp", "CASE FILE 88-0413 / RESTRICTED",
                14f, MenuPalette.TextMuted, TextAlignmentOptions.TopLeft, tracking: 12f);
            AnchorTopLeft(caseStamp.gameObject, new Vector2(48f, -48f), new Vector2(420f, 60f));

            GameObject titleBlock = new GameObject("TitleBlock", typeof(RectTransform), typeof(VerticalLayoutGroup));
            titleBlock.transform.SetParent(canvasGo.transform, false);
            AnchorTopStretch(titleBlock, -140f, 220f);
            VerticalLayoutGroup titleLayout = titleBlock.GetComponent<VerticalLayoutGroup>();
            titleLayout.spacing = 16f;
            titleLayout.childAlignment = TextAnchor.UpperCenter;
            titleLayout.childControlWidth = true;
            titleLayout.childControlHeight = true;
            titleLayout.childForceExpandWidth = false;
            titleLayout.childForceExpandHeight = false;

            TextMeshProUGUI title = CreateTmpText(titleBlock.transform, "Title", "FALSE POSITIVE", 92f,
                MenuPalette.TextPrimary, TextAlignmentOptions.Center, tracking: 18f, style: FontStyles.Bold);
            SetLayoutHeight(title.gameObject, 110f);

            GameObject titleRuleGo = new GameObject("TitleRule", typeof(RectTransform), typeof(Image));
            titleRuleGo.transform.SetParent(titleBlock.transform, false);
            Image titleRule = titleRuleGo.GetComponent<Image>();
            titleRule.sprite = null;
            titleRule.color = MenuPalette.Rule;
            titleRule.raycastTarget = false;
            LayoutElement titleRuleLe = titleRuleGo.AddComponent<LayoutElement>();
            titleRuleLe.preferredWidth = 520f;
            SetLayoutHeight(titleRuleGo, 2f);

            TextMeshProUGUI subtitle = CreateTmpText(titleBlock.transform, "Subtitle", "AN INTERROGATION", 20f,
                MenuPalette.TextSecondary, TextAlignmentOptions.Center, tracking: 34f);
            SetLayoutHeight(subtitle.gameObject, 30f);

            GameObject panel = new GameObject("MenuPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvasGo.transform, false);
            AnchorCenter(panel, new Vector2(0f, -90f), new Vector2(460f, 540f));
            VerticalLayoutGroup panelLayout = panel.GetComponent<VerticalLayoutGroup>();
            panelLayout.spacing = 10f;
            panelLayout.childAlignment = TextAnchor.UpperCenter;
            panelLayout.childControlWidth = true;
            panelLayout.childControlHeight = true;
            panelLayout.childForceExpandWidth = true;
            panelLayout.childForceExpandHeight = false;

            Button playButton = CreateTmpButton(panel.transform, "PlayButton", "Begin", 22f, accent: true);
            SetLayoutHeight(playButton.gameObject, 68f);

            Button offlineButton = CreateTmpButton(panel.transform, "OfflineButton", "Offline Demo", 22f);
            SetLayoutHeight(offlineButton.gameObject, 68f);

            TextMeshProUGUI offlineCaption = CreateTmpText(panel.transform, "OfflineCaption",
                "Plays the full story with a scripted interrogation — no voice service required.", 15f,
                MenuPalette.TextMuted, TextAlignmentOptions.Top);
            SetLayoutHeight(offlineCaption.gameObject, 44f);

            CreateSpacer(panel.transform, 18f);

            Button controlsButton = CreateTmpButton(panel.transform, "ControlsButton", "How to Play", 19f);
            SetLayoutHeight(controlsButton.gameObject, 56f);

            Button settingsButton = CreateTmpButton(panel.transform, "SettingsButton", "Settings", 19f);
            SetLayoutHeight(settingsButton.gameObject, 56f);

            Button creditsButton = CreateTmpButton(panel.transform, "CreditsButton", "Credits", 19f);
            SetLayoutHeight(creditsButton.gameObject, 56f);

            CreateSpacer(panel.transform, 18f);

            Button quitButton = CreateTmpButton(panel.transform, "QuitButton", "Quit", 19f);
            SetLayoutHeight(quitButton.gameObject, 56f);

            TextMeshProUGUI buildStamp = CreateTmpText(canvasGo.transform, "BuildStamp", Application.version, 13f,
                MenuPalette.TextMuted, TextAlignmentOptions.BottomRight, tracking: 4f);
            AnchorBottomStretch(buildStamp.gameObject, 28f, 24f);
            RectTransform buildStampRt = buildStamp.rectTransform;
            buildStampRt.offsetMin = new Vector2(buildStampRt.offsetMin.x + 28f, buildStampRt.offsetMin.y);
            buildStampRt.offsetMax = new Vector2(buildStampRt.offsetMax.x - 28f, buildStampRt.offsetMax.y);

            // --- MenuOverlay: Quit/Credits/Controls windows (sortOrder 50) ---
            // Scene-local, not _Persistent — only reachable from this menu, so
            // plain [SerializeField] wiring is fine (see MainMenuController's
            // doc comment on why SettingsPanel, which IS reused in _Persistent
            // for A15, cannot be wired the same way).
            GameObject overlayCanvasGo = CreateCanvasRoot("MenuOverlay", 50); // matchWidthOrHeight is set inside CreateCanvasRoot

            QuitConfirmWindow quitConfirm = BuildQuitConfirmWindow(overlayCanvasGo.transform);
            MenuWindow creditsWindow = BuildCreditsWindow(overlayCanvasGo.transform);
            MenuWindow controlsWindow = BuildControlsWindow(overlayCanvasGo.transform);

            MainMenuController controller = canvasGo.AddComponent<MainMenuController>();
            SetField(controller, "playButton", playButton);
            SetField(controller, "offlineButton", offlineButton);
            SetField(controller, "settingsButton", settingsButton);
            SetField(controller, "quitButton", quitButton);
            SetField(controller, "controlsButton", controlsButton);
            SetField(controller, "creditsButton", creditsButton);
            SetField(controller, "quitConfirm", quitConfirm);
            SetField(controller, "creditsWindow", creditsWindow);
            SetField(controller, "controlsWindow", controlsWindow);

            // No EventSystem here by design — _Persistent's is never unloaded
            // and already covers every additively-loaded scene's canvases.
            // MainMenuController.Awake also carries a runtime safety net that
            // creates one if none exists, for the case where this scene is
            // played on its own (PersistentSceneBootstrap normally prevents
            // that from being necessary, but it costs nothing as a backstop).

            SaveScene(scene, MainMenuScenePath);
            Debug.Log("[ProjectBootstrapBuilder] MainMenu.unity rebuilt.");
        }

        /// <summary>Idempotent TagManager.asset edit claiming a layer slot for
        /// the menu backdrop — scans 8-31 by name first so re-running this
        /// never claims a second slot, and only ever writes an *empty* slot
        /// (risk 5 in the menu overhaul plan: never overwrite an existing
        /// layer some other system already claimed).</summary>
        private static int EnsureMenuBackdropLayer()
        {
            int existing = LayerMask.NameToLayer(MenuBackdropLayerName);
            if (existing >= 0) return existing;

            SerializedObject tagManager = new SerializedObject(
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layers = tagManager.FindProperty("layers");

            for (int i = 8; i <= 31; i++)
            {
                SerializedProperty layerProp = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(layerProp.stringValue))
                {
                    layerProp.stringValue = MenuBackdropLayerName;
                    tagManager.ApplyModifiedProperties();
                    Debug.Log($"[ProjectBootstrapBuilder] Claimed layer {i} as \"{MenuBackdropLayerName}\".");
                    return i;
                }
            }

            throw new InvalidOperationException(
                $"[ProjectBootstrapBuilder] No free layer slot (8-31) for \"{MenuBackdropLayerName}\".");
        }

        /// <summary>OpenOrCreateEmptyScene only destroys root GameObjects on a
        /// re-run, not RenderSettings — so every rebuild must set these
        /// explicitly. Values lifted verbatim from Memory_CabinNight.unity's
        /// shipped night look.</summary>
        private static void SetRenderSettingsForMenu()
        {
            Material skybox = AssetDatabase.LoadAssetAtPath<Material>(StormSkyMaterialPath);
            if (skybox != null) RenderSettings.skybox = skybox;
            else Debug.LogWarning("[ProjectBootstrapBuilder] StormSky.mat not found — menu skybox will be the default.");

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.075f, 0.09f, 0.15f);
            RenderSettings.ambientEquatorColor = new Color(0.035f, 0.03f, 0.04f);
            RenderSettings.ambientGroundColor = new Color(0.012f, 0.012f, 0.017f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = new Color(0.055f, 0.075f, 0.12f);
            RenderSettings.fogDensity = 0.011f;

            RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;

            // Never let batch mode stall waiting on GI for a menu backdrop.
            Lightmapping.giWorkflowMode = Lightmapping.GIWorkflowMode.OnDemand;
        }

        private static Bounds EncapsulateRendererBounds(GameObject root)
        {
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static void SetLayerRecursively(GameObject root, int layer)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.layer = layer;
            }
        }

        /// <summary>Mirrors LoadOrCreateFireMaterial() in MemorySceneBuilderV2.cs
        /// with one deliberate difference: alpha blend instead of additive,
        /// since falling snow needs to occlude like a real particle rather than
        /// glow like fire. SnowParticle.mat is Opaque (_Surface:0, _ZWrite:1)
        /// and would render Snowflake.png as solid white squares — this writes
        /// a new asset rather than mutating that one, which Memory_CabinNight.unity
        /// still references by guid.</summary>
        private static Material LoadOrCreateSnowMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SnowMaterialPath);
            if (material != null) return material;

            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                ?? Shader.Find("Particles/Standard Unlit");
            material = new Material(shader) { name = "SnowParticle_Transparent" };

            Texture2D snowflake = AssetDatabase.LoadAssetAtPath<Texture2D>(SnowflakeTexturePath);
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", snowflake);
            else if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", snowflake);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", Color.white);

            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f); // URP BlendMode.Alpha
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            Directory.CreateDirectory(Path.GetDirectoryName(SnowMaterialPath));
            AssetDatabase.CreateAsset(material, SnowMaterialPath);
            return material;
        }

        /// <summary>The only "art" in the menu — ~44 flat Images that share one
        /// material (no sprite = no atlas lookup, so they batch into one draw
        /// call). GlobalScrim sits the 3D scene back; edge bands frame it;
        /// ColorGrade unifies it with Memory_CabinNight's fog colour. All
        /// raycastTarget=false — a vignette band must never steal a click.</summary>
        private static void CreateVignette(Transform canvas)
        {
            Image globalScrim = CreateFlatImage(canvas, "GlobalScrim", new Color(MenuPalette.Ink.r, MenuPalette.Ink.g, MenuPalette.Ink.b, 0.28f));
            StretchFill(globalScrim.gameObject);
            globalScrim.transform.SetSiblingIndex(0);

            GameObject bottom = new GameObject("VignetteBottom", typeof(RectTransform));
            bottom.transform.SetParent(canvas, false);
            StretchFill(bottom);
            for (int i = 0; i < 18; i++)
            {
                float t = i / 17f;
                float alpha = Mathf.Lerp(0.92f, 0f, t * t);
                Image band = CreateFlatImage(bottom.transform, "Band" + i, new Color(MenuPalette.Ink.r, MenuPalette.Ink.g, MenuPalette.Ink.b, alpha));
                AnchorBottomStretch(band.gameObject, i * 22f, 22f);
            }

            GameObject top = new GameObject("VignetteTop", typeof(RectTransform));
            top.transform.SetParent(canvas, false);
            StretchFill(top);
            for (int i = 0; i < 12; i++)
            {
                float t = i / 11f;
                float alpha = Mathf.Lerp(0.85f, 0f, t * t);
                Image band = CreateFlatImage(top.transform, "Band" + i, new Color(MenuPalette.Ink.r, MenuPalette.Ink.g, MenuPalette.Ink.b, alpha));
                AnchorTopStretch(band.gameObject, -(i * 20f), 20f);
            }

            GameObject left = new GameObject("VignetteLeft", typeof(RectTransform));
            left.transform.SetParent(canvas, false);
            StretchFill(left);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float alpha = Mathf.Lerp(0.55f, 0f, t * t);
                Image band = CreateFlatImage(left.transform, "Band" + i, new Color(MenuPalette.Ink.r, MenuPalette.Ink.g, MenuPalette.Ink.b, alpha));
                RectTransform rt = band.rectTransform;
                rt.anchorMin = new Vector2(0f, 0f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(40f, 0f);
                rt.anchoredPosition = new Vector2(i * 40f, 0f); // was "+ 20f" — pivot is already on the screen edge, the +20 left a 20px unshaded strip
            }

            GameObject right = new GameObject("VignetteRight", typeof(RectTransform));
            right.transform.SetParent(canvas, false);
            StretchFill(right);
            for (int i = 0; i < 7; i++)
            {
                float t = i / 6f;
                float alpha = Mathf.Lerp(0.55f, 0f, t * t);
                Image band = CreateFlatImage(right.transform, "Band" + i, new Color(MenuPalette.Ink.r, MenuPalette.Ink.g, MenuPalette.Ink.b, alpha));
                RectTransform rt = band.rectTransform;
                rt.anchorMin = new Vector2(1f, 0f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(40f, 0f);
                rt.anchoredPosition = new Vector2(-(i * 40f), 0f); // was "+ 20f"
            }

            Image colorGrade = CreateFlatImage(canvas, "ColorGrade", new Color(0.055f, 0.075f, 0.12f, 0.16f));
            StretchFill(colorGrade.gameObject);
        }

        private static void CreateSpacer(Transform parent, float height)
        {
            GameObject spacer = new GameObject("Spacer", typeof(RectTransform));
            spacer.transform.SetParent(parent, false);
            SetLayoutHeight(spacer, height);
        }

        private static void CreateSectionHeader(Transform parent, string label, float height = 28f)
        {
            TextMeshProUGUI header = CreateTmpText(parent, label.Replace(" ", string.Empty) + "Header", label, 17f,
                MenuPalette.TextSecondary, TextAlignmentOptions.BottomLeft, tracking: 10f,
                style: FontStyles.Bold | FontStyles.UpperCase);
            SetLayoutHeight(header.gameObject, height);
        }

        /// <summary>One row of WASD/Mouse/Shift/E/Esc-style keycaps followed by
        /// a left-aligned action label — the How-to-play window's only custom
        /// composition, built from CreateKeyCap + CreateTmpText rather than a
        /// generic helper since nothing else in the menu needs this shape.</summary>
        private static void CreateControlRow(Transform parent, string actionLabel, params string[] keys)
        {
            GameObject row = new GameObject(actionLabel.Replace(" ", string.Empty).Replace("(", string.Empty).Replace(")", string.Empty) + "Row",
                typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(parent, false);
            SetLayoutHeight(row, 44f);
            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childAlignment = TextAnchor.MiddleLeft;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childForceExpandHeight = true;

            foreach (string key in keys)
            {
                CreateKeyCap(row.transform, key, key.Length > 1 ? 88f : 64f);
            }

            TextMeshProUGUI label = CreateTmpText(row.transform, "Label", actionLabel, 17f,
                MenuPalette.TextSecondary, TextAlignmentOptions.MidlineLeft);
            LayoutElement labelLe = label.gameObject.AddComponent<LayoutElement>();
            labelLe.flexibleWidth = 1f;
            labelLe.minWidth = 160f;
        }

        private static QuitConfirmWindow BuildQuitConfirmWindow(Transform overlayCanvas)
        {
            WindowRefs window = CreateWindow(overlayCanvas, "QuitConfirmWindow", "Quit to Desktop", new Vector2(640f, 300f));

            TextMeshProUGUI body = CreateTmpText(window.Content, "Body",
                "Your progress in this interrogation will not be saved.\nQuit to desktop?",
                18f, MenuPalette.TextSecondary, TextAlignmentOptions.Center, lineSpacing: 10f);
            StretchFill(body.gameObject);

            Button cancelButton = CreateTmpButton(window.Footer, "CancelButton", "Cancel", 19f);
            RectTransform cancelRt = cancelButton.GetComponent<RectTransform>();
            cancelRt.anchorMin = new Vector2(1f, 0.5f);
            cancelRt.anchorMax = new Vector2(1f, 0.5f);
            cancelRt.pivot = new Vector2(1f, 0.5f);
            cancelRt.anchoredPosition = new Vector2(-28f - 180f - 12f, 0f);
            cancelRt.sizeDelta = new Vector2(180f, 48f);

            Button confirmButton = CreateTmpButton(window.Footer, "ConfirmButton", "Quit Game", 19f, accent: true);
            RectTransform confirmRt = confirmButton.GetComponent<RectTransform>();
            confirmRt.anchorMin = new Vector2(1f, 0.5f);
            confirmRt.anchorMax = new Vector2(1f, 0.5f);
            confirmRt.pivot = new Vector2(1f, 0.5f);
            confirmRt.anchoredPosition = new Vector2(-28f, 0f);
            confirmRt.sizeDelta = new Vector2(180f, 48f);

            MenuWindow menuWindow = window.Host.AddComponent<MenuWindow>();
            SetField(menuWindow, "root", window.Root);
            SetField(menuWindow, "canvasGroup", window.CanvasGroup);
            SetField(menuWindow, "backdropButton", window.BackdropButton);
            SetField(menuWindow, "closeButton", null);
            SetField(menuWindow, "defaultSelection", cancelButton); // never default to the destructive action
            SetField(menuWindow, "closeOnBackdropClick", true);

            QuitConfirmWindow quitConfirm = window.Host.AddComponent<QuitConfirmWindow>();
            SetField(quitConfirm, "window", menuWindow);
            SetField(quitConfirm, "confirmButton", confirmButton);
            SetField(quitConfirm, "cancelButton", cancelButton);

            window.Root.SetActive(false);
            return quitConfirm;
        }

        /// <summary>Copy sourced from docs/PRIVACY.md and docs/CONCEPT.md at
        /// implementation time rather than paraphrased. IEMOCAP's licence is
        /// still an open item across every doc in this repo (see
        /// docs/ROADMAP.md's checklist) — this deliberately states the model's
        /// lineage without asserting a licence status nothing in the repo has
        /// actually confirmed yet.</summary>
        private static MenuWindow BuildCreditsWindow(Transform overlayCanvas)
        {
            WindowRefs window = CreateWindow(overlayCanvas, "CreditsWindow", "Credits", new Vector2(940f, 720f));

            GameObject scrollGo = DefaultControls.CreateScrollView(UIResources);
            scrollGo.name = "CreditsScroll";
            scrollGo.transform.SetParent(window.Content, false);
            StretchFill(scrollGo);

            ScrollRect scrollRect = scrollGo.GetComponent<ScrollRect>();
            scrollRect.horizontal = false;

            Transform horizontalScrollbar = scrollGo.transform.Find("Scrollbar Horizontal");
            if (horizontalScrollbar != null) UnityEngine.Object.DestroyImmediate(horizontalScrollbar.gameObject);

            Transform viewport = scrollGo.transform.Find("Viewport");
            Mask viewportMask = viewport.GetComponent<Mask>();
            if (viewportMask != null) UnityEngine.Object.DestroyImmediate(viewportMask);
            Image viewportImage = viewport.GetComponent<Image>();
            if (viewportImage != null) UnityEngine.Object.DestroyImmediate(viewportImage);
            viewport.gameObject.AddComponent<RectMask2D>();

            Transform verticalScrollbar = scrollGo.transform.Find("Scrollbar Vertical");
            RectTransform vsRt = verticalScrollbar.GetComponent<RectTransform>();
            vsRt.sizeDelta = new Vector2(4f, vsRt.sizeDelta.y);
            Image track = verticalScrollbar.GetComponent<Image>();
            if (track != null) { track.sprite = null; track.color = MenuPalette.RuleDim; }
            Transform handle = verticalScrollbar.Find("Sliding Area/Handle");
            if (handle != null)
            {
                Image handleImage = handle.GetComponent<Image>();
                if (handleImage != null) { handleImage.sprite = null; handleImage.color = MenuPalette.TextMuted; }
            }

            Transform content = scrollGo.transform.Find("Viewport/Content");
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = Vector2.zero;

            VerticalLayoutGroup contentLayout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 20f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;
            contentLayout.padding = new RectOffset(4, 20, 4, 24);
            ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            TextMeshProUGUI tagline = CreateTmpText(content, "Tagline",
                "\"The detective can hear your fear. It cannot tell why you are afraid.\"", 18f,
                MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft, style: FontStyles.Italic, lineSpacing: 8f);

            CreateSectionHeader(content, "Built With");
            AddCreditsLine(content, "Unity", "Game engine, Universal Render Pipeline.");
            AddCreditsLine(content, "Google Gemini 3.6 Flash", "Writes the detective's dialogue.");
            AddCreditsLine(content, "Google Cloud Speech-to-Text", "Transcribes what you say.");
            AddCreditsLine(content, "ElevenLabs", "The detective's voice.");
            AddCreditsLine(content, "HuBERT (trained in part on IEMOCAP)", "Reads tone: hesitation, tension, pace.");

            CreateSectionHeader(content, "Your Voice");
            TextMeshProUGUI voiceBody = CreateTmpText(content, "VoiceBody",
                "The game is played by voice, so the microphone stays open for the whole interrogation. " +
                "Each utterance you speak is sent over an encrypted connection to be transcribed and read " +
                "for tone, then thrown away — never written to disk. Nothing you say is used to train any " +
                "model, and no name, email, or account is ever collected.",
                16f, MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft, lineSpacing: 8f);

            CreateSectionHeader(content, "What It Cannot Do");
            TextMeshProUGUI limitsBody = CreateTmpText(content, "LimitsBody",
                "It cannot detect lies. Neither can anything else. The detective only measures affect — " +
                "how your voice moves — and has no way of knowing why. Fear sounds like guilt. It also " +
                "sounds like a faulty memory.",
                16f, MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft, lineSpacing: 8f);

            Button backButton = CreateTmpButton(window.Footer, "BackButton", "Back", 19f);
            RectTransform backRt = backButton.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1f, 0.5f);
            backRt.anchorMax = new Vector2(1f, 0.5f);
            backRt.pivot = new Vector2(1f, 0.5f);
            backRt.anchoredPosition = new Vector2(-28f, 0f);
            backRt.sizeDelta = new Vector2(180f, 48f);

            MenuWindow menuWindow = window.Host.AddComponent<MenuWindow>();
            SetField(menuWindow, "root", window.Root);
            SetField(menuWindow, "canvasGroup", window.CanvasGroup);
            SetField(menuWindow, "backdropButton", window.BackdropButton);
            SetField(menuWindow, "closeButton", backButton);
            SetField(menuWindow, "defaultSelection", backButton);
            SetField(menuWindow, "closeOnBackdropClick", true);

            window.Root.SetActive(false);
            return menuWindow;
        }

        private static void AddCreditsLine(Transform parent, string name, string description)
        {
            GameObject row = new GameObject(name.Replace(" ", string.Empty), typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetLayoutHeight(row, 44f);

            TextMeshProUGUI nameText = CreateTmpText(row.transform, "Name", name, 17f, MenuPalette.TextPrimary,
                TextAlignmentOptions.TopLeft, style: FontStyles.Bold);
            RectTransform nameRt = nameText.rectTransform;
            nameRt.anchorMin = new Vector2(0f, 1f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0f, 1f);
            nameRt.anchoredPosition = Vector2.zero;
            nameRt.sizeDelta = new Vector2(0f, 22f);

            TextMeshProUGUI descText = CreateTmpText(row.transform, "Description", description, 15f, MenuPalette.TextMuted,
                TextAlignmentOptions.TopLeft);
            RectTransform descRt = descText.rectTransform;
            descRt.anchorMin = new Vector2(0f, 1f);
            descRt.anchorMax = new Vector2(1f, 1f);
            descRt.pivot = new Vector2(0f, 1f);
            descRt.anchoredPosition = new Vector2(0f, -22f);
            descRt.sizeDelta = new Vector2(0f, 20f);
        }

        private static MenuWindow BuildControlsWindow(Transform overlayCanvas)
        {
            WindowRefs window = CreateWindow(overlayCanvas, "ControlsWindow", "How to Play", new Vector2(1040f, 820f));

            VerticalLayoutGroup contentLayout = window.Content.gameObject.AddComponent<VerticalLayoutGroup>();
            contentLayout.spacing = 16f;
            contentLayout.childAlignment = TextAnchor.UpperLeft;
            contentLayout.childControlWidth = true;
            contentLayout.childControlHeight = true;
            contentLayout.childForceExpandWidth = true;
            contentLayout.childForceExpandHeight = false;

            CreateSectionHeader(window.Content, "Movement");
            CreateControlRow(window.Content, "Move", "W", "A", "S", "D");
            CreateControlRow(window.Content, "Look", "Mouse");
            CreateControlRow(window.Content, "Sprint (hold)", "Shift");

            CreateSectionHeader(window.Content, "Interaction");
            CreateControlRow(window.Content, "Interact", "E");

            CreateSectionHeader(window.Content, "Voice");
            TextMeshProUGUI voiceBody = CreateTmpText(window.Content, "VoiceBody",
                "There is no push-to-talk. Your microphone stays open during an interrogation — speak " +
                "normally, in full sentences, and pause when you're done. The detective is listening for " +
                "both what you say and how you say it.",
                17f, MenuPalette.TextSecondary, TextAlignmentOptions.TopLeft, lineSpacing: 10f);

            CreateSectionHeader(window.Content, "Menus");
            CreateControlRow(window.Content, "Close / Back", "Esc");

            Button backButton = CreateTmpButton(window.Footer, "BackButton", "Back", 19f);
            RectTransform backRt = backButton.GetComponent<RectTransform>();
            backRt.anchorMin = new Vector2(1f, 0.5f);
            backRt.anchorMax = new Vector2(1f, 0.5f);
            backRt.pivot = new Vector2(1f, 0.5f);
            backRt.anchoredPosition = new Vector2(-28f, 0f);
            backRt.sizeDelta = new Vector2(180f, 48f);

            MenuWindow menuWindow = window.Host.AddComponent<MenuWindow>();
            SetField(menuWindow, "root", window.Root);
            SetField(menuWindow, "canvasGroup", window.CanvasGroup);
            SetField(menuWindow, "backdropButton", window.BackdropButton);
            SetField(menuWindow, "closeButton", backButton);
            SetField(menuWindow, "defaultSelection", backButton);
            SetField(menuWindow, "closeOnBackdropClick", true);

            window.Root.SetActive(false);
            return menuWindow;
        }

        // ------------------------------------------------------------------
        // 3. Config ScriptableObjects
        // ------------------------------------------------------------------

        [MenuItem("Tools/False Positive/Bootstrap/3 - Build Config Assets")]
        public static void BuildConfigAssets()
        {
            Directory.CreateDirectory(ConfigRoot.TrimEnd('/'));

            MemoryFlagCatalog catalog = AssetDatabase.LoadAssetAtPath<MemoryFlagCatalog>(ConfigRoot + "MemoryFlagCatalog.asset");
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<MemoryFlagCatalog>();
                AssetDatabase.CreateAsset(catalog, ConfigRoot + "MemoryFlagCatalog.asset");
            }
            TextAsset flagsSource = AssetDatabase.LoadAssetAtPath<TextAsset>(PromptsRoot + "memory_flags.txt");
            SetField(catalog, "source", flagsSource);
            EditorUtility.SetDirty(catalog);

            PhasePromptSet prompts = AssetDatabase.LoadAssetAtPath<PhasePromptSet>(ConfigRoot + "PhasePromptSet.asset");
            if (prompts == null)
            {
                prompts = ScriptableObject.CreateInstance<PhasePromptSet>();
                AssetDatabase.CreateAsset(prompts, ConfigRoot + "PhasePromptSet.asset");
            }
            SetField(prompts, "caseFile", AssetDatabase.LoadAssetAtPath<TextAsset>(PromptsRoot + "case_file.txt"));
            SetField(prompts, "p2Recall", AssetDatabase.LoadAssetAtPath<TextAsset>(PromptsRoot + "phase_p2_recall.txt"));
            SetField(prompts, "p3Verdict", AssetDatabase.LoadAssetAtPath<TextAsset>(PromptsRoot + "phase_p3_verdict.txt"));
            EditorUtility.SetDirty(prompts);

            AssetDatabase.SaveAssets();
            Debug.Log("[ProjectBootstrapBuilder] MemoryFlagCatalog + PhasePromptSet built.");
        }

        // ------------------------------------------------------------------
        // 4. Interrogation.unity fixups
        // ------------------------------------------------------------------

        [MenuItem("Tools/False Positive/Bootstrap/4 - Fix Interrogation Scene")]
        public static void FixInterrogationScene()
        {
            Scene scene = EditorSceneManager.OpenScene(InterrogationScenePath, OpenSceneMode.Single);

            // Remove objects/components that moved to _Persistent in the A0 split.
            DestroyIfFound("MicMeterFill");
            DestroyIfFound("MicMeterBackground");
            DestroyIfFound("MicMeterRoot");
            DestroyIfFound("ScreenFader");

            GameObject voiceSystem = GameObject.Find("VoiceSystem");
            if (voiceSystem != null) UnityEngine.Object.DestroyImmediate(voiceSystem);

            GameObject gameSystems = GameObject.Find("GameSystems");
            if (gameSystems != null)
            {
                RemoveComponentIfPresent<InterrogationSidecarClient>(gameSystems);
                RemoveComponentIfPresent<SidecarProcessLauncher>(gameSystems);
                // GameBootstrap.cs was deleted; the leftover "Missing Script"
                // component logs a warning every time this object is touched
                // in play mode, so strip it explicitly rather than leaving it.
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(gameSystems);
            }

            GameObject dialogueManagerGo = GameObject.Find("DialogueManager");
            GameObject playerGo = GameObject.Find("Player");
            if (dialogueManagerGo == null || playerGo == null)
            {
                throw new InvalidOperationException(
                    "[ProjectBootstrapBuilder] Interrogation.unity is missing DialogueManager or Player — " +
                    "cannot wire InterrogationSceneBinder.");
            }

            DialogueManager dialogueManager = dialogueManagerGo.GetComponent<DialogueManager>();
            PlayerStateController playerState = playerGo.GetComponent<PlayerStateController>();

            GameObject binderGo = GameObject.Find("SceneBinder") ?? new GameObject("SceneBinder");
            InterrogationSceneBinder binder = binderGo.GetComponent<InterrogationSceneBinder>() ?? binderGo.AddComponent<InterrogationSceneBinder>();
            SetField(binder, "dialogueManager", dialogueManager);
            SetField(binder, "playerState", playerState);

            PhasePromptSet prompts = AssetDatabase.LoadAssetAtPath<PhasePromptSet>(ConfigRoot + "PhasePromptSet.asset");
            TextAsset storyMarks = AssetDatabase.LoadAssetAtPath<TextAsset>(PromptsRoot + "story_marks.txt");
            OfflineDialogueScript offlineScript = AssetDatabase.LoadAssetAtPath<OfflineDialogueScript>(ConfigRoot + "OfflineDialogueScript.asset");
            if (offlineScript == null)
            {
                Debug.LogWarning("[ProjectBootstrapBuilder] OfflineDialogueScript.asset not found — run " +
                    "Bootstrap step 10, then re-run this step, or Offline demo mode will have no lines.");
            }

            GameObject phaseGo = GameObject.Find("PhaseDialogueController") ?? new GameObject("PhaseDialogueController");
            PhaseDialogueController phaseController = phaseGo.GetComponent<PhaseDialogueController>() ?? phaseGo.AddComponent<PhaseDialogueController>();
            SetField(phaseController, "prompts", prompts);
            SetField(phaseController, "storyMarksSource", storyMarks);
            SetField(phaseController, "binder", binder);
            SetField(phaseController, "offlineScript", offlineScript);

            WireCopModel();
            WireAnimationDirector();

            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, InterrogationScenePath);
            Debug.Log("[ProjectBootstrapBuilder] Interrogation.unity fixed up.");
        }

        /// <summary>Verification helper — logs the cross-scene uLipSync
        /// proxy wiring (CutsceneDirector.VoSourceLipSync, CutsceneVoSource's
        /// uLipSyncAudioSource, CutsceneAnimationDirector.lipSync). Not part
        /// of the bootstrap pipeline; same rationale as
        /// LogCopModelWiringDiagnostics.</summary>
        public static void LogCutsceneLipSyncWiringDiagnostics()
        {
            Scene persistent = EditorSceneManager.OpenScene(PersistentScenePath, OpenSceneMode.Single);
            GameObject cutsceneGo = GameObject.Find("CutsceneDirector");
            CutsceneDirector director = cutsceneGo.GetComponent<CutsceneDirector>();
            Debug.Log($"[Diag] CutsceneDirector.VoSourceLipSync: {(director.VoSourceLipSync == null ? "NULL" : director.VoSourceLipSync.gameObject.name)}");

            GameObject voSourceGo = GameObject.Find("CutsceneVoSource");
            ULS.uLipSyncAudioSource proxy = voSourceGo.GetComponent<ULS.uLipSyncAudioSource>();
            Debug.Log($"[Diag] CutsceneVoSource has uLipSyncAudioSource: {proxy != null}");

            Scene interrogation = EditorSceneManager.OpenScene(InterrogationScenePath, OpenSceneMode.Single);
            GameObject animGo = GameObject.Find("AnimationDirector");
            CutsceneAnimationDirector animDir = animGo.GetComponent<CutsceneAnimationDirector>();
            SerializedObject so = new SerializedObject(animDir);
            SerializedProperty lipSyncProp = so.FindProperty("lipSync");
            Debug.Log($"[Diag] CutsceneAnimationDirector.lipSync: {(lipSyncProp.objectReferenceValue == null ? "NULL" : lipSyncProp.objectReferenceValue.name)}");
            SerializedProperty jawProp = so.FindProperty("jawMouth");
            Debug.Log($"[Diag] stale jawMouth field still present: {jawProp != null}");
        }

        /// <summary>Verification helper — logs the cop model wiring state.
        /// Not part of the bootstrap pipeline; exists so RunCommand-driven
        /// checks don't need a direct uLipSync assembly reference of their
        /// own (the ephemeral script-compile context used by that tool
        /// doesn't have one).</summary>
        public static void LogCopModelWiringDiagnostics()
        {
            GameObject cop = GameObject.Find("Cop");
            if (cop == null) { Debug.LogWarning("[Diag] No Cop GameObject."); return; }

            for (int i = 0; i < cop.transform.childCount; i++)
            {
                Debug.Log($"[Diag] Cop child[{i}] = {cop.transform.GetChild(i).name}");
            }

            Animator animator = cop.GetComponentInChildren<Animator>();
            Debug.Log($"[Diag] Animator: isHuman={(animator.avatar != null && animator.avatar.isHuman)}, " +
                $"applyRootMotion={animator.applyRootMotion}, " +
                $"controller={(animator.runtimeAnimatorController == null ? "null" : animator.runtimeAnimatorController.name)}");

            CopIdleAnimator idle = cop.GetComponent<CopIdleAnimator>();
            Debug.Log($"[Diag] CopIdleAnimator present: {idle != null}");

            CopTalkGestureAnimator gesture = cop.GetComponent<CopTalkGestureAnimator>();
            Debug.Log($"[Diag] CopTalkGestureAnimator present: {gesture != null}");
            if (gesture != null)
            {
                SerializedObject gso = new SerializedObject(gesture);
                foreach (string f in new[] { "leftArm", "leftForeArm", "leftHand", "rightArm", "rightForeArm", "rightHand", "spine1", "lipSync" })
                {
                    SerializedProperty p = gso.FindProperty(f);
                    Debug.Log($"[Diag]   gesture.{f} = {(p.objectReferenceValue == null ? "NULL" : p.objectReferenceValue.name)}");
                }
                if (idle != null)
                {
                    MonoScript idleScript = MonoScript.FromMonoBehaviour(idle);
                    MonoScript gestureScript = MonoScript.FromMonoBehaviour(gesture);
                    Debug.Log($"[Diag] Execution order: CopIdleAnimator={MonoImporter.GetExecutionOrder(idleScript)} " +
                        $"CopTalkGestureAnimator={MonoImporter.GetExecutionOrder(gestureScript)}");
                }
            }

            GameObject animGoCheck = GameObject.Find("AnimationDirector");
            if (animGoCheck != null)
            {
                Debug.Log("[Diag] AnimationDirector components: " +
                    string.Join(", ", animGoCheck.GetComponents<Component>().Select(c => c == null ? "MISSING" : c.GetType().Name)));
            }

            BlendShapeCopMouth bsMouth = cop.GetComponent<BlendShapeCopMouth>();
            Debug.Log($"[Diag] BlendShapeCopMouth present: {bsMouth != null}");

            CopMouthController mouthController = cop.GetComponent<CopMouthController>();
            Debug.Log($"[Diag] CopMouthController present: {mouthController != null}");

            ULS.uLipSync lipSync = cop.GetComponent<ULS.uLipSync>();
            ULS.uLipSyncBlendShape blendShape = cop.GetComponent<ULS.uLipSyncBlendShape>();
            Debug.Log($"[Diag] lipSync found: {lipSync != null}, blendShape found: {blendShape != null}");

            if (lipSync != null)
            {
                Debug.Log($"[Diag] uLipSync.profile: {(lipSync.profile == null ? "NULL" : lipSync.profile.name)}");
                Debug.Log($"[Diag] onLipSyncUpdate persistent listener count: {lipSync.onLipSyncUpdate.GetPersistentEventCount()}");
            }

            if (blendShape != null)
            {
                Debug.Log($"[Diag] uLipSyncBlendShape.skinnedMeshRenderer: {(blendShape.skinnedMeshRenderer == null ? "NULL" : blendShape.skinnedMeshRenderer.name)}");
                Debug.Log($"[Diag] blendShapes.Count: {blendShape.blendShapes.Count}");
                foreach (var bs in blendShape.blendShapes)
                {
                    Debug.Log($"[Diag]   phoneme={bs.phoneme} index={bs.index}");
                }
            }
        }

        /// <summary>Swaps the interrogation cop's model from the old Avaturn T1
        /// export (cop_rigged.fbx, jaw-bone + mouth-dimple surgery, no morph
        /// targets) to the new Avaturn T2 export (NewCop_rigged.fbx, built by
        /// Tools/blender/rig_newcop.py — 73 real morph targets on Head_Mesh
        /// including a full Oculus-viseme set, no jaw bone at all). Runs
        /// BEFORE WireAnimationDirector so that method's own
        /// cop.GetComponentInChildren&lt;Animator&gt;() picks up the new rig.
        /// Non-destructive: cop_rigged.fbx/cop.glb and JawBoneCopMouth are
        /// left on disk, just no longer referenced from the scene.</summary>
        private static void WireCopModel()
        {
            GameObject cop = GameObject.Find("Cop");
            if (cop == null)
            {
                Debug.LogWarning("[ProjectBootstrapBuilder] No 'Cop' GameObject — skipping cop model swap.");
                return;
            }

            Transform oldModel = cop.transform.Find("cop_rigged");
            if (oldModel != null) UnityEngine.Object.DestroyImmediate(oldModel.gameObject);

            Transform newModelTransform = cop.transform.Find("NewCop_rigged");
            GameObject newModel;
            if (newModelTransform == null)
            {
                GameObject newModelSource = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/NewCop_rigged.fbx");
                if (newModelSource == null)
                {
                    Debug.LogWarning("[ProjectBootstrapBuilder] NewCop_rigged.fbx not found — run Tools/blender/rig_newcop.py first.");
                    return;
                }
                newModel = (GameObject)PrefabUtility.InstantiatePrefab(newModelSource, cop.transform);
                newModel.transform.localPosition = Vector3.zero;
                newModel.transform.localRotation = Quaternion.identity;
                newModel.transform.localScale = Vector3.one;
            }
            else
            {
                newModel = newModelTransform.gameObject;
            }

            // Bone re-point for CopIdleAnimator — same convention as the old
            // rig (spine/spine1/neck/head; Spine2 intentionally skipped),
            // just targeting the new rig's identically-named bones.
            Transform[] allChildren = newModel.GetComponentsInChildren<Transform>(true);
            Transform FindBone(string name)
            {
                foreach (Transform t in allChildren)
                {
                    if (t.name == name) return t;
                }
                return null;
            }

            CopIdleAnimator idleAnimator = cop.GetComponent<CopIdleAnimator>();
            if (idleAnimator != null)
            {
                SetField(idleAnimator, "spine", FindBone("Spine"));
                SetField(idleAnimator, "spine1", FindBone("Spine1"));
                SetField(idleAnimator, "neck", FindBone("Neck"));
                SetField(idleAnimator, "head", FindBone("Head"));
            }

            Animator copAnimator = newModel.GetComponentInChildren<Animator>();
            if (copAnimator != null)
            {
                copAnimator.applyRootMotion = false;
            }

            // Talking-with-hands body gesture — see CopTalkGestureAnimator's
            // class doc for why this replaced a baked Timeline clip. Bones
            // wired via Animator.GetBoneTransform (this rig's own Humanoid
            // avatar mapping), not FindBone-by-name — arm/hand bones aren't
            // in CopIdleAnimator's existing FindBone set above and
            // GetBoneTransform is the more robust source of truth for a
            // Humanoid rig regardless of the underlying bone names.
            CopTalkGestureAnimator gesture = cop.GetComponent<CopTalkGestureAnimator>();
            if (gesture == null) gesture = cop.AddComponent<CopTalkGestureAnimator>();
            if (copAnimator != null)
            {
                SetField(gesture, "leftArm", copAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm));
                SetField(gesture, "leftForeArm", copAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm));
                SetField(gesture, "leftHand", copAnimator.GetBoneTransform(HumanBodyBones.LeftHand));
                SetField(gesture, "rightArm", copAnimator.GetBoneTransform(HumanBodyBones.RightUpperArm));
                SetField(gesture, "rightForeArm", copAnimator.GetBoneTransform(HumanBodyBones.RightLowerArm));
                SetField(gesture, "rightHand", copAnimator.GetBoneTransform(HumanBodyBones.RightHand));
            }
            SetField(gesture, "spine1", FindBone("Spine1"));
            SetField(gesture, "lipSync", cop.GetComponent<ULS.uLipSync>());

            // CopTalkGestureAnimator's spine1 accent is additive on top of
            // whatever CopIdleAnimator's breathing curve wrote to spine1
            // THIS FRAME (see that class's LateUpdate comment) — both write
            // in LateUpdate, so this only composes correctly if
            // CopTalkGestureAnimator's LateUpdate runs after
            // CopIdleAnimator's. Unity's default same-phase execution order
            // is otherwise unspecified, so pin it explicitly rather than
            // rely on incidental component-add order.
            MonoScript idleScript = idleAnimator != null ? MonoScript.FromMonoBehaviour(idleAnimator) : null;
            MonoScript gestureScript = MonoScript.FromMonoBehaviour(gesture);
            if (idleScript != null && gestureScript != null)
            {
                int idleOrder = MonoImporter.GetExecutionOrder(idleScript);
                MonoImporter.SetExecutionOrder(gestureScript, idleOrder + 1);
            }

            // Mouth: this rig has no jaw bone at all (see class doc) — switch
            // from the jaw-bone tier to the real blendshape tier.
            // BlendShapeCopMouth already exists (Scripts/Cop/BlendShapeCopMouth.cs)
            // and wraps uLipSync/uLipSyncBlendShape, both already present on
            // Cop but previously unconfigured.
            BlendShapeCopMouth blendMouth = cop.GetComponent<BlendShapeCopMouth>();
            if (blendMouth == null) blendMouth = cop.AddComponent<BlendShapeCopMouth>();

            ULS.uLipSync lipSync = cop.GetComponent<ULS.uLipSync>();
            ULS.uLipSyncBlendShape blendShape = cop.GetComponent<ULS.uLipSyncBlendShape>();
            SetField(blendMouth, "lipSync", lipSync);
            SetField(blendMouth, "blendShape", blendShape);

            CopMouthController mouthController = cop.GetComponent<CopMouthController>();
            if (mouthController != null)
            {
                SetField(mouthController, "mouthImplementation", blendMouth);
            }

            SkinnedMeshRenderer headRenderer = null;
            foreach (SkinnedMeshRenderer smr in newModel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.gameObject.name == "Head_Mesh") { headRenderer = smr; break; }
            }
            if (blendShape != null && headRenderer != null)
            {
                blendShape.skinnedMeshRenderer = headRenderer;
                blendShape.blendShapes.Clear();
                // uLipSync-Profile-Sample-Male's own phoneme set (A, I, U, E,
                // O, '-', S — read directly off the .asset, not guessed)
                // mapped to the closest Oculus-viseme shapes this T2 model
                // ships. AddBlendShape looks up the blend shape index itself.
                blendShape.AddBlendShape("A", "viseme_aa");
                blendShape.AddBlendShape("I", "viseme_I");
                blendShape.AddBlendShape("U", "viseme_U");
                blendShape.AddBlendShape("E", "viseme_E");
                blendShape.AddBlendShape("O", "viseme_O");
                blendShape.AddBlendShape("-", "viseme_sil");
                blendShape.AddBlendShape("S", "viseme_SS");
                EditorUtility.SetDirty(blendShape);
            }

            // Profile: copied into the project rather than referenced
            // straight out of PackageCache, which can be regenerated/moved.
            const string profileDest = "Assets/_Project/Config/uLipSyncProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(profileDest) == null)
            {
                const string profileSource = "Packages/com.hecomi.ulipsync/Assets/Profiles/uLipSync-Profile-Sample-Male.asset";
                AssetDatabase.CopyAsset(profileSource, profileDest);
            }
            ULS.Profile profile = AssetDatabase.LoadAssetAtPath<ULS.Profile>(profileDest);
            if (lipSync != null && profile != null)
            {
                lipSync.profile = profile;
                EditorUtility.SetDirty(lipSync);
            }

            // Persistent listener, not just Begin()-time: nothing calls
            // CopMouthController.Begin() during a cutscene (only DialogueManager
            // calls it, for live turns), so without this the blendshapes would
            // never move for CutsceneId.SpasskyAnswer no matter how correctly
            // the audio is routed (see CutsceneAnimationDirector). Persistent/
            // always-on covers both paths uniformly with one wiring.
            //
            // AddPersistentListener always APPENDS — it does not dedupe — so
            // re-running this step without first clearing would silently
            // accumulate a duplicate listener (same delegate called N times)
            // on every bootstrap run. Clear to zero first for idempotency,
            // matching every other builder in this file/CopAnimationBuilder's
            // own clear-then-rewrite convention.
            if (lipSync != null && blendShape != null)
            {
                while (lipSync.onLipSyncUpdate.GetPersistentEventCount() > 0)
                {
                    UnityEventTools.RemovePersistentListener(lipSync.onLipSyncUpdate, 0);
                }
                UnityEventTools.AddPersistentListener(lipSync.onLipSyncUpdate, blendShape.OnLipSyncUpdate);
                EditorUtility.SetDirty(lipSync);
            }
        }

        /// <summary>Creates/refreshes the AnimationDirector GameObject —
        /// now scoped to only what CutsceneAnimationDirector still does:
        /// redirect uLipSync's audio analysis to CutsceneId.SpasskyAnswer's
        /// own VO for that cutscene's duration (see that class's doc for
        /// why a cross-scene proxy redirect is still needed). It no longer
        /// plays a Timeline clip — see class doc for why (the body is now
        /// driven by CopTalkGestureAnimator, wired in WireCopModel, off
        /// uLipSync's own volume, uniformly for live dialogue and cutscenes
        /// alike, not just this one cutscene). CopAnimationBuilder's Cop_Talk
        /// clip and Cutscene_SpasskyAnswer.playable are left on disk,
        /// unreferenced — matching this project's convention for superseded
        /// assets (see the T1 cop model) — so this method deliberately does
        /// NOT call CopAnimationBuilder.EnsureBuilt() any more.
        /// Idempotent, same GameObject.Find-or-create pattern as the rest of
        /// this method.</summary>
        private static void WireAnimationDirector()
        {
            GameObject cop = GameObject.Find("Cop");
            if (cop == null)
            {
                Debug.LogWarning("[ProjectBootstrapBuilder] No 'Cop' GameObject — skipping AnimationDirector wiring.");
                return;
            }

            // Deliberately NO runtimeAnimatorController assigned, and no
            // PlayableDirector driving this Animator any more either — see
            // ASSETS_TODO.md #2 for the sink bug a permanent controller
            // caused, and this method's own class doc for why the Timeline
            // path was retired on top of that. Nothing drives this
            // Animator's muscles at all now; CopIdleAnimator and
            // CopTalkGestureAnimator both work by writing bone
            // Transform.localRotation directly, bypassing Mecanim
            // evaluation entirely, which is what keeps this immune to the
            // root-motion/sink class of bug regardless of clip authoring.
            Animator copAnimator = cop.GetComponentInChildren<Animator>();
            if (copAnimator != null)
            {
                copAnimator.applyRootMotion = false;
                // Explicit null, not just "don't assign" — a stale prior-
                // session save may have persisted a controller onto this
                // Animator, and merely no longer writing it here would leave
                // that stale reference in place across a re-run of this step.
                copAnimator.runtimeAnimatorController = null;
            }

            // NOTE: deliberately NOT using `x ?? y` for these UnityEngine.Object
            // gets/adds — GetComponent<T>() ?? AddComponent<T>() was confirmed
            // (empirically, in this environment) to silently discard the
            // AddComponent side even when GetComponent returned a genuine
            // null, leaving `animDir` null. Explicit if-null checks below,
            // matching CabinAnimationBuilder's LoadOrCreateClip style
            // elsewhere in this codebase.
            GameObject animGo = GameObject.Find("AnimationDirector");
            if (animGo == null) animGo = new GameObject("AnimationDirector");

            // A stale prior-session save may still carry the PlayableDirector
            // this GameObject used to have when it played a Timeline clip —
            // remove it so re-running this step actually converges instead
            // of leaving an inert, unreferenced component behind.
            PlayableDirector stalePlayableDirector = animGo.GetComponent<PlayableDirector>();
            if (stalePlayableDirector != null) UnityEngine.Object.DestroyImmediate(stalePlayableDirector);

            CutsceneAnimationDirector animDir = animGo.GetComponent<CutsceneAnimationDirector>();
            if (animDir == null) animDir = animGo.AddComponent<CutsceneAnimationDirector>();
            SetField(animDir, "cutsceneId", CutsceneId.SpasskyAnswer);
            SetField(animDir, "lipSync", cop.GetComponent<ULS.uLipSync>());
        }

        // ------------------------------------------------------------------
        // 5. Build Settings
        // ------------------------------------------------------------------

        [MenuItem("Tools/False Positive/Bootstrap/5 - Rewrite Build Settings")]
        public static void RewriteBuildSettings()
        {
            List<string> orderedScenes = new List<string>
            {
                PersistentScenePath,
                MainMenuScenePath,
                InterrogationScenePath,
                NightScenePath,
                MorningScenePath,
            };

            List<EditorBuildSettingsScene> entries = new List<EditorBuildSettingsScene>();
            foreach (string path in orderedScenes)
            {
                bool exists = AssetDatabase.LoadAssetAtPath<SceneAsset>(path) != null;
                entries.Add(new EditorBuildSettingsScene(path, exists));
                if (!exists)
                {
                    Debug.LogWarning($"[ProjectBootstrapBuilder] {path} does not exist yet — added to Build " +
                        "Settings disabled. Re-run this step once it is created (see step T04).");
                }
            }

            EditorBuildSettings.scenes = entries.ToArray();
            Debug.Log("[ProjectBootstrapBuilder] Build Settings rewritten: " + string.Join(", ", orderedScenes));
        }

        // ------------------------------------------------------------------
        // Helpers
        // ------------------------------------------------------------------

        private static InterrogationConfig RequireConfig()
        {
            InterrogationConfig config = AssetDatabase.LoadAssetAtPath<InterrogationConfig>(ConfigPath);
            if (config == null)
            {
                throw new InvalidOperationException("[ProjectBootstrapBuilder] Missing " + ConfigPath);
            }
            return config;
        }

        private static Scene OpenOrCreateEmptyScene(string path)
        {
            if (File.Exists(path))
            {
                Scene existing = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                foreach (GameObject root in existing.GetRootGameObjects())
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
                return existing;
            }

            return EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        private static void SaveScene(Scene scene, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.SaveAssets();
        }

        private static void DestroyIfFound(string name)
        {
            GameObject go = GameObject.Find(name);
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        }

        private static void RemoveComponentIfPresent<T>(GameObject go) where T : Component
        {
            T component = go.GetComponent<T>();
            if (component != null) UnityEngine.Object.DestroyImmediate(component);
        }

        private static GameObject NewChild(Transform parent, string name)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            return go;
        }

        /// <summary>Reflection setter for private [SerializeField] fields. Fails loudly —
        /// a missing field name means the script and the runtime component have drifted,
        /// and a silent null there is a worse failure mode than a thrown exception here.</summary>
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (field == null)
            {
                throw new InvalidOperationException(
                    $"[ProjectBootstrapBuilder] {target.GetType().Name} has no field '{fieldName}' — " +
                    "the script and this builder have drifted.");
            }
            field.SetValue(target, value);
        }

        private static void ApplyLabelMaterial(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;
            Material material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            if (material.shader == null) material = new Material(Shader.Find("Standard"));
            material.color = color;
            renderer.sharedMaterial = material;
        }

        // --- UI construction ---

        private static GameObject CreateCanvasRoot(string name, int sortOrder)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortOrder;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f; // default 0 (match width) overflows at 21:9/32:9 — risk 8; centralized here so every canvas gets it, not just the ones that remembered to set it at the call site
            return go;
        }

        private static void StretchFill(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorCenter(GameObject go, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void AnchorTopRight(GameObject go, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void AnchorBottomRight(GameObject go, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void SetLayoutHeight(GameObject go, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void AnchorTopLeft(GameObject go, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void AnchorBottomCenter(GameObject go, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
        }

        private static void AnchorTopStretch(GameObject go, float yOffsetFromTop, float height)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, yOffsetFromTop);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static void AnchorBottomStretch(GameObject go, float yOffsetFromBottom, float height)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, yOffsetFromBottom);
            rt.sizeDelta = new Vector2(0f, height);
        }

        private static Text CreateText(Transform parent, string name, string content, int fontSize)
        {
            GameObject go = DefaultControls.CreateText(UIResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            Text text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = fontSize;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            return text;
        }

        private static Button CreateButton(Transform parent, string name, string label)
        {
            GameObject go = DefaultControls.CreateButton(UIResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            Text label_ = go.GetComponentInChildren<Text>();
            if (label_ != null) label_.text = label;
            return go.GetComponent<Button>();
        }

        private static Dropdown CreateDropdown(Transform parent, string name)
        {
            GameObject go = DefaultControls.CreateDropdown(UIResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            return go.GetComponent<Dropdown>();
        }

        private static Slider CreateSlider(Transform parent, string name, float min, float max, float value)
        {
            GameObject go = DefaultControls.CreateSlider(UIResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            Slider slider = go.GetComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            return slider;
        }

        private static Toggle CreateToggle(Transform parent, string name, bool value)
        {
            GameObject go = DefaultControls.CreateToggle(UIResources);
            go.name = name;
            go.transform.SetParent(parent, false);
            Toggle toggle = go.GetComponent<Toggle>();
            toggle.isOn = value;
            return toggle;
        }

        private static Image CreateDotImage(Transform parent, string name, Vector2 anchoredPos)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            AnchorCenter(go, anchoredPos, new Vector2(16f, 16f));
            return go.GetComponent<Image>();
        }

        // ------------------------------------------------------------------
        // Menu UI construction — TextMeshPro, flat Images, framed windows.
        // Everything below is new for the menu overhaul; the legacy Text/
        // Button/Dropdown/Slider/Toggle helpers above stay untouched and
        // keep serving _Persistent's HUD and the pre-existing panels.
        // ------------------------------------------------------------------

        private static TextMeshProUGUI CreateTmpText(Transform parent, string name, string content, float fontSize,
            Color color, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft, float tracking = 0f,
            FontStyles style = FontStyles.Normal, float lineSpacing = 0f)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.characterSpacing = tracking;
            text.fontStyle = style;
            text.lineSpacing = lineSpacing;
            text.enableWordWrapping = true;
            text.raycastTarget = false; // a label must never steal a backdrop-dismiss click
            return text;
        }

        /// <summary>Solid-colour quad — no sprite, so every instance shares one
        /// material and batches into a single draw call. The workhorse behind
        /// every plate, rule, band and window fill in the menu.</summary>
        private static Image CreateFlatImage(Transform parent, string name, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = null;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>Single home for the menu's ColorTint block. Values >1 are
        /// legal for ColorTint (it multiplies) and are the cheapest hover/press
        /// feedback available with no second sprite to author.</summary>
        private static void ApplyMenuColors(Selectable selectable)
        {
            ColorBlock colors = selectable.colors;
            colors.normalColor = new Color(1f, 1f, 1f, 1f);
            colors.highlightedColor = new Color(1.42f, 1.42f, 1.45f, 1f);
            colors.pressedColor = new Color(0.78f, 0.78f, 0.80f, 1f);
            colors.selectedColor = new Color(1.28f, 1.32f, 1.40f, 1f);
            colors.disabledColor = new Color(1f, 1f, 1f, 0.38f);
            colors.fadeDuration = 0.10f;
            selectable.colors = colors;
        }

        /// <summary>Flat plate + a 3px accent rule (left) + a 1px hairline
        /// (bottom) + a tracked-out UpperCase TMP label — replaces every
        /// DefaultControls.CreateButton in the menu itself (Settings/Credits/
        /// Controls/Quit still reuse the legacy Slider/Toggle/Dropdown
        /// widgets, just restyled, per the plan's explicit Dropdown-stays-
        /// legacy note).</summary>
        private static Button CreateTmpButton(Transform parent, string name, string label, float fontSize, bool accent = false)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            Image plate = go.GetComponent<Image>();
            plate.sprite = null;
            plate.color = MenuPalette.PanelRaised;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = plate;
            ApplyMenuColors(button);

            Image accentRule = CreateFlatImage(go.transform, "AccentRule", accent ? MenuPalette.Accent : MenuPalette.Rule);
            RectTransform accentRt = accentRule.rectTransform;
            accentRt.anchorMin = new Vector2(0f, 0f);
            accentRt.anchorMax = new Vector2(0f, 1f);
            accentRt.pivot = new Vector2(0f, 0.5f);
            accentRt.sizeDelta = new Vector2(3f, 0f);
            accentRt.anchoredPosition = Vector2.zero;

            Image edgeRule = CreateFlatImage(go.transform, "EdgeRule", MenuPalette.RuleDim);
            RectTransform edgeRt = edgeRule.rectTransform;
            edgeRt.anchorMin = new Vector2(0f, 0f);
            edgeRt.anchorMax = new Vector2(1f, 0f);
            edgeRt.pivot = new Vector2(0.5f, 0f);
            edgeRt.sizeDelta = new Vector2(0f, 1f);
            edgeRt.anchoredPosition = Vector2.zero;

            TextMeshProUGUI labelText = CreateTmpText(go.transform, "Label", label.ToUpperInvariant(), fontSize,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, tracking: 8f, style: FontStyles.UpperCase);
            RectTransform labelRt = labelText.rectTransform;
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = new Vector2(28f, 0f);
            labelRt.offsetMax = Vector2.zero;

            return button;
        }

        private struct WindowRefs
        {
            public GameObject Host;
            public GameObject Root;
            public CanvasGroup CanvasGroup;
            public Button BackdropButton;
            public Transform Content;
            public Transform Footer;
        }

        /// <summary>The one that pays for itself four times — Settings, Quit,
        /// Credits and How-to-play all start here. Emits WindowHost -> Root ->
        /// {Backdrop, Frame{Fill, 4x hairline Rule, HeaderBand, TitleText,
        /// HeaderRule, Content, FooterRule, Footer}}. WindowHost stays
        /// always-active and sits above Root for exactly the reason MenuWindow
        /// and SettingsPanel both document: a fade coroutine must live above
        /// the GameObject it eventually SetActive(false)s, or StartCoroutine
        /// throws (Root disabled) or the coroutine dies mid-fade (hosted on
        /// Root itself).</summary>
        private static WindowRefs CreateWindow(Transform parent, string hostName, string title, Vector2 size)
        {
            GameObject host = new GameObject(hostName, typeof(RectTransform));
            host.transform.SetParent(parent, false);
            StretchFill(host); // without this Root's own StretchFill resolves against a zero-size
                                // parent rect and every backdrop in the game is 0x0 (root cause 1.A)

            GameObject root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(host.transform, false);
            StretchFill(root);
            CanvasGroup canvasGroup = root.AddComponent<CanvasGroup>();
            canvasGroup.alpha = 0f; // MenuWindow/SettingsPanel never set this themselves —
                                     // without it the first Open() fades from the
                                     // CanvasGroup default (1) to 1, i.e. not at all.

            GameObject backdrop = new GameObject("Backdrop", typeof(RectTransform), typeof(Image), typeof(Button));
            backdrop.transform.SetParent(root.transform, false);
            StretchFill(backdrop);
            Image backdropImage = backdrop.GetComponent<Image>();
            backdropImage.sprite = null;
            backdropImage.color = MenuPalette.BackdropScrim;
            backdropImage.raycastTarget = true;
            Button backdropButton = backdrop.GetComponent<Button>();
            backdropButton.transition = Selectable.Transition.None;

            GameObject frame = new GameObject("Frame", typeof(RectTransform));
            frame.transform.SetParent(root.transform, false);
            AnchorCenter(frame, Vector2.zero, size);

            Image fill = CreateFlatImage(frame.transform, "Fill", MenuPalette.PanelFill);
            StretchFill(fill.gameObject);
            fill.raycastTarget = true; // clicks inside the frame must not fall through to Backdrop

            // Four 2px hairlines, not a 9-sliced sprite — the built-in skin's
            // soft grey rounded border reads "Unity default" instantly.
            Image top = CreateFlatImage(frame.transform, "RuleTop", MenuPalette.Rule);
            AnchorTopStretch(top.gameObject, 0f, 2f);
            Image bottom = CreateFlatImage(frame.transform, "RuleBottom", MenuPalette.Rule);
            AnchorBottomStretch(bottom.gameObject, 0f, 2f);
            Image left = CreateFlatImage(frame.transform, "RuleLeft", MenuPalette.Rule);
            RectTransform leftRt = left.rectTransform;
            leftRt.anchorMin = new Vector2(0f, 0f);
            leftRt.anchorMax = new Vector2(0f, 1f);
            leftRt.pivot = new Vector2(0f, 0.5f);
            leftRt.sizeDelta = new Vector2(2f, 0f);
            leftRt.anchoredPosition = Vector2.zero;
            Image right = CreateFlatImage(frame.transform, "RuleRight", MenuPalette.Rule);
            RectTransform rightRt = right.rectTransform;
            rightRt.anchorMin = new Vector2(1f, 0f);
            rightRt.anchorMax = new Vector2(1f, 1f);
            rightRt.pivot = new Vector2(1f, 0.5f);
            rightRt.sizeDelta = new Vector2(2f, 0f);
            rightRt.anchoredPosition = Vector2.zero;

            GameObject headerBand = new GameObject("HeaderBand", typeof(RectTransform), typeof(Image));
            headerBand.transform.SetParent(frame.transform, false);
            headerBand.GetComponent<Image>().color = MenuPalette.PanelRaised;
            AnchorTopStretch(headerBand, 0f, 64f);

            TextMeshProUGUI titleText = CreateTmpText(headerBand.transform, "TitleText", title.ToUpperInvariant(), 24f,
                MenuPalette.TextPrimary, TextAlignmentOptions.MidlineLeft, tracking: 12f, style: FontStyles.Bold | FontStyles.UpperCase);
            RectTransform titleRt = titleText.rectTransform;
            titleRt.anchorMin = Vector2.zero;
            titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = new Vector2(28f, 0f);
            titleRt.offsetMax = Vector2.zero;

            Image headerRule = CreateFlatImage(frame.transform, "HeaderRule", MenuPalette.RuleDim);
            AnchorTopStretch(headerRule.gameObject, -64f, 2f);

            GameObject content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(frame.transform, false);
            RectTransform contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero;
            contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(28f, 84f + 20f);
            contentRt.offsetMax = new Vector2(-28f, -(64f + 20f));

            Image footerRule = CreateFlatImage(frame.transform, "FooterRule", MenuPalette.RuleDim);
            AnchorBottomStretch(footerRule.gameObject, 84f, 2f);

            GameObject footer = new GameObject("Footer", typeof(RectTransform));
            footer.transform.SetParent(frame.transform, false);
            AnchorBottomStretch(footer, 0f, 84f);

            return new WindowRefs
            {
                Host = host,
                Root = root,
                CanvasGroup = canvasGroup,
                BackdropButton = backdropButton,
                Content = content.transform,
                Footer = footer.transform,
            };
        }

        private struct SettingsRowRefs
        {
            public Transform Row;
            public Transform ControlSlot;
            public TextMeshProUGUI Readout;
        }

        private static SettingsRowRefs CreateSettingsRow(Transform parent, string label, bool withReadout = true, float height = 56f)
        {
            GameObject row = new GameObject(label.Replace(" ", string.Empty) + "Row", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetLayoutHeight(row, height);

            TextMeshProUGUI labelText = CreateTmpText(row.transform, "Label", label, 16f, MenuPalette.TextSecondary,
                TextAlignmentOptions.MidlineLeft);
            RectTransform labelRt = labelText.rectTransform;
            labelRt.anchorMin = new Vector2(0f, 0.5f);
            labelRt.anchorMax = new Vector2(0f, 0.5f);
            labelRt.pivot = new Vector2(0f, 0.5f);
            labelRt.anchoredPosition = Vector2.zero;
            labelRt.sizeDelta = new Vector2(280f, height);

            GameObject controlSlot = new GameObject("ControlSlot", typeof(RectTransform));
            controlSlot.transform.SetParent(row.transform, false);
            RectTransform slotRt = controlSlot.GetComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0f, 0f);
            slotRt.anchorMax = new Vector2(0f, 1f);
            slotRt.pivot = new Vector2(0f, 0.5f);
            slotRt.sizeDelta = new Vector2(420f, 0f); // was 500 — overlapped the right-anchored Readout by 70px at max slider value
            slotRt.anchoredPosition = new Vector2(304f, 0f);

            TextMeshProUGUI readout = null;
            if (withReadout)
            {
                readout = CreateTmpText(row.transform, "Readout", string.Empty, 16f, MenuPalette.TextPrimary,
                    TextAlignmentOptions.MidlineRight);
                RectTransform readoutRt = readout.rectTransform;
                readoutRt.anchorMin = new Vector2(1f, 0.5f);
                readoutRt.anchorMax = new Vector2(1f, 0.5f);
                readoutRt.pivot = new Vector2(1f, 0.5f);
                readoutRt.anchoredPosition = Vector2.zero;
                readoutRt.sizeDelta = new Vector2(90f, height);
            }

            return new SettingsRowRefs { Row = row.transform, ControlSlot = controlSlot.transform, Readout = readout };
        }

        private static GameObject CreateKeyCap(Transform parent, string keyLabel, float minWidth = 64f)
        {
            GameObject go = new GameObject("KeyCap_" + keyLabel, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            Image plate = go.GetComponent<Image>();
            plate.sprite = null;
            plate.color = MenuPalette.PanelRaised;
            plate.raycastTarget = false;

            Image border = CreateFlatImage(go.transform, "Border", MenuPalette.Rule);
            RectTransform borderRt = border.rectTransform;
            borderRt.anchorMin = Vector2.zero;
            borderRt.anchorMax = Vector2.one;
            borderRt.offsetMin = new Vector2(-2f, -2f);
            borderRt.offsetMax = new Vector2(2f, 2f);
            border.transform.SetAsFirstSibling(); // sibling of plate, drawn before it -> a ring, not a cap

            TextMeshProUGUI label = CreateTmpText(go.transform, "Label", keyLabel, 15f, MenuPalette.TextPrimary,
                TextAlignmentOptions.Center, style: FontStyles.UpperCase);
            StretchFill(label.gameObject);

            LayoutElement layoutElement = go.GetComponent<LayoutElement>();
            layoutElement.minWidth = minWidth;
            layoutElement.preferredWidth = minWidth;
            layoutElement.minHeight = 32f;
            layoutElement.preferredHeight = 32f;

            return go;
        }

        private static void RestyleSlider(Slider slider)
        {
            Transform background = slider.transform.Find("Background");
            if (background != null)
            {
                Image bgImage = background.GetComponent<Image>();
                if (bgImage != null) { bgImage.sprite = null; bgImage.color = MenuPalette.RuleDim; }
                RectTransform bgRt = background.GetComponent<RectTransform>();
                bgRt.anchorMin = new Vector2(0f, 0.5f);
                bgRt.anchorMax = new Vector2(1f, 0.5f);
                bgRt.sizeDelta = new Vector2(0f, 4f);
            }

            Transform fill = slider.transform.Find("Fill Area/Fill");
            if (fill != null)
            {
                Image fillImage = fill.GetComponent<Image>();
                if (fillImage != null) { fillImage.sprite = null; fillImage.color = MenuPalette.Focus; }
            }

            Transform handle = slider.transform.Find("Handle Slide Area/Handle");
            if (handle != null)
            {
                Image handleImage = handle.GetComponent<Image>();
                if (handleImage != null) { handleImage.sprite = null; handleImage.color = MenuPalette.TextPrimary; }
                handle.GetComponent<RectTransform>().sizeDelta = new Vector2(12f, 24f);
            }

            ApplyMenuColors(slider);
        }

        private static void RestyleToggle(Toggle toggle)
        {
            // DefaultControls.CreateToggle ships its own placeholder "Label" —
            // CreateSettingsRow already draws the row's label to its left, so
            // the internal one would duplicate/clash with it.
            Transform toggleLabel = toggle.transform.Find("Label");
            if (toggleLabel != null) UnityEngine.Object.DestroyImmediate(toggleLabel.gameObject);

            Transform background = toggle.transform.Find("Background");
            if (background != null)
            {
                RectTransform bgRt = background.GetComponent<RectTransform>();
                bgRt.sizeDelta = new Vector2(26f, 26f);
                Image bgImage = background.GetComponent<Image>();
                if (bgImage != null) { bgImage.sprite = null; bgImage.color = MenuPalette.PanelRaised; }

                // Sibling of Background (not a child of it — children draw
                // after their parent, which would bury this under the plate),
                // inserted immediately before it so it renders behind as a
                // 2px ring.
                GameObject borderGo = new GameObject("Border", typeof(RectTransform), typeof(Image));
                borderGo.transform.SetParent(toggle.transform, false);
                borderGo.transform.SetSiblingIndex(background.GetSiblingIndex());
                RectTransform borderRt = borderGo.GetComponent<RectTransform>();
                borderRt.anchorMin = bgRt.anchorMin;
                borderRt.anchorMax = bgRt.anchorMax;
                borderRt.pivot = bgRt.pivot;
                borderRt.anchoredPosition = bgRt.anchoredPosition;
                borderRt.sizeDelta = bgRt.sizeDelta + new Vector2(4f, 4f);
                Image borderImage = borderGo.GetComponent<Image>();
                borderImage.sprite = null;
                borderImage.color = MenuPalette.Rule;
                borderImage.raycastTarget = false;

                Transform checkmark = background.Find("Checkmark");
                if (checkmark != null)
                {
                    Image checkImage = checkmark.GetComponent<Image>();
                    if (checkImage != null) { checkImage.sprite = null; checkImage.color = MenuPalette.Accent; }
                    RectTransform checkRt = checkmark.GetComponent<RectTransform>();
                    checkRt.anchorMin = Vector2.zero;
                    checkRt.anchorMax = Vector2.one;
                    checkRt.offsetMin = new Vector2(6f, 6f);
                    checkRt.offsetMax = new Vector2(-6f, -6f);
                }
            }

            RectTransform toggleRt = toggle.GetComponent<RectTransform>();
            toggleRt.anchorMin = new Vector2(0f, 0.5f);
            toggleRt.anchorMax = new Vector2(0f, 0.5f);
            toggleRt.pivot = new Vector2(0f, 0.5f);
            toggleRt.anchoredPosition = Vector2.zero;
            toggleRt.sizeDelta = new Vector2(26f, 26f);

            ApplyMenuColors(toggle);
        }

        private static void RestyleDropdown(Dropdown dropdown)
        {
            Image rootImage = dropdown.GetComponent<Image>();
            if (rootImage != null) { rootImage.sprite = null; rootImage.color = MenuPalette.PanelRaised; }

            Image bottomRule = CreateFlatImage(dropdown.transform, "BottomRule", MenuPalette.Rule);
            AnchorBottomStretch(bottomRule.gameObject, 0f, 2f);

            // Label/Arrow/Template children stay legacy Arial per the plan —
            // swapping to TMP_Dropdown would change SettingsPanel's field type.
            ApplyMenuColors(dropdown);
        }
    }
}
