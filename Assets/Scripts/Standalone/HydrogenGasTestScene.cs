// ============================================================
// HydrogenGasTestScene.cs  —  AR Virtual Lab
// Experiment: Reaction of Zinc with Dilute Sulphuric Acid and Testing of Hydrogen Gas
// Chemical Reaction: Zn(s) + H2SO4(aq) -> ZnSO4(aq) + H2(g)^
// Complete interactive 9-step virtual chemistry experiment.
// UI, Canvas, RectTransform anchors, Camera, Framing, and Layout
// exact 1:1 match with the working MagnesiumLabScene template.
// Namespace: ARVirtualLab.HydrogenGasTest
// ============================================================
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARVirtualLab.UI;
using ARVirtualLab.AR;

namespace ARVirtualLab.HydrogenGasTest
{
    // ---------------------------------------------------------
    //  Experiment step enum (8-step sequence)
    // ---------------------------------------------------------
    public enum HydrogenGasTestStep
    {
        Intro                             = 0,
        Step1_PlaceTestTubeOnStand        = 1,
        Step2_AddZinc                     = 2,
        Step3_AddDiluteAcid               = 3,
        Step4_SealWithCorkDeliveryTube    = 4,
        Step5_ConnectDeliveryTubeToBasin  = 5,
        Step6_GasEvolution                = 6,
        Step7_SoapBubbles                 = 7,
        Step8_HydrogenCandlePopTest       = 8,
        Step8_Results                     = 9,
        Completed                         = 10
    }

    public enum HydrogenGasTestTag { None, Stand, TestTube, Zinc, Acid, Cork, SoapBasin, Candle, Bubble }

    public class HydrogenGasTestInteractable : MonoBehaviour
    {
        public HydrogenGasTestTag htag = HydrogenGasTestTag.None;
    }

    [RequireComponent(typeof(AudioSource))]
    public class HydrogenGasTestScene : MonoBehaviour
    {
        // === Constants ===
        // Platform surface at Y = -0.032f, matching HydrogenLabScene brown bench surface
        private const float TABLE_TOP_Y = -0.032f;
        private const float TUBE_LOWER_DIST = 0.025f;   // Step 5 lowers the clamped tube by this much so the outlet dips in

        // === State ===
        private HydrogenGasTestStep _step = HydrogenGasTestStep.Intro;
        private bool _isCompleted = false;
        private AudioSource _audio;

        // === 3D Apparatus Objects ===
        private GameObject _table;
        private GameObject _stand;
        private GameObject _testTube;
        private GameObject _tubeLiquid;
        private GameObject _zincGranules;
        private GameObject _zincInTube;
        private GameObject _acidBottle;
        private GameObject _corkDeliveryTube;
        private GameObject _soapBasin;
        private GameObject _soapLiquid;
        private GameObject _candle;
        private GameObject _candleFlame;

        // === Hydrogen soap bubbles (several, rise out of the basin like the textbook figure) ===
        private class HBubble
        {
            public GameObject go;
            public float age, size, phase, riseSpeed, maxRise;
            public Vector3 basePos, drift;
        }
        private readonly List<HBubble> _bubbles = new List<HBubble>();
        private const int MAX_BUBBLES = 6;
        private float _bubbleTimer;
        private bool _bubblesEmitting;
        private float _liquidSurfaceY;

        // === Delivery tube gas travel (dots follow the tube: cork -> up -> across -> down into soap) ===
        private readonly List<Transform> _gasDots = new List<Transform>();
        private readonly List<float> _gasDotT = new List<float>();
        private Vector3[] _tubePathLocal;            // in corkDeliveryTube local space
        private Vector3 _tubeTipWorld;
        private bool _gasFlowing;
        private float _gasFlowTime;

        // === Measured apparatus geometry ===
        private Vector3 _testTubeClampedPos;
        private Vector3 _basinFinalPos;
        private float _testTubeH = 0.17f;
        private float _tubeOriginOffsetY;
        private float _tubeDia = 0.03f;
        private float _liquidFullH = 0.06f;
        private float _acidTopOffset = 0.15f;
        private GameObject _pourStream;
        private GameObject _starFlash;
        private Material _starMat;
        private bool _animating;
        private Vector2 _pointerDownScreen;

        // === Particle Systems & VFX ===
        private ParticleSystem _reactionBubblePS;
        private ParticleSystem _soapBubblePS;
        private ParticleSystem _popPS;
        private Light _popLight;
        private Light _candleLight;
        private Camera _cam;

        // === Positions & Anchors (Matching HydrogenLabScene brown board platform) ===
        // Stand and clamp on the left, basin on right, candle in foreground right
        // The stand's clamp jaw reaches ~6 cm behind the rod (model measured), so the tube is clamped at z = stand.z + 0.06.
        private readonly Vector3 POS_STAND          = new Vector3(-0.220f, TABLE_TOP_Y,          0.040f);
        private readonly Vector3 POS_TUBE_CLAMPED   = new Vector3(-0.220f, TABLE_TOP_Y + 0.037f, 0.100f);
        private readonly Vector3 POS_TUBE_START     = new Vector3(-0.120f, TABLE_TOP_Y,         -0.070f);
        private readonly Vector3 POS_ZINC_START     = new Vector3(-0.010f, TABLE_TOP_Y,         -0.150f);
        private readonly Vector3 POS_ACID_START     = new Vector3( 0.090f, TABLE_TOP_Y,         -0.120f);
        private readonly Vector3 POS_CORK_START     = new Vector3(-0.220f, TABLE_TOP_Y,          0.100f);   // unused: cork assembly appears above the tube mouth in Step 4
        private readonly Vector3 POS_BASIN          = new Vector3( 0.050f, TABLE_TOP_Y,          0.100f);
        private readonly Vector3 POS_CANDLE         = new Vector3( 0.250f, TABLE_TOP_Y,         -0.100f);

        private Vector3 _candleOriginPos;
        private Vector3 _acidBottleOriginPos;
        private Vector3 _corkOriginPos;                 // hover pose above the tube mouth (Step 4)
        private Vector3 _corkRestPos;                   // lying on the bench from the start
        private static readonly Quaternion CORK_REST_ROT = Quaternion.Euler(90f, 0f, 0f);
        private const float CORK_REST_SCALE = 0.6f;
        private bool _corkLifted;
        private Vector3 _zincOriginPos;
        private Vector3 _testTubeOriginPos;
        private float _testTubeTopY;
        private float _corkSealedX;

        // === Materials ===
        private Material _glassMat;
        private Material _liquidMat;
        private Material _soapMat;
        private Material _zincMat;
        private Material _acidMat;
        private Material _corkMat;
        private Material _candleMat;
        private Material _metalMat;
        private Material _tableMat;
        private Material _benchMat;
        private Material _bubbleMat;

        // === Assembly & Interaction State ===
        private GameObject _draggingObject = null;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private bool _zincPrepared    = false;
        private bool _tubeClamped     = false;
        private bool _zincInTubeAdded = false;
        private bool _acidAdded       = false;
        private bool _corkPlaced      = false;
        private bool _tubeInBasin     = false;
        private bool _candleLit       = false;
        private bool _reactionStarted = false;
        private bool _popTriggered    = false;

        // === Zones ===
        private GameObject _standClampZone;
        private GameObject _testTubeMouthZone;
        private GameObject _corkZone;
        private GameObject _soapBasinZone;
        private GameObject _bubbleZone;

        // === Coroutines ===
        private Coroutine _feedbackCoroutine;
        private Coroutine _bubbleFloatCoroutine;

        // === UI ===
        private Canvas _canvas;
        private GameObject _introPanel;
        private GameObject _hudPanel;
        private readonly List<Image> _stepPillIndicators = new List<Image>();
        private TMP_Text _stepNumberBadge;
        private TMP_Text _stepTitle;
        private TMP_Text _stepDesc;
        private TMP_Text _feedbackText;
        private Button _startBtn;
        private Button _resetBtn;
        private Button _safetyBtn;
        private Button _prevBtn;
        private Button _nextBtn;
        private Button _obsBtn;
        private Button _eqBtn;
        private Button _resultBtn;
        private GameObject _safetyPanel;
        private GameObject _obsPanel;
        private GameObject _eqPanel;
        private GameObject _resultPanel;
        private GameObject _aboutPanel;
        private GameObject _resetConfirmDialog;
        private HydrogenGasTestARTray _arTray;
        private Camera _standaloneCam;
        private bool _isARMode = false;

        // =========================================================
        //  Unity Lifecycle
        // =========================================================
        private void Awake()
        {
            BuildCameraAndLighting();
            EnsureEventSystem();

            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            CreateMaterials();
            BuildSceneEquipment();
            BuildUI();

            if (ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToAR)
            {
                ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToAR = false;
                SetStep(HydrogenGasTestStep.Step1_PlaceTestTubeOnStand);
                TriggerARMode();
            }
            else if (ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToExperiment)
            {
                ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToExperiment = false;
                SetStep(HydrogenGasTestStep.Step1_PlaceTestTubeOnStand);
            }
            else
            {
                SetStep(HydrogenGasTestStep.Intro);
            }
        }

        public void OnEnterARMode(Camera arCam)
        {
            _isARMode = true;
            if (_standaloneCam == null) _standaloneCam = Camera.main;
            if (_standaloneCam != null && _standaloneCam != arCam)
            {
                _standaloneCam.gameObject.SetActive(false);
            }
            _cam = arCam != null ? arCam : Camera.main;

            if (_table != null) _table.SetActive(false); // Hide virtual table in AR
            if (_introPanel != null) _introPanel.SetActive(false);
            if (_hudPanel != null && !_hudPanel.activeSelf) _hudPanel.SetActive(true);
        }

        private void TriggerARMode()
        {
            if (!ARVirtualLab.AR.ARExperimentMode.Active) ToggleUnifiedAR();
        }

        // ── Mixed reality: place the whole lab on a real surface (see ARExperimentMode) ──
        private Camera _arStandaloneCam;

        private void ToggleUnifiedAR()
        {
            if (!ARVirtualLab.AR.ARExperimentMode.Active && _arStandaloneCam == null) _arStandaloneCam = _cam;
            ARVirtualLab.AR.ARExperimentMode.Toggle(new ARVirtualLab.AR.ARExperimentSetup
            {
                SceneRoot = transform,
                StandaloneCamera = _arStandaloneCam,
                Bench = _table,
                BenchSurfaceY = TABLE_TOP_Y,
                BenchCenterZ = 0.03f,
                OnStarted = c => { _cam = c; },
                OnStopped = c => { _cam = c; }
            });
        }

        private HintRing _hint;

        // Pulsing ring around whatever the student should touch next
        private void UpdateHint()
        {
            if (_hint == null) _hint = HintRing.Create(transform);
            GameObject t = null;
            switch (_step)
            {
                case HydrogenGasTestStep.Step1_PlaceTestTubeOnStand:      t = _testTube; break;
                case HydrogenGasTestStep.Step2_AddZinc:                   t = _zincGranules; break;
                case HydrogenGasTestStep.Step3_AddDiluteAcid:             t = _acidBottle; break;
                case HydrogenGasTestStep.Step5_ConnectDeliveryTubeToBasin: t = _testTube; break;
                case HydrogenGasTestStep.Step8_HydrogenCandlePopTest:     t = _candleLit ? null : _candle; break;
            }
            if (_step == HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube && _corkDeliveryTube != null)
                _hint.Target(_corkDeliveryTube, 0.05f, new Vector3(0f, 0.03f, 0f));   // ring the stopper, not the whole tube
            else
                _hint.Target(t);
            _hint.SetVisible(!_animating && _draggingObject == null && !_popTriggered);
        }

        private void Update()
        {
            UpdateHint();
            ARVirtualLab.UI.CameraFit.Apply(_cam, 48f);   // keep the same width of view on tall phones
            UpdateInteraction();
            UpdateBubbles();
            UpdateGasDots();
            UpdateCandleFlame();
        }

        // =========================================================
        //  CAMERA & LIGHTING — Exact 1:1 match with MagnesiumLabScene
        // =========================================================
        private void BuildCameraAndLighting()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGO = new GameObject("Main Camera");
                _cam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
                camGO.AddComponent<AudioListener>();
            }

            _cam.enabled            = true;
            _cam.cullingMask        = -1;
            // Exact same camera framing as MagnesiumLabScene
            _cam.transform.position = new Vector3(0f, 1.05f, -0.84f);
            _cam.transform.rotation = Quaternion.Euler(51f, 0f, 0f);
            _cam.fieldOfView        = 48f;
            _cam.backgroundColor    = ARVirtualLab.UI.EduTheme.CameraBackground;
            _cam.clearFlags         = CameraClearFlags.SolidColor;
            _cam.nearClipPlane      = 0.01f;
            _cam.farClipPlane       = 100f;
            _cam.depth              = 0;

            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.70f, 1f);

            var keyGO = new GameObject("KeyLight");
            keyGO.transform.SetParent(transform, true);
            var keyL = keyGO.AddComponent<Light>();
            keyL.type = LightType.Directional;
            keyL.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            keyL.color = new Color(1f, 0.98f, 0.94f);
            keyL.intensity = 1.4f;

            var fillGO = new GameObject("FillLight");
            fillGO.transform.SetParent(transform, true);
            var fillL = fillGO.AddComponent<Light>();
            fillL.type = LightType.Directional;
            fillL.transform.rotation = Quaternion.Euler(35f, 155f, 0f);
            fillL.color = new Color(0.82f, 0.90f, 1f);
            fillL.intensity = 0.85f;

            var rimGO = new GameObject("RimLight");
            rimGO.transform.SetParent(transform, true);
            var rimL = rimGO.AddComponent<Light>();
            rimL.type = LightType.Directional;
            rimL.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
            rimL.color = Color.white;
            rimL.intensity = 0.55f;

            var urpData = _cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urpData == null)
                urpData = _cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urpData != null)
                urpData.renderPostProcessing = true;
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }
        }

        // =========================================================
        //  PBR & URP Materials
        // =========================================================
        private void CreateMaterials()
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");

            // Transparent glass for test tube and basin
            _glassMat = new Material(litShader);
            ApplyURPTransparent(_glassMat, new Color(0.82f, 0.94f, 1.00f, 0.22f));
            _glassMat.SetFloat("_Smoothness", 0.95f);
            _glassMat.SetFloat("_Metallic", 0.04f);

            // Clear dilute acid liquid inside test tube
            _liquidMat = new Material(litShader);
            ApplyURPTransparent(_liquidMat, new Color(0.85f, 0.96f, 1.00f, 0.50f));
            _liquidMat.SetFloat("_Smoothness", 0.88f);
            _liquidMat.renderQueue = 2999;

            // Zinc granules metallic silvery grey
            _zincMat = SimpleMat(new Color(0.72f, 0.74f, 0.77f), 0.70f, 0.85f);

            // Laboratory clamp stand dark cast iron
            _metalMat = SimpleMat(new Color(0.24f, 0.26f, 0.30f), 0.55f, 0.60f);

            // Cork stopper natural warm tan
            _corkMat = SimpleMat(new Color(0.76f, 0.54f, 0.32f), 0.25f, 0.02f);

            // Table and bench surface materials matching HydrogenLabScene
            _tableMat = SimpleMat(new Color(0.30f, 0.18f, 0.10f), 0.35f, 0.02f);
            _benchMat = SimpleMat(new Color(0.42f, 0.26f, 0.15f), 0.45f, 0.02f);

            // Dilute sulphuric acid amber/chemical bottle
            _acidMat = SimpleMat(new Color(0.85f, 0.55f, 0.15f), 0.70f, 0.05f);

            // Candle body warm wax
            _candleMat = SimpleMat(new Color(0.96f, 0.94f, 0.88f), 0.30f, 0.01f);

            // Transparent soap solution liquid in basin
            _soapMat = new Material(litShader);
            ApplyURPTransparent(_soapMat, new Color(0.35f, 0.70f, 0.92f, 0.45f));
            _soapMat.SetFloat("_Smoothness", 0.90f);
            _soapMat.renderQueue = 2999;

            // Iridescent transparent soap bubble
            _bubbleMat = new Material(litShader);
            ApplyURPTransparent(_bubbleMat, new Color(0.90f, 0.96f, 1.0f, 0.35f));
            _bubbleMat.SetFloat("_Smoothness", 0.98f);
            _bubbleMat.SetFloat("_Metallic",   0.15f);
            _bubbleMat.renderQueue = 3001;
        }

        private Material SimpleMat(Color c, float smoothness = 0.4f, float metallic = 0.0f)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
            if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", metallic);
            return m;
        }

        private void ApplyURPTransparent(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;

            mat.SetFloat("_Surface",  1f);
            mat.SetFloat("_Blend",    0f);
            mat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",    0);
            mat.SetInt("_AlphaClip", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHABLEND_ON");
            mat.DisableKeyword("_ALPHATEST_ON");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }

        // =========================================================
        //  3D Scene Equipment — Unassembled for Step-by-Step User Lab
        // =========================================================
        private void BuildSceneEquipment()
        {
            // ── 1. Table Platform (Matching HydrogenLabScene Brown Board) ──
            _table = new GameObject("LabPlatform");
            _table.transform.SetParent(transform, false);
            Box("LabTable", _table.transform, new Vector3(0f, -0.06f, 0.05f), new Vector3(1.20f, 0.04f, 0.70f), _tableMat);
            Box("BenchSurface", _table.transform, new Vector3(0f, -0.038f, 0.05f), new Vector3(1.18f, 0.012f, 0.68f), _benchMat);

            // ── 2. Clamp Stand (Left) ─────────────────────────────────
            var standContainer = new GameObject("ClampStand");
            standContainer.transform.SetParent(transform, false);
            standContainer.transform.localPosition = POS_STAND;

            bool standLoaded = TryLoadModel("Clamp Stand", "ClampStandModel", out _stand);
            if (standLoaded)
            {
                _stand.name = "ClampStandModel";
                _stand.transform.SetParent(standContainer.transform, false);
                _stand.transform.localPosition = Vector3.zero;
                _stand.transform.localRotation = Quaternion.identity;
                _stand.transform.localScale = new Vector3(0.140f, 0.140f, 0.140f);
                foreach (var c in _stand.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                AlignToTableSurface(_stand, TABLE_TOP_Y);
            }
            else
            {
                _stand = ProcStand();
                _stand.transform.SetParent(standContainer.transform, false);
                _stand.transform.localPosition = Vector3.zero;
                AlignToTableSurface(_stand, TABLE_TOP_Y);
            }
            _stand = standContainer;
            var standCol = standContainer.GetComponent<Collider>() ?? standContainer.AddComponent<BoxCollider>();
            if (standCol is BoxCollider sbc)
            {
                sbc.size = new Vector3(0.20f, 0.40f, 0.20f);
                sbc.center = new Vector3(0f, 0.15f, 0f);
            }
            AddInteractable(_stand, HydrogenGasTestTag.Stand);
            foreach (var col in _stand.GetComponentsInChildren<Collider>(true))
            {
                AddInteractable(col.gameObject, HydrogenGasTestTag.Stand);
            }

            // ── 3. Test Tube (Separate Draggable Object at Startup) ────
            _testTubeOriginPos = POS_TUBE_START;
            var tubeContainer = new GameObject("TestTube");
            tubeContainer.transform.SetParent(transform, false);
            tubeContainer.transform.localPosition = _testTubeOriginPos;

            bool tubeLoaded = TryLoadModel("Test Tube", "TestTubeModel", out _testTube);
            if (tubeLoaded)
            {
                _testTube.name = "TestTubeModel";
                _testTube.transform.SetParent(tubeContainer.transform, false);
                _testTube.transform.localPosition = Vector3.zero;
                _testTube.transform.localRotation = Quaternion.identity;
                _testTube.transform.localScale = new Vector3(0.090f, 0.090f, 0.090f);
                foreach (var c in _testTube.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                EnhanceGlassMaterials(_testTube);
                AlignToTableSurface(tubeContainer, TABLE_TOP_Y);
            }
            else
            {
                _testTube = ProcTestTube();
                _testTube.transform.SetParent(tubeContainer.transform, false);
                _testTube.transform.localPosition = Vector3.zero;
                AlignToTableSurface(tubeContainer, TABLE_TOP_Y);
            }
            var tubeCol = tubeContainer.AddComponent<BoxCollider>();
            tubeCol.size = new Vector3(0.08f, 0.24f, 0.08f);
            tubeCol.center = Vector3.zero;   // container origin is the tube's centre (model pivot)
            AddInteractable(tubeContainer, HydrogenGasTestTag.TestTube);
            foreach (var col in tubeContainer.GetComponentsInChildren<Collider>(true))
            {
                AddInteractable(col.gameObject, HydrogenGasTestTag.TestTube);
            }
            _testTube = tubeContainer;

            // Measure the real tube (model pivot may not be at its base)
            _testTubeOriginPos = tubeContainer.transform.position;
            {
                Bounds tb = GetObjectBounds(tubeContainer);
                _testTubeH = Mathf.Clamp(tb.size.y, 0.08f, 0.30f);
                _tubeDia = Mathf.Clamp(Mathf.Min(tb.size.x, tb.size.z), 0.015f, 0.06f);
                _tubeOriginOffsetY = tubeContainer.transform.position.y - tb.min.y;
            }
            // Mounted in the clamp jaw (raised); Step 5 slides it down LOWER_DIST so the outlet dips into the soap
            _testTubeClampedPos = new Vector3(POS_TUBE_CLAMPED.x, _testTubeOriginPos.y + (POS_TUBE_CLAMPED.y - TABLE_TOP_Y), POS_TUBE_CLAMPED.z);
            _testTubeTopY = POS_TUBE_CLAMPED.y + _testTubeH;
            _liquidFullH = _testTubeH * 0.36f;
            tubeCol.size = new Vector3(0.07f, _testTubeH + 0.02f, 0.07f);

            // ── 4. Zinc Inside Tube & Dilute Acid (Hidden at Startup) ──
            _zincInTube = ProcZincSmall();
            _zincInTube.transform.SetParent(tubeContainer.transform, false);
            _zincInTube.transform.localPosition = new Vector3(0f, -_tubeOriginOffsetY + 0.014f, 0f);
            _zincInTube.transform.localScale    = new Vector3(1.10f, 1.10f, 1.10f);
            foreach (var r in _zincInTube.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial.renderQueue = 2998;
            _zincInTube.SetActive(false); // User must add in Step 3!

            _tubeLiquid = Cyl("TubeLiquid", tubeContainer.transform,
                new Vector3(0f, -_tubeOriginOffsetY + 0.01f, 0f), new Vector3(_tubeDia * 0.72f, 0.001f, _tubeDia * 0.72f), _liquidMat);
            _tubeLiquid.GetComponent<Renderer>().sharedMaterial.renderQueue = 2999;
            _tubeLiquid.SetActive(false); // Poured in during Step 3
            SetTubeLiquidHeight(0f);

            // ── 5. Zinc Granules on Table (Separate Draggable Dish/Cluster) ──
            _zincOriginPos = POS_ZINC_START;
            var zincContainer = new GameObject("ZincGranules");
            zincContainer.transform.SetParent(transform, false);
            zincContainer.transform.localPosition = _zincOriginPos;

            bool zincLoaded = TryLoadModel("Zinc Granules", "ZincGranulesModel", out _zincGranules);
            if (zincLoaded)
            {
                _zincGranules.name = "ZincGranulesModel";
                _zincGranules.transform.SetParent(zincContainer.transform, false);
                _zincGranules.transform.localPosition = Vector3.zero;
                _zincGranules.transform.localRotation = Quaternion.identity;
                _zincGranules.transform.localScale = new Vector3(0.080f, 0.080f, 0.080f);
                foreach (var c in _zincGranules.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                EnhanceZincMaterials(_zincGranules);
                AlignToTableSurface(zincContainer, TABLE_TOP_Y);
            }
            else
            {
                _zincGranules = ProcZincPile();
                _zincGranules.transform.SetParent(zincContainer.transform, false);
                _zincGranules.transform.localPosition = Vector3.zero;
                AlignToTableSurface(zincContainer, TABLE_TOP_Y);
            }
            var zincCol = zincContainer.AddComponent<BoxCollider>();
            zincCol.size = new Vector3(0.12f, 0.08f, 0.12f);
            zincCol.center = new Vector3(0f, 0.03f, 0f);
            AddInteractable(zincContainer, HydrogenGasTestTag.Zinc);
            _zincGranules = zincContainer;
            _zincOriginPos = zincContainer.transform.position;   // after aligning to the bench

            // ── 6. Sulphuric Acid Bottle (Separate Draggable Object) ───
            _acidBottleOriginPos = POS_ACID_START;
            var acidContainer = new GameObject("AcidBottle");
            acidContainer.transform.SetParent(transform, false);
            acidContainer.transform.localPosition = _acidBottleOriginPos;

            bool acidLoaded = TryLoadModel("Acid Bottle", "SulfuricAcidBottleModel", out _acidBottle);
            if (acidLoaded)
            {
                _acidBottle.name = "SulfuricAcidBottleModel";
                _acidBottle.transform.SetParent(acidContainer.transform, false);
                _acidBottle.transform.localPosition = Vector3.zero;
                _acidBottle.transform.localRotation = Quaternion.identity;
                _acidBottle.transform.localScale = new Vector3(0.085f, 0.085f, 0.085f);
                foreach (var c in _acidBottle.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                AlignToTableSurface(acidContainer, TABLE_TOP_Y);
            }
            else
            {
                _acidBottle = ProcAcidBottle();
                _acidBottle.transform.SetParent(acidContainer.transform, false);
                _acidBottle.transform.localPosition = Vector3.zero;
                AlignToTableSurface(acidContainer, TABLE_TOP_Y);
            }
            var acidCol = acidContainer.AddComponent<BoxCollider>();
            acidCol.size = new Vector3(0.10f, 0.20f, 0.10f);
            acidCol.center = new Vector3(0f, 0.08f, 0f);
            AddInteractable(acidContainer, HydrogenGasTestTag.Acid);
            _acidBottle = acidContainer;
            _acidBottleOriginPos = acidContainer.transform.position;   // after aligning to the bench
            _acidTopOffset = Mathf.Max(0.06f, GetObjectBounds(acidContainer).max.y - acidContainer.transform.position.y);

            // ── 7. Soap Solution Basin (Right) ────────────────────────
            var basinContainer = new GameObject("SoapBasin");
            basinContainer.transform.SetParent(transform, false);
            basinContainer.transform.localPosition = POS_BASIN;

            bool basinLoaded = TryLoadModel("Basin With Soap", "SoapBasinModel", out _soapBasin);
            if (basinLoaded)
            {
                _soapBasin.name = "SoapBasinModel";
                _soapBasin.transform.SetParent(basinContainer.transform, false);
                _soapBasin.transform.localPosition = Vector3.zero;
                _soapBasin.transform.localRotation = Quaternion.identity;
                _soapBasin.transform.localScale = new Vector3(0.082f, 0.082f, 0.082f);
                foreach (var c in _soapBasin.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                EnhanceGlassMaterials(_soapBasin);
                AlignToTableSurface(_soapBasin, TABLE_TOP_Y);
            }
            else
            {
                _soapBasin = ProcSoapBasin();
                _soapBasin.transform.SetParent(basinContainer.transform, false);
                _soapBasin.transform.localPosition = Vector3.zero;
                AlignToTableSurface(_soapBasin, TABLE_TOP_Y);
            }
            var basinCol = basinContainer.AddComponent<BoxCollider>();
            basinCol.size = new Vector3(0.18f, 0.10f, 0.18f);
            basinCol.center = new Vector3(0f, 0.035f, 0f);
            AddInteractable(basinContainer, HydrogenGasTestTag.SoapBasin);
            _soapBasin = basinContainer;

            _soapLiquid = Cyl("SoapLiquidSurface", basinContainer.transform,
                new Vector3(0f, 0.028f, 0f), new Vector3(0.130f, 0.018f, 0.130f), _soapMat);
            _soapLiquid.GetComponent<Renderer>().sharedMaterial.renderQueue = 2999;
            _soapLiquid.SetActive(true);

            _basinFinalPos = POS_BASIN;
            _liquidSurfaceY = TABLE_TOP_Y + 0.028f + 0.018f;

            // ── 8. Cork + Delivery Tube ─────────────────────────────────
            // Measured from the Meshy mesh (unit scale): cork stopper bottom-centre (0.603, 0.328), outlet (-0.72, -0.93),
            // with the cork on +X. Mirroring X puts the cork over the test tube (left) and the outlet over the basin (right).
            const float MC_X = 0.603f, MC_Y = 0.328f, MT_X = -0.72f, MT_Y = -0.93f;
            float spanDist = POS_BASIN.x - POS_TUBE_CLAMPED.x;                 // cork -> outlet, horizontal
            float scaleX   = spanDist / (MC_X - MT_X);
            // While the tube is clamped high the outlet hovers TUBE_LOWER_DIST above where it dips 2 cm into the soap.
            float tipHighY = _liquidSurfaceY - 0.02f + TUBE_LOWER_DIST;
            float scaleYZ  = Mathf.Clamp((_testTubeTopY - tipHighY) / (MC_Y - MT_Y), 0.10f, 0.25f);
            float tubeDrop = (MC_Y - MT_Y) * scaleYZ;
            _corkSealedX = POS_TUBE_CLAMPED.x;

            // The whole assembly is created in the pose "hovering just above the tube mouth" and stays hidden until Step 4.
            _corkOriginPos = new Vector3(POS_TUBE_CLAMPED.x, _testTubeTopY + 0.10f, POS_TUBE_CLAMPED.z);
            var corkContainer = new GameObject("CorkDeliveryTube");
            corkContainer.transform.SetParent(transform, false);
            corkContainer.transform.localPosition = _corkOriginPos;

            bool corkLoaded = TryLoadModel("Cork With Delivery", "CorkWithDeliveryTubeModel", out _corkDeliveryTube);
            if (corkLoaded)
            {
                _corkDeliveryTube.name = "CorkWithDeliveryTubeModel";
                _corkDeliveryTube.transform.SetParent(corkContainer.transform, false);
                // Put the stopper's bottom-centre exactly on the container origin
                _corkDeliveryTube.transform.localPosition = new Vector3(MC_X * scaleX, -MC_Y * scaleYZ, 0f);
                _corkDeliveryTube.transform.localRotation = Quaternion.identity;
                _corkDeliveryTube.transform.localScale = new Vector3(-scaleX, scaleYZ, scaleYZ);   // -X = mirror
                foreach (var c in _corkDeliveryTube.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                EnhanceCorkMaterials(_corkDeliveryTube);
            }
            else
            {
                _corkDeliveryTube = ProcCorkDeliveryTube();
                _corkDeliveryTube.transform.SetParent(corkContainer.transform, false);
                _corkDeliveryTube.transform.localPosition = Vector3.zero;
            }

            // Cork stopper collider (container origin = stopper bottom)
            var corkCol = corkContainer.AddComponent<BoxCollider>();
            corkCol.size = new Vector3(0.12f, 0.12f, 0.12f);
            corkCol.center = new Vector3(0f, 0.03f, 0f);
            AddInteractable(corkContainer, HydrogenGasTestTag.Cork);

            // Child collider on the tube outlet so tapping the open end also registers as Cork
            var outletColGO = new GameObject("TubeOutletCollider");
            outletColGO.transform.SetParent(corkContainer.transform, false);
            outletColGO.transform.localPosition = new Vector3(spanDist, -tubeDrop, 0f);
            var oc = outletColGO.AddComponent<SphereCollider>();
            oc.radius = 0.06f;
            AddInteractable(outletColGO, HydrogenGasTestTag.Cork);

            _corkDeliveryTube = corkContainer;
            // Lies on the bench (back row, between the stand and the basin) until Step 4 lifts it over the tube mouth.
            _corkRestPos = new Vector3(-0.13f, TABLE_TOP_Y + 0.173f * scaleYZ * CORK_REST_SCALE + 0.002f, 0.31f);
            corkContainer.transform.SetPositionAndRotation(_corkRestPos, CORK_REST_ROT);
            corkContainer.transform.localScale = Vector3.one * CORK_REST_SCALE;

            // Path the hydrogen follows (measured tube centre-line, converted to container space):
            // up out of the stopper, across the top, then down the slanted leg to the outlet.
            Vector3[] mp =
            {
                new Vector3( 0.603f, 0.36f, 0f), new Vector3( 0.603f, 0.90f, 0f), new Vector3(-0.020f, 0.90f, 0f),
                new Vector3(-0.260f, 0.20f, 0f), new Vector3(-0.550f, -0.50f, 0f), new Vector3(-0.720f, -0.91f, 0f)
            };
            _tubePathLocal = new Vector3[mp.Length];
            for (int i = 0; i < mp.Length; i++)
                _tubePathLocal[i] = new Vector3((MC_X - mp[i].x) * scaleX, (mp[i].y - MC_Y) * scaleYZ, 0f);
            // final (dipped) outlet position
            _tubeTipWorld = new Vector3(POS_TUBE_CLAMPED.x + spanDist, _testTubeTopY - tubeDrop - TUBE_LOWER_DIST, POS_TUBE_CLAMPED.z);

            // ── 9. Candle (Resting on brown board, flame OFF at start) ──
            _candleOriginPos = POS_CANDLE;
            var candleContainer = new GameObject("Candle");
            candleContainer.transform.SetParent(transform, false);
            candleContainer.transform.localPosition = _candleOriginPos;

            bool candleLoaded = TryLoadModel("Candle", "CandleModel", out _candle);
            if (candleLoaded)
            {
                _candle.name = "CandleModel";
                _candle.transform.SetParent(candleContainer.transform, false);
                _candle.transform.localPosition = Vector3.zero;
                _candle.transform.localScale = new Vector3(0.075f, 0.075f, 0.075f);
                foreach (var c in _candle.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                AlignToTableSurface(_candle, TABLE_TOP_Y);
            }
            else
            {
                _candle = ProcCandle();
                _candle.transform.SetParent(candleContainer.transform, false);
                _candle.transform.localPosition = Vector3.zero;
                AlignToTableSurface(_candle, TABLE_TOP_Y);
            }
            var candleCol = candleContainer.AddComponent<BoxCollider>();
            candleCol.size = new Vector3(0.08f, 0.18f, 0.08f);
            candleCol.center = new Vector3(0f, 0.08f, 0f);
            AddInteractable(candleContainer, HydrogenGasTestTag.Candle);
            _candleOriginPos = candleContainer.transform.position;

            float candleTopY = GetObjectTopY(candleContainer);
            if (candleTopY <= TABLE_TOP_Y + 0.04f) candleTopY = TABLE_TOP_Y + 0.12f;

            _candleFlame = ProcFlame();
            _candleFlame.transform.SetParent(candleContainer.transform, true);
            _candleFlame.transform.position = new Vector3(candleContainer.transform.position.x, candleTopY + 0.012f, candleContainer.transform.position.z);
            _candleFlame.SetActive(false); // NO flame at Step 1

            var clGO = new GameObject("CandleLight");
            clGO.transform.SetParent(_candleFlame.transform, false);
            _candleLight = clGO.AddComponent<Light>();
            _candleLight.type = LightType.Point;
            _candleLight.range = 0.22f;
            _candleLight.intensity = 0.35f;
            _candleLight.color = new Color(1f, 0.75f, 0.35f);

            _candle = candleContainer;
            _candle.SetActive(true);

            // ── 10. Gas dots that travel along the delivery tube ──────
            BuildGasDots();
            BuildPourStream();
            BuildStarFlash();

            // ── 11. Particle Systems (All STOPPED at Step 1) ──────────
            var bubbleAnchor = new GameObject("ReactionBubbleAnchor");
            bubbleAnchor.transform.SetParent(tubeContainer.transform, false);
            bubbleAnchor.transform.localPosition = new Vector3(0f, -_tubeOriginOffsetY + 0.012f, 0f);
            _reactionBubblePS = BuildTubeBubbles(bubbleAnchor.transform);

            // Underwater fizz where the outlet dips into the soap solution (world-space, stays at the tip)
            var tipAnchor = new GameObject("SubmergedTip");
            tipAnchor.transform.SetParent(transform, true);
            tipAnchor.transform.position = _tubeTipWorld;
            _soapBubblePS = BuildSoapBubbles(tipAnchor.transform);

            _popPS = BuildPopVFX(new Vector3(POS_BASIN.x, TABLE_TOP_Y + 0.150f, POS_BASIN.z));

            var popLightGO = new GameObject("PopLight");
            popLightGO.transform.SetParent(transform, true);
            popLightGO.transform.position = new Vector3(POS_BASIN.x, TABLE_TOP_Y + 0.150f, POS_BASIN.z);
            _popLight = popLightGO.AddComponent<Light>();
            _popLight.type = LightType.Point;
            _popLight.range = 0.32f;
            _popLight.intensity = 0f;
            _popLight.color = new Color(1f, 0.85f, 0.4f);

            StopAndClearAllVFX();

            // ── 12. Placement & Trigger Zones ─────────────────────────
            _standClampZone     = Zone("Zone_StandClamp",    new Vector3(POS_TUBE_CLAMPED.x, POS_TUBE_CLAMPED.y + _testTubeH * 0.5f, POS_TUBE_CLAMPED.z), new Vector3(0.18f, 0.25f, 0.18f));
            _testTubeMouthZone  = Zone("Zone_TubeMouth",     new Vector3(POS_TUBE_CLAMPED.x, _testTubeTopY, POS_TUBE_CLAMPED.z), new Vector3(0.18f, 0.20f, 0.18f));
            _corkZone           = Zone("Zone_CorkNeck",      new Vector3(POS_TUBE_CLAMPED.x, _testTubeTopY, POS_TUBE_CLAMPED.z), new Vector3(0.18f, 0.20f, 0.18f));
            _soapBasinZone      = Zone("Zone_SoapBasin",     new Vector3(POS_BASIN.x, TABLE_TOP_Y + 0.040f, POS_BASIN.z), new Vector3(0.24f, 0.18f, 0.24f));
            _bubbleZone         = Zone("Zone_BubbleTarget",  new Vector3(POS_BASIN.x, TABLE_TOP_Y + 0.150f, POS_BASIN.z), new Vector3(0.20f, 0.20f, 0.20f));
        }

        private void StopAndClearAllVFX()
        {
            if (_reactionBubblePS != null)
            {
                var em = _reactionBubblePS.emission;
                em.enabled = false;
                em.rateOverTime = 0f;
                _reactionBubblePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _reactionBubblePS.Clear(true);
            }
            _gasFlowing = false;
            foreach (var d in _gasDots) if (d != null) d.gameObject.SetActive(false);
            ClearBubbles();
            if (_pourStream != null) _pourStream.SetActive(false);
            if (_soapBubblePS != null)
            {
                var em = _soapBubblePS.emission;
                em.enabled = false;
                em.rateOverTime = 0f;
                _soapBubblePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _soapBubblePS.Clear(true);
            }
            if (_popPS != null)
            {
                var em = _popPS.emission;
                em.enabled = false;
                _popPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _popPS.Clear(true);
            }
            if (_popLight != null) _popLight.intensity = 0f;
        }

        // =========================================================
        //  Model Loader (Resources -> AssetDatabase -> Procedural)
        // =========================================================
        private bool TryLoadModel(string keyword, string prefabName, out GameObject go)
        {
            var prefab = Resources.Load<GameObject>("Models/" + prefabName);
            if (prefab != null)
            {
                go = Instantiate(prefab);
                go.transform.SetParent(transform, true);
                return true;
            }

#if UNITY_EDITOR
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:GameObject", new[] { "Assets/ThirdParty", "Assets/Models" });
            foreach (var g in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(g);
                if (path.ToLower().Contains(keyword.ToLower().Replace(" ", "_")) ||
                    path.ToLower().Contains(prefabName.ToLower()))
                {
                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset != null)
                    {
                        go = Instantiate(asset);
                        go.transform.SetParent(transform, true);
                        return true;
                    }
                }
            }
#endif
            go = null;
            return false;
        }

        private void EnhanceGlassMaterials(GameObject go)
        {
            if (go == null) return;
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                int matCount = Mathf.Max(r.sharedMaterials.Length, 1);
                var newMats  = new Material[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    var glassMat = new Material(litShader);
                    ApplyURPTransparent(glassMat, new Color(0.82f, 0.94f, 1.00f, 0.22f));
                    glassMat.SetFloat("_Smoothness", 0.95f);
                    glassMat.SetFloat("_Metallic",   0.04f);
                    glassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                    newMats[i] = glassMat;
                }
                r.sharedMaterials = newMats;
            }
        }

        private void EnhanceZincMaterials(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_BaseColor"))
                        mat.SetColor("_BaseColor", new Color(0.90f, 0.92f, 0.95f, 1f));
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.65f);
                    if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0.85f);
                }
            }
        }

        private void EnhanceCorkMaterials(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_BaseColor"))
                    {
                        var c = mat.GetColor("_BaseColor");
                        mat.SetColor("_BaseColor", new Color(
                            Mathf.Clamp(c.r, 0.75f, 0.88f),
                            Mathf.Clamp(c.g, 0.55f, 0.68f),
                            Mathf.Clamp(c.b, 0.32f, 0.45f), 1f));
                    }
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.25f);
                    if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",   0.02f);
                }
            }
        }

        private void EnhanceAcidBottleMaterials(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.70f);
                }
            }
        }

        private void AlignToTableSurface(GameObject go, float targetSurfaceY)
        {
            if (go == null) return;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;
            float minY = float.MaxValue;
            foreach (var r in renderers)
            {
                if (r.bounds.min.y < minY) minY = r.bounds.min.y;
            }
            if (minY < float.MaxValue)
            {
                float diff = targetSurfaceY - minY;
                go.transform.position += new Vector3(0f, diff, 0f);
            }
        }

        private float GetObjectTopY(GameObject go)
        {
            if (go == null) return TABLE_TOP_Y;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return TABLE_TOP_Y;
            float maxY = float.MinValue;
            foreach (var r in renderers)
            {
                if (r.bounds.max.y > maxY) maxY = r.bounds.max.y;
            }
            return maxY > float.MinValue ? maxY : TABLE_TOP_Y;
        }

        // =========================================================
        //  Procedural Fallbacks
        // =========================================================
        private GameObject ProcStand()
        {
            var root = new GameObject("Stand_Proc");
            root.transform.SetParent(transform, true);
            Box("Base", root.transform, Vector3.zero, new Vector3(0.12f, 0.012f, 0.12f), _metalMat);
            Cyl("Rod",  root.transform, new Vector3(-0.035f, 0.11f, 0), new Vector3(0.007f, 0.22f, 0.007f), _metalMat);
            Box("ClampBoss", root.transform, new Vector3(-0.035f, 0.13f, 0), new Vector3(0.018f, 0.018f, 0.018f), _metalMat);
            Box("ClampArm",  root.transform, new Vector3(0.005f, 0.13f, 0), new Vector3(0.06f, 0.008f, 0.008f), _metalMat);
            return root;
        }

        private GameObject ProcTestTube()
        {
            var root = new GameObject("TestTube_Proc");
            root.transform.SetParent(transform, true);
            Cyl("Body", root.transform, new Vector3(0, 0.075f, 0), new Vector3(0.022f, 0.15f, 0.022f), _glassMat);
            var b = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            b.transform.SetParent(root.transform, false);
            b.transform.localPosition = new Vector3(0, 0.005f, 0);
            b.transform.localScale = Vector3.one * 0.022f;
            b.GetComponent<Renderer>().sharedMaterial = _glassMat;
            Destroy(b.GetComponent<Collider>());
            return root;
        }

        private GameObject ProcSoapBasin()
        {
            var root = new GameObject("SoapBasin_Proc");
            root.transform.SetParent(transform, true);
            Cyl("Trough", root.transform, new Vector3(0, 0.020f, 0), new Vector3(0.12f, 0.04f, 0.12f), _glassMat);
            return root;
        }

        private GameObject ProcCorkDeliveryTube()
        {
            var root = new GameObject("CorkDeliveryTube_Proc");
            root.transform.SetParent(transform, true);
            Cyl("Cork", root.transform, Vector3.zero, new Vector3(0.024f, 0.026f, 0.024f), _corkMat);
            var tubeMat = SimpleMat(new Color(0.85f, 0.95f, 1f, 0.35f), 0.92f);
            Cyl("Rise", root.transform, new Vector3(0, 0.040f, 0), new Vector3(0.006f, 0.060f, 0.006f), tubeMat);
            float spanDist = POS_BASIN.x - POS_TUBE_CLAMPED.x;
            var h = Cyl("Span", root.transform, new Vector3(spanDist * 0.5f, 0.070f, 0), new Vector3(0.006f, spanDist, 0.006f), tubeMat);
            h.transform.localEulerAngles = new Vector3(0, 0, 90f);
            Cyl("Drop", root.transform, new Vector3(spanDist, 0.015f, 0), new Vector3(0.006f, 0.110f, 0.006f), tubeMat);
            return root;
        }

        private GameObject ProcCandle()
        {
            var root = new GameObject("Candle_Proc");
            root.transform.SetParent(transform, true);
            Cyl("Wax", root.transform, new Vector3(0, 0.05f, 0), new Vector3(0.020f, 0.10f, 0.020f), _candleMat);
            Cyl("Wick", root.transform, new Vector3(0, 0.105f, 0), new Vector3(0.002f, 0.010f, 0.002f), SimpleMat(Color.black));
            return root;
        }

        private GameObject ProcFlame()
        {
            var root = new GameObject("Flame");
            var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            inner.transform.SetParent(root.transform, false);
            inner.transform.localPosition = new Vector3(0, 0.006f, 0);
            inner.transform.localScale    = new Vector3(0.010f, 0.016f, 0.010f);
            inner.GetComponent<Renderer>().material = SimpleMat(new Color(1f, 0.95f, 0.5f, 0.90f));
            Destroy(inner.GetComponent<Collider>());

            var outer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            outer.transform.SetParent(root.transform, false);
            outer.transform.localPosition = Vector3.zero;
            outer.transform.localScale    = new Vector3(0.018f, 0.024f, 0.018f);
            outer.GetComponent<Renderer>().material = SimpleMat(new Color(1f, 0.45f, 0.05f, 0.70f));
            Destroy(outer.GetComponent<Collider>());
            return root;
        }

        private GameObject ProcZincSmall()
        {
            var root = new GameObject("ZincInTube");
            root.transform.SetParent(transform, true);
            for (int i = 0; i < 5; i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.transform.SetParent(root.transform, false);
                float a = i * 72f * Mathf.Deg2Rad;
                s.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.005f, 0f, Mathf.Sin(a) * 0.005f);
                s.transform.localScale    = Vector3.one * 0.006f;
                s.GetComponent<Renderer>().sharedMaterial = _zincMat;
                Destroy(s.GetComponent<Collider>());
            }
            return root;
        }

        private GameObject ProcZincPile()
        {
            var root = new GameObject("ZincGranules_Proc");
            root.transform.SetParent(transform, true);
            float[][] cfg = {
                new[]{0f,    0f,    0f,    0.016f},
                new[]{0.015f,0f,    0.010f,0.014f},
                new[]{-0.012f,0f,  0.012f,0.012f},
                new[]{0.008f,0f,   -0.015f,0.013f},
                new[]{-0.016f,0f,  -0.008f,0.011f},
                new[]{0f,   0.012f, 0.005f,0.010f}
            };
            foreach (var c in cfg)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.transform.SetParent(root.transform, false);
                s.transform.localPosition = new Vector3(c[0], c[1], c[2]);
                s.transform.localScale    = new Vector3(c[3], c[3] * 0.85f, c[3]);
                s.GetComponent<Renderer>().sharedMaterial = _zincMat;
                Destroy(s.GetComponent<Collider>());
            }
            return root;
        }

        private GameObject ProcAcidBottle()
        {
            var root = new GameObject("AcidBottle_Proc");
            root.transform.SetParent(transform, true);
            Cyl("Body", root.transform, new Vector3(0, 0, 0),     new Vector3(0.030f, 0.055f, 0.030f), _acidMat);
            Cyl("Neck", root.transform, new Vector3(0, 0.062f, 0), new Vector3(0.012f, 0.020f, 0.012f), _acidMat);
            Cyl("Cap",  root.transform, new Vector3(0, 0.080f, 0), new Vector3(0.014f, 0.008f, 0.014f), SimpleMat(new Color(0.85f, 0.1f, 0.1f)));
            return root;
        }

        private ParticleSystem BuildGasFlow(Transform parent)
        {
            var go = new GameObject("H2GasFlow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop = true;
            main.playOnAwake = false;
            main.maxParticles = 40;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.8f, 1.5f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.014f);
            main.startColor = new Color(0.88f, 0.95f, 1.0f, 0.35f);
            main.gravityModifier = -0.08f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var em = ps.emission;
            em.enabled = false;
            em.rateOverTime = 0f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 10f;
            sh.radius = 0.006f;

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = SimpleMat(new Color(0.80f, 0.92f, 1.0f, 0.30f));

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            return ps;
        }

        // =========================================================
        //  Particle Systems
        // =========================================================
        private ParticleSystem BuildTubeBubbles(Transform parent)
        {
            var go = new GameObject("ReactionBubbles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop           = true;
            main.playOnAwake    = false;
            main.maxParticles   = 60;
            main.startLifetime  = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed     = new ParticleSystem.MinMaxCurve(0.06f, 0.10f);
            main.startSize      = new ParticleSystem.MinMaxCurve(0.004f, 0.008f);
            main.startColor     = new Color(0.90f, 0.98f, 1.0f, 0.85f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var em = ps.emission;
            em.enabled = false;
            em.rateOverTime = 0f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 4f;
            sh.radius = Mathf.Max(0.004f, _tubeDia * 0.25f);
            sh.rotation = new Vector3(-90f, 0f, 0f);   // shoot up the tube

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material   = ParticleMat(new Color(0.90f, 0.98f, 1.0f, 0.85f));

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            return ps;
        }

        private ParticleSystem BuildSoapBubbles(Transform parent)
        {
            var go = new GameObject("SoapBubbles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop           = true;
            main.playOnAwake    = false;
            main.maxParticles   = 40;
            main.startLifetime  = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);   // short: fizz stays inside the soap solution
            main.startSpeed     = new ParticleSystem.MinMaxCurve(0.03f, 0.05f);
            main.startSize      = new ParticleSystem.MinMaxCurve(0.005f, 0.011f);
            main.startColor     = new Color(0.92f, 0.98f, 1.0f, 0.80f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.enabled = false;
            em.rateOverTime = 0f;

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle = 18f;
            sh.radius = 0.006f;
            sh.rotation = new Vector3(-90f, 0f, 0f);   // cone axis points up (default shape axis is +Z)

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material   = ParticleMat(new Color(0.92f, 0.98f, 1.0f, 0.80f));

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            return ps;
        }

        private ParticleSystem BuildPopVFX(Vector3 pos)
        {
            var go = new GameObject("PopVFX");
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop = false;
            main.playOnAwake = false;
            main.maxParticles = 50;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.45f);
            main.startSpeed    = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSize     = new ParticleSystem.MinMaxCurve(0.012f, 0.028f);
            main.startColor    = new ParticleSystem.MinMaxGradient(new Color(1f, 0.9f, 0.3f, 0.95f), new Color(1f, 0.45f, 0.05f, 0.8f));
            main.gravityModifier = 0.15f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.enabled = false;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 35) });

            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Sphere;
            sh.radius = 0.015f;

            go.GetComponent<ParticleSystemRenderer>().material = ParticleMat(new Color(1f, 0.75f, 0.15f, 0.95f));
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            return ps;
        }

        // =========================================================
        //  Animation, gas travel, bubbles & pop effects
        // =========================================================
        private static Texture2D _softDotTex;
        private static Texture2D SoftDotTex()
        {
            if (_softDotTex != null) return _softDotTex;
            const int N = 64;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(1f - r);
                a = Mathf.Pow(a, 0.6f) * SS(0f, 1f, (1f - r) * 6f);   // soft-edged round dot
                t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            t.Apply();
            return _softDotTex = t;
        }

        /// <summary>GLSL-style smoothstep (Mathf.SmoothStep has a different signature).</summary>
        private static float SS(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D _bubbleTex;
        /// <summary>Soap-bubble sprite: transparent centre, bright iridescent rim, small specular highlight.</summary>
        private static Texture2D BubbleTex()
        {
            if (_bubbleTex != null) return _bubbleTex;
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float th = Mathf.Atan2(dy, dx);
                float edge = 1f - SS(0.965f, 1.0f, r);                       // crisp outer edge
                float rim  = SS(0.72f, 0.95f, r) * edge;                       // ring
                float body = 0.10f * edge;                                                   // faint film across the middle
                float hue  = 0.55f + 0.18f * Mathf.Sin(th * 2f + 0.8f);                      // cyan..violet/pink shimmer
                Color tint = Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.25f, 1f);
                float hx = dx + 0.38f, hy = dy - 0.42f;                                      // highlight, upper-left
                float hl = Mathf.Clamp01(1f - Mathf.Sqrt(hx * hx + hy * hy) / 0.20f);
                hl = hl * hl;
                float a = Mathf.Clamp01(body + rim * 0.80f + hl * 0.9f);
                Color c = Color.Lerp(tint, Color.white, Mathf.Clamp01(hl * 1.2f));
                c.a = a;
                t.SetPixel(x, y, c);
            }
            t.Apply();
            return _bubbleTex = t;
        }

        private Material _bubbleSpriteMat;
        private Material BubbleSpriteMat()
        {
            if (_bubbleSpriteMat != null) return _bubbleSpriteMat;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", BubbleTex());
            m.mainTexture = BubbleTex();
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            m.renderQueue = 3001;
            return _bubbleSpriteMat = m;
        }

        private Material ParticleMat(Color c)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(s);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", SoftDotTex());
            m.mainTexture = SoftDotTex();
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            else m.color = c;
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5;
            }
            return m;
        }

        private static Bounds GetObjectBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool first = true;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Sets how full the test tube is with dilute acid (0 = empty).</summary>
        private void SetTubeLiquidHeight(float h)
        {
            if (_tubeLiquid == null) return;
            h = Mathf.Max(0.0005f, h);
            var t = _tubeLiquid.transform;
            t.localScale    = new Vector3(_tubeDia * 0.72f, h * 0.5f, _tubeDia * 0.72f);   // Unity cylinder is 2 units tall
            t.localPosition = new Vector3(0f, -_tubeOriginOffsetY + 0.006f + h * 0.5f, 0f);
        }

        private void SetEffervescence(float rate)
        {
            if (_reactionBubblePS == null) return;
            var em = _reactionBubblePS.emission;
            if (rate <= 0f)
            {
                em.enabled = false;
                em.rateOverTime = 0f;
                _reactionBubblePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
                return;
            }
            // keep the fizz inside the liquid column
            var main = _reactionBubblePS.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            float rise = Mathf.Max(0.3f, _liquidFullH / 0.08f);          // seconds to cross the liquid column at ~8 cm/s
            main.startLifetime = new ParticleSystem.MinMaxCurve(rise * 0.5f, rise);
            em.enabled = true;
            em.rateOverTime = rate;
            if (!_reactionBubblePS.isPlaying) _reactionBubblePS.Play();
        }

        private IEnumerator MoveRot(Transform t, Vector3 pos, Quaternion rot, float dur)
        {
            Vector3 p0 = t.position;
            Quaternion r0 = t.rotation;
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                t.position = Vector3.Lerp(p0, pos, k);
                t.rotation = Quaternion.Slerp(r0, rot, k);
                yield return null;
            }
            t.position = pos;
            t.rotation = rot;
        }

        // ---- Gas dots travelling along the delivery tube --------------------------------
        private void BuildGasDots()
        {
            var mat = new Material(_bubbleMat);
            ApplyURPTransparent(mat, new Color(0.40f, 0.58f, 0.76f, 0.95f));   // mid blue-grey: visible on the light background
            mat.renderQueue = 3002;
            for (int i = 0; i < 12; i++)
            {
                var d = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                d.name = "H2GasDot";
                d.transform.SetParent(transform, true);
                d.transform.localScale = Vector3.one * 0.011f;
                Destroy(d.GetComponent<Collider>());
                var r = d.GetComponent<Renderer>();
                r.sharedMaterial = mat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.SetActive(false);
                _gasDots.Add(d.transform);
                _gasDotT.Add(-i / 12f);    // staggered along the tube
            }
        }

        private Vector3 TubePoint(float t)
        {
            if (_tubePathLocal == null || _tubePathLocal.Length < 2 || _corkDeliveryTube == null) return _tubeTipWorld;
            float total = 0f;
            for (int i = 1; i < _tubePathLocal.Length; i++) total += Vector3.Distance(_tubePathLocal[i - 1], _tubePathLocal[i]);
            float dist = Mathf.Clamp01(t) * total;
            for (int i = 1; i < _tubePathLocal.Length; i++)
            {
                float seg = Vector3.Distance(_tubePathLocal[i - 1], _tubePathLocal[i]);
                if (dist <= seg || i == _tubePathLocal.Length - 1)
                {
                    Vector3 l = Vector3.Lerp(_tubePathLocal[i - 1], _tubePathLocal[i], seg > 0f ? Mathf.Clamp01(dist / seg) : 0f);
                    return _corkDeliveryTube.transform.TransformPoint(l);
                }
                dist -= seg;
            }
            return _tubeTipWorld;
        }

        private void UpdateGasDots()
        {
            if (!_gasFlowing) return;
            _gasFlowTime += Time.deltaTime;
            for (int i = 0; i < _gasDots.Count; i++)
            {
                var d = _gasDots[i];
                if (d == null) continue;
                float t = _gasDotT[i] + Time.deltaTime * 0.28f;
                if (t > 1f) t -= 1.1f;      // recycle from the cork end
                _gasDotT[i] = t;
                bool show = t >= 0f && t <= 1f;
                if (d.gameObject.activeSelf != show) d.gameObject.SetActive(show);
                if (show) d.position = TubePoint(t);
            }
        }

        private void StartGasFlow()
        {
            _gasFlowing = true;
            _gasFlowTime = 0f;
            for (int i = 0; i < _gasDotT.Count; i++) _gasDotT[i] = -i / 12f;
            // underwater fizz once the first gas reaches the outlet
            StartCoroutine(TipBubblesAfter(2.2f));
        }

        private IEnumerator TipBubblesAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (!_gasFlowing || _soapBubblePS == null) yield break;
            var em = _soapBubblePS.emission;
            em.enabled = true;
            em.rateOverTime = 22f;
            _soapBubblePS.Play();
        }

        // ---- Hydrogen soap bubbles ------------------------------------------------------
        private void SpawnBubble()
        {
            if (_bubbles.Count >= MAX_BUBBLES) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);   // billboarded bubble sprite
            go.name = "H2SoapBubble";
            go.transform.SetParent(transform, true);
            Destroy(go.GetComponent<Collider>());
            var rend = go.GetComponent<Renderer>();
            rend.sharedMaterial = BubbleSpriteMat();
            rend.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.6f;                          // generous tap target (a bit bigger than the bubble)
            AddInteractable(go, HydrogenGasTestTag.Bubble);

            float size = UnityEngine.Random.Range(0.030f, 0.054f);
            var b = new HBubble
            {
                go = go,
                size = size,
                phase = UnityEngine.Random.value * 6.28f,
                riseSpeed = UnityEngine.Random.Range(0.025f, 0.045f),
                maxRise = UnityEngine.Random.Range(0.10f, 0.20f),
                drift = new Vector3(UnityEngine.Random.Range(-0.010f, 0.016f), 0f, UnityEngine.Random.Range(-0.008f, 0.008f)),
                basePos = new Vector3(_tubeTipWorld.x + UnityEngine.Random.Range(-0.02f, 0.02f),
                                      _liquidSurfaceY + size * 0.4f,
                                      _tubeTipWorld.z + UnityEngine.Random.Range(-0.02f, 0.02f))
            };
            go.transform.position = b.basePos;
            go.transform.localScale = Vector3.zero;
            _bubbles.Add(b);
        }

        private void UpdateBubbles()
        {
            if (_bubblesEmitting)
            {
                _bubbleTimer -= Time.deltaTime;
                if (_bubbleTimer <= 0f)
                {
                    SpawnBubble();
                    _bubbleTimer = UnityEngine.Random.Range(0.7f, 1.3f);
                }
            }

            for (int i = _bubbles.Count - 1; i >= 0; i--)
            {
                var b = _bubbles[i];
                if (b.go == null) { _bubbles.RemoveAt(i); continue; }
                b.age += Time.deltaTime;
                float grow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(b.age / 0.9f));
                float rise = Mathf.Min(b.age * b.riseSpeed, b.maxRise);
                float dAge = Mathf.Min(b.age, 6f);
                b.go.transform.position = b.basePos + new Vector3(
                    b.drift.x * dAge + Mathf.Sin(b.age * 1.3f + b.phase) * 0.006f,
                    rise + Mathf.Sin(b.age * 1.9f + b.phase) * 0.004f,
                    b.drift.z * dAge);
                b.go.transform.localScale = Vector3.one * (b.size * grow);
                if (_cam != null) b.go.transform.rotation = _cam.transform.rotation;
            }

            // A lit candle flame touching any hydrogen bubble makes it burn with a pop
            if (_step == HydrogenGasTestStep.Step8_HydrogenCandlePopTest && _candleLit && !_popTriggered &&
                _candleFlame != null && _candleFlame.activeInHierarchy)
            {
                // The candle is dragged in a plane parallel to the screen, so "touching" is judged along the line of
                // sight: the flame must overlap the bubble on screen and be at roughly the same depth (within 30 cm).
                Vector3 f = _candleFlame.transform.position;
                Vector3 cp = _cam != null ? _cam.transform.position : Vector3.zero;
                Vector3 toF = f - cp;
                float fDist = toF.magnitude;
                Vector3 dir = fDist > 0.0001f ? toF / fDist : Vector3.forward;
                for (int i = 0; i < _bubbles.Count; i++)
                {
                    var b = _bubbles[i];
                    if (b.go == null || b.age < 0.9f) continue;
                    Vector3 toB = b.go.transform.position - cp;
                    float along = Vector3.Dot(toB, dir);
                    float perp  = (toB - dir * along).magnitude;
                    if (perp < b.size * 0.5f + 0.02f && Mathf.Abs(along - fDist) < 0.30f)
                    {
                        PopBubble(b);
                        break;
                    }
                }
            }
        }

        private void ClearBubbles()
        {
            _bubblesEmitting = false;
            foreach (var b in _bubbles) if (b != null && b.go != null) Destroy(b.go);
            _bubbles.Clear();
        }

        private HBubble NearestBubbleTo(Vector3 p)
        {
            HBubble best = null; float bd = float.MaxValue;
            foreach (var b in _bubbles)
            {
                if (b == null || b.go == null) continue;
                float d = Vector3.Distance(p, b.go.transform.position);
                if (d < bd) { bd = d; best = b; }
            }
            return best;
        }

        private void UpdateCandleFlame()
        {
            if (_candleFlame == null || !_candleFlame.activeSelf) return;
            float n = Mathf.PerlinNoise(Time.time * 9f, 0.3f);
            _candleFlame.transform.localScale = new Vector3(1f + (n - 0.5f) * 0.25f, 1f + (n - 0.3f) * 0.45f, 1f + (n - 0.5f) * 0.25f);
            if (_candleLight != null) _candleLight.intensity = 0.25f + n * 0.25f;
        }

        // ---- Pour stream & star-burst pop ------------------------------------------------
        private void BuildPourStream()
        {
            _pourStream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _pourStream.name = "AcidPourStream";
            _pourStream.transform.SetParent(transform, true);
            Destroy(_pourStream.GetComponent<Collider>());
            var r = _pourStream.GetComponent<Renderer>();
            r.sharedMaterial = _liquidMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pourStream.SetActive(false);
        }

        private void PlacePourStream(Vector3 from, Vector3 to)
        {
            if (_pourStream == null) return;
            Vector3 mid = (from + to) * 0.5f;
            float len = Mathf.Max(0.001f, (from - to).magnitude);
            _pourStream.transform.position = mid;
            _pourStream.transform.localScale = new Vector3(0.0045f, len * 0.5f, 0.0045f);
            _pourStream.transform.rotation = Quaternion.FromToRotation(Vector3.up, (from - to).normalized);
            _pourStream.SetActive(true);
        }

        private void BuildStarFlash()
        {
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f;
                float dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float th = Mathf.Atan2(dy, dx);
                // spiky star like the textbook "pop" burst: 9 long points, short valleys
                float spike = 0.28f + 0.70f * Mathf.Pow(Mathf.Abs(Mathf.Cos(th * 4.5f)), 5f);
                float a = r < spike ? Mathf.Pow(1f - r / spike, 0.45f) : 0f;
                Color c = Color.Lerp(new Color(1f, 0.98f, 0.75f), new Color(1f, 0.55f, 0.10f), Mathf.Clamp01(r / spike));
                c.a = a;
                tex.SetPixel(x, y, c);
            }
            tex.Apply();

            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _starMat = new Material(s);
            if (_starMat.HasProperty("_BaseMap")) _starMat.SetTexture("_BaseMap", tex);
            _starMat.mainTexture = tex;
            if (_starMat.HasProperty("_BaseColor")) _starMat.SetColor("_BaseColor", Color.white);
            if (_starMat.HasProperty("_Surface"))
            {
                _starMat.SetFloat("_Surface", 1f);
                _starMat.SetFloat("_Blend", 0f);
                _starMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _starMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _starMat.SetInt("_ZWrite", 0);
                _starMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            _starMat.renderQueue = 3100;

            _starFlash = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _starFlash.name = "PopStarBurst";
            _starFlash.transform.SetParent(transform, true);
            Destroy(_starFlash.GetComponent<Collider>());
            var r2 = _starFlash.GetComponent<Renderer>();
            r2.sharedMaterial = _starMat;
            r2.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _starFlash.SetActive(false);
        }

        private IEnumerator StarBurstRoutine(Vector3 pos)
        {
            if (_starFlash == null) yield break;
            _starFlash.SetActive(true);
            _starFlash.transform.position = pos;
            float e = 0f, dur = 0.55f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                float sz = Mathf.Lerp(0.03f, 0.19f, 1f - (1f - k) * (1f - k));
                _starFlash.transform.localScale = new Vector3(sz, sz, 1f);
                if (_cam != null) _starFlash.transform.rotation = _cam.transform.rotation;
                float alpha = k < 0.35f ? 1f : Mathf.Lerp(1f, 0f, (k - 0.35f) / 0.65f);
                var c = new Color(1f, 1f, 1f, alpha);
                if (_starMat.HasProperty("_BaseColor")) _starMat.SetColor("_BaseColor", c); else _starMat.color = c;
                yield return null;
            }
            _starFlash.SetActive(false);
        }

        // ---------------------------------------------------------
        //  Geometry Helpers
        // ---------------------------------------------------------
        private static GameObject CreateObjectWithMesh(string name, Transform parent, Vector3 localPos, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var mf = go.AddComponent<MeshFilter>();
            if (mesh != null) mf.sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            return go;
        }

        private static Mesh LoadObjMesh(string modelName)
        {
            string cleanName = Path.GetFileNameWithoutExtension(modelName);

#if UNITY_EDITOR
            // Preference 1: If Unity imported the model asset natively, use its Mesh directly
            var importedMesh = UnityEditor.AssetDatabase.LoadAssetAtPath<Mesh>("Assets/Models/" + cleanName + ".obj");
            if (importedMesh != null) return importedMesh;

            var importedGO = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/" + cleanName + ".obj");
            if (importedGO != null)
            {
                var mf = importedGO.GetComponentInChildren<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) return mf.sharedMesh;
            }
#endif

            // Preference 2: Load TextAsset from Resources and parse OBJ safely
            var ta = Resources.Load<TextAsset>("Models/" + cleanName);
            if (ta != null && !string.IsNullOrEmpty(ta.text))
            {
                try
                {
                    return ParseObjString(ta.text, cleanName);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LoadObjMesh] ParseObjString failed on TextAsset '{cleanName}': {ex.Message}");
                }
            }

            // Preference 3: Read raw .obj file from Assets/Models/
            string full = Path.Combine(Application.dataPath, "Models/" + cleanName + ".obj");
            if (File.Exists(full))
            {
                try
                {
                    return ParseObjString(File.ReadAllText(full), cleanName);
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[LoadObjMesh] ParseObjString failed on file '{cleanName}': {ex.Message}");
                }
            }

            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m = Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);
            m.name = cleanName + "_Fallback";
            Destroy(cube);
            return m;
        }

        private static Mesh ParseObjString(string objContent, string name)
        {
            var positions    = new List<Vector3>();
            var normals      = new List<Vector3>();
            var uvs          = new List<Vector2>();
            var finalVerts   = new List<Vector3>();
            var finalNormals = new List<Vector3>();
            var finalUvs     = new List<Vector2>();
            var triIndices   = new List<int>();
            var vertexMap    = new Dictionary<string, int>();

            using (var reader = new StringReader(objContent))
            {
                string rawLine;
                while ((rawLine = reader.ReadLine()) != null)
                {
                    string line = rawLine.Trim();
                    if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                    string[] parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts == null || parts.Length == 0) continue;

                    try
                    {
                        if (parts[0] == "v" && parts.Length >= 4)
                        {
                            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
                            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
                            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
                            positions.Add(new Vector3(x, y, z));
                        }
                        else if (parts[0] == "vn" && parts.Length >= 4)
                        {
                            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x);
                            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y);
                            float.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float z);
                            normals.Add(new Vector3(x, y, z));
                        }
                        else if (parts[0] == "vt" && parts.Length >= 3)
                        {
                            float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float u);
                            float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v);
                            uvs.Add(new Vector2(u, v));
                        }
                        else if (parts[0] == "f" && parts.Length >= 4)
                        {
                            // Convex polygon triangulation into triangles (v1, v_{i+1}, v_{i+2})
                            // For parts.Length = N + 1, valid vertex tokens are parts[1] through parts[parts.Length - 1]
                            for (int i = 1; i <= parts.Length - 3; i++)
                            {
                                int idxA = 1;
                                int idxB = i + 1;
                                int idxC = i + 2;

                                if (idxC >= parts.Length) break;

                                string[] fTokens = { parts[idxA], parts[idxB], parts[idxC] };
                                foreach (var token in fTokens)
                                {
                                    if (string.IsNullOrEmpty(token)) continue;

                                    if (!vertexMap.TryGetValue(token, out int idx))
                                    {
                                        var segs = token.Split('/');
                                        int posIdx = 0;
                                        if (segs.Length > 0 && int.TryParse(segs[0], out int pIdx))
                                            posIdx = pIdx - 1;

                                        Vector3 pos = (posIdx >= 0 && posIdx < positions.Count) ? positions[posIdx] : Vector3.zero;
                                        Vector3 nrm = Vector3.up;
                                        Vector2 uv  = Vector2.zero;

                                        if (segs.Length > 1 && !string.IsNullOrEmpty(segs[1]) && int.TryParse(segs[1], out int ui))
                                        {
                                            int uIndex = ui - 1;
                                            if (uIndex >= 0 && uIndex < uvs.Count) uv = uvs[uIndex];
                                        }

                                        if (segs.Length > 2 && !string.IsNullOrEmpty(segs[2]) && int.TryParse(segs[2], out int ni))
                                        {
                                            int nIndex = ni - 1;
                                            if (nIndex >= 0 && nIndex < normals.Count) nrm = normals[nIndex];
                                        }

                                        idx = finalVerts.Count;
                                        finalVerts.Add(pos);
                                        finalNormals.Add(nrm);
                                        finalUvs.Add(uv);
                                        vertexMap[token] = idx;
                                    }
                                    triIndices.Add(idx);
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[ParseObjString] Ignored malformed line '{line}': {ex.Message}");
                    }
                }
            }

            var mesh = new Mesh { name = name };
            if (finalVerts.Count > 65535) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(finalVerts);
            if (finalNormals.Count == finalVerts.Count && normals.Count > 0) mesh.SetNormals(finalNormals);
            else mesh.RecalculateNormals();
            if (finalUvs.Count == finalVerts.Count && uvs.Count > 0) mesh.SetUVs(0, finalUvs);
            mesh.SetTriangles(triIndices, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private GameObject Box(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = scale;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private GameObject Cyl(string name, Transform parent, Vector3 localPos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = scale;
            go.GetComponent<Renderer>().material = mat;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private GameObject Zone(string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = pos;
            var bc = go.AddComponent<BoxCollider>();
            bc.size      = size;
            bc.isTrigger = true;
            return go;
        }

        private static void AddInteractable(GameObject go, HydrogenGasTestTag tag)
        {
            var it = go.GetComponent<HydrogenGasTestInteractable>() ?? go.AddComponent<HydrogenGasTestInteractable>();
            it.htag = tag;
        }

        // =========================================================
        //  Interaction Loop
        // =========================================================
        private void UpdateInteraction()
        {
            if (ARVirtualLab.AR.ARExperimentMode.BlockSceneInput) return;    // AR: lab not placed yet
            if (_step == HydrogenGasTestStep.Intro || _step == HydrogenGasTestStep.Completed) return;

            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)       PointerDown(touch.position);
                else if (touch.phase == TouchPhase.Moved)  PointerMove(touch.position);
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    PointerUp(touch.position);
                return;
            }

            if (Input.GetMouseButtonDown(0)) PointerDown(Input.mousePosition);
            else if (Input.GetMouseButton(0)) PointerMove(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) PointerUp(Input.mousePosition);
        }

        private void PointerDown(Vector2 screen)
        {
            if (_animating) return;
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            _pointerDownScreen = screen;

            Ray ray = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            if (!Physics.Raycast(ray, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return;

            var hi = hit.collider.GetComponentInParent<HydrogenGasTestInteractable>();
            if (hi == null) return;

            switch (_step)
            {
                case HydrogenGasTestStep.Step1_PlaceTestTubeOnStand:
                    if (hi.htag == HydrogenGasTestTag.TestTube)
                    {
                        StartDrag(RootOf(hi.gameObject), ray);
                    }
                    break;

                case HydrogenGasTestStep.Step2_AddZinc:
                    if (hi.htag == HydrogenGasTestTag.Zinc)
                    {
                        StartDrag(RootOf(hi.gameObject), ray);
                    }
                    break;

                case HydrogenGasTestStep.Step3_AddDiluteAcid:
                    if (hi.htag == HydrogenGasTestTag.Acid)
                    {
                        StartDrag(RootOf(hi.gameObject), ray);
                    }
                    break;

                case HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube:
                    if (hi.htag == HydrogenGasTestTag.Cork)
                    {
                        StartDrag(RootOf(hi.gameObject), ray);
                    }
                    break;

                case HydrogenGasTestStep.Step5_ConnectDeliveryTubeToBasin:
                    // Tap the clamped test tube / delivery tube / soap basin: lower the clamp so the outlet dips into the soap
                    if (hi.htag == HydrogenGasTestTag.TestTube || hi.htag == HydrogenGasTestTag.Cork ||
                        hi.htag == HydrogenGasTestTag.Stand || hi.htag == HydrogenGasTestTag.SoapBasin)
                    {
                        ConnectDeliveryTubeToBasin();
                    }
                    break;

                case HydrogenGasTestStep.Step6_GasEvolution:
                    if (hi.htag == HydrogenGasTestTag.TestTube || hi.htag == HydrogenGasTestTag.Stand || hi.htag == HydrogenGasTestTag.Cork)
                    {
                        ShowFeedback("Gas travels through the delivery tube toward the soap solution.");
                        SetStep(HydrogenGasTestStep.Step7_SoapBubbles);
                    }
                    break;

                case HydrogenGasTestStep.Step7_SoapBubbles:
                    if (hi.htag == HydrogenGasTestTag.SoapBasin || hi.htag == HydrogenGasTestTag.Bubble)
                    {
                        ShowFeedback("Soap bubbles filled with hydrogen formed! Now test with a flame.");
                        SetStep(HydrogenGasTestStep.Step8_HydrogenCandlePopTest);
                    }
                    break;

                case HydrogenGasTestStep.Step8_HydrogenCandlePopTest:
                    if (hi.htag == HydrogenGasTestTag.Candle)
                    {
                        if (!_candleLit)
                        {
                            LightCandle();
                        }
                        else
                        {
                            StartDrag(RootOf(hi.gameObject), ray);
                        }
                    }
                    else if (hi.htag == HydrogenGasTestTag.Bubble)
                    {
                        if (_candleLit)
                        {
                            // Flame tapped onto a bubble: move the flame there and burn it
                            var hb = NearestBubbleTo(hit.point);
                            if (hb != null) PopBubble(hb);
                        }
                        else ShowFeedback("Light the candle flame first by tapping it.");
                    }
                    break;
            }
        }

        private void PointerMove(Vector2 screen)
        {
            if (_draggingObject == null) return;
            Ray ray = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            if (_dragPlane.Raycast(ray, out float dist))
                _draggingObject.transform.position = ray.GetPoint(dist) - _dragOffset;
            // (flame-to-bubble contact is detected every frame in UpdateBubbles)
        }

        private void PointerUp(Vector2 screen)
        {
            if (_draggingObject == null) return;
            var dropped = _draggingObject;
            _draggingObject = null;

            switch (_step)
            {
                case HydrogenGasTestStep.Step1_PlaceTestTubeOnStand:
                    if (Near(dropped, _standClampZone) || Near(dropped, _stand) ||
                        Vector3.Distance(dropped.transform.position, POS_TUBE_CLAMPED) < 0.28f)
                    {
                        PlaceTestTubeOnStand();
                    }
                    else
                    {
                        dropped.transform.position = _testTubeOriginPos;
                        ShowFeedback("Drag the test tube to the clamp on the vertical stand.");
                    }
                    break;

                case HydrogenGasTestStep.Step2_AddZinc:
                    if (Near(dropped, _testTubeMouthZone) || Near(dropped, _testTube) ||
                        Vector3.Distance(dropped.transform.position, _testTubeMouthZone.transform.position) < 0.28f)
                    {
                        AddZincToTestTube();
                    }
                    else
                    {
                        dropped.transform.position = _zincOriginPos;
                        ShowFeedback("Drop zinc granules into the test tube opening.");
                    }
                    break;

                case HydrogenGasTestStep.Step3_AddDiluteAcid:
                    if (Near(dropped, _testTubeMouthZone) || Near(dropped, _testTube) ||
                        Vector3.Distance(dropped.transform.position, _testTubeMouthZone.transform.position) < 0.28f)
                    {
                        AddDiluteAcid();
                    }
                    else
                    {
                        dropped.transform.position = _acidBottleOriginPos;
                        ShowFeedback("Pour dilute sulphuric acid into the test tube.");
                    }
                    break;

                case HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube:
                    if (Vector2.Distance(screen, _pointerDownScreen) < 12f ||
                        Near(dropped, _corkZone) || Near(dropped, _testTube) ||
                        Vector3.Distance(dropped.transform.position, _corkZone.transform.position) < 0.28f)
                    {
                        PlaceCorkAndDeliveryTube();
                    }
                    else
                    {
                        StartCoroutine(MoveRot(dropped.transform, _corkOriginPos, Quaternion.identity, 0.3f));
                        ShowFeedback("Press the cork stopper into the test tube neck.");
                    }
                    break;

                case HydrogenGasTestStep.Step8_HydrogenCandlePopTest:
                    if (_candleLit)
                    {
                        // Flame contact with a bubble is handled every frame; otherwise the candle goes back to its spot
                        if (!_popTriggered)
                        {
                            StartCoroutine(MoveRot(dropped.transform, _candleOriginPos, Quaternion.identity, 0.35f));
                            ShowFeedback("Bring the candle flame close to a rising hydrogen bubble.");
                        }
                    }
                    else
                    {
                        StartCoroutine(MoveRot(dropped.transform, _candleOriginPos, Quaternion.identity, 0.3f));
                        ShowFeedback("Tap the candle to light it first.");
                    }
                    break;
            }
        }

        private void StartDrag(GameObject go, Ray ray)
        {
            _draggingObject = go;
            Vector3 camForward = _cam != null ? _cam.transform.forward : Vector3.forward;
            _dragPlane = new Plane(-camForward, go.transform.position);
            if (_dragPlane.Raycast(ray, out float dist))
                _dragOffset = ray.GetPoint(dist) - go.transform.position;
            else
                _dragOffset = Vector3.zero;
        }

        private bool Near(GameObject obj, GameObject zone) =>
            obj != null && zone != null && Vector3.Distance(obj.transform.position, zone.transform.position) < 0.26f;

        private GameObject RootOf(GameObject go)
        {
            Transform t = go.transform;
            while (t.parent != null && t.parent != transform)
                t = t.parent;
            return t.gameObject;
        }

        // =========================================================
        //  Step Actions (Step-by-Step User Laboratory)
        // =========================================================
        // Every step action starts an animation and only then advances the step, so the student sees it happen.
        private void PlaceTestTubeOnStand()
        {
            if (_animating) return;
            StartCoroutine(PlaceTubeRoutine());
        }

        private IEnumerator PlaceTubeRoutine()
        {
            _animating = true;
            _tubeClamped = true;
            yield return MoveRot(_testTube.transform, _testTubeClampedPos, Quaternion.identity, 0.45f);
            ShowFeedback("✅ Test tube clamped securely onto the vertical stand!");
            _animating = false;
            SetStep(HydrogenGasTestStep.Step2_AddZinc);
        }

        private void AddZincToTestTube()
        {
            if (_animating) return;
            StartCoroutine(ZincDropRoutine());
        }

        private IEnumerator ZincDropRoutine()
        {
            _animating = true;
            var zt = _zincGranules.transform;
            Vector3 aboveMouth = new Vector3(_testTubeClampedPos.x, _testTubeTopY + 0.07f, _testTubeClampedPos.z);
            Vector3 inside     = new Vector3(_testTubeClampedPos.x, _testTubeClampedPos.y - _tubeOriginOffsetY + 0.03f, _testTubeClampedPos.z);
            Vector3 fullScale  = zt.localScale;

            yield return MoveRot(zt, aboveMouth, zt.rotation, 0.4f);

            // granules shrink into the tube neck and fall to the bottom
            float e = 0f, dur = 0.45f;
            Vector3 p0 = zt.position;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                zt.position   = Vector3.Lerp(p0, inside, k * k);
                zt.localScale = Vector3.Lerp(fullScale, fullScale * 0.30f, k);
                yield return null;
            }
            zt.localScale = fullScale;
            _zincGranules.SetActive(false);
            _zincInTube.SetActive(true);
            _zincInTubeAdded = true;
            ShowFeedback("✅ Zinc granules added inside the test tube!");
            _animating = false;
            SetStep(HydrogenGasTestStep.Step3_AddDiluteAcid);
        }

        private void AddDiluteAcid()
        {
            if (_animating) return;
            StartCoroutine(PourAcidRoutine());
        }

        private IEnumerator PourAcidRoutine()
        {
            _animating = true;
            var bt = _acidBottle.transform;

            // Tilt the bottle so its neck is just over the tube mouth
            Quaternion tilt = Quaternion.Euler(0f, 0f, 78f);
            Vector3 neckOffset = tilt * new Vector3(0f, _acidTopOffset, 0f);
            Vector3 neckTarget = new Vector3(_testTubeClampedPos.x, _testTubeTopY + 0.035f, _testTubeClampedPos.z);
            Vector3 pourPos = neckTarget - neckOffset;

            yield return MoveRot(bt, pourPos, tilt, 0.6f);

            // acid streams into the tube and the level rises
            _tubeLiquid.SetActive(true);
            float e = 0f, dur = 1.3f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                float h = _liquidFullH * k;
                SetTubeLiquidHeight(h);
                Vector3 liquidTop = new Vector3(_testTubeClampedPos.x, _testTubeClampedPos.y - _tubeOriginOffsetY + 0.006f + h, _testTubeClampedPos.z);
                PlacePourStream(neckTarget, liquidTop);
                if (k > 0.35f) SetEffervescence(Mathf.Lerp(6f, 22f, (k - 0.35f) / 0.65f));   // zinc starts fizzing as acid reaches it
                yield return null;
            }
            if (_pourStream != null) _pourStream.SetActive(false);

            yield return MoveRot(bt, _acidBottleOriginPos, Quaternion.identity, 0.55f);

            _acidAdded = true;
            ShowFeedback("✅ Dilute sulphuric acid added — bubbles of hydrogen already form on the zinc!");
            _animating = false;
            SetStep(HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube);
        }

        private void ShowCorkAssembly()
        {
            if (_corkDeliveryTube == null) return;
            if (_corkLifted) return;
            _corkLifted = true;
            StartCoroutine(LiftCorkRoutine());
        }

        /// <summary>Cork + delivery tube rise from the bench, turn upright and grow to full size above the tube mouth.</summary>
        private IEnumerator LiftCorkRoutine()
        {
            _animating = true;
            var t = _corkDeliveryTube.transform;
            Vector3 p0 = t.position; Quaternion r0 = t.rotation; Vector3 s0 = t.localScale;
            Vector3 mid = new Vector3(Mathf.Lerp(p0.x, _corkOriginPos.x, 0.5f), _corkOriginPos.y + 0.05f, Mathf.Lerp(p0.z, _corkOriginPos.z, 0.5f));
            float e = 0f, dur = 0.9f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                // arc through a raised midpoint so it does not pass through the apparatus
                Vector3 a = Vector3.Lerp(p0, mid, k), bpt = Vector3.Lerp(mid, _corkOriginPos, k);
                t.position = Vector3.Lerp(a, bpt, k);
                t.rotation = Quaternion.Slerp(r0, Quaternion.identity, k);
                t.localScale = Vector3.Lerp(s0, Vector3.one, k);
                yield return null;
            }
            t.SetPositionAndRotation(_corkOriginPos, Quaternion.identity);
            t.localScale = Vector3.one;
            _animating = false;
        }

        private IEnumerator PopIn(Transform t, float dur)
        {
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                float overshoot = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
                t.localScale = Vector3.one * (Mathf.SmoothStep(0f, 1f, k) * overshoot);
                yield return null;
            }
            t.localScale = Vector3.one;
        }

        private Vector3 CorkSealedPos => new Vector3(POS_TUBE_CLAMPED.x, _testTubeTopY, POS_TUBE_CLAMPED.z);

        private void PlaceCorkAndDeliveryTube()
        {
            if (_animating) return;
            StartCoroutine(SealCorkRoutine());
        }

        private IEnumerator SealCorkRoutine()
        {
            _animating = true;
            var ct = _corkDeliveryTube.transform;
            // Cork bottom is the container origin: line up over the mouth, then press in
            yield return MoveRot(ct, CorkSealedPos + new Vector3(0f, 0.04f, 0f), Quaternion.identity, 0.25f);
            yield return MoveRot(ct, CorkSealedPos, Quaternion.identity, 0.25f);
            _corkPlaced = true;
            ShowFeedback("✅ Cork sealed into the test tube! Now lower the clamped tube so the outlet dips into the soap solution.");
            _animating = false;
            SetStep(HydrogenGasTestStep.Step5_ConnectDeliveryTubeToBasin);
        }

        private void ConnectDeliveryTubeToBasin()
        {
            if (_animating) return;
            StartCoroutine(LowerTubeRoutine());
        }

        private IEnumerator LowerTubeRoutine()
        {
            _animating = true;
            Vector3 down = new Vector3(0f, -TUBE_LOWER_DIST, 0f);
            var tubeT = _testTube.transform;
            var corkT = _corkDeliveryTube.transform;
            Vector3 t0 = tubeT.position, c0 = corkT.position;
            float e = 0f, dur = 0.8f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                tubeT.position = t0 + down * k;
                corkT.position = c0 + down * k;
                yield return null;
            }
            tubeT.position = t0 + down;
            corkT.position = c0 + down;
            _tubeInBasin = true;
            ShowFeedback("✅ The delivery tube outlet now dips into the soap solution! Hydrogen gas is evolving.");
            _animating = false;
            SetStep(HydrogenGasTestStep.Step6_GasEvolution);
        }

        private void LightCandle()
        {
            _candleLit = true;
            if (_candleFlame != null) _candleFlame.SetActive(true);
            if (_candleLight != null) _candleLight.intensity = 0.4f;
            ShowFeedback("🔥 Candle lit! Now bring the flame to a hydrogen soap bubble.");
        }

        // Kept for the Next button: burns the bubble nearest the flame (or any bubble)
        private void TriggerCandlePop()
        {
            if (_popTriggered) return;
            Vector3 from = (_candleFlame != null) ? _candleFlame.transform.position : _tubeTipWorld;
            var b = NearestBubbleTo(from);
            if (b != null) PopBubble(b);
            else
            {
                // No bubble left in the air: burst at the soap surface so the experiment can still finish
                PopAt(new Vector3(_tubeTipWorld.x, _liquidSurfaceY + 0.12f, _tubeTipWorld.z));
            }
        }

        private void PopBubble(HBubble b)
        {
            if (_popTriggered || b == null || b.go == null) return;
            Vector3 popPos = b.go.transform.position;
            Destroy(b.go);
            _bubbles.Remove(b);
            PopAt(popPos);
        }

        private void PopAt(Vector3 popPos)
        {
            if (_popTriggered) return;
            _popTriggered = true;
            _bubblesEmitting = false;

            if (_popPS != null)
            {
                _popPS.transform.position = popPos;
                var em = _popPS.emission;
                em.enabled = true;
                _popPS.Play();
            }
            if (_popLight != null) _popLight.transform.position = popPos;
            StartCoroutine(StarBurstRoutine(popPos));
            StartCoroutine(FlashLight());
            PlayPopSound();
            ShowFeedback("💥 \"Pop!\" — Hydrogen gas burns with a characteristic pop sound!");
            StartCoroutine(FinishAfterPop());
        }

        private IEnumerator FlashLight()
        {
            if (_popLight == null) yield break;
            _popLight.intensity = 1.6f; yield return new WaitForSeconds(0.06f);
            _popLight.intensity = 0f; yield return new WaitForSeconds(0.04f);
            _popLight.intensity = 0.8f; yield return new WaitForSeconds(0.08f);
            _popLight.intensity = 0f;
        }

        private IEnumerator FinishAfterPop()
        {
            yield return new WaitForSeconds(2.0f);
            SetStep(HydrogenGasTestStep.Step8_Results);
            StopAndClearAllVFX();
            if (_candleFlame != null) _candleFlame.SetActive(false);
            if (_candle != null) _candle.transform.position = _candleOriginPos;
            _isCompleted = true;
        }

        private void PlayPopSound()
        {
            if (_audio == null) return;
            int samples = 4410;
            var data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / 44100f;
                float env = Mathf.Exp(-t * 60f);
                float wave = Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.7f
                           + Mathf.Sin(2f * Mathf.PI * 95f  * t) * 0.3f;
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-t * 120f) * 0.45f;
                data[i] = Mathf.Clamp(wave * env + noise, -1f, 1f);
            }
            var clip = AudioClip.Create("PopSound", samples, 1, 44100, false);
            clip.SetData(data, 0);
            _audio.PlayOneShot(clip, 1.0f);
        }

        // =========================================================
        //  Public Navigation
        // =========================================================
        public void NextStep()
        {
            if (_step == HydrogenGasTestStep.Intro) { SetStep(HydrogenGasTestStep.Step1_PlaceTestTubeOnStand); return; }
            if (_isCompleted || _animating) return;

            switch (_step)
            {
                case HydrogenGasTestStep.Step1_PlaceTestTubeOnStand:
                    PlaceTestTubeOnStand();
                    return;

                case HydrogenGasTestStep.Step2_AddZinc:
                    AddZincToTestTube();
                    return;

                case HydrogenGasTestStep.Step3_AddDiluteAcid:
                    AddDiluteAcid();
                    return;

                case HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube:
                    PlaceCorkAndDeliveryTube();
                    return;

                case HydrogenGasTestStep.Step5_ConnectDeliveryTubeToBasin:
                    ConnectDeliveryTubeToBasin();
                    return;

                case HydrogenGasTestStep.Step6_GasEvolution:
                    SetStep(HydrogenGasTestStep.Step7_SoapBubbles);
                    return;

                case HydrogenGasTestStep.Step7_SoapBubbles:
                    SetStep(HydrogenGasTestStep.Step8_HydrogenCandlePopTest);
                    return;

                case HydrogenGasTestStep.Step8_HydrogenCandlePopTest:
                    if (!_candleLit)
                    {
                        LightCandle();
                        return;
                    }
                    if (!_popTriggered)
                    {
                        TriggerCandlePop();
                        return;
                    }
                    break;
            }

            int next = (int)_step + 1;
            if (next <= (int)HydrogenGasTestStep.Completed) SetStep((HydrogenGasTestStep)next);
        }

        public void PrevStep()
        {
            if (_step <= HydrogenGasTestStep.Step1_PlaceTestTubeOnStand) return;
            _step = (HydrogenGasTestStep)((int)_step - 1);
            RefreshUI();
        }

        public void ResetExperiment()
        {
            StopAllCoroutines();            // cancel any running pour / drop / pop animation
            _bubbleFloatCoroutine = null;
            _feedbackCoroutine = null;
            _animating = false;
            _corkLifted = false;
            if (_starFlash != null) _starFlash.SetActive(false);
            StopAndClearAllVFX();
            SetEffervescence(0f);
            SetTubeLiquidHeight(0f);

            // Return all objects to unmounted/initial positions
            if (_testTube != null)
            {
                _testTube.transform.position = _testTubeOriginPos;
                _testTube.transform.rotation = Quaternion.identity;
                _testTube.SetActive(true);
            }
            if (_zincGranules != null)
            {
                _zincGranules.transform.position = _zincOriginPos;
                _zincGranules.transform.rotation = Quaternion.identity;
                _zincGranules.transform.localScale = Vector3.one;
                _zincGranules.SetActive(true);
            }
            if (_zincInTube != null) _zincInTube.SetActive(false);
            if (_tubeLiquid != null) _tubeLiquid.SetActive(false);

            if (_acidBottle != null)
            {
                _acidBottle.transform.position = _acidBottleOriginPos;
                _acidBottle.transform.rotation = Quaternion.identity;
                _acidBottle.SetActive(true);
            }
            if (_corkDeliveryTube != null)
            {
                _corkDeliveryTube.transform.SetPositionAndRotation(_corkRestPos, CORK_REST_ROT);
                _corkDeliveryTube.transform.localScale = Vector3.one * CORK_REST_SCALE;
                _corkDeliveryTube.SetActive(true);
            }
            if (_soapBasin != null)
            {
                _soapBasin.transform.position = _basinFinalPos;
                _soapBasin.transform.rotation = Quaternion.identity;
                _soapBasin.SetActive(true);
            }

            if (_candle != null)
            {
                _candle.transform.position = _candleOriginPos;
                _candle.SetActive(true);
            }
            if (_candleFlame != null) _candleFlame.SetActive(false);
            if (_candleLight != null) _candleLight.intensity = 0f;

            _zincPrepared    = false;
            _tubeClamped     = false;
            _zincInTubeAdded = false;
            _acidAdded       = false;
            _corkPlaced      = false;
            _tubeInBasin     = false;
            _candleLit       = false;
            _reactionStarted = false;
            _popTriggered    = false;
            _isCompleted     = false;
            _draggingObject  = null;

            if (_safetyPanel != null) _safetyPanel.SetActive(false);
            if (_obsPanel != null) _obsPanel.SetActive(false);
            if (_eqPanel != null) _eqPanel.SetActive(false);
            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_aboutPanel != null) _aboutPanel.SetActive(false);
            if (_resetConfirmDialog != null) _resetConfirmDialog.SetActive(false);

            SetStep(HydrogenGasTestStep.Step1_PlaceTestTubeOnStand);
            ShowFeedback("Experiment reset. Apparatus returned to unassembled starting state.");
        }

        // =========================================================
        //  UI Builder — Exact 1:1 Match with MagnesiumLabScene UI
        //  1080 x 1920 portrait canvas, dark navy theme
        // =========================================================
        private void BuildUI()
        {
            BuildUIRaw();
            if (_canvas != null) ARVirtualLab.UI.EduTheme.Apply(_canvas.transform);   // light, textbook-style theme
        }

        private void BuildUIRaw()
        {
            var canvasGO = new GameObject("HydrogenGasTestCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            var root = canvasGO.GetComponent<RectTransform>();

            // 1. Intro Panel
            BuildIntroPanel(root);

            // 2. HUD Panel
            BuildHUDPanel(root);

            // 3. Modal Info Panels
            BuildModalPanels(root);

            // 4. AR Component Tray
            var trayGO = new GameObject("HydrogenGasTestARTrayRoot");
            trayGO.transform.SetParent(root, false);
            _arTray = trayGO.AddComponent<HydrogenGasTestARTray>();
            _arTray.BuildTray(root);
        }

        private void BuildIntroPanel(RectTransform root)
        {
            _introPanel = MakePanel(root, "IntroPanel", new Color(0.02f, 0.04f, 0.09f, 0.95f));
            var rootRT = _introPanel.GetComponent<RectTransform>();

            // Center card
            var card = MakePanel(rootRT, "Card", new Color(0.06f, 0.10f, 0.20f, 0.98f));
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.05f, 0.10f);
            cardRT.anchorMax = new Vector2(0.95f, 0.92f);
            var cardImg = card.GetComponent<Image>();
            if (cardImg != null) cardImg.raycastTarget = true;

            var cardOutline = card.AddComponent<Outline>();
            cardOutline.effectColor = new Color(0.18f, 0.55f, 0.95f, 0.70f);
            cardOutline.effectDistance = new Vector2(2f, -2f);

            // Badge Pill at top
            var badge = MakePanel(cardRT, "Badge", new Color(0.12f, 0.25f, 0.48f, 0.90f));
            var bRT = badge.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0.15f, 0.90f);
            bRT.anchorMax = new Vector2(0.85f, 0.96f);
            MakeText(bRT, "BadgeText", "VIRTUAL CHEMISTRY LABORATORY",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                18, new Color(0.45f, 0.85f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);

            // Title
            MakeText(cardRT, "Title", "REACTION OF ZINC WITH DILUTE SULPHURIC ACID",
                new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.89f),
                32, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            // Subtitle
            MakeText(cardRT, "Subtitle", "Testing of Hydrogen Gas by Burning",
                new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.79f),
                20, new Color(0.70f, 0.82f, 0.98f), FontStyles.Normal, TextAlignmentOptions.Center);

            // Reaction Equation Pill
            var eqPill = MakePanel(cardRT, "EqPill", new Color(0.08f, 0.15f, 0.30f, 0.95f));
            var eqRT = eqPill.GetComponent<RectTransform>();
            eqRT.anchorMin = new Vector2(0.10f, 0.63f);
            eqRT.anchorMax = new Vector2(0.90f, 0.71f);
            var eqOut = eqPill.AddComponent<Outline>();
            eqOut.effectColor = new Color(0.15f, 0.60f, 1.0f, 0.50f);
            eqOut.effectDistance = new Vector2(1.5f, -1.5f);
            MakeText(eqRT, "EqText", "Zn(s) + H\u2082SO\u2084(aq) \u2192 ZnSO\u2084(aq) + H\u2082(g)\u2191",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                20, new Color(0.35f, 0.90f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);

            // Experiment Overview Summary Box
            var infoBox = MakePanel(cardRT, "InfoBox", new Color(0.04f, 0.07f, 0.15f, 0.90f));
            var ibRT = infoBox.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0.06f, 0.28f);
            ibRT.anchorMax = new Vector2(0.94f, 0.61f);

            MakeText(ibRT, "InfoTitle", "EXPERIMENT OVERVIEW",
                new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.98f),
                16, new Color(0.40f, 0.75f, 1f), FontStyles.Bold, TextAlignmentOptions.Left);

            MakeText(ibRT, "InfoBody",
                "1. Place Test Tube on Stand\n" +
                "2. Add Zinc Granules\n" +
                "3. Add Dilute Sulphuric Acid\n" +
                "4. Seal with Cork & Delivery Tube\n" +
                "5. Connect Delivery Tube to Soap Solution\n" +
                "6. Observe Hydrogen Gas Evolution\n" +
                "7. Form Hydrogen Soap Bubbles\n" +
                "8. Hydrogen Identification Test",
                new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.84f),
                16, new Color(0.82f, 0.88f, 0.96f), FontStyles.Normal, TextAlignmentOptions.Left);

            // Primary Button: START EXPERIMENT
            _startBtn = MakeButton(cardRT, "StartBtn", "Start Experiment",
                new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.25f),
                new Color(0.02f, 0.50f, 0.88f));
            var sbOut = _startBtn.gameObject.AddComponent<Outline>();
            sbOut.effectColor = new Color(0.30f, 0.80f, 1f, 0.80f);
            sbOut.effectDistance = new Vector2(1.5f, -1.5f);
            _startBtn.onClick.AddListener(() => SetStep(HydrogenGasTestStep.Step1_PlaceTestTubeOnStand));

            // Secondary Button: AR LAB
            var arIntroBtn = MakeButton(cardRT, "ARIntroBtn", "AR Lab",
                new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.145f),
                new Color(0.32f, 0.24f, 0.72f));
            arIntroBtn.onClick.AddListener(() => {
                SetStep(HydrogenGasTestStep.Step1_PlaceTestTubeOnStand);
                TriggerARMode();
            });

            // Footer Options: Home, Safety & About
            var homeLink = MakeButton(cardRT, "HomeLink", "← Home",
                new Vector2(0.08f, 0.01f), new Vector2(0.34f, 0.055f),
                new Color(0.12f, 0.22f, 0.38f));
            homeLink.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());

            var safetyLink = MakeButton(cardRT, "SafetyLink", "Safety",
                new Vector2(0.37f, 0.01f), new Vector2(0.63f, 0.055f),
                new Color(0.15f, 0.22f, 0.35f));
            safetyLink.onClick.AddListener(() => TogglePanel(_safetyPanel));

            var aboutLink = MakeButton(cardRT, "AboutLink", "About",
                new Vector2(0.66f, 0.01f), new Vector2(0.92f, 0.055f),
                new Color(0.15f, 0.22f, 0.35f));
            aboutLink.onClick.AddListener(() => TogglePanel(_aboutPanel));
        }

        private void BuildHUDPanel(RectTransform root)
        {
            _hudPanel = MakePanel(root, "HUD", Color.clear);
            _hudPanel.SetActive(false);

            // ── Top Header Card (safe-area aware, exact match with MagnesiumLabScene) ──
            var topBar = MakePanel(_hudPanel.GetComponent<RectTransform>(), "TopHeaderCard",
                new Color(0.04f, 0.08f, 0.16f, 0.95f));
            var topRT = topBar.GetComponent<RectTransform>();
            topRT.anchorMin = new Vector2(0.03f, 0.81f);
            topRT.anchorMax = new Vector2(0.97f, 0.985f);
            var tbOut = topBar.AddComponent<Outline>();
            tbOut.effectColor = new Color(0.15f, 0.45f, 0.85f, 0.60f);
            tbOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Row 1: Back Button, App Header & AR Button
            var backBtn = MakeButton(topRT, "BackButton", "← Back",
                new Vector2(0.03f, 0.79f), new Vector2(0.24f, 0.97f),
                new Color(0.12f, 0.22f, 0.38f));
            backBtn.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());

            MakeText(topRT, "AppLabel", "HYDROGEN GAS TEST",
                new Vector2(0.26f, 0.82f), new Vector2(0.70f, 0.97f),
                20, new Color(0.35f, 0.85f, 1.00f), FontStyles.Bold, TextAlignmentOptions.Left);

            var arBtn = MakeButton(topRT, "ARButton", "AR LAB",
                new Vector2(0.72f, 0.79f), new Vector2(0.97f, 0.97f),
                new Color(0.32f, 0.24f, 0.72f));
            arBtn.onClick.AddListener(ToggleUnifiedAR);

            // Row 2: 8-Step Progress Indicators (Pills)
            var pillRow = new GameObject("StepPillRow", typeof(RectTransform));
            pillRow.transform.SetParent(topRT, false);
            var prRT = pillRow.GetComponent<RectTransform>();
            prRT.anchorMin = new Vector2(0.04f, 0.69f);
            prRT.anchorMax = new Vector2(0.96f, 0.76f);
            prRT.offsetMin = prRT.offsetMax = Vector2.zero;

            _stepPillIndicators.Clear();
            float stepWidth = 1.0f / 8.0f;
            for (int i = 0; i < 8; i++)
            {
                var pill = new GameObject($"Pill_{i + 1}", typeof(RectTransform), typeof(Image));
                pill.transform.SetParent(prRT, false);
                var pRT = pill.GetComponent<RectTransform>();
                pRT.anchorMin = new Vector2(i * stepWidth + 0.012f, 0f);
                pRT.anchorMax = new Vector2((i + 1) * stepWidth - 0.012f, 1f);
                pRT.offsetMin = pRT.offsetMax = Vector2.zero;
                var pImg = pill.GetComponent<Image>();
                pImg.color = (i == 0) ? ARVirtualLab.UI.EduTheme.PillActive : ARVirtualLab.UI.EduTheme.PillIdle;
                _stepPillIndicators.Add(pImg);
            }

            // Row 3: Step Badge & Title
            var stepBadge = MakePanel(topRT, "StepBadge", new Color(0.10f, 0.28f, 0.58f, 0.95f));
            var sbrt = stepBadge.GetComponent<RectTransform>();
            sbrt.anchorMin = new Vector2(0.04f, 0.44f);
            sbrt.anchorMax = new Vector2(0.32f, 0.63f);
            _stepNumberBadge = MakeText(sbrt, "BadgeText", "STEP 1 OF 8",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                14, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            _stepTitle = MakeText(topRT, "StepTitle", "Step 1: Place Test Tube on Stand",
                new Vector2(0.35f, 0.44f), new Vector2(0.96f, 0.65f),
                21, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);

            // Row 4: Instruction Description
            _stepDesc = MakeText(topRT, "StepDesc", "Notice the zinc granules inside the clamped test tube.",
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.42f),
                18, new Color(0.85f, 0.92f, 0.98f), FontStyles.Normal, TextAlignmentOptions.Left);

            // ── Toast Feedback (Safe bottom positioning above action bar) ──
            var toastBg = MakePanel(_hudPanel.GetComponent<RectTransform>(), "Toast",
                new Color(0.04f, 0.10f, 0.22f, 0.94f));
            var toastRT = toastBg.GetComponent<RectTransform>();
            toastRT.anchorMin = new Vector2(0.08f, 0.11f);
            toastRT.anchorMax = new Vector2(0.92f, 0.18f);
            var tOut = toastBg.AddComponent<Outline>();
            tOut.effectColor = new Color(0.20f, 0.70f, 1.0f, 0.70f);
            tOut.effectDistance = new Vector2(1.5f, -1.5f);
            _feedbackText = MakeText(toastRT, "FeedbackText", "",
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                18, new Color(0.92f, 0.97f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);
            toastBg.SetActive(false);

            // ── Bottom Action Bar (Exact match with MagnesiumLabScene) ──
            var botBar = MakePanel(_hudPanel.GetComponent<RectTransform>(), "BotBar",
                new Color(0.04f, 0.08f, 0.16f, 0.95f));
            var bbRT = botBar.GetComponent<RectTransform>();
            bbRT.anchorMin = new Vector2(0.04f, 0.02f);
            bbRT.anchorMax = new Vector2(0.96f, 0.085f);
            var bbOut = botBar.AddComponent<Outline>();
            bbOut.effectColor = new Color(0.18f, 0.45f, 0.85f, 0.50f);
            bbOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Reset
            _resetBtn = MakeButton(bbRT, "Reset", "Reset",
                new Vector2(0.02f, 0.12f), new Vector2(0.22f, 0.88f),
                new Color(0.48f, 0.12f, 0.16f));
            _resetBtn.onClick.AddListener(() => {
                if (_resetConfirmDialog != null) _resetConfirmDialog.SetActive(true);
                else ResetExperiment();
            });

            // Safety
            _safetyBtn = MakeButton(bbRT, "Safety", "Safety",
                new Vector2(0.25f, 0.12f), new Vector2(0.45f, 0.88f),
                new Color(0.55f, 0.35f, 0.05f));
            _safetyBtn.onClick.AddListener(() => TogglePanel(_safetyPanel));

            // Previous
            _prevBtn = MakeButton(bbRT, "Prev", "Previous",
                new Vector2(0.48f, 0.12f), new Vector2(0.70f, 0.88f),
                new Color(0.18f, 0.24f, 0.36f));
            _prevBtn.onClick.AddListener(PrevStep);
            _prevBtn.gameObject.SetActive(false);

            // Next
            _nextBtn = MakeButton(bbRT, "Next", "Next",
                new Vector2(0.73f, 0.12f), new Vector2(0.98f, 0.88f),
                new Color(0.02f, 0.50f, 0.88f));
            _nextBtn.onClick.AddListener(NextStep);
            _nextBtn.gameObject.SetActive(true);

            // Observation / Equation / Result (appear at end)
            _obsBtn = MakeButton(bbRT, "Obs", "Observation",
                new Vector2(0.48f, 0.12f), new Vector2(0.64f, 0.88f),
                new Color(0.06f, 0.45f, 0.28f));
            _obsBtn.onClick.AddListener(() => TogglePanel(_obsPanel));
            _obsBtn.gameObject.SetActive(false);

            _eqBtn = MakeButton(bbRT, "Eq", "Equation",
                new Vector2(0.66f, 0.12f), new Vector2(0.82f, 0.88f),
                new Color(0.35f, 0.20f, 0.65f));
            _eqBtn.onClick.AddListener(() => TogglePanel(_eqPanel));
            _eqBtn.gameObject.SetActive(false);

            _resultBtn = MakeButton(bbRT, "Result", "Result",
                new Vector2(0.84f, 0.12f), new Vector2(0.98f, 0.88f),
                new Color(0.08f, 0.48f, 0.62f));
            _resultBtn.onClick.AddListener(() => TogglePanel(_resultPanel));
            _resultBtn.gameObject.SetActive(false);
        }

        private void BuildModalPanels(RectTransform root)
        {
            _obsPanel = MakeInfoPanel(root, "ObsPanel", "OBSERVATION",
                "• Rapid effervescence (bubbling) occurs around the zinc granules upon contact with dilute sulphuric acid.\n\n" +
                "• Colorless, odorless hydrogen gas (H\u2082) evolves and passes through the delivery tube.\n\n" +
                "• Passing the gas into soap solution traps it inside soap bubbles that rise into the air.\n\n" +
                "• When a burning candle flame touches a hydrogen bubble, it bursts with a characteristic 'pop' sound.");
            _obsPanel.SetActive(false);

            _eqPanel = MakeInfoPanel(root, "EqPanel", "CHEMICAL EQUATION",
                "Reaction of Zinc with Dilute Sulphuric Acid:\n\n" +
                "Zn(s)   +   H\u2082SO\u2084(aq)   \u2192   ZnSO\u2084(aq)   +   H\u2082(g)\u2191\n\n" +
                "Reactants:\n" +
                "  • Zinc (Zn) — Silvery-grey metallic granules\n" +
                "  • Dilute Sulphuric Acid (H\u2082SO\u2084) — Colorless aqueous acid\n\n" +
                "Products:\n" +
                "  • Zinc Sulphate (ZnSO\u2084) — Colorless salt solution\n" +
                "  • Hydrogen Gas (H\u2082) — Colorless, flammable gas\n\n" +
                "Hydrogen Burning Test:\n" +
                "2H\u2082(g)   +   O\u2082(g)   \u2192   2H\u2082O(g)   +   Energy (Pop Sound)");
            _eqPanel.SetActive(false);

            _resultPanel = MakeInfoPanel(root, "ResultPanel", "EXPERIMENT RESULT",
                "EXPERIMENT CONCLUSION:\n\n" +
                "1. Single Displacement: Zinc reacts with dilute sulphuric acid to produce zinc sulphate and liberate hydrogen gas.\n\n" +
                "2. Soap Bubble Collection: Passing hydrogen into soap solution traps the gas inside lightweight soap bubbles that float upwards.\n\n" +
                "3. Hydrogen Identification: Hydrogen gas is uniquely confirmed because it burns in air with a distinctive 'pop' sound when exposed to flame.\n\n" +
                "Equation: Zn(s) + H\u2082SO\u2084(aq) \u2192 ZnSO\u2084(aq) + H\u2082(g)\u2191");
            _resultPanel.SetActive(false);

            _safetyPanel = MakeInfoPanel(root, "SafetyPanel", "SAFETY PRECAUTIONS",
                "LABORATORY SAFETY GUIDELINES:\n\n" +
                "1. Always wear certified safety goggles and lab coat.\n\n" +
                "2. Handle dilute sulphuric acid (H\u2082SO\u2084) carefully — avoid contact with skin and eyes.\n\n" +
                "3. In case of accidental acid spill, flush immediately with copious amounts of water.\n\n" +
                "4. Ensure cork and delivery tube connections are airtight.\n\n" +
                "5. Keep open candle flames away from the reaction test tube.\n\n" +
                "6. SAFETY NOTE: The flame pop test in this application is an EDUCATIONAL VIRTUAL SIMULATION ONLY.");
            _safetyPanel.SetActive(false);

            _aboutPanel = MakeInfoPanel(root, "AboutPanel", "ABOUT EXPERIMENT",
                "REACTION OF ZINC WITH DILUTE SULPHURIC ACID\n\n" +
                "Subject: Class 10 Chemistry / Virtual Science Laboratory\n\n" +
                "Concept: Action of acids on metals and evolution of hydrogen gas.\n\n" +
                "Apparatus:\n" +
                "  • Laboratory Stand & Clamp\n" +
                "  • Test Tube\n" +
                "  • Zinc Granules & Dilute Sulphuric Acid\n" +
                "  • Cork with Glass Delivery Tube\n" +
                "  • Soap Solution Basin\n" +
                "  • Testing Candle\n\n" +
                "Compatible with Unity 6.3 Universal Render Pipeline (URP).");
            _aboutPanel.SetActive(false);

            // Reset confirmation modal
            _resetConfirmDialog = MakePanel(root, "ResetConfirmOverlay", new Color(0f, 0f, 0f, 0.70f));
            var rcImg = _resetConfirmDialog.GetComponent<Image>();
            if (rcImg != null) rcImg.raycastTarget = true;

            var bg = MakePanel(_resetConfirmDialog.GetComponent<RectTransform>(), "Bg",
                new Color(0.06f, 0.10f, 0.20f, 0.98f));
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.12f, 0.40f);
            bgRT.anchorMax = new Vector2(0.88f, 0.60f);

            var bgOut = bg.AddComponent<Outline>();
            bgOut.effectColor = new Color(0.20f, 0.50f, 0.90f, 0.60f);
            bgOut.effectDistance = new Vector2(1.5f, -1.5f);

            MakeText(bgRT, "Title", "Reset Experiment?",
                new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.90f),
                22, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            MakeText(bgRT, "Msg", "All progress will be reset to Step 1.",
                new Vector2(0.05f, 0.35f), new Vector2(0.95f, 0.55f),
                16, new Color(0.80f, 0.88f, 0.98f), FontStyles.Normal, TextAlignmentOptions.Center);

            var cancelBtn = MakeButton(bgRT, "CancelBtn", "Cancel",
                new Vector2(0.08f, 0.08f), new Vector2(0.46f, 0.28f),
                new Color(0.20f, 0.26f, 0.38f));
            cancelBtn.onClick.AddListener(() => _resetConfirmDialog.SetActive(false));

            var confirmBtn = MakeButton(bgRT, "ConfirmBtn", "Reset",
                new Vector2(0.54f, 0.08f), new Vector2(0.92f, 0.28f),
                new Color(0.58f, 0.14f, 0.18f));
            confirmBtn.onClick.AddListener(() => {
                _resetConfirmDialog.SetActive(false);
                ResetExperiment();
            });

            _resetConfirmDialog.SetActive(false);
        }

        private static GameObject MakeInfoPanel(RectTransform root, string name, string heading, string body)
        {
            var overlay = MakePanel(root, name + "_Overlay", new Color(0f, 0f, 0f, 0.70f));
            var oImg = overlay.GetComponent<Image>();
            if (oImg != null) oImg.raycastTarget = true;

            var bg = MakePanel(overlay.GetComponent<RectTransform>(), "Bg",
                new Color(0.05f, 0.09f, 0.18f, 0.98f));
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.06f, 0.10f);
            bgRT.anchorMax = new Vector2(0.94f, 0.90f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            var bgOut = bg.AddComponent<Outline>();
            bgOut.effectColor = new Color(0.18f, 0.55f, 0.95f, 0.70f);
            bgOut.effectDistance = new Vector2(2f, -2f);

            MakeText(bgRT, "Heading", heading,
                new Vector2(0.06f, 0.88f), new Vector2(0.94f, 0.97f),
                28, new Color(0.40f, 0.85f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);

            MakeText(bgRT, "Body", body,
                new Vector2(0.06f, 0.12f), new Vector2(0.94f, 0.86f),
                18, new Color(0.85f, 0.92f, 0.98f), FontStyles.Normal, TextAlignmentOptions.Left);

            var closeBtn = MakeButton(bgRT, "CloseBtn", "✕ Close",
                new Vector2(0.30f, 0.02f), new Vector2(0.70f, 0.09f),
                new Color(0.18f, 0.24f, 0.36f));
            closeBtn.onClick.AddListener(() => overlay.SetActive(false));

            return overlay;
        }

        private static TMP_FontAsset _tmpFont;
        private static TMP_FontAsset GetFont()
        {
            if (_tmpFont != null) return _tmpFont;
            _tmpFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_tmpFont == null)
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (all.Length > 0) _tmpFont = all[0];
            }
            return _tmpFont;
        }

        private static GameObject MakePanel(RectTransform parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (color.a > 0.001f)
            {
                var img = go.AddComponent<Image>();
                img.color = color;
                img.raycastTarget = false;
            }
            return go;
        }

        private static TMP_Text MakeText(RectTransform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color color,
            FontStyles style, TextAlignmentOptions align)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text               = text;
            t.fontSize           = fontSize;
            t.color              = color;
            t.fontStyle          = style;
            t.alignment          = align;
            t.raycastTarget      = false;
            t.textWrappingMode   = TextWrappingModes.Normal;
            t.overflowMode       = TextOverflowModes.Overflow;
            var f = GetFont();
            if (f != null) t.font = f;
            MobileText.Setup(t, name, fontSize);
            return t;
        }

        private static Button MakeButton(RectTransform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bgColor;
            img.raycastTarget = true;

            var outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(1f, 1f, 1f, 0.15f);
            outl.effectDistance = new Vector2(1f, -1f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.highlightedColor = Color.Lerp(bgColor, Color.white, 0.25f);
            colors.pressedColor     = Color.Lerp(bgColor, Color.black, 0.25f);
            btn.colors = colors;
            btn.targetGraphic = img;

            MakeText(rt, "Lbl", label,
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                18, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            return btn;
        }

        private void TogglePanel(GameObject panel)
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
        }

        public void ShowFeedback(string message)
        {
            if (_feedbackText == null) return;
            _feedbackText.text = MobileText.Clean(message);
            _feedbackText.transform.parent.gameObject.SetActive(true);
            if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = StartCoroutine(HideFeedback(3.5f));
        }

        private IEnumerator HideFeedback(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_feedbackText != null)
                _feedbackText.transform.parent.gameObject.SetActive(false);
        }

        private void SetStep(HydrogenGasTestStep s)
        {
            _step = s;

            switch (_step)
            {
                case HydrogenGasTestStep.Step1_PlaceTestTubeOnStand:
                case HydrogenGasTestStep.Step2_AddZinc:
                case HydrogenGasTestStep.Step3_AddDiluteAcid:
                    StopAndClearAllVFX();
                    if (_candleFlame != null) _candleFlame.SetActive(false);
                    break;

                case HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube:
                case HydrogenGasTestStep.Step5_ConnectDeliveryTubeToBasin:
                    // Zinc is already fizzing in the acid; the gas has nowhere to go yet
                    ShowCorkAssembly();
                    if (_bubbleFloatCoroutine != null) { StopCoroutine(_bubbleFloatCoroutine); _bubbleFloatCoroutine = null; }
                    _gasFlowing = false;
                    ClearBubbles();
                    foreach (var d in _gasDots) if (d != null) d.gameObject.SetActive(false);
                    if (_candleFlame != null) _candleFlame.SetActive(false);
                    SetEffervescence(_acidAdded ? 22f : 0f);
                    break;

                case HydrogenGasTestStep.Step6_GasEvolution:
                    SetEffervescence(45f);
                    if (!_gasFlowing) StartGasFlow();
                    // let the student watch the gas travel, then move on to the bubbles
                    if (_bubbleFloatCoroutine != null) StopCoroutine(_bubbleFloatCoroutine);
                    _bubbleFloatCoroutine = StartCoroutine(AutoAdvance(HydrogenGasTestStep.Step6_GasEvolution, 5.5f));
                    break;

                case HydrogenGasTestStep.Step7_SoapBubbles:
                    SetEffervescence(45f);
                    if (!_gasFlowing) StartGasFlow();
                    if (_soapBubblePS != null)
                    {
                        var em = _soapBubblePS.emission;
                        em.enabled = true;
                        em.rateOverTime = 26f;
                        _soapBubblePS.Play();
                    }
                    _bubblesEmitting = true;
                    _bubbleTimer = 0.2f;
                    if (_bubbleFloatCoroutine != null) StopCoroutine(_bubbleFloatCoroutine);
                    _bubbleFloatCoroutine = StartCoroutine(AutoAdvance(HydrogenGasTestStep.Step7_SoapBubbles, 7f));
                    break;

                case HydrogenGasTestStep.Step8_HydrogenCandlePopTest:
                    if (_bubbleFloatCoroutine != null) { StopCoroutine(_bubbleFloatCoroutine); _bubbleFloatCoroutine = null; }
                    if (!_gasFlowing) StartGasFlow();
                    _bubblesEmitting = !_popTriggered;      // keep making bubbles until one is burned
                    if (_candleFlame != null && _candleLit) _candleFlame.SetActive(true);
                    if (_candleLight != null && _candleLit) _candleLight.intensity = 0.4f;
                    break;

                case HydrogenGasTestStep.Step8_Results:
                case HydrogenGasTestStep.Completed:
                    StopAndClearAllVFX();
                    if (_candleFlame != null) _candleFlame.SetActive(false);
                    break;
            }

            RefreshUI();
        }

        /// <summary>Moves on by itself after the student has had time to watch: Step 6 -> 7 -> 8.</summary>
        private IEnumerator AutoAdvance(HydrogenGasTestStep from, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            while (_animating) yield return null;
            if (_step != from) yield break;
            SetStep((HydrogenGasTestStep)((int)from + 1));
        }

        private void RefreshUI()
        {
            bool isIntro   = (_step == HydrogenGasTestStep.Intro);
            bool isResults = (_step == HydrogenGasTestStep.Step8_Results || _step == HydrogenGasTestStep.Completed);

            if (_introPanel != null) _introPanel.SetActive(isIntro);
            if (_hudPanel   != null) _hudPanel.SetActive(!isIntro);

            if (isIntro) return;

            int stepIdx = Mathf.Clamp((int)_step, 1, 8);

            // 8-pill progress indicator matching MagnesiumLabScene exactly
            for (int i = 0; i < _stepPillIndicators.Count; i++)
            {
                if (_stepPillIndicators[i] == null) continue;
                if (i < stepIdx - 1)
                {
                    _stepPillIndicators[i].color = ARVirtualLab.UI.EduTheme.PillDone; // Emerald completed
                }
                else if (i == stepIdx - 1)
                {
                    _stepPillIndicators[i].color = ARVirtualLab.UI.EduTheme.PillActive; // Cyan active
                }
                else
                {
                    _stepPillIndicators[i].color = ARVirtualLab.UI.EduTheme.PillIdle; // Inactive
                }
            }

            if (_stepNumberBadge != null)
            {
                if (stepIdx >= 1 && stepIdx <= 8)
                    _stepNumberBadge.text = MobileText.Clean($"STEP {stepIdx} OF 8");
                else if (_step == HydrogenGasTestStep.Completed)
                    _stepNumberBadge.text = MobileText.Clean("COMPLETE");
                else
                    _stepNumberBadge.text = MobileText.Clean("START");
            }

            if (_stepTitle != null) _stepTitle.text = MobileText.Clean(StepTitle(_step));
            if (_stepDesc  != null) _stepDesc.text  = MobileText.Clean(StepDesc(_step));

            bool canGoBack = (stepIdx > 1);
            if (_prevBtn != null) _prevBtn.gameObject.SetActive(canGoBack);

            if (isResults)
            {
                if (_prevBtn   != null) _prevBtn.gameObject.SetActive(false);
                if (_nextBtn   != null) _nextBtn.gameObject.SetActive(false);
                if (_obsBtn    != null) _obsBtn.gameObject.SetActive(true);
                if (_eqBtn     != null) _eqBtn.gameObject.SetActive(true);
                if (_resultBtn != null) _resultBtn.gameObject.SetActive(true);
            }
            else
            {
                if (_obsBtn    != null) _obsBtn.gameObject.SetActive(false);
                if (_eqBtn     != null) _eqBtn.gameObject.SetActive(false);
                if (_resultBtn != null) _resultBtn.gameObject.SetActive(false);
                if (_nextBtn   != null) _nextBtn.gameObject.SetActive(true);
            }
        }

        private string StepTitle(HydrogenGasTestStep s)
        {
            switch (s)
            {
                case HydrogenGasTestStep.Step1_PlaceTestTubeOnStand:       return "Step 1: Place Test Tube on Stand";
                case HydrogenGasTestStep.Step2_AddZinc:                  return "Step 2: Add Zinc Granules";
                case HydrogenGasTestStep.Step3_AddDiluteAcid:            return "Step 3: Add Dilute Sulphuric Acid";
                case HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube: return "Step 4: Seal with Cork & Delivery Tube";
                case HydrogenGasTestStep.Step5_ConnectDeliveryTubeToBasin: return "Step 5: Dip Delivery Tube into Soap Solution";
                case HydrogenGasTestStep.Step6_GasEvolution:             return "Step 6: Observe Hydrogen Gas Evolution";
                case HydrogenGasTestStep.Step7_SoapBubbles:              return "Step 7: Form Hydrogen Soap Bubbles";
                case HydrogenGasTestStep.Step8_HydrogenCandlePopTest:    return "Step 8: Hydrogen Identification Test";
                case HydrogenGasTestStep.Step8_Results:                  return "Results & Observations";
                case HydrogenGasTestStep.Completed:                      return "Experiment Completed";
                default: return "";
            }
        }

        private string StepDesc(HydrogenGasTestStep s)
        {
            switch (s)
            {
                case HydrogenGasTestStep.Step1_PlaceTestTubeOnStand:
                    return "Drag the test tube and clamp it securely onto the vertical stand.";
                case HydrogenGasTestStep.Step2_AddZinc:
                    return "Drag the zinc granules over the clamped test tube and drop them in.";
                case HydrogenGasTestStep.Step3_AddDiluteAcid:
                    return "Drag the dilute sulphuric acid bottle over the test tube opening to pour acid.";
                case HydrogenGasTestStep.Step4_SealWithCorkDeliveryTube:
                    return "Tap or drag the cork stopper (with its delivery tube) down into the mouth of the test tube.";
                case HydrogenGasTestStep.Step5_ConnectDeliveryTubeToBasin:
                    return "Tap the test tube to slide it down the clamp until the delivery tube outlet dips into the soap solution.";
                case HydrogenGasTestStep.Step6_GasEvolution:
                    return "Zn(s) + H\u2082SO\u2084(aq) \u2192 ZnSO\u2084(aq) + H\u2082\u2191. Rapid effervescence occurs as hydrogen gas travels through the tube.";
                case HydrogenGasTestStep.Step7_SoapBubbles:
                    return "Hydrogen gas passes into the soap solution basin, forming bubbles filled with hydrogen that rise into the air.";
                case HydrogenGasTestStep.Step8_HydrogenCandlePopTest:
                    return "Tap the candle to light the flame, then drag the burning candle near a rising soap bubble to test the gas.";
                case HydrogenGasTestStep.Step8_Results:
                    return "Hydrogen gas burns with a characteristic pop sound! Tap Observation, Equation, or Result below to review.";
                case HydrogenGasTestStep.Completed:
                    return "Experiment complete! Tap Reset to repeat from Step 1 or Back to return to Home.";
                default: return "";
            }
        }
    }
}
