// ============================================================
// ARLabSceneSetup.cs  —  Editor Only
// Run from menu:  AR Lab ▶ Setup Full Scene
//
// What this does:
//  1. Finds/uses existing ARCamera + ImageTarget in scene
//  2. Fixes VideoPlayer: render mode, RenderTexture, no PlayOnAwake
//  3. Creates ExperimentRoot with MagnesiumExperiment under ImageTarget
//  4. Creates World-Space Canvas UI under ImageTarget with all panels
//  5. Creates a GameManager in the scene root
//  6. Wires all serialized references between every manager
//  7. Positions everything correctly relative to the ImageTarget
// ============================================================
#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;
using ARVirtualLab.Core;
using ARVirtualLab.AR;
using ARVirtualLab.Video;
using ARVirtualLab.Experiments;
using ARVirtualLab.Experiments.MagnesiumRibbon;
using ARVirtualLab.Results;
using ARVirtualLab.AI;
using ARVirtualLab.UI;

namespace ARVirtualLab.Editor
{
    public static class ARLabSceneSetup
    {
        private const string MENU = "AR Lab/";

        [MenuItem(MENU + "Setup Full Scene", priority = 1)]
        public static void SetupFullScene()
        {
            Debug.Log("=== AR Lab Scene Setup START ===");

            // ── Step 1: Find essential existing objects ────────────
            GameObject imageTarget = FindByName("ImageTarget");
            if (imageTarget == null)
            {
                Debug.LogError("ImageTarget not found in scene. Make sure a Vuforia ImageTarget exists.");
                return;
            }

            GameObject videoScreen = FindInChildren(imageTarget, "VideoScreen");
            GameObject videoPlayerGO = FindInChildren(imageTarget, "VideoPlayer");

            // ── Step 2: Fix VideoPlayer ───────────────────────────
            FixVideoPlayer(videoPlayerGO, videoScreen, imageTarget);

            // ── Step 3: Create ExperimentRoot ─────────────────────
            GameObject experimentRoot = GetOrCreate("ExperimentRoot", imageTarget.transform);
            experimentRoot.transform.localPosition = Vector3.zero;
            experimentRoot.transform.localRotation = Quaternion.identity;

            MagnesiumExperiment magExp = experimentRoot.GetComponent<MagnesiumExperiment>();
            if (magExp == null) magExp = experimentRoot.AddComponent<MagnesiumExperiment>();

            ExperimentManager expMgr = experimentRoot.GetComponent<ExperimentManager>();
            if (expMgr == null) expMgr = experimentRoot.AddComponent<ExperimentManager>();
            expMgr.experimentRoot       = experimentRoot;
            expMgr.magnesiumExperiment  = magExp;

            // ── Step 4: Create UI Canvas ──────────────────────────
            var (uiRoot, uiCtrl) = BuildUI(imageTarget);

            // Connect UIController refs to experiment.
            magExp.uiController = uiCtrl;

            // ── Step 5: Results & AI panels ───────────────────────
            ExperimentResultManager resultMgr = uiRoot.GetComponent<ExperimentResultManager>();
            if (resultMgr == null) resultMgr = uiRoot.AddComponent<ExperimentResultManager>();
            resultMgr.experimentManager = expMgr;

            AIExplanationManager aiMgr = uiRoot.GetComponent<AIExplanationManager>();
            if (aiMgr == null) aiMgr = uiRoot.AddComponent<AIExplanationManager>();

            // ── Step 6: ImageTargetManager on ImageTarget ─────────
            ImageTargetManager imgTgtMgr = imageTarget.GetComponent<ImageTargetManager>();
            if (imgTgtMgr == null) imgTgtMgr = imageTarget.AddComponent<ImageTargetManager>();

            // ── Step 7: GameManager (scene root) ──────────────────
            GameObject gmGO = GetOrCreate("GameManager", null);
            gmGO.transform.localPosition = Vector3.zero;
            GameManager gm = gmGO.GetComponent<GameManager>();
            if (gm == null) gm = gmGO.AddComponent<GameManager>();

            // Wire manager refs.
            gm.imageTargetManager = imgTgtMgr;
            gm.videoController    = videoPlayerGO != null
                ? videoPlayerGO.GetComponent<ARVideoController>() : null;
            gm.experimentManager  = expMgr;
            gm.resultManager      = resultMgr;
            gm.aiManager          = aiMgr;
            gm.uiController       = uiCtrl;

            if (gm.videoController != null)
                gm.videoController.uiController = uiCtrl;

            // ── Step 8: Position VideoScreen ──────────────────────
            if (videoScreen != null)
            {
                videoScreen.transform.localPosition = new Vector3(0f, 0.35f, 0f);
                videoScreen.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
                videoScreen.transform.localScale    = new Vector3(0.56f, 0.315f, 1f);
                videoScreen.SetActive(false); // Hidden until target detected
            }

            // ── Finish ────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("=== AR Lab Scene Setup COMPLETE ===");
            Debug.Log("Next steps: File > Save, then Build & Run to Android.");
            EditorUtility.DisplayDialog("AR Lab Setup",
                "Scene setup complete!\n\n" +
                "1. File → Save (Ctrl+S)\n" +
                "2. Assign your instructional MP4 to the VideoPlayer clip field.\n" +
                "3. File → Build Settings → Android → Build & Run.",
                "OK");
        }

        // ── VideoPlayer Fix ───────────────────────────────────────
        private static void FixVideoPlayer(
            GameObject vpGO, GameObject videoScreen, GameObject imageTarget)
        {
            if (vpGO == null)
            {
                vpGO = new GameObject("VideoPlayer");
                vpGO.transform.SetParent(imageTarget.transform, false);
                vpGO.transform.localPosition = Vector3.zero;
            }

            VideoPlayer vp = vpGO.GetComponent<VideoPlayer>();
            if (vp == null) vp = vpGO.AddComponent<VideoPlayer>();

            // ── Create / fix RenderTexture ────────────────────────
            RenderTexture rt = AssetDatabase.LoadAssetAtPath<RenderTexture>(
                "Assets/VideoTexture.renderTexture");
            if (rt == null)
            {
                rt = new RenderTexture(1920, 1080, 0, RenderTextureFormat.ARGB32);
                rt.name = "VideoTexture";
                AssetDatabase.CreateAsset(rt, "Assets/VideoTexture.renderTexture");
                AssetDatabase.SaveAssets();
                Debug.Log("[Setup] Created new RenderTexture at Assets/VideoTexture.renderTexture");
            }
            if (!rt.IsCreated()) rt.Create();

            // ── Configure VideoPlayer ─────────────────────────────
            vp.renderMode       = VideoRenderMode.RenderTexture;
            vp.targetTexture    = rt;
            vp.playOnAwake      = false;
            vp.waitForFirstFrame = true;
            vp.audioOutputMode  = VideoAudioOutputMode.AudioSource;
            vp.isLooping        = false;

            AudioSource asSrc = vpGO.GetComponent<AudioSource>();
            if (asSrc == null) asSrc = vpGO.AddComponent<AudioSource>();
            asSrc.playOnAwake = false;
            vp.SetTargetAudioSource(0, asSrc);

            // ── Fix VideoScreen material ──────────────────────────
            if (videoScreen != null)
            {
                Renderer rend = videoScreen.GetComponent<Renderer>();
                if (rend != null)
                {
                    Material mat = rend.sharedMaterial;
                    if (mat == null)
                    {
                        mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                        if (mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                            mat = new Material(Shader.Find("Unlit/Texture"));
                        AssetDatabase.CreateAsset(mat, "Assets/VideoMaterial.mat");
                        AssetDatabase.SaveAssets();
                        rend.sharedMaterial = mat;
                    }
                    mat.mainTexture = rt;
                    EditorUtility.SetDirty(mat);
                    Debug.Log("[Setup] VideoScreen material updated with RenderTexture.");
                }
            }

            // Add ARVideoController.
            ARVideoController avc = vpGO.GetComponent<ARVideoController>();
            if (avc == null) avc = vpGO.AddComponent<ARVideoController>();
            avc.videoPlayer   = vp;
            avc.videoScreen   = videoScreen;
            avc.renderTexture = rt;

            EditorUtility.SetDirty(vpGO);
            Debug.Log("[Setup] VideoPlayer configured successfully.");
        }

        // ── UI Builder ────────────────────────────────────────────
        private static (GameObject root, ARUIController ctrl) BuildUI(GameObject imageTarget)
        {
            // World-Space Canvas parented to ImageTarget.
            GameObject uiRoot = GetOrCreate("UIRoot", imageTarget.transform);
            uiRoot.transform.localPosition = new Vector3(0f, 0.01f, 0f);
            uiRoot.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            uiRoot.transform.localScale    = new Vector3(0.001f, 0.001f, 0.001f);

            Canvas canvas = uiRoot.GetComponent<Canvas>();
            if (canvas == null) canvas = uiRoot.AddComponent<Canvas>();
            canvas.renderMode       = RenderMode.WorldSpace;
            canvas.worldCamera      = Camera.main;

            CanvasScaler cs = uiRoot.GetComponent<CanvasScaler>();
            if (cs == null) cs = uiRoot.AddComponent<CanvasScaler>();
            cs.dynamicPixelsPerUnit = 10f;

            uiRoot.GetOrAddComponent<GraphicRaycaster>();

            RectTransform rt = uiRoot.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(800, 600);

            // ── ARUIController ─────────────────────────────────────
            ARUIController uiCtrl = uiRoot.GetComponent<ARUIController>();
            if (uiCtrl == null) uiCtrl = uiRoot.AddComponent<ARUIController>();
            // ── Scanning Overlay (Screen-Space Canvas) ───────────
            GameObject scanOverlay = GetOrCreate("ScanningOverlay", null);
            Canvas scanCanvas = GetOrAddComponent<Canvas>(scanOverlay);
            scanCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            GetOrAddComponent<CanvasScaler>(scanOverlay);
            GetOrAddComponent<GraphicRaycaster>(scanOverlay);
            
            GameObject scanPanel = GetOrCreate("Panel", scanOverlay.transform);
            RectTransform panelRT = scanPanel.GetComponent<RectTransform>();
            if (panelRT == null) panelRT = scanPanel.AddComponent<RectTransform>();
            panelRT.anchorMin = new Vector2(0f, 0.85f); // top 15% of screen
            panelRT.anchorMax = new Vector2(1f, 1f);
            panelRT.pivot = new Vector2(0.5f, 0.5f);
            panelRT.offsetMin = Vector2.zero;
            panelRT.offsetMax = Vector2.zero;
            AddBackground(scanPanel, new Color(0f, 0f, 0f, 0.65f));

            TMP_Text scanText = CreateTextChild(scanPanel, "ScanText",
                "Point camera at the Magnesium Ribbon marker",
                26, TextAlignmentOptions.Center);
            RectTransform textRT = scanText.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;
            scanText.color = Color.yellow;
            
            uiCtrl.scanningOverlay = scanOverlay;
            scanOverlay.SetActive(false);

            // ── Main Menu Panel ───────────────────────────────────
            GameObject menuPanel = BuildMenuPanel(uiRoot, uiCtrl);

            // ── Video Control Panel ───────────────────────────────
            GameObject videoPanel = BuildVideoPanel(uiRoot, uiCtrl, imageTarget);

            // ── Experiment Step Panel ─────────────────────────────
            GameObject stepPanel = BuildStepPanel(uiRoot, uiCtrl);

            // ── Results Panel ─────────────────────────────────────
            GameObject resultsPanel = BuildResultsPanel(uiRoot, uiCtrl);
            ExperimentResultManager resMgr = resultsPanel.GetComponent<ExperimentResultManager>();
            if (resMgr == null) resMgr = resultsPanel.AddComponent<ExperimentResultManager>();
            uiRoot.GetComponent<ExperimentResultManager>()?.Equals(null); // suppress
            // Attach to uiRoot instead.
            ExperimentResultManager uiRootResMgr = uiRoot.GetComponent<ExperimentResultManager>();
            if (uiRootResMgr == null) uiRootResMgr = uiRoot.AddComponent<ExperimentResultManager>();
            uiRootResMgr.resultsPanel = resultsPanel;

            // ── AI Panel ─────────────────────────────────────────
            GameObject aiPanel = BuildAIPanel(uiRoot, uiCtrl);
            AIExplanationManager uiRootAIMgr = uiRoot.GetComponent<AIExplanationManager>();
            if (uiRootAIMgr == null) uiRootAIMgr = uiRoot.AddComponent<AIExplanationManager>();
            uiRootAIMgr.aiPanel = aiPanel;

            return (uiRoot, uiCtrl);
        }

        // ── Menu Panel ────────────────────────────────────────────
        private static GameObject BuildMenuPanel(GameObject parent, ARUIController ctrl)
        {
            GameObject panel = GetOrCreate("MainMenuPanel", parent.transform);
            SetRectFull(panel, new Vector2(800, 600));
            AddBackground(panel, new Color(0.05f, 0.07f, 0.15f, 0.9f));

            TMP_Text title = CreateTextChild(panel, "Title",
                "AR Physics & Chemistry Lab", 52, TextAlignmentOptions.Center);
            title.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 240);
            title.color = new Color(0.4f, 0.85f, 1f);

            TMP_Text subtitle = CreateTextChild(panel, "Subtitle",
                "Experiment: Burning Magnesium Ribbon", 30, TextAlignmentOptions.Center);
            subtitle.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 190);
            subtitle.color = Color.white;

            // Menu buttons.
            float startY = 120f;
            float spacing = 65f;
            (string label, string name)[] menuItems = new[]
            {
                ("▶  Watch Explanation",   "WatchVideoBtn"),
                ("🔬  Perform Experiment", "PerformExpBtn"),
                ("📋  Experiment Steps",   "StepsBtn"),
                ("📊  Observe Result",     "ResultBtn"),
                ("🤖  AI Explanation",     "AIBtn"),
                ("↺  Reset Experiment",    "ResetBtn"),
            };

            Button[] buttons = new Button[menuItems.Length];
            for (int i = 0; i < menuItems.Length; i++)
            {
                var btn = CreateButton(panel, menuItems[i].name, menuItems[i].label,
                    new Vector2(0, startY - i * spacing),
                    new Vector2(620, 55));
                buttons[i] = btn;
            }

            ctrl.mainMenuPanel          = panel;
            ctrl.watchVideoButton        = buttons[0];
            ctrl.performExperimentButton = buttons[1];
            ctrl.experimentStepsButton   = buttons[2];
            ctrl.observeResultButton     = buttons[3];
            ctrl.aiExplanationButton     = buttons[4];
            ctrl.resetExperimentButton   = buttons[5];

            panel.SetActive(false);
            return panel;
        }

        // ── Video Control Panel ───────────────────────────────────
        private static GameObject BuildVideoPanel(
            GameObject uiRoot, ARUIController ctrl, GameObject imageTarget)
        {
            // A thin strip panel positioned below VideoScreen.
            GameObject panel = GetOrCreate("VideoControlPanel", uiRoot.transform);
            SetRectFull(panel, new Vector2(800, 120));
            panel.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -240);
            AddBackground(panel, new Color(0.0f, 0.0f, 0.0f, 0.75f));

            (string label, string name)[] btns = new[]
            {
                ("▶ Play",    "VidPlayBtn"),
                ("⏸ Pause",  "VidPauseBtn"),
                ("↺ Replay",  "VidReplayBtn"),
                ("⏭ Menu",   "VidContinueBtn"),
            };

            Button[] buttons = new Button[btns.Length];
            float x = -280f;
            for (int i = 0; i < btns.Length; i++)
            {
                buttons[i] = CreateButton(panel, btns[i].name, btns[i].label,
                    new Vector2(x, 0), new Vector2(160, 60));
                x += 185f;
            }

            // "Start Experiment" button (hidden until video ends).
            Button startExpBtn = CreateButton(panel, "StartExpBtn", "🔬 Start Experiment",
                new Vector2(0, -55), new Vector2(500, 60));
            startExpBtn.GetComponent<Image>().color = new Color(0.1f, 0.6f, 0.1f);
            startExpBtn.gameObject.SetActive(false);

            ctrl.videoControlPanel         = panel;
            ctrl.videoPlayButton            = buttons[0];
            ctrl.videoPauseButton           = buttons[1];
            ctrl.videoReplayButton          = buttons[2];
            ctrl.videoContinueButton        = buttons[3];
            ctrl.videoStartExperimentButton = startExpBtn;

            // Wire video buttons to ARVideoController.
            ARVideoController avc = imageTarget.GetComponentInChildren<ARVideoController>(true);
            if (avc != null)
            {
                buttons[0].onClick.AddListener(avc.OnPlayButton);
                buttons[1].onClick.AddListener(avc.OnPauseButton);
                buttons[2].onClick.AddListener(avc.OnReplayButton);
            }

            panel.SetActive(false);
            return panel;
        }

        // ── Experiment Step Panel ─────────────────────────────────
        private static GameObject BuildStepPanel(GameObject parent, ARUIController ctrl)
        {
            GameObject panel = GetOrCreate("ExperimentStepPanel", parent.transform);
            SetRectFull(panel, new Vector2(800, 600));
            AddBackground(panel, new Color(0.03f, 0.05f, 0.12f, 0.92f));

            TMP_Text stepCounter = CreateTextChild(panel, "StepCounter",
                "Step 0 / 7", 34, TextAlignmentOptions.Left);
            stepCounter.GetComponent<RectTransform>().anchoredPosition = new Vector2(-260, 260);
            stepCounter.color = new Color(0.4f, 0.85f, 1f);

            TMP_Text stepDesc = CreateTextChild(panel, "StepDescription",
                "Tap 'Next Step' to begin.", 28, TextAlignmentOptions.Left);
            RectTransform sdRT = stepDesc.GetComponent<RectTransform>();
            sdRT.anchoredPosition = new Vector2(0, 80);
            sdRT.sizeDelta        = new Vector2(740, 300);
            stepDesc.color         = Color.white;
            stepDesc.enableWordWrapping = true;

            TMP_Text eqText = CreateTextChild(panel, "EquationText",
                "", 36, TextAlignmentOptions.Center);
            eqText.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -80);
            eqText.color = new Color(1f, 0.85f, 0.3f);
            eqText.gameObject.SetActive(false);

            TMP_Text obsText = CreateTextChild(panel, "ObservationText",
                "", 24, TextAlignmentOptions.Left);
            RectTransform obsRT = obsText.GetComponent<RectTransform>();
            obsRT.anchoredPosition = new Vector2(0, -120);
            obsRT.sizeDelta        = new Vector2(740, 120);
            obsText.color          = new Color(0.8f, 1f, 0.8f);
            obsText.enableWordWrapping = true;
            obsText.gameObject.SetActive(false);

            // Feedback Toast.
            GameObject toastGO = GetOrCreate("FeedbackToast", panel.transform);
            toastGO.transform.localPosition = new Vector3(0, -200, 0);
            CanvasGroup cg = GetOrAddComponent<CanvasGroup>(toastGO);
            cg.alpha = 0f;
            TMP_Text toastText = CreateTextChild(toastGO, "ToastText",
                "", 28, TextAlignmentOptions.Center);
            toastText.color = new Color(0.3f, 1f, 0.4f);
            RectTransform toastRT = toastGO.GetComponent<RectTransform>();
            if (toastRT == null) toastRT = toastGO.AddComponent<RectTransform>();
            toastRT.sizeDelta = new Vector2(700, 60);

            // Bottom buttons.
            Button nextBtn  = CreateButton(panel, "NextStepBtn", "Next Step ▶",
                new Vector2(-160, -255), new Vector2(250, 60));
            Button resetBtn = CreateButton(panel, "ResetBtn", "↺ Reset",
                new Vector2(100, -255), new Vector2(200, 60));
            resetBtn.GetComponent<Image>().color = new Color(0.6f, 0.1f, 0.1f);

            Button viewResBtn = CreateButton(panel, "ViewResultsBtn", "📊 View Results",
                new Vector2(0, -255), new Vector2(280, 60));
            viewResBtn.GetComponent<Image>().color = new Color(0.1f, 0.4f, 0.1f);
            viewResBtn.gameObject.SetActive(false);

            ctrl.experimentStepPanel  = panel;
            ctrl.nextStepButton        = nextBtn;
            ctrl.resetButton           = resetBtn;
            ctrl.viewResultsButton     = viewResBtn;
            ctrl.stepCounterText       = stepCounter;
            ctrl.feedbackText          = stepDesc;
            ctrl.feedbackToastGroup    = cg;
            ctrl.feedbackToastText     = toastText;

            // Set MagnesiumExperiment text refs.
            MagnesiumExperiment magExp = GameObject.FindObjectOfType<MagnesiumExperiment>();
            if (magExp != null)
            {
                magExp.stepDescriptionText = stepDesc;
                magExp.equationText        = eqText;
                magExp.observationText     = obsText;
            }

            panel.SetActive(false);
            return panel;
        }

        // ── Results Panel ─────────────────────────────────────────
        private static GameObject BuildResultsPanel(GameObject parent, ARUIController ctrl)
        {
            GameObject panel = GetOrCreate("ResultsPanel", parent.transform);
            SetRectFull(panel, new Vector2(800, 600));
            AddBackground(panel, new Color(0.02f, 0.06f, 0.04f, 0.95f));

            TMP_Text title = CreateTextChild(panel, "ResultsTitle",
                "Experiment Results", 48, TextAlignmentOptions.Center);
            title.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 258);
            title.color = new Color(0.4f, 1f, 0.5f);

            string[] fieldNames = { "ExpNameText","MaterialsText","ProcedureText",
                                    "ObservationText","EquationText","ProductText","SummaryText" };
            string[] labels = { "","","","","","","" };
            float yStart = 195f;
            TMP_Text[] fields = new TMP_Text[fieldNames.Length];
            for (int i = 0; i < fieldNames.Length; i++)
            {
                TMP_Text t = CreateTextChild(panel, fieldNames[i], labels[i], 22, TextAlignmentOptions.Left);
                RectTransform r = t.GetComponent<RectTransform>();
                r.anchoredPosition = new Vector2(0, yStart - i * 62f);
                r.sizeDelta        = new Vector2(760, 55);
                t.enableWordWrapping = true;
                t.color = Color.white;
                fields[i] = t;
            }

            // Restart + AI buttons.
            Button restartBtn = CreateButton(panel, "RestartExpBtn", "↺ Restart Experiment",
                new Vector2(-170, -270), new Vector2(300, 60));

            Button askAIBtn = CreateButton(panel, "AskAIBtn", "🤖 Ask AI",
                new Vector2(150, -270), new Vector2(240, 60));
            askAIBtn.GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.55f);
            askAIBtn.onClick.AddListener(() => Core.GameManager.Instance?.RequestShowAI());
            restartBtn.onClick.AddListener(() => Core.GameManager.Instance?.RequestResetExperiment());

            ctrl.resultsPanel = panel;

            // Wire ResultManager fields.
            ExperimentResultManager resMgr = panel.GetComponent<ExperimentResultManager>();
            if (resMgr == null) resMgr = panel.AddComponent<ExperimentResultManager>();
            resMgr.resultsPanel       = panel;
            resMgr.experimentNameText = fields[0];
            resMgr.materialsText      = fields[1];
            resMgr.procedureText      = fields[2];
            resMgr.observationText    = fields[3];
            resMgr.equationText       = fields[4];
            resMgr.finalProductText   = fields[5];
            resMgr.resultSummaryText  = fields[6];

            panel.SetActive(false);
            return panel;
        }

        // ── AI Panel ─────────────────────────────────────────────
        private static GameObject BuildAIPanel(GameObject parent, ARUIController ctrl)
        {
            GameObject panel = GetOrCreate("AIPanel", parent.transform);
            SetRectFull(panel, new Vector2(800, 600));
            AddBackground(panel, new Color(0.05f, 0.02f, 0.15f, 0.95f));

            TMP_Text title = CreateTextChild(panel, "AITitle",
                "🤖 Gemini AI Assistant", 44, TextAlignmentOptions.Center);
            title.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 260);
            title.color = new Color(0.7f, 0.5f, 1f);

            TMP_Text status = CreateTextChild(panel, "AIStatus",
                "Ask a question about the experiment.", 24, TextAlignmentOptions.Center);
            status.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 210);
            status.color = new Color(0.7f, 0.7f, 0.9f);

            // Quick question buttons.
            string[] qs = {
                "Why does Mg burn brightly?",
                "What is formed?",
                "Type of reaction?",
                "Simple explanation"
            };
            Button[] qBtns = new Button[qs.Length];
            float qx = -370f;
            for (int i = 0; i < qs.Length; i++)
            {
                qBtns[i] = CreateButton(panel, "Q" + (i + 1) + "Btn", qs[i],
                    new Vector2(qx + i * 248f, 155), new Vector2(235, 50));
                qBtns[i].GetComponent<Image>().color = new Color(0.2f, 0.15f, 0.4f);
            }

            // Response area.
            TMP_Text responseText = CreateTextChild(panel, "AIResponse",
                "Your answer will appear here.", 26, TextAlignmentOptions.Left);
            RectTransform respRT = responseText.GetComponent<RectTransform>();
            respRT.anchoredPosition = new Vector2(0, 10);
            respRT.sizeDelta        = new Vector2(760, 250);
            responseText.color          = Color.white;
            responseText.enableWordWrapping = true;

            // Input field for custom questions.
            GameObject inputGO = new GameObject("QuestionInput");
            inputGO.transform.SetParent(panel.transform, false);
            RectTransform inputRT = inputGO.AddComponent<RectTransform>();
            inputRT.anchoredPosition = new Vector2(0, -170);
            inputRT.sizeDelta        = new Vector2(600, 55);

            Image inputBG = inputGO.AddComponent<Image>();
            inputBG.color = new Color(0.1f, 0.1f, 0.2f, 1f);
            TMP_InputField inputField = inputGO.AddComponent<TMP_InputField>();

            // Input text child.
            GameObject inputTextGO = new GameObject("InputText");
            inputTextGO.transform.SetParent(inputGO.transform, false);
            TMP_Text inputText = inputTextGO.AddComponent<TMP_Text>() as TMP_Text;
            if (inputText == null) inputText = inputTextGO.AddComponent<TextMeshProUGUI>();
            inputText.fontSize = 26;
            inputText.color    = Color.white;
            RectTransform itRT = inputText.GetComponent<RectTransform>();
            itRT.anchorMin = Vector2.zero;
            itRT.anchorMax = Vector2.one;
            itRT.offsetMin = new Vector2(8, 4);
            itRT.offsetMax = new Vector2(-8, -4);
            inputField.textComponent = inputText;

            Button submitBtn = CreateButton(panel, "SubmitQuestionBtn", "Ask ▶",
                new Vector2(350, -170), new Vector2(150, 55));
            submitBtn.GetComponent<Image>().color = new Color(0.3f, 0.1f, 0.6f);

            Button backBtn = CreateButton(panel, "AIBackBtn", "← Back",
                new Vector2(-170, -270), new Vector2(250, 60));
            backBtn.onClick.AddListener(() => Core.GameManager.Instance?.RequestBackToMenu());

            ctrl.aiPanel = panel;

            // Wire AIExplanationManager.
            AIExplanationManager aiMgr = panel.GetComponent<AIExplanationManager>();
            if (aiMgr == null) aiMgr = panel.AddComponent<AIExplanationManager>();
            aiMgr.aiPanel       = panel;
            aiMgr.responseText  = responseText;
            aiMgr.questionInput = inputField;
            aiMgr.statusText    = status;
            aiMgr.q1Button      = qBtns[0];
            aiMgr.q2Button      = qBtns[1];
            aiMgr.q3Button      = qBtns[2];
            aiMgr.q4Button      = qBtns[3];
            submitBtn.onClick.AddListener(aiMgr.OnSubmitQuestion);

            panel.SetActive(false);
            return panel;
        }

        // ── Utility Helpers ───────────────────────────────────────
        private static GameObject GetOrCreate(string name, Transform parent)
        {
            Transform found = parent != null
                ? parent.Find(name)
                : GameObject.Find(name)?.transform;

            if (found != null) return found.gameObject;

            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject FindByName(string name)
        {
            return GameObject.Find(name);
        }

        private static GameObject FindInChildren(GameObject parent, string name)
        {
            if (parent == null) return null;
            Transform t = parent.transform.Find(name);
            if (t != null) return t.gameObject;

            // Deep search.
            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;

            return null;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }

        private static void SetRectFull(GameObject go, Vector2 size)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin  = new Vector2(0.5f, 0.5f);
            rt.anchorMax  = new Vector2(0.5f, 0.5f);
            rt.pivot      = new Vector2(0.5f, 0.5f);
            rt.sizeDelta  = size;
            rt.anchoredPosition = Vector2.zero;
        }

        private static void AddBackground(GameObject go, Color color)
        {
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = color;
        }

        private static TMP_Text CreateTextChild(
            GameObject parent, string name, string text,
            int fontSize, TextAlignmentOptions align)
        {
            GameObject go = GetOrCreate(name, parent.transform);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text              = text;
            tmp.fontSize          = fontSize;
            tmp.alignment         = align;
            tmp.color             = Color.white;
            tmp.enableWordWrapping = true;

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(700, 50);
            rt.anchoredPosition = Vector2.zero;
            return tmp;
        }

        private static Button CreateButton(
            GameObject parent, string name, string label,
            Vector2 anchoredPos, Vector2 size)
        {
            GameObject go = GetOrCreate(name, parent.transform);

            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta        = size;

            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = new Color(0.15f, 0.35f, 0.65f, 1f);

            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            ColorBlock cb = btn.colors;
            cb.highlightedColor = new Color(0.25f, 0.5f, 0.85f);
            cb.pressedColor     = new Color(0.05f, 0.2f, 0.45f);
            btn.colors = cb;

            // Label child.
            GameObject labelGO = GetOrCreate("Label", go.transform);
            TextMeshProUGUI tmp = labelGO.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = labelGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 28;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color     = Color.white;

            RectTransform labelRT = labelGO.GetComponent<RectTransform>();
            if (labelRT == null) labelRT = labelGO.AddComponent<RectTransform>();
            labelRT.anchorMin        = Vector2.zero;
            labelRT.anchorMax        = Vector2.one;
            labelRT.offsetMin        = new Vector2(8, 4);
            labelRT.offsetMax        = new Vector2(-8, -4);

            return btn;
        }

        // ── Additional Menus ──────────────────────────────────────
        [MenuItem(MENU + "Fix VideoPlayer Only", priority = 10)]
        public static void FixVideoOnly()
        {
            GameObject imageTarget = FindByName("ImageTarget");
            if (imageTarget == null) { Debug.LogError("ImageTarget not found."); return; }
            GameObject vpGO = FindInChildren(imageTarget, "VideoPlayer");
            GameObject vsGO = FindInChildren(imageTarget, "VideoScreen");
            FixVideoPlayer(vpGO, vsGO, imageTarget);
            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            Debug.Log("VideoPlayer fix applied. Save the scene.");
        }

        [MenuItem(MENU + "Select GameManager", priority = 20)]
        public static void SelectGameManager()
        {
            GameObject gm = GameObject.Find("GameManager");
            if (gm != null) Selection.activeGameObject = gm;
            else Debug.Log("GameManager not found. Run 'Setup Full Scene' first.");
        }
    }
}

// Extension helper used by ARLabSceneSetup.
public static class GOExtensions
{
    public static T GetOrAddComponent<T>(this GameObject go) where T : Component
    {
        T comp = go.GetComponent<T>();
        return comp != null ? comp : go.AddComponent<T>();
    }
}

#endif
