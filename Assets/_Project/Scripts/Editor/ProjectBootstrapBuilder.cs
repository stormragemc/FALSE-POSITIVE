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

        [MenuItem("Tools/False Positive/Bootstrap/Build Everything (0-9)")]
        public static void BuildEverything()
        {
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
            Text micIndText = CreateText(micIndGo.transform, "Label", "Microphone inactive", 20);
            AnchorTopStretch(micIndText.gameObject, 0f, 40f);
            Image micIndDot = CreateDotImage(micIndGo.transform, "Dot", new Vector2(-150f, 12f));

            GameObject meterBg = new GameObject("LevelMeterBackground", typeof(RectTransform), typeof(Image));
            meterBg.transform.SetParent(micIndGo.transform, false);
            meterBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            RectTransform meterBgRt = meterBg.GetComponent<RectTransform>();
            meterBgRt.anchorMin = new Vector2(0f, 0f);
            meterBgRt.anchorMax = new Vector2(1f, 0f);
            meterBgRt.anchoredPosition = new Vector2(0f, 7f);
            meterBgRt.sizeDelta = new Vector2(-8f, 12f);

            GameObject meterFillGo = new GameObject("LevelMeterFill", typeof(RectTransform), typeof(Image));
            meterFillGo.transform.SetParent(meterBg.transform, false);
            RectTransform meterFillRt = meterFillGo.GetComponent<RectTransform>();
            meterFillRt.anchorMin = Vector2.zero;
            meterFillRt.anchorMax = new Vector2(0f, 1f);
            meterFillRt.offsetMin = Vector2.zero;
            meterFillRt.offsetMax = Vector2.zero;
            Image meterFill = meterFillGo.GetComponent<Image>();
            meterFill.color = new Color(0.6f, 0.6f, 0.6f);

            MicIndicator micIndicator = micIndGo.AddComponent<MicIndicator>();
            SetField(micIndicator, "mic", mic);
            SetField(micIndicator, "vad", vad);
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
            GameObject calibrationGo = BuildCalibrationPanel(hud.transform, calibration, mic, vad, out CalibrationPanelUI calibrationPanelUi);

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

        private static GameObject BuildConsentPanel(Transform hud, MicrophoneService mic, out MicConsentFlow flow)
        {
            GameObject root = new GameObject("ConsentPanel", typeof(RectTransform));
            root.transform.SetParent(hud, false);
            StretchFill(root);
            Image backing = root.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.85f);

            Text copy = CreateText(root.transform, "CopyText",
                "This game listens. Your microphone stays on for the whole session so the officer can " +
                "hear you. Your voice is processed to transcribe what you say and read the tone you say " +
                "it in. Nothing is recorded to disk.", 26);
            copy.alignment = TextAnchor.MiddleCenter;
            AnchorCenter(copy.gameObject, new Vector2(0f, 80f), new Vector2(1000f, 240f));

            Dropdown dropdown = CreateDropdown(root.transform, "DeviceDropdown");
            AnchorCenter(dropdown.gameObject, new Vector2(0f, -40f), new Vector2(400f, 40f));

            Button enableButton = CreateButton(root.transform, "EnableButton", "Enable microphone");
            AnchorCenter(enableButton.gameObject, new Vector2(-140f, -140f), new Vector2(240f, 50f));

            Button backButton = CreateButton(root.transform, "BackButton", "Back");
            AnchorCenter(backButton.gameObject, new Vector2(140f, -140f), new Vector2(160f, 50f));

            flow = root.AddComponent<MicConsentFlow>();
            SetField(flow, "root", root);
            SetField(flow, "copyText", copy);
            SetField(flow, "deviceDropdown", dropdown);
            SetField(flow, "enableButton", enableButton);
            SetField(flow, "backButton", backButton);
            SetField(flow, "mic", mic);
            root.SetActive(false);
            return root;
        }

        private static GameObject BuildCalibrationPanel(
            Transform hud, MicCalibration calibration, MicrophoneService mic, VoiceActivityDetector vad,
            out CalibrationPanelUI panelUi)
        {
            GameObject root = new GameObject("CalibrationPanel", typeof(RectTransform));
            root.transform.SetParent(hud, false);
            StretchFill(root);
            Image backing = root.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.85f);

            Text status = CreateText(root.transform, "StatusText", "Speak normally for a few seconds. Say anything.", 26);
            AnchorCenter(status.gameObject, new Vector2(0f, 100f), new Vector2(900f, 100f));

            GameObject meterBg = new GameObject("LevelMeterBackground", typeof(RectTransform), typeof(Image));
            meterBg.transform.SetParent(root.transform, false);
            meterBg.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
            AnchorCenter(meterBg, new Vector2(0f, 20f), new Vector2(500f, 24f));

            GameObject meterFillGo = new GameObject("LevelMeterFill", typeof(RectTransform), typeof(Image));
            meterFillGo.transform.SetParent(meterBg.transform, false);
            RectTransform fillRt = meterFillGo.GetComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f);
            fillRt.anchorMax = new Vector2(0f, 1f);
            fillRt.offsetMin = Vector2.zero;
            fillRt.offsetMax = Vector2.zero;
            Image meterFill = meterFillGo.GetComponent<Image>();
            meterFill.color = new Color(0.3f, 0.85f, 0.4f);

            GameObject failurePanel = new GameObject("FailurePanel", typeof(RectTransform));
            failurePanel.transform.SetParent(root.transform, false);
            AnchorCenter(failurePanel, new Vector2(0f, -60f), new Vector2(500f, 140f));
            Text failureText = CreateText(failurePanel.transform, "FailureText", "I can't hear you. Check your microphone.", 22);
            StretchFill(failureText.gameObject);

            Dropdown deviceDropdown = CreateDropdown(failurePanel.transform, "DeviceDropdown");
            AnchorBottomStretch(deviceDropdown.gameObject, 40f, 40f);

            Button retryButton = CreateButton(root.transform, "RetryButton", "Retry");
            AnchorCenter(retryButton.gameObject, new Vector2(0f, -220f), new Vector2(160f, 50f));

            failurePanel.SetActive(false);

            panelUi = root.AddComponent<CalibrationPanelUI>();
            SetField(panelUi, "root", root);
            SetField(panelUi, "statusText", status);
            SetField(panelUi, "levelMeterFill", meterFill);
            SetField(panelUi, "failurePanel", failurePanel);
            SetField(panelUi, "deviceDropdown", deviceDropdown);
            SetField(panelUi, "retryButton", retryButton);
            SetField(panelUi, "calibration", calibration);
            SetField(panelUi, "mic", mic);
            SetField(panelUi, "vad", vad);
            root.SetActive(false);
            return root;
        }

        private static GameObject BuildSettingsPanel(Transform hud, MicrophoneService mic, out SettingsPanel settingsPanel)
        {
            GameObject root = new GameObject("SettingsPanelRoot", typeof(RectTransform));
            root.transform.SetParent(hud, false);
            StretchFill(root);
            Image backing = root.AddComponent<Image>();
            backing.color = new Color(0f, 0f, 0f, 0.9f);

            Dropdown micDropdown = CreateDropdown(root.transform, "MicDeviceDropdown");
            AnchorCenter(micDropdown.gameObject, new Vector2(0f, 220f), new Vector2(400f, 40f));

            Slider masterSlider = CreateSlider(root.transform, "MasterVolumeSlider", 0f, 1f, 1f);
            AnchorCenter(masterSlider.gameObject, new Vector2(0f, 150f), new Vector2(400f, 30f));
            Slider voiceSlider = CreateSlider(root.transform, "VoiceVolumeSlider", 0f, 1f, 1f);
            AnchorCenter(voiceSlider.gameObject, new Vector2(0f, 100f), new Vector2(400f, 30f));
            Slider sfxSlider = CreateSlider(root.transform, "SfxVolumeSlider", 0f, 1f, 1f);
            AnchorCenter(sfxSlider.gameObject, new Vector2(0f, 50f), new Vector2(400f, 30f));
            Slider sensitivitySlider = CreateSlider(root.transform, "MouseSensitivitySlider", 0.1f, 3f, 1f);
            AnchorCenter(sensitivitySlider.gameObject, new Vector2(0f, 0f), new Vector2(400f, 30f));

            Toggle subtitlesToggle = CreateToggle(root.transform, "SubtitlesToggle", true);
            AnchorCenter(subtitlesToggle.gameObject, new Vector2(-100f, -60f), new Vector2(200f, 30f));
            Toggle invertYToggle = CreateToggle(root.transform, "InvertYToggle", false);
            AnchorCenter(invertYToggle.gameObject, new Vector2(100f, -60f), new Vector2(200f, 30f));

            Button backButton = CreateButton(root.transform, "BackButton", "Back");
            AnchorCenter(backButton.gameObject, new Vector2(0f, -140f), new Vector2(160f, 50f));

            settingsPanel = root.AddComponent<SettingsPanel>();
            SetField(settingsPanel, "root", root);
            SetField(settingsPanel, "micDeviceDropdown", micDropdown);
            SetField(settingsPanel, "masterVolumeSlider", masterSlider);
            SetField(settingsPanel, "voiceVolumeSlider", voiceSlider);
            SetField(settingsPanel, "sfxVolumeSlider", sfxSlider);
            SetField(settingsPanel, "mouseSensitivitySlider", sensitivitySlider);
            SetField(settingsPanel, "subtitlesToggle", subtitlesToggle);
            SetField(settingsPanel, "invertYToggle", invertYToggle);
            SetField(settingsPanel, "backButton", backButton);
            SetField(settingsPanel, "mic", mic);
            root.SetActive(false);
            return root;
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

            // Flat 2D menu, deliberately — no 3D geometry anywhere in this
            // scene. Orthographic + Solid Color + a UI-only culling mask is
            // what makes "the camera can only see the main menu" literally
            // true, rather than a perspective camera pointed at whatever
            // happens to still be active in the shared additive world.
            GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 0f, -10f);
            cameraGo.transform.rotation = Quaternion.identity;
            Camera camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.04f, 0.045f, 0.06f);
            camera.cullingMask = LayerMask.GetMask("UI");
            camera.depth = -1f;

            GameObject storm = new GameObject("StormAmbience", typeof(AudioSource));
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

            GameObject canvasGo = CreateCanvasRoot("Canvas", 0);
            GameObject panel = new GameObject("MenuPanel", typeof(RectTransform), typeof(VerticalLayoutGroup));
            panel.transform.SetParent(canvasGo.transform, false);
            AnchorCenter(panel, new Vector2(0f, -60f), new Vector2(420f, 460f));
            VerticalLayoutGroup layout = panel.GetComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            Text title = CreateText(canvasGo.transform, "Title", "FALSE POSITIVE", 56);
            title.fontStyle = FontStyle.Bold;
            title.color = new Color(0.85f, 0.86f, 0.9f);
            AnchorTopStretch(title.gameObject, -80f, 80f);

            Button playButton = CreateButton(panel.transform, "PlayButton", "Play");
            SetLayoutHeight(playButton.gameObject, 64f);

            Button offlineButton = CreateButton(panel.transform, "OfflineButton", "Offline demo");
            SetLayoutHeight(offlineButton.gameObject, 64f);
            Text offlineCaption = CreateText(panel.transform, "OfflineCaption",
                "Plays the full story with a scripted interrogation — no voice service required.", 16);
            offlineCaption.color = new Color(0.7f, 0.72f, 0.78f);
            offlineCaption.fontStyle = FontStyle.Italic;
            SetLayoutHeight(offlineCaption.gameObject, 40f);

            Button settingsButton = CreateButton(panel.transform, "SettingsButton", "Settings");
            SetLayoutHeight(settingsButton.gameObject, 64f);
            Button quitButton = CreateButton(panel.transform, "QuitButton", "Quit");
            SetLayoutHeight(quitButton.gameObject, 64f);

            MainMenuController controller = canvasGo.AddComponent<MainMenuController>();
            SetField(controller, "playButton", playButton);
            SetField(controller, "offlineButton", offlineButton);
            SetField(controller, "settingsButton", settingsButton);
            SetField(controller, "quitButton", quitButton);

            // No EventSystem here by design — _Persistent's is never unloaded
            // and already covers every additively-loaded scene's canvases.
            // MainMenuController.Awake also carries a runtime safety net that
            // creates one if none exists, for the case where this scene is
            // played on its own (PersistentSceneBootstrap normally prevents
            // that from being necessary, but it costs nothing as a backstop).

            SaveScene(scene, MainMenuScenePath);
            Debug.Log("[ProjectBootstrapBuilder] MainMenu.unity rebuilt.");
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
    }
}
