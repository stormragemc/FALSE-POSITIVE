using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using FalsePositive.Audio;
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
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

        private static readonly DefaultControls.Resources UIResources = new DefaultControls.Resources();

        [MenuItem("Tools/False Positive/Bootstrap/Build Everything (1-6)")]
        public static void BuildEverything()
        {
            BuildConfigAssets();
            BuildPersistentScene();
            BuildMainMenuScene();
            FixInterrogationScene();
            MemorySceneBuilder.BuildMemoryScenes();
            RewriteBuildSettings();
            CutsceneRecipeBuilder.PopulateRecipes();
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
            AnchorTopRight(micIndGo, new Vector2(-160f, -30f), new Vector2(300f, 40f));
            Text micIndText = CreateText(micIndGo.transform, "Label", "Microphone inactive", 20);
            StretchFill(micIndText.gameObject);
            Image micIndDot = CreateDotImage(micIndGo.transform, "Dot", new Vector2(-150f, 0f));
            MicIndicator micIndicator = micIndGo.AddComponent<MicIndicator>();
            SetField(micIndicator, "mic", mic);
            SetField(micIndicator, "vad", vad);
            SetField(micIndicator, "label", micIndText);
            SetField(micIndicator, "dot", micIndDot);

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

            GameObject outcomeScreen = new GameObject("OutcomeScreen", typeof(RectTransform));
            outcomeScreen.transform.SetParent(hud.transform, false);
            StretchFill(outcomeScreen);
            outcomeScreen.SetActive(false);

            // CutsceneDirector (B1) — same-scene wiring only, per its own
            // doc-comment: never a camera/object from whichever memory or
            // interrogation scene happens to be active.
            GameObject cutsceneGo = new GameObject("CutsceneDirector");
            GameObject voSourceGo = new GameObject("CutsceneVoSource", typeof(AudioSource));
            voSourceGo.transform.SetParent(cutsceneGo.transform, false);
            AudioSource cutsceneVoSource = voSourceGo.GetComponent<AudioSource>();
            CutsceneDirector cutsceneDirector = cutsceneGo.AddComponent<CutsceneDirector>();
            SetField(cutsceneDirector, "fader", fader);
            SetField(cutsceneDirector, "subtitles", subtitleUi);
            SetField(cutsceneDirector, "voSource", cutsceneVoSource);

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

            MemoryFlagCatalog catalog = AssetDatabase.LoadAssetAtPath<MemoryFlagCatalog>(ConfigRoot + "MemoryFlagCatalog.asset");
            if (catalog != null) SetField(gfd, "memoryFlagCatalog", catalog);
            else Debug.LogWarning("[ProjectBootstrapBuilder] MemoryFlagCatalog.asset not found yet — run step 3 (Build Config Assets) and re-run step 1, or it will bind on the next pass.");

            SaveScene(scene, PersistentScenePath);
            Debug.Log("[ProjectBootstrapBuilder] _Persistent.unity rebuilt.");
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

            GameObject cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.position = new Vector3(0f, 1.6f, -6f);
            cameraGo.transform.rotation = Quaternion.Euler(5f, 0f, 0f);

            GameObject lightGo = new GameObject("Directional Light", typeof(Light));
            Light light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.4f;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            GameObject environment = new GameObject("Environment");
            GameObject placeholderCabin = GameObject.CreatePrimitive(PrimitiveType.Cube);
            placeholderCabin.name = "CabinExteriorPlaceholder";
            placeholderCabin.transform.SetParent(environment.transform);
            placeholderCabin.transform.position = new Vector3(0f, 0.5f, 0f);
            placeholderCabin.transform.localScale = new Vector3(4f, 3f, 3f);
            ApplyLabelMaterial(placeholderCabin, new Color(0.15f, 0.15f, 0.2f));

            GameObject storm = new GameObject("StormAmbience", typeof(AudioSource));
            AudioSource stormSource = storm.GetComponent<AudioSource>();
            stormSource.loop = true;
            stormSource.playOnAwake = true;
            stormSource.volume = 0.3f;

            GameObject canvasGo = CreateCanvasRoot("Canvas", 0);
            GameObject panel = new GameObject("MenuPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasGo.transform, false);
            AnchorCenter(panel, Vector2.zero, new Vector2(400f, 260f));

            Button playButton = CreateButton(panel.transform, "PlayButton", "Play");
            AnchorTopStretch(playButton.gameObject, -20f, 60f);
            Button settingsButton = CreateButton(panel.transform, "SettingsButton", "Settings");
            AnchorCenter(settingsButton.gameObject, new Vector2(0f, 0f), new Vector2(400f, 60f));
            Button quitButton = CreateButton(panel.transform, "QuitButton", "Quit");
            AnchorBottomStretch(quitButton.gameObject, 20f, 60f);

            MainMenuController controller = canvasGo.AddComponent<MainMenuController>();
            SetField(controller, "playButton", playButton);
            SetField(controller, "settingsButton", settingsButton);
            SetField(controller, "quitButton", quitButton);

            // No EventSystem here — _Persistent's is never unloaded and already
            // covers every additively-loaded scene's canvases. A second one in
            // this scene would just log "2 event systems in the scene".

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

            GameObject phaseGo = GameObject.Find("PhaseDialogueController") ?? new GameObject("PhaseDialogueController");
            PhaseDialogueController phaseController = phaseGo.GetComponent<PhaseDialogueController>() ?? phaseGo.AddComponent<PhaseDialogueController>();
            SetField(phaseController, "prompts", prompts);
            SetField(phaseController, "storyMarksSource", storyMarks);
            SetField(phaseController, "binder", binder);

            EditorSceneManager.MarkSceneDirty(scene);
            SaveScene(scene, InterrogationScenePath);
            Debug.Log("[ProjectBootstrapBuilder] Interrogation.unity fixed up.");
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
