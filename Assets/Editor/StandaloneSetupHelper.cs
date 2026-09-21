// ============================================================
// StandaloneSetupHelper.cs  —  Editor Only
// Sets up the Standalone Virtual Chemistry Lab scene with
// realistic 3D models, proper scale, mobile portrait framing,
// and responsive 1080x1920 Canvas UI.
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARVirtualLab.Standalone;

namespace ARVirtualLab.Editor
{
    public static class StandaloneSetupHelper
    {
        private const string MENU = "AR Lab/";

        [MenuItem(MENU + "Setup Standalone Scene", priority = 2)]
        public static void SetupStandalone()
        {
            Debug.Log("=== Standalone Scene Setup START ===");
            SetupStandaloneInternal();
            Debug.Log("=== Standalone Scene Setup COMPLETE ===");
            EditorUtility.DisplayDialog("Standalone Setup",
                "Realistic Magnesium Ribbon Experiment Scene configured successfully!\n\n" +
                "• Realistic 3D Models & Materials applied.\n" +
                "• Mobile Portrait scale & framing calibrated.\n" +
                "• 1080x1920 UI with full 15-step interactive lab ready.\n\n" +
                "Press Play to test!",
                "OK");
        }

        public static void SetupStandaloneInternal()
        {
            Debug.Log("=== Standalone Scene Setup (Internal) START ===");

            // 1. Ensure 3D assets and materials exist
            LabAssetGenerator.GenerateAllAssets();

            // 2. Setup EventSystem
            SetupEventSystem();

            // 3. Setup Camera and Lighting for Mobile Portrait
            AdjustCameraAndLighting();

            // 4. Setup Root Lab GameObject
            GameObject labRoot = GetOrCreate("StandaloneVirtualLab", null);
            labRoot.transform.position = Vector3.zero;
            labRoot.transform.rotation = Quaternion.identity;

            StandaloneMagnesiumExperiment exp = labRoot.GetComponent<StandaloneMagnesiumExperiment>();
            if (exp == null) exp = labRoot.AddComponent<StandaloneMagnesiumExperiment>();

            // 5. Build Realistic 3D Lab Equipment
            BuildRealisticLabEquipment(labRoot, exp);

            // 6. Build Mobile Portrait Responsive UI (1080 x 1920)
            var (uiRoot, uiCtrl) = BuildStandaloneUI(labRoot);
            exp.uiController = uiCtrl;

            exp.ValidateAndAutoAssignReferences();

            EditorSceneManager.MarkSceneDirty(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Debug.Log("=== Standalone Scene Setup (Internal) COMPLETE ===");
        }

        private static void SetupEventSystem()
        {
            GameObject esGO = GetOrCreate("EventSystem", null);
            GetOrAddComponent<UnityEngine.EventSystems.EventSystem>(esGO);

            var inputModule = esGO.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (inputModule == null)
            {
                var legacyModule = esGO.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
                if (legacyModule == null)
                {
                    esGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                }
            }
        }

        private static void AdjustCameraAndLighting()
        {
            // Setup Camera for Android Mobile Portrait View (9:16)
            Camera mainCam = Camera.main;
            if (mainCam == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                mainCam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
            }
            mainCam.transform.position = new Vector3(0f, 0.28f, -0.32f);
            mainCam.transform.rotation = Quaternion.Euler(38f, 0f, 0f);
            mainCam.orthographic = false;
            mainCam.fieldOfView = 48f;
            mainCam.clearFlags = CameraClearFlags.SolidColor;
            mainCam.backgroundColor = new Color(0.08f, 0.10f, 0.15f, 1f);
            mainCam.nearClipPlane = 0.01f;
            mainCam.farClipPlane = 100f;
            GetOrAddComponent<AudioListener>(mainCam.gameObject);

            try
            {
                GetOrAddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>(mainCam.gameObject);
            }
            catch {}

            // Setup main key light
            Light dirLight = GameObject.Find("Directional Light")?.GetComponent<Light>();
            if (dirLight == null)
            {
                dirLight = GameObject.FindObjectOfType<Light>();
            }
            if (dirLight == null)
            {
                GameObject lightGO = new GameObject("Directional Light");
                dirLight = lightGO.AddComponent<Light>();
                dirLight.type = LightType.Directional;
            }
            dirLight.transform.position = new Vector3(0f, 3f, 0f);
            dirLight.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            dirLight.color = new Color(1f, 0.98f, 0.95f);
            dirLight.intensity = 1.3f;

            // Setup soft fill light for crisp table shadows
            GameObject fillLightGO = GetOrCreate("Fill Light", null);
            Light fillLight = GetOrAddComponent<Light>(fillLightGO);
            fillLight.type = LightType.Directional;
            fillLight.transform.rotation = Quaternion.Euler(40f, 150f, 0f);
            fillLight.color = new Color(0.85f, 0.92f, 1f);
            fillLight.intensity = 0.6f;
        }

        private static void BuildRealisticLabEquipment(GameObject parent, StandaloneMagnesiumExperiment exp)
        {
            Material matCastIron = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_CastIron.mat");
            Material matBurnerBrass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_BurnerBrass.mat");
            Material matStainless = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_StainlessSteel.mat");
            Material matWatchGlass = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_WatchGlass.mat");
            Material matTableTop = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_LabTableTop.mat");
            Material matSandpaper = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_Sandpaper.mat");
            Material matMgRibbon = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_MagnesiumRibbon.mat");
            Material matMgO = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Mat_MagnesiumOxide.mat");

            Mesh meshTable = LoadMeshFromObj("Assets/Models/LaboratoryTable.obj");
            Mesh meshBurner = LoadMeshFromObj("Assets/Models/BunsenBurner.obj");
            Mesh meshTongs = LoadMeshFromObj("Assets/Models/CrucibleTongs.obj");
            Mesh meshWatchGlass = LoadMeshFromObj("Assets/Models/WatchGlass.obj");
            Mesh meshSandpaper = LoadMeshFromObj("Assets/Models/Sandpaper.obj");
            Mesh meshRibbon = LoadMeshFromObj("Assets/Models/MagnesiumRibbon.obj");
            Mesh meshMgOPowder = LoadMeshFromObj("Assets/Models/MgOPowder.obj");
            Mesh meshMgOResidue = LoadMeshFromObj("Assets/Models/MgOResidue.obj");

            // 1. Laboratory Table (Workbench)
            GameObject table = GetOrCreate("Table", parent.transform);
            table.transform.localPosition = new Vector3(0f, 0f, 0f);
            table.transform.localRotation = Quaternion.identity;
            table.transform.localScale = Vector3.one;
            SetMeshModel(table, meshTable, matTableTop);
            exp.tableObject = table;

            // 2. Sandpaper Pad
            GameObject sandpaper = GetOrCreate("Sandpaper", parent.transform);
            sandpaper.transform.localPosition = new Vector3(-0.14f, 0.001f, 0.05f);
            sandpaper.transform.localRotation = Quaternion.identity;
            sandpaper.transform.localScale = Vector3.one;
            SetMeshModel(sandpaper, meshSandpaper, matSandpaper);
            BoxCollider spCol = sandpaper.GetComponent<BoxCollider>();
            if (spCol == null) spCol = sandpaper.AddComponent<BoxCollider>();
            spCol.size = new Vector3(0.08f, 0.02f, 0.08f);
            spCol.isTrigger = true;
            exp.sandpaperObject = sandpaper;
            AddInteractableTag(sandpaper, StandaloneObjectType.Sandpaper);

            // 3. Bunsen Burner
            // Hierarchy: BunsenBurner -> Body, Base, Nozzle, FlamePoint, HeatingZone
            GameObject burner = GetOrCreate("BunsenBurner", parent.transform);
            burner.transform.localPosition = new Vector3(0.12f, 0f, 0.06f);
            burner.transform.localRotation = Quaternion.identity;
            burner.transform.localScale = Vector3.one;
            SetMeshModel(burner, meshBurner, matBurnerBrass);

            CapsuleCollider burnerCol = burner.GetComponent<CapsuleCollider>();
            if (burnerCol == null) burnerCol = burner.AddComponent<CapsuleCollider>();
            burnerCol.radius = 0.04f;
            burnerCol.height = 0.14f;
            burnerCol.center = new Vector3(0, 0.055f, 0);
            burnerCol.direction = 1;
            burnerCol.isTrigger = true;
            exp.burnerObject = burner;
            AddInteractableTag(burner, StandaloneObjectType.BunsenBurner);

            // Bunsen Burner Sub-Transforms
            GameObject flamePoint = GetOrCreate("FlamePoint", burner.transform);
            flamePoint.transform.localPosition = new Vector3(0f, 0.108f, 0f);
            flamePoint.transform.localRotation = Quaternion.identity;
            exp.flamePoint = flamePoint.transform;

            // Flame Object at FlamePoint
            GameObject flame = GetOrCreate("BunsenFlame", flamePoint.transform);
            flame.transform.localPosition = Vector3.zero;
            flame.transform.localScale = new Vector3(0.016f, 0.045f, 0.016f);
            SetMeshPrimitive(flame, PrimitiveType.Sphere, new Color(0.15f, 0.65f, 1f, 0.85f), matBurnerBrass);
            
            // Inner Core Cone Flame
            GameObject innerFlame = GetOrCreate("InnerFlame", flame.transform);
            innerFlame.transform.localPosition = new Vector3(0f, -0.1f, 0f);
            innerFlame.transform.localScale = new Vector3(0.55f, 0.65f, 0.55f);
            SetMeshPrimitive(innerFlame, PrimitiveType.Sphere, new Color(0.85f, 0.95f, 1f, 0.95f), matBurnerBrass);

            flame.SetActive(false);
            exp.flameObject = flame;

            // HeatingZone at Burner Flame
            GameObject heatZone = GetOrCreate("HeatingZone", burner.transform);
            heatZone.transform.localPosition = new Vector3(0f, 0.11f, 0f);
            SphereCollider hzCol = heatZone.GetComponent<SphereCollider>();
            if (hzCol == null) hzCol = heatZone.AddComponent<SphereCollider>();
            hzCol.radius = 0.07f;
            hzCol.isTrigger = true;
            HeatingZoneController hzCtrl = heatZone.GetComponent<HeatingZoneController>();
            if (hzCtrl == null) hzCtrl = heatZone.AddComponent<HeatingZoneController>();
            exp.flameHeatZoneObject = heatZone;

            // 4. Watch Glass
            // Hierarchy: WatchGlass -> GlassModel, CollectionPoint
            GameObject watchGlass = GetOrCreate("WatchGlass", parent.transform);
            watchGlass.transform.localPosition = new Vector3(0.09f, 0.001f, -0.06f);
            watchGlass.transform.localRotation = Quaternion.identity;
            watchGlass.transform.localScale = Vector3.one;
            SetMeshModel(watchGlass, meshWatchGlass, matWatchGlass);

            SphereCollider wgCol = watchGlass.GetComponent<SphereCollider>();
            if (wgCol == null) wgCol = watchGlass.AddComponent<SphereCollider>();
            wgCol.radius = 0.045f;
            wgCol.center = new Vector3(0, 0.005f, 0);
            wgCol.isTrigger = true;
            exp.watchGlassObject = watchGlass;
            AddInteractableTag(watchGlass, StandaloneObjectType.WatchGlass);

            GameObject collectionPoint = GetOrCreate("CollectionPoint", watchGlass.transform);
            collectionPoint.transform.localPosition = new Vector3(0f, 0.002f, 0f);
            collectionPoint.transform.localRotation = Quaternion.identity;
            exp.watchGlassCollectionPoint = collectionPoint.transform;

            // MgO Powder inside WatchGlass CollectionPoint
            GameObject mgoInGlass = GetOrCreate("MgOPowderInWatchGlass", collectionPoint.transform);
            mgoInGlass.transform.localPosition = Vector3.zero;
            mgoInGlass.transform.localRotation = Quaternion.identity;
            mgoInGlass.transform.localScale = Vector3.one;
            SetMeshModel(mgoInGlass, meshMgOPowder, matMgO);
            mgoInGlass.SetActive(false);
            exp.mgoInGlassObject = mgoInGlass;

            // 5. Crucible Tongs
            // Hierarchy: Tongs -> TongsModel, GripPoint
            GameObject tongs = GetOrCreate("Tongs", parent.transform);
            tongs.transform.localPosition = new Vector3(-0.02f, 0.008f, -0.06f);
            tongs.transform.localRotation = Quaternion.Euler(0f, 15f, 0f);
            tongs.transform.localScale = Vector3.one;
            SetMeshModel(tongs, meshTongs, matStainless);

            CapsuleCollider tongsCol = tongs.GetComponent<CapsuleCollider>();
            if (tongsCol == null) tongsCol = tongs.AddComponent<CapsuleCollider>();
            tongsCol.radius = 0.04f;
            tongsCol.height = 0.16f;
            tongsCol.direction = 2; // Z-axis along length
            tongsCol.center = Vector3.zero;
            tongsCol.isTrigger = true;
            exp.tongsObject = tongs;
            AddInteractableTag(tongs, StandaloneObjectType.Tongs);

            GameObject gripPoint = GetOrCreate("GripPoint", tongs.transform);
            gripPoint.transform.localPosition = new Vector3(0f, 0f, 0.052f);
            gripPoint.transform.localRotation = Quaternion.identity;
            exp.tongsGripPoint = gripPoint.transform;

            // MgO Residue on Tongs GripPoint (when burned)
            GameObject mgoResidue = GetOrCreate("MgOResidue", gripPoint.transform);
            mgoResidue.transform.localPosition = Vector3.zero;
            mgoResidue.transform.localRotation = Quaternion.identity;
            mgoResidue.transform.localScale = Vector3.one;
            SetMeshModel(mgoResidue, meshMgOResidue, matMgO);
            mgoResidue.SetActive(false);
            exp.mgoResidueObject = mgoResidue;
            AddInteractableTag(mgoResidue, StandaloneObjectType.MgOResidue);

            // 6. Magnesium Ribbon
            GameObject ribbon = GetOrCreate("MagnesiumRibbon", parent.transform);
            ribbon.transform.localPosition = new Vector3(-0.10f, 0.006f, 0.03f);
            ribbon.transform.localRotation = Quaternion.identity;
            ribbon.transform.localScale = Vector3.one;
            SetMeshModel(ribbon, meshRibbon, matMgRibbon);

            BoxCollider ribbonCol = ribbon.GetComponent<BoxCollider>();
            if (ribbonCol == null) ribbonCol = ribbon.AddComponent<BoxCollider>();
            ribbonCol.size = new Vector3(0.06f, 0.04f, 0.08f); // Touch-friendly collider
            ribbonCol.isTrigger = true;
            exp.ribbonObject = ribbon;
            AddInteractableTag(ribbon, StandaloneObjectType.MagnesiumRibbon);

            // 7. Burn Particles (Sparks & White Smoke)
            GameObject psGO = GetOrCreate("BurnParticles", parent.transform);
            psGO.transform.localPosition = new Vector3(0.12f, 0.11f, 0.06f);
            ParticleSystem ps = psGO.GetComponent<ParticleSystem>();
            if (ps == null) ps = psGO.AddComponent<ParticleSystem>();
            ConfigureParticles(ps);
            exp.burnParticles = ps;

            // 8. Burn Light (Intense dazzling white emission)
            GameObject lightGO = GetOrCreate("BurnLight", parent.transform);
            lightGO.transform.localPosition = new Vector3(0.12f, 0.14f, 0.06f);
            Light l = lightGO.GetComponent<Light>();
            if (l == null) l = lightGO.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.98f, 0.95f);
            l.range = 0.8f;
            l.intensity = 0f;
            l.enabled = false;
            exp.burnLight = l;
        }

        private static (GameObject root, StandaloneUIController ctrl) BuildStandaloneUI(GameObject labRoot)
        {
            GameObject uiCanvas = GetOrCreate("ExperimentCanvas", null);
            Canvas canvas = uiCanvas.GetComponent<Canvas>();
            if (canvas == null) canvas = uiCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler cs = uiCanvas.GetComponent<CanvasScaler>();
            if (cs == null) cs = uiCanvas.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080, 1920); // Android Mobile Portrait (9:16)
            cs.matchWidthOrHeight = 0.5f;

            GetOrAddComponent<GraphicRaycaster>(uiCanvas);

            StandaloneUIController uiCtrl = uiCanvas.GetComponent<StandaloneUIController>();
            if (uiCtrl == null) uiCtrl = uiCanvas.AddComponent<StandaloneUIController>();

            // Clean background panels
            BuildIntroPanel(uiCanvas, uiCtrl, labRoot);
            BuildExperimentPanel(uiCanvas, uiCtrl, labRoot);
            BuildSecondaryPanels(uiCanvas, uiCtrl);
            BuildToastFeedback(uiCanvas, uiCtrl);

            return (uiCanvas, uiCtrl);
        }

        private static void BuildIntroPanel(GameObject parent, StandaloneUIController ctrl, GameObject labRoot)
        {
            GameObject panel = GetOrCreate("IntroPanel", parent.transform);
            SetRectStreched(panel);
            Image panelImg = panel.GetComponent<Image>();
            if (panelImg != null) Object.DestroyImmediate(panelImg);

            // Top Header Bar
            GameObject topBar = GetOrCreate("IntroTopBar", panel.transform);
            RectTransform topRT = topBar.GetComponent<RectTransform>();
            if (topRT == null) topRT = topBar.AddComponent<RectTransform>();
            topRT.anchorMin = new Vector2(0.06f, 0.80f);
            topRT.anchorMax = new Vector2(0.94f, 0.96f);
            topRT.pivot = new Vector2(0.5f, 1f);
            topRT.offsetMin = Vector2.zero;
            topRT.offsetMax = Vector2.zero;
            AddBackground(topBar, new Color(0.04f, 0.07f, 0.14f, 0.92f));

            TMP_Text title = CreateTextChild(topBar, "Title", "BURNING OF MAGNESIUM RIBBON IN AIR", 30, TextAlignmentOptions.Center);
            title.color = new Color(0.35f, 0.85f, 1f);
            RectTransform titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.02f, 0.45f);
            titleRT.anchorMax = new Vector2(0.98f, 0.95f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            TMP_Text subTitle = CreateTextChild(topBar, "SubTitle", "Virtual Chemistry Laboratory Experiment  |  2Mg + O<sub>2</sub> -> 2MgO", 18, TextAlignmentOptions.Center);
            subTitle.color = new Color(0.8f, 0.88f, 0.95f);
            RectTransform subRT = subTitle.GetComponent<RectTransform>();
            subRT.anchorMin = new Vector2(0.02f, 0.05f);
            subRT.anchorMax = new Vector2(0.98f, 0.48f);
            subRT.offsetMin = Vector2.zero;
            subRT.offsetMax = Vector2.zero;

            // Bottom Action Card
            GameObject bottomCard = GetOrCreate("IntroBottomCard", panel.transform);
            RectTransform botRT = bottomCard.GetComponent<RectTransform>();
            if (botRT == null) botRT = bottomCard.AddComponent<RectTransform>();
            botRT.anchorMin = new Vector2(0.06f, 0.03f);
            botRT.anchorMax = new Vector2(0.94f, 0.24f);
            botRT.pivot = new Vector2(0.5f, 0f);
            botRT.offsetMin = Vector2.zero;
            botRT.offsetMax = Vector2.zero;
            AddBackground(bottomCard, new Color(0.04f, 0.07f, 0.14f, 0.92f));

            TMP_Text desc = CreateTextChild(bottomCard, "Description", 
                "In this experiment, magnesium ribbon is burned in air to observe the formation of magnesium oxide.", 
                20, TextAlignmentOptions.Center);
            desc.color = Color.white;
            RectTransform descRT = desc.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.04f, 0.52f);
            descRT.anchorMax = new Vector2(0.96f, 0.96f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;

            // Action Buttons
            Button startBtn = CreateButton(bottomCard, "StartBtn", "▶ START EXPERIMENT", new Vector2(0, -32), new Vector2(340, 56));
            startBtn.GetComponent<Image>().color = new Color(0.12f, 0.65f, 0.30f);

            Button safetyBtn = CreateButton(bottomCard, "SafetyBtn", "⚠ Safety Precautions", new Vector2(-200, -95), new Vector2(230, 46));
            safetyBtn.GetComponent<Image>().color = new Color(0.75f, 0.45f, 0.10f);

            Button instrBtn = CreateButton(bottomCard, "InstrBtn", "ℹ Instructions", new Vector2(200, -95), new Vector2(230, 46));
            instrBtn.GetComponent<Image>().color = new Color(0.20f, 0.48f, 0.75f);

            ctrl.introPanel = panel;
            ctrl.startButton = startBtn;
            ctrl.introSafetyButton = safetyBtn;
            ctrl.introInstructionsButton = instrBtn;

            var exp = labRoot.GetComponent<StandaloneMagnesiumExperiment>();
            if (exp != null)
            {
                startBtn.onClick.AddListener(exp.StartButtonTriggered);
            }
        }

        private static void BuildExperimentPanel(GameObject parent, StandaloneUIController ctrl, GameObject labRoot)
        {
            GameObject panel = GetOrCreate("ExperimentPanel", parent.transform);
            SetRectStreched(panel);
            Image panelImg = panel.GetComponent<Image>();
            if (panelImg != null) Object.DestroyImmediate(panelImg);
            
            // Top HUD panel
            GameObject topHud = GetOrCreate("TopHud", panel.transform);
            RectTransform hudRT = topHud.GetComponent<RectTransform>();
            if (hudRT == null) hudRT = topHud.AddComponent<RectTransform>();
            hudRT.anchorMin = new Vector2(0.04f, 0.81f);
            hudRT.anchorMax = new Vector2(0.96f, 0.97f);
            hudRT.pivot = new Vector2(0.5f, 1f);
            hudRT.offsetMin = Vector2.zero;
            hudRT.offsetMax = Vector2.zero;
            AddBackground(topHud, new Color(0.04f, 0.07f, 0.14f, 0.92f));

            TMP_Text stepTitle = CreateTextChild(topHud, "StepTitle", "Step 1: Clean the Magnesium Ribbon", 24, TextAlignmentOptions.Left);
            stepTitle.color = new Color(0.35f, 0.85f, 1f);
            RectTransform titleRT = stepTitle.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.04f, 0.52f);
            titleRT.anchorMax = new Vector2(0.74f, 0.95f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            TMP_Text progress = CreateTextChild(topHud, "Progress", "Step 1 / 8", 22, TextAlignmentOptions.Right);
            progress.color = new Color(1f, 0.85f, 0.2f);
            RectTransform progRT = progress.GetComponent<RectTransform>();
            progRT.anchorMin = new Vector2(0.75f, 0.52f);
            progRT.anchorMax = new Vector2(0.96f, 0.95f);
            progRT.offsetMin = Vector2.zero;
            progRT.offsetMax = Vector2.zero;

            TMP_Text stepDesc = CreateTextChild(topHud, "StepDescription", "Tap the Magnesium Ribbon to select it, then rub it against sandpaper.", 18, TextAlignmentOptions.Left);
            stepDesc.color = Color.white;
            RectTransform descRT = stepDesc.GetComponent<RectTransform>();
            descRT.anchorMin = new Vector2(0.04f, 0.05f);
            descRT.anchorMax = new Vector2(0.96f, 0.50f);
            descRT.offsetMin = Vector2.zero;
            descRT.offsetMax = Vector2.zero;

            // Bottom Buttons Panel
            GameObject botHud = GetOrCreate("BottomHud", panel.transform);
            RectTransform botRT = botHud.GetComponent<RectTransform>();
            if (botRT == null) botRT = botHud.AddComponent<RectTransform>();
            botRT.anchorMin = new Vector2(0.04f, 0.02f);
            botRT.anchorMax = new Vector2(0.96f, 0.12f);
            botRT.pivot = new Vector2(0.5f, 0f);
            botRT.offsetMin = Vector2.zero;
            botRT.offsetMax = Vector2.zero;
            AddBackground(botHud, new Color(0.04f, 0.07f, 0.14f, 0.92f));

            // Row 1: Action / Explore Buttons
            Button resetBtn = CreateButton(botHud, "ResetBtn", "Reset", new Vector2(-360, 0), new Vector2(140, 52));
            resetBtn.GetComponent<Image>().color = new Color(0.65f, 0.18f, 0.18f);
            
            Button safetyBtn = CreateButton(botHud, "SafetyBtn", "Safety", new Vector2(-200, 0), new Vector2(140, 52));
            safetyBtn.GetComponent<Image>().color = new Color(0.72f, 0.45f, 0.10f);

            Button obsBtn = CreateButton(botHud, "ObsBtn", "Observation", new Vector2(-40, 0), new Vector2(150, 52));
            Button eqBtn = CreateButton(botHud, "EqBtn", "2Mg + O<sub>2</sub> -> 2MgO", new Vector2(130, 0), new Vector2(165, 52));
            Button resBtn = CreateButton(botHud, "ResultBtn", "Result", new Vector2(290, 0), new Vector2(125, 52));
            Button nextBtn = CreateButton(botHud, "NextBtn", "Next ▶", new Vector2(410, 0), new Vector2(100, 52));

            obsBtn.GetComponent<Image>().color = new Color(0.12f, 0.58f, 0.28f);
            eqBtn.GetComponent<Image>().color = new Color(0.48f, 0.22f, 0.70f);
            resBtn.GetComponent<Image>().color = new Color(0.12f, 0.52f, 0.65f);
            nextBtn.GetComponent<Image>().color = new Color(0.18f, 0.48f, 0.88f);
            nextBtn.gameObject.SetActive(false);

            ctrl.experimentPanel = panel;
            ctrl.stepTitleText = stepTitle;
            ctrl.stepDescriptionText = stepDesc;
            ctrl.progressText = progress;
            
            ctrl.uiNextButton = nextBtn;
            ctrl.uiResetButton = resetBtn;
            ctrl.uiSafetyButton = safetyBtn;

            ctrl.openObservationButton = obsBtn;
            ctrl.openEquationButton = eqBtn;
            ctrl.openResultButton = resBtn;

            var exp = labRoot.GetComponent<StandaloneMagnesiumExperiment>();
            if (exp != null)
            {
                resetBtn.onClick.AddListener(exp.ResetButtonTriggered);
                nextBtn.onClick.AddListener(exp.ManualNextStepAttempted);
            }
        }

        private static void BuildSecondaryPanels(GameObject parent, StandaloneUIController ctrl)
        {
            // 1. Observation Panel
            GameObject obs = CreatePopupWindow(parent, "ObservationPanel", "OBSERVATION",
                "• The magnesium ribbon burns with a dazzling, intense bright white flame.\n\n" +
                "• A large amount of heat and light energy is released.\n\n" +
                "• A white powder / ash (Magnesium Oxide, MgO) is formed and collected inside the watch glass.");
            ctrl.observationPanel = obs;
            ctrl.closeObservationButton = FindInChildren(obs, "CloseBtn").GetComponent<Button>();

            // 2. Chemical Equation Panel
            GameObject eq = CreatePopupWindow(parent, "EquationPanel", "CHEMICAL EQUATION",
                "Chemical Reaction:\n\n" +
                "    2Mg (s)   +   O<sub>2</sub> (g)   ->   2MgO (s)\n\n" +
                "Reactants:\n" +
                "• Magnesium (Mg) — Silvery-white metal ribbon\n" +
                "• Oxygen (O<sub>2</sub>) — Gas from surrounding atmosphere\n\n" +
                "Product:\n" +
                "• Magnesium Oxide (MgO) — White powdery solid ash\n\n" +
                "Explanation:\n" +
                "Magnesium reacts with oxygen in air at high temperature to form magnesium oxide.");
            ctrl.equationPanel = eq;
            ctrl.closeEquationButton = FindInChildren(eq, "CloseBtn").GetComponent<Button>();

            // 3. Result Panel
            GameObject res = CreatePopupWindow(parent, "ResultPanel", "EXPERIMENT RESULT",
                "RESULT:\n\n" +
                "Magnesium burns in air and forms magnesium oxide.\n\n" +
                "Key Conclusions:\n" +
                "1. Combination / Synthesis Reaction: Two reactants (Mg and O<sub>2</sub>) combine to form a single product (MgO).\n\n" +
                "2. Exothermic Oxidation Reaction: Magnesium undergoes oxidation by gaining oxygen, releasing intense white light and thermal energy.\n\n" +
                "3. Chemical Equation: 2Mg + O<sub>2</sub> -> 2MgO");
            ctrl.resultPanel = res;
            ctrl.closeResultButton = FindInChildren(res, "CloseBtn").GetComponent<Button>();

            // 4. Safety Precautions Panel
            GameObject safety = CreatePopupWindow(parent, "SafetyPanel", "SAFETY PRECAUTIONS",
                "LABORATORY SAFETY GUIDELINES:\n\n" +
                "1. Wear safety goggles at all times.\n\n" +
                "2. Perform the experiment under teacher supervision.\n\n" +
                "3. Do not look directly at the intense white flame (UV radiation can cause photochemical eye damage).\n\n" +
                "4. Handle the Bunsen burner and hot materials carefully using tongs.\n\n" +
                "5. Allow the product to cool completely before collection.");
            ctrl.safetyPanel = safety;
            ctrl.closeSafetyButton = FindInChildren(safety, "CloseBtn").GetComponent<Button>();

            // 5. Instructions Panel
            GameObject instr = CreatePopupWindow(parent, "InstructionsPanel", "EXPERIMENT INSTRUCTIONS",
                "STEP-BY-STEP PROCEDURE:\n\n" +
                "1. Select the magnesium ribbon lying on the table.\n" +
                "2. Rub the ribbon back and forth on the sandpaper to clean the protective oxide coating.\n" +
                "3. Tap the laboratory tongs to grip the clean ribbon.\n" +
                "4. Tap the Bunsen burner to light the flame.\n" +
                "5. Drag the tongs to move the ribbon into the flame.\n" +
                "6. Heat the ribbon until it ignites with a bright white flame.\n" +
                "7. Wait for the formed Magnesium Oxide (MgO) product to cool.\n" +
                "8. Drag the tongs over the watch glass to deposit and collect the powder.");
            ctrl.instructionsPanel = instr;
            ctrl.closeInstructionsButton = FindInChildren(instr, "CloseBtn").GetComponent<Button>();
        }

        private static void BuildToastFeedback(GameObject parent, StandaloneUIController ctrl)
        {
            GameObject toast = GetOrCreate("FeedbackToast", parent.transform);
            RectTransform rt = toast.GetComponent<RectTransform>();
            if (rt == null) rt = toast.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.12f, 0.72f);
            rt.anchorMax = new Vector2(0.88f, 0.79f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            CanvasGroup cg = toast.GetComponent<CanvasGroup>();
            if (cg == null) cg = toast.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            AddBackground(toast, new Color(0.04f, 0.07f, 0.12f, 0.92f));

            TMP_Text text = CreateTextChild(toast, "ToastText", "Toast Message", 22, TextAlignmentOptions.Center);
            text.color = new Color(0.35f, 1f, 0.45f);
            RectTransform trt = text.GetComponent<RectTransform>();
            trt.anchorMin = Vector2.zero;
            trt.anchorMax = Vector2.one;
            trt.offsetMin = Vector2.zero;
            trt.offsetMax = Vector2.zero;

            ctrl.feedbackToastGroup = cg;
            ctrl.feedbackToastText = text;
        }

        private static GameObject CreatePopupWindow(GameObject parent, string name, string headerTitle, string bodyContent)
        {
            GameObject window = GetOrCreate(name, parent.transform);
            SetRectStreched(window);
            window.SetActive(false);

            AddBackground(window, new Color(0f, 0f, 0f, 0.65f));

            GameObject dialog = GetOrCreate("Dialog", window.transform);
            RectTransform drt = dialog.GetComponent<RectTransform>();
            if (drt == null) drt = dialog.AddComponent<RectTransform>();
            drt.anchorMin = new Vector2(0.08f, 0.14f);
            drt.anchorMax = new Vector2(0.92f, 0.86f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.offsetMin = Vector2.zero;
            drt.offsetMax = Vector2.zero;
            AddBackground(dialog, new Color(0.06f, 0.09f, 0.16f, 0.98f));

            TMP_Text title = CreateTextChild(dialog, "Header", headerTitle, 28, TextAlignmentOptions.Center);
            title.color = new Color(0.35f, 0.85f, 1f);
            RectTransform titleRT = title.GetComponent<RectTransform>();
            titleRT.anchorMin = new Vector2(0.04f, 0.88f);
            titleRT.anchorMax = new Vector2(0.96f, 0.98f);
            titleRT.offsetMin = Vector2.zero;
            titleRT.offsetMax = Vector2.zero;

            TMP_Text body = CreateTextChild(dialog, "Body", bodyContent, 20, TextAlignmentOptions.Left);
            body.color = Color.white;
            RectTransform bodyRT = body.GetComponent<RectTransform>();
            bodyRT.anchorMin = new Vector2(0.06f, 0.14f);
            bodyRT.anchorMax = new Vector2(0.94f, 0.86f);
            bodyRT.offsetMin = Vector2.zero;
            bodyRT.offsetMax = Vector2.zero;

            Button closeBtn = CreateButton(dialog, "CloseBtn", "✕ Close", Vector2.zero, new Vector2(220, 52));
            closeBtn.GetComponent<Image>().color = new Color(0.25f, 0.28f, 0.35f);
            RectTransform closeRT = closeBtn.GetComponent<RectTransform>();
            closeRT.anchorMin = new Vector2(0.35f, 0.03f);
            closeRT.anchorMax = new Vector2(0.65f, 0.11f);
            closeRT.offsetMin = Vector2.zero;
            closeRT.offsetMax = Vector2.zero;

            return window;
        }

        private static Mesh LoadMeshFromObj(string objPath)
        {
            GameObject modelGo = AssetDatabase.LoadAssetAtPath<GameObject>(objPath);
            if (modelGo != null)
            {
                MeshFilter mf = modelGo.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
            }
            return null;
        }

        private static void SetMeshModel(GameObject go, Mesh mesh, Material mat)
        {
            if (go == null) return;
            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc);

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            if (mesh != null) mf.sharedMesh = mesh;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
        }

        private static void SetMeshPrimitive(GameObject go, PrimitiveType primType, Color col, Material baseMat)
        {
            GameObject temp = GameObject.CreatePrimitive(primType);
            Mesh m = temp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(temp);

            MeshCollider mc = go.GetComponent<MeshCollider>();
            if (mc != null) Object.DestroyImmediate(mc);

            MeshFilter mf = go.GetComponent<MeshFilter>();
            if (mf == null) mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = m;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null) mr = go.AddComponent<MeshRenderer>();
            Material mat = new Material(baseMat);
            mat.color = col;
            mr.material = mat;
        }

        private static void ConfigureParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.duration = 4.0f;
            main.loop = true;
            main.startLifetime = 0.35f;
            main.startSpeed = 0.6f;
            main.startSize = 0.008f;
            main.startColor = new Color(1f, 0.98f, 0.90f);
            main.maxParticles = 60;

            var emission = ps.emission;
            emission.rateOverTime = 40f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.015f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(1f, 0.9f, 0.5f), 0.7f), new GradientColorKey(new Color(0.8f, 0.8f, 0.8f), 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.8f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = grad;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private static void AddInteractableTag(GameObject go, StandaloneObjectType type)
        {
            var io = go.GetComponent<StandaloneInteractable>();
            if (io == null) io = go.AddComponent<StandaloneInteractable>();
            io.objectType = type;
            io.isInteractable = true;
        }

        private static GameObject GetOrCreate(string name, Transform parent)
        {
            Transform found = parent != null ? parent.Find(name) : GameObject.Find(name)?.transform;
            if (found != null) return found.gameObject;

            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static GameObject FindInChildren(GameObject parent, string name)
        {
            if (parent == null) return null;
            Transform t = parent.transform.Find(name);
            if (t != null) return t.gameObject;

            foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
                if (child.name == name) return child.gameObject;

            return null;
        }

        private static void SetRectStreched(GameObject go)
        {
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        private static void AddBackground(GameObject go, Color col)
        {
            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = col;
            img.raycastTarget = true;
        }

        private static TMP_Text CreateTextChild(GameObject parent, string name, string text, int fontSize, TextAlignmentOptions align)
        {
            GameObject go = GetOrCreate(name, parent.transform);
            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = align;
            tmp.enableWordWrapping = true;
            tmp.raycastTarget = false;
            return tmp;
        }

        private static Button CreateButton(GameObject parent, string name, string label, Vector2 pos, Vector2 size)
        {
            GameObject go = GetOrCreate(name, parent.transform);
            RectTransform rt = go.GetComponent<RectTransform>();
            if (rt == null) rt = go.AddComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;

            Image img = go.GetComponent<Image>();
            if (img == null) img = go.AddComponent<Image>();
            img.color = new Color(0.2f, 0.2f, 0.25f);

            Button btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            GameObject textGO = GetOrCreate("Text", go.transform);
            TextMeshProUGUI tmp = textGO.GetComponent<TextMeshProUGUI>();
            if (tmp == null) tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = Mathf.RoundToInt(size.y * 0.38f);
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;

            RectTransform textRT = textGO.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = Vector2.zero;
            textRT.offsetMax = Vector2.zero;

            return btn;
        }

        private static T GetOrAddComponent<T>(GameObject go) where T : Component
        {
            T comp = go.GetComponent<T>();
            if (comp == null) comp = go.AddComponent<T>();
            return comp;
        }
    }
}
#endif
