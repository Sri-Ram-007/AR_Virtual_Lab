// ============================================================
// HydrogenLabScene.cs  —  AR Virtual Lab
// Formation of Hydrogen Gas: Zn + H2SO4 -> ZnSO4 + H2 (gas)
// Complete interactive 9-step virtual chemistry experiment.
// Builds the full 3D lab and all UI procedurally at runtime.
// Mobile-optimised (Android portrait, touch drag-and-drop).
// Namespace: ARVirtualLab.Hydrogen
// Formation of Hydrogen Gas: Zn + H2SO4 -> ZnSO4 + H2 (gas)
// Complete interactive 9-step virtual chemistry experiment.
// Builds the full 3D lab and all UI procedurally at runtime.
// Mobile-optimised (Android portrait, touch drag-and-drop).
// Namespace: ARVirtualLab.Hydrogen
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARVirtualLab.UI;

namespace ARVirtualLab.Hydrogen
{
    // ---------------------------------------------------------
    //  Experiment step enum (9-step sequence)
    // ---------------------------------------------------------
    public enum HydrogenStep
    {
        Intro                   = 0,
        Step1_SelectZinc        = 1,
        Step2_PlaceZinc         = 2,
        Step3_AddAcid           = 3,
        Step4_CloseCork         = 4,
        Step5_ObserveReaction   = 5,
        Step6_GasEvolution      = 6,
        Step7_GasFormation      = 7,
        Step8_ObserveCompletion = 8,
        Step9_Results           = 9,
        Completed               = 10
    }

    // Tag enum — completely separate from ARVirtualLab.Lab.LabTag
    public enum HydrogenTag { None, Flask, Zinc, Acid, Cork }

    // Simple tag component
    public class HydrogenInteractable : MonoBehaviour
    {
        public HydrogenTag htag = HydrogenTag.None;
    }

    // ---------------------------------------------------------
    //  Main MonoBehaviour
    // ---------------------------------------------------------
    [RequireComponent(typeof(AudioSource))]
    public class HydrogenLabScene : MonoBehaviour
    {
        // === Constants ===
        private const float TABLE_TOP_Y = -0.032f; // Top of the wood bench surface

        // === State ===
        private HydrogenStep _step = HydrogenStep.Intro;
        private bool _isCompleted = false;
        private AudioSource _audio;

        // === 3D Objects ===
        private GameObject _table;
        private GameObject _flask;
        private GameObject _flaskLiquid;
        private GameObject _zincGranules;
        private GameObject _zincInFlask;
        private GameObject _acidBottle;
        private GameObject _cork;
        private ParticleSystem _bubblePS;
        private ParticleSystem _gasPS;
        private Camera _cam;

        // === Gas Path Anchors ===
        // HydrogenTubeOutlet — child of cork container at tube open end
        private Transform _tubeOutlet;
        // World position for the cork container to firmly seat inside the flask neck
        private const float _corkSealedYOffset = 0.095f;

        // === Materials ===
        private Material _glassMat;
        private Material _liquidMat;
        private Material _zincMat;
        private Material _acidMat;
        private Material _corkMat;
        private Material _tableMat;
        private Material _benchMat;

        // === Interaction ===
        private GameObject _draggingObject = null;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private bool _zincSelected    = false;
        private bool _zincPlaced      = false;
        private bool _acidAdded       = false;
        private bool _corkPlaced      = false;
        private bool _reactionStarted = false;

        // === Reset Positions ===
        private Vector3 _zincOriginPos;
        private Vector3 _corkOriginPos;
        private Vector3 _acidBottleOriginPos;

        // === Zones ===
        private GameObject _flaskZone;
        private GameObject _acidZone;
        private GameObject _corkZone;
        private float _flaskNeckY = 0.288f;

        // === Coroutines ===
        private Coroutine _reactionCoroutine;

        // === Animation state ===
        private bool _animating;
        private Vector2 _pointerDownScreen;
        private float _acidTopOffset = 0.15f;      // pivot -> bottle neck tip
        private GameObject _pourStream;
        private const float LIQUID_FULL_H = 0.052f; // full acid column height in the flask base

        // === UI ===
        private Canvas _canvas;
        private GameObject _introPanel;
        private GameObject _hudPanel;
        private readonly List<Image> _stepPillIndicators = new List<Image>();
        private TMP_Text _stepNumberBadge;
        private TMP_Text _stepTitle;
        private TMP_Text _stepDesc;
        private TMP_Text _feedbackText;
        private Coroutine _feedbackCoroutine;
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
        private readonly List<GameObject> _equipLabels = new List<GameObject>();
        private HydrogenARTray _arTray;

        // =========================================================
        //  Unity Lifecycle
        // =========================================================
        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();

            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            BuildMaterials();
            BuildScene();
            BuildUI();

            if (ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToAR)
            {
                ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToAR = false;
                SetStep(HydrogenStep.Step1_SelectZinc);
                ToggleUnifiedAR();
            }
            else if (ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToExperiment)
            {
                ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToExperiment = false;
                SetStep(HydrogenStep.Step1_SelectZinc);
            }
            else
            {
                SetStep(HydrogenStep.Intro);
            }
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
                case HydrogenStep.Step1_SelectZinc:
                case HydrogenStep.Step2_PlaceZinc:  t = _zincGranules; break;
                case HydrogenStep.Step3_AddAcid:    t = _acidBottle;   break;
                case HydrogenStep.Step4_CloseCork:  t = _cork;         break;
            }
            _hint.Target(t);
            _hint.SetVisible(!_animating && _draggingObject == null);
        }

        private void Update()
        {
            UpdateHint();
            ARVirtualLab.UI.CameraFit.Apply(_cam, 48f);   // keep the same width of view on tall phones
            HandleTouchInput();
        }

        // =========================================================
        //  Camera & Event System
        // =========================================================
        private void EnsureCamera()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var camGO = GameObject.FindWithTag("MainCamera");
                if (camGO != null) _cam = camGO.GetComponent<Camera>();
            }

            if (_cam == null)
            {
                var camGO = new GameObject("Main Camera");
                _cam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
                camGO.AddComponent<AudioListener>();
            }

            _cam.enabled            = true;
            _cam.cullingMask        = -1;
            // Clear, uncropped portrait framing matching MagnesiumLabScene layout exactly
            _cam.transform.position = new Vector3(0f, 1.05f, -0.84f);
            _cam.transform.rotation = Quaternion.Euler(51f, 0f, 0f);
            _cam.fieldOfView        = 48f;
            _cam.backgroundColor    = ARVirtualLab.UI.EduTheme.CameraBackground;
            _cam.clearFlags         = CameraClearFlags.SolidColor;
            _cam.nearClipPlane      = 0.01f;
            _cam.farClipPlane       = 100f;
            _cam.depth              = 0;

            var urpData = _cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            if (urpData == null)
            {
                urpData = _cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            }
            if (urpData != null)
            {
                urpData.renderPostProcessing = true;
            }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem",
                    typeof(UnityEngine.EventSystems.EventSystem),
                    typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // =========================================================
        //  Material Library
        // =========================================================
        private void BuildMaterials()
        {
            Shader lit   = Shader.Find("Universal Render Pipeline/Lit")   ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            Shader unlit = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");

            // Glass (semi-transparent blue-clear with visible highlights and rim)
            _glassMat = new Material(lit);
            SetTransparent(_glassMat, new Color(0.82f, 0.94f, 1.00f, 0.22f));
            _glassMat.SetFloat("_Smoothness", 0.95f);

            // Acid liquid (colorless clear liquid)
            _liquidMat = new Material(lit);
            SetTransparent(_liquidMat, new Color(0.62f, 0.84f, 1.00f, 0.42f));
            _liquidMat.SetFloat("_Smoothness", 0.90f);

            // Zinc (bright silvery metallic for strong contrast)
            _zincMat = new Material(lit);
            SetProp(_zincMat, "_BaseColor", new Color(0.90f, 0.92f, 0.95f, 1f));
            _zincMat.SetFloat("_Smoothness", 0.65f);
            _zincMat.SetFloat("_Metallic",   0.85f);

            // Acid bottle (amber glass)
            _acidMat = new Material(lit);
            SetTransparent(_acidMat, new Color(0.95f, 0.98f, 0.80f, 0.45f));

            // Cork (warm natural tan brown)
            _corkMat = new Material(lit);
            SetProp(_corkMat, "_BaseColor", new Color(0.78f, 0.56f, 0.32f, 1f));
            _corkMat.SetFloat("_Smoothness", 0.25f);

            // Table frame / border (dark walnut brown)
            _tableMat = new Material(lit);
            SetProp(_tableMat, "_BaseColor", new Color(0.30f, 0.18f, 0.10f, 1f));
            _tableMat.SetFloat("_Smoothness", 0.35f);
            _tableMat.SetFloat("_Metallic", 0.02f);

            // Bench surface: warm polished medium-brown laboratory wood
            _benchMat = new Material(lit);
            SetProp(_benchMat, "_BaseColor", new Color(0.42f, 0.26f, 0.15f, 1f));
            _benchMat.SetFloat("_Smoothness", 0.45f);
            _benchMat.SetFloat("_Metallic", 0.02f);
        }

        private void SetTransparent(Material mat, Color color)
        {
            SetProp(mat, "_BaseColor", color);
            mat.SetFloat("_Surface",  1f);   // 0=Opaque, 1=Transparent
            mat.SetFloat("_Blend",    0f);   // 0=Alpha blend
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

        private void SetProp(Material mat, string prop, Color val)
        {
            if (mat.HasProperty(prop)) mat.SetColor(prop, val);
            else mat.color = val;
        }

        // =========================================================
        //  3D Scene Builder
        // =========================================================
        private void BuildScene()
        {
            // Table (medium-brown wooden laboratory bench providing high contrast)
            _table = Box("LabTable", new Vector3(0f, -0.06f, 0.05f), new Vector3(1.20f, 0.04f, 0.70f), _tableMat);
            Box("BenchSurface", new Vector3(0f, -0.038f, 0.05f), new Vector3(1.18f, 0.012f, 0.68f), _benchMat);

            // ── CONICAL FLASK (Centre-Left) ───────────────────────────
            var flaskContainer = new GameObject("ConicalFlask");
            flaskContainer.transform.SetParent(transform, false);
            flaskContainer.transform.position = new Vector3(-0.040f, TABLE_TOP_Y, 0.000f);

            bool flaskLoaded = TryLoadMeshy("Conical Flask", out _flask);
            if (flaskLoaded)
            {
                _flask.name = "ConicalFlaskModel";
                _flask.transform.SetParent(flaskContainer.transform, false);
                _flask.transform.localPosition = Vector3.zero;
                _flask.transform.localRotation = Quaternion.identity;
                // Scale ~40% larger (0.105f -> 0.145f) for prominent, realistic mobile presence
                _flask.transform.localScale = new Vector3(0.145f, 0.145f, 0.145f);
                foreach (var c in _flask.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                foreach (var r in _flask.GetComponentsInChildren<Renderer>(true))
                    r.gameObject.SetActive(true);
                EnhanceGlassMaterials(_flask);
                AlignToTableSurface(_flask, TABLE_TOP_Y);
            }
            else
            {
                _flask = ProcFlask();
                _flask.transform.SetParent(flaskContainer.transform, false);
                _flask.transform.localPosition = Vector3.zero;
                AlignToTableSurface(_flask, TABLE_TOP_Y);
            }
            EnsureBoxCollider(flaskContainer, new Vector3(0.26f, 0.40f, 0.26f));
            Tag(flaskContainer, HydrogenTag.Flask);
            _flask = flaskContainer;

            // ── ACID LIQUID (contained physically INSIDE the Conical Flask) ───
            // Parented directly to flaskContainer so it strictly follows the flask's transform.
            // Positioned at the wide bottom base, well below the neck opening.
            // Render queue 2999 = just below the glass (3000) so liquid draws inside the flask.
            _liquidMat.renderQueue = 2999;
            _flaskLiquid = Cyl("AcidLiquid", flaskContainer.transform,
                new Vector3(0f, 0.038f, 0f), new Vector3(0.110f, 0.026f, 0.110f), _liquidMat);
            _flaskLiquid.GetComponent<Renderer>().sharedMaterial.renderQueue = 2999;
            _flaskLiquid.SetActive(false);

            // ── ZINC GRANULES (source pile on bench right, matching watch glass area) ──
            var zincContainer = new GameObject("ZincGranules");
            zincContainer.transform.SetParent(transform, false);
            zincContainer.transform.position = new Vector3(0.245f, TABLE_TOP_Y, 0.035f);

            bool zincLoaded = TryLoadMeshy("Zinc Granules", out _zincGranules);
            if (zincLoaded)
            {
                _zincGranules.name = "ZincGranulesModel";
                _zincGranules.transform.SetParent(zincContainer.transform, false);
                _zincGranules.transform.localPosition = Vector3.zero;
                _zincGranules.transform.localRotation = Quaternion.identity;
                _zincGranules.transform.localScale = new Vector3(0.065f, 0.065f, 0.065f);
                foreach (var c in _zincGranules.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                foreach (var r in _zincGranules.GetComponentsInChildren<Renderer>(true))
                    r.gameObject.SetActive(true);
                EnhanceZincMaterials(_zincGranules);
                AlignToTableSurface(_zincGranules, TABLE_TOP_Y);
            }
            else
            {
                _zincGranules = ProcZincPile();
                _zincGranules.transform.SetParent(zincContainer.transform, false);
                _zincGranules.transform.localPosition = Vector3.zero;
                AlignToTableSurface(_zincGranules, TABLE_TOP_Y);
            }
            EnsureBoxCollider(zincContainer, new Vector3(0.14f, 0.08f, 0.14f));
            Tag(zincContainer, HydrogenTag.Zinc);
            _zincGranules = zincContainer;
            _zincOriginPos = _zincGranules.transform.position;

            // ── ZINC INSIDE FLASK (shown after placement, parented inside flask base) ──
            _zincInFlask = ProcZincSmall();
            _zincInFlask.transform.SetParent(flaskContainer.transform, false);
            _zincInFlask.transform.localScale    = new Vector3(1.6f, 1.6f, 1.6f);
            _zincInFlask.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            // Render queue 2998 = behind liquid (2999) and glass (3000), so zinc is clearly
            // visible through the transparent glass walls once placed inside the flask.
            foreach (var r in _zincInFlask.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial.renderQueue = 2998;
            _zincInFlask.SetActive(false);

            // ── ACID BOTTLE (Upper right bench position) ──────────────
            var acidContainer = new GameObject("AcidBottle");
            acidContainer.transform.SetParent(transform, false);
            acidContainer.transform.position = new Vector3(0.115f, TABLE_TOP_Y, -0.135f);

            bool acidLoaded = TryLoadMeshy("Acid Bottle", out _acidBottle);
            if (acidLoaded)
            {
                _acidBottle.name = "SulfuricAcidBottleModel";
                _acidBottle.transform.SetParent(acidContainer.transform, false);
                _acidBottle.transform.localPosition = Vector3.zero;
                _acidBottle.transform.localRotation = Quaternion.identity;
                _acidBottle.transform.localScale = new Vector3(0.085f, 0.085f, 0.085f);
                foreach (var c in _acidBottle.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                foreach (var r in _acidBottle.GetComponentsInChildren<Renderer>(true))
                    r.gameObject.SetActive(true);
                EnhanceAcidBottleMaterials(_acidBottle);
                AlignToTableSurface(_acidBottle, TABLE_TOP_Y);
            }
            else
            {
                _acidBottle = ProcAcidBottle();
                _acidBottle.transform.SetParent(acidContainer.transform, false);
                _acidBottle.transform.localPosition = Vector3.zero;
                AlignToTableSurface(_acidBottle, TABLE_TOP_Y);
            }
            EnsureBoxCollider(acidContainer, new Vector3(0.10f, 0.20f, 0.10f));
            Tag(acidContainer, HydrogenTag.Acid);
            _acidBottle = acidContainer;
            _acidBottleOriginPos = _acidBottle.transform.position;
            _acidTopOffset = Mathf.Max(0.06f, GetObjectTopY(acidContainer) - acidContainer.transform.position.y);

            // Thin acid stream used by the pouring animation
            _pourStream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            _pourStream.name = "AcidPourStream";
            _pourStream.transform.SetParent(transform, true);
            Destroy(_pourStream.GetComponent<Collider>());
            _pourStream.GetComponent<Renderer>().sharedMaterial = _liquidMat;
            _pourStream.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _pourStream.SetActive(false);

            // ── CORK + DELIVERY TUBE (Lower right position) ───────────
            var corkContainer = new GameObject("CorkWithTube");
            corkContainer.transform.SetParent(transform, false);
            corkContainer.transform.position = new Vector3(0.240f, TABLE_TOP_Y, 0.175f);

            bool corkLoaded = TryLoadMeshy("Tube with Cork", out _cork);
            if (corkLoaded)
            {
                _cork.name = "CorkWithTubeModel";
                _cork.transform.SetParent(corkContainer.transform, false);
                _cork.transform.localPosition = Vector3.zero;
                _cork.transform.localRotation = Quaternion.identity;
                _cork.transform.localScale = new Vector3(0.085f, 0.085f, 0.085f);
                foreach (var c in _cork.GetComponentsInChildren<Collider>(true))
                    if (!c.isTrigger) Destroy(c);
                foreach (var r in _cork.GetComponentsInChildren<Renderer>(true))
                    r.gameObject.SetActive(true);
                EnhanceCorkMaterials(_cork);
                AlignToTableSurface(_cork, TABLE_TOP_Y);
            }
            else
            {
                _cork = ProcCorkTube();
                _cork.transform.SetParent(corkContainer.transform, false);
                _cork.transform.localPosition = Vector3.zero;
                AlignToTableSurface(_cork, TABLE_TOP_Y);
            }
            // Mobile-friendly generous collider covering cork and entire tube
            EnsureBoxCollider(corkContainer, new Vector3(0.10f, 0.24f, 0.10f));
            var corkCol = corkContainer.GetComponentInChildren<BoxCollider>();
            if (corkCol != null) corkCol.center = new Vector3(0f, 0.08f, 0f);
            Tag(corkContainer, HydrogenTag.Cork);
            _cork = corkContainer;
            _corkOriginPos = _cork.transform.position;

            // ── HYDROGEN TUBE OUTLET — child of corkContainer at the glass tube's open end ──
            // In the Cork+Glass Tube model, the glass delivery tube extends upward through the stopper.
            // When corkContainer is aligned to table surface (or sealed), the open tube outlet is
            // at local Y ~ 0.165f relative to corkContainer.
            // Gas/smoke particles emit from here after the flask is sealed.
            var tubeOutletGO = new GameObject("HydrogenTubeOutlet");
            tubeOutletGO.transform.SetParent(corkContainer.transform, false);
            tubeOutletGO.transform.localPosition = new Vector3(0f, 0.165f, 0f);
            _tubeOutlet = tubeOutletGO.transform;

            // ── Calculate exact height for flask neck ─────────────────
            float neckY = GetObjectTopY(flaskContainer);
            if (neckY <= TABLE_TOP_Y + 0.05f) neckY = TABLE_TOP_Y + 0.320f;
            _flaskNeckY = neckY;

            // ── INTERACTION ZONES (aligned with exact flask neck) ────
            _flaskZone = Zone("Zone_Flask", new Vector3(-0.040f, (TABLE_TOP_Y + neckY) * 0.5f, 0.000f), new Vector3(0.26f, 0.38f, 0.26f));
            _acidZone  = Zone("Zone_Acid",  new Vector3(-0.040f, neckY, 0.000f), new Vector3(0.24f, 0.24f, 0.24f));
            _corkZone  = Zone("Zone_Cork",  new Vector3(-0.040f, neckY, 0.000f), new Vector3(0.24f, 0.24f, 0.24f));

            // ── PARTICLE SYSTEMS ───────────────────────────────────────
            // 1. Bubbles origin: inside flask at base around zinc granules
            var bubbleOriginGO = new GameObject("ZincBubbleOrigin");
            bubbleOriginGO.transform.SetParent(flaskContainer.transform, false);
            bubbleOriginGO.transform.localPosition = new Vector3(0f, 0.015f, 0f);
            _bubblePS = BuildBubbles(bubbleOriginGO.transform);

            // 2. Gas flow origin: open end of glass delivery tube on the cork container
            _gasPS = BuildGasFlow(_tubeOutlet);
            StopAndClearAllVFX();

            // ── SCENE LIGHTING — 3-point portrait studio lighting matching MagnesiumLabScene ──
            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.60f, 1f);

            // Key Light
            var keyGO = new GameObject("KeyLight");
            keyGO.transform.SetParent(transform, true);
            var keyL = keyGO.AddComponent<Light>();
            keyL.type = LightType.Directional;
            keyL.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            keyL.color = new Color(1f, 0.98f, 0.94f);
            keyL.intensity = 1.30f;

            // Fill Light
            var fillGO = new GameObject("FillLight");
            fillGO.transform.SetParent(transform, true);
            var fillL = fillGO.AddComponent<Light>();
            fillL.type = LightType.Directional;
            fillL.transform.rotation = Quaternion.Euler(35f, 155f, 0f);
            fillL.color = new Color(0.82f, 0.90f, 1f);
            fillL.intensity = 0.70f;

            // Overhead Rim Light
            var rimGO = new GameObject("RimLight");
            rimGO.transform.SetParent(transform, true);
            var rimL = rimGO.AddComponent<Light>();
            rimL.type = LightType.Directional;
            rimL.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
            rimL.color = Color.white;
            rimL.intensity = 0.50f;
        }

        // ---------------------------------------------------------
        //  VFX Management — guarantees particles stay completely OFF
        //  until explicitly triggered by experiment logic.
        // ---------------------------------------------------------
        private void StopAndClearAllVFX()
        {
            if (_bubblePS != null)
            {
                var em = _bubblePS.emission;
                em.enabled = false;
                em.rateOverTime = 0f;
                _bubblePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _bubblePS.Clear(true);
            }
            if (_gasPS != null)
            {
                var em = _gasPS.emission;
                em.enabled = false;
                em.rateOverTime = 0f;
                _gasPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                _gasPS.Clear(true);
            }
        }

        // ---------------------------------------------------------
        //  Material enhancers for Meshy models
        // ---------------------------------------------------------
        private void EnhanceGlassMaterials(GameObject go)
        {
            if (go == null) return;
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                // Replace every material slot with a fresh transparent glass material.
                // The Meshy AI-baked textures use fully-opaque _BaseColor, so we cannot simply
                // set alpha on the existing materials; we must create new ones with all URP
                // transparency flags properly set for correct rendering on mobile/Android.
                int matCount = Mathf.Max(r.sharedMaterials.Length, 1);
                var newMats  = new Material[matCount];
                for (int i = 0; i < matCount; i++)
                {
                    var glassMat = new Material(litShader);
                    // Semi-transparent blue-clear glass tint (alpha 0.22 = clearly see-through)
                    ApplyURPTransparent(glassMat, new Color(0.82f, 0.94f, 1.00f, 0.22f));
                    glassMat.SetFloat("_Smoothness", 0.95f);
                    glassMat.SetFloat("_Metallic",   0.04f);
                    // Keep specular highlights for realistic glass look on mobile
                    if (glassMat.HasProperty("_SpecularHighlights"))
                        glassMat.SetFloat("_SpecularHighlights", 1f);
                    if (glassMat.HasProperty("_EnvironmentReflections"))
                        glassMat.SetFloat("_EnvironmentReflections", 1f);
                    glassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent; // 3000
                    newMats[i] = glassMat;
                }
                r.sharedMaterials = newMats;
            }
        }

        /// <summary>
        /// Correctly configures all URP Lit shader properties for an alpha-blended transparent
        /// surface. Works with both URP/Lit and Standard fallback shaders on all platforms.
        /// </summary>
        private void ApplyURPTransparent(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else mat.color = color;

            mat.SetFloat("_Surface",  1f);   // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend",    0f);   // 0 = Alpha, 1 = Premultiply, 2 = Additive, 3 = Multiply
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


        private void EnhanceZincMaterials(GameObject go)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var mat in r.materials)
                {
                    if (mat == null) continue;
                    if (mat.HasProperty("_BaseColor"))
                    {
                        mat.SetColor("_BaseColor", new Color(0.90f, 0.92f, 0.95f, 1f));
                    }
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

        // ---------------------------------------------------------
        //  Meshy loader — mirrors the exact Magnesium Resources.Load workflow.
        //  Prefabs live in Assets/Resources/Models/ (created by
        //  HydrogenModelImporter Editor script from GLBs in Assets/Models/Hydrogen/).
        //  The original GLBs are NEVER moved or duplicated.
        // ---------------------------------------------------------
        private bool TryLoadMeshy(string keyword, out GameObject go)
        {
            // Map keyword → prefab name (matches HydrogenModelImporter._map)
            string prefabName = null;
            if (keyword == "Conical Flask")  prefabName = "ConicalFlaskModel";
            else if (keyword == "Zinc Granules")  prefabName = "ZincGranulesModel";
            else if (keyword == "Tube with Cork") prefabName = "CorkWithTubeModel";
            else if (keyword == "Acid Bottle" || keyword == "Sulfuric Acid") prefabName = "SulfuricAcidBottleModel";

            if (prefabName != null)
            {
                // Primary: Resources.Load (works in Editor, APK, iOS, WebGL)
                var prefab = Resources.Load<GameObject>("Models/" + prefabName);
                if (prefab != null)
                {
                    go = Instantiate(prefab);
                    go.transform.SetParent(transform, true);
                    Debug.Log($"[HydrogenLabScene] Loaded Meshy model: Models/{prefabName}");
                    return true;
                }
                Debug.LogWarning($"[HydrogenLabScene] Prefab not found: Models/{prefabName} — run Tools > Hydrogen Lab > Create Hydrogen Model Prefabs first.");
            }

            go = null;
            return false;
        }

        // ---------------------------------------------------------
        //  Object Alignment & Surface Bounds Helpers
        // ---------------------------------------------------------
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
            return maxY;
        }

        // ---------------------------------------------------------
        //  Procedural meshes
        // ---------------------------------------------------------
        private GameObject ProcFlask()
        {
            var root = new GameObject("Flask_Proc");
            root.transform.SetParent(transform, true);
            Cyl("Base",  root.transform, new Vector3(0,0,0),      new Vector3(0.055f,0.045f,0.055f), _glassMat);
            Cyl("Mid",   root.transform, new Vector3(0,0.06f,0),  new Vector3(0.038f,0.040f,0.038f), _glassMat);
            Cyl("Neck",  root.transform, new Vector3(0,0.11f,0),  new Vector3(0.018f,0.040f,0.018f), _glassMat);
            Cyl("Rim",   root.transform, new Vector3(0,0.148f,0), new Vector3(0.022f,0.006f,0.022f), _glassMat);
            Label3D(root.transform, "Conical Flask", new Vector3(0,0.17f,0), 8, new Color(0.6f,0.85f,1f));
            return root;
        }

        private GameObject ProcZincPile()
        {
            var root = new GameObject("Zinc_Proc");
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
                s.transform.localPosition = new Vector3(c[0],c[1],c[2]);
                s.transform.localScale    = new Vector3(c[3], c[3]*0.85f, c[3]);
                s.GetComponent<Renderer>().sharedMaterial = _zincMat;
                Destroy(s.GetComponent<Collider>());
            }
            Label3D(root.transform, "Zinc Granules", new Vector3(0,0.04f,0), 9, new Color(0.85f,0.85f,0.85f));
            return root;
        }

        private GameObject ProcZincSmall()
        {
            var root = new GameObject("ZincInFlask");
            root.transform.SetParent(transform, true);
            for (int i=0;i<5;i++)
            {
                var s = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                s.transform.SetParent(root.transform, false);
                float ang = i * 72f * Mathf.Deg2Rad;
                s.transform.localPosition = new Vector3(Mathf.Cos(ang)*0.012f, 0f, Mathf.Sin(ang)*0.012f);
                s.transform.localScale    = Vector3.one * 0.009f;
                s.GetComponent<Renderer>().sharedMaterial = _zincMat;
                Destroy(s.GetComponent<Collider>());
            }
            return root;
        }

        private GameObject ProcAcidBottle()
        {
            var root = new GameObject("AcidBottle_Proc");
            root.transform.SetParent(transform, true);
            Cyl("Body", root.transform, new Vector3(0,0,0),     new Vector3(0.030f,0.055f,0.030f), _acidMat);
            Cyl("Neck", root.transform, new Vector3(0,0.062f,0),new Vector3(0.012f,0.020f,0.012f), _acidMat);
            Cyl("Cap",  root.transform, new Vector3(0,0.080f,0),new Vector3(0.014f,0.008f,0.014f), SimpleMat(new Color(0.85f,0.1f,0.1f)));
            Label3D(root.transform, "Dil.\nH\u2082SO\u2084", new Vector3(0,0.08f,0.04f), 8, new Color(1f,0.55f,0.1f));
            return root;
        }

        private GameObject ProcCorkTube()
        {
            var root = new GameObject("Cork_Proc");
            root.transform.SetParent(transform, true);
            Cyl("CorkBody", root.transform, Vector3.zero,          new Vector3(0.018f,0.022f,0.018f), _corkMat);
            var tubeMat = SimpleMat(new Color(0.8f, 0.95f, 1.0f, 0.35f));
            Cyl("TubeRise",  root.transform, new Vector3(0,0.040f,0),     new Vector3(0.005f,0.040f,0.005f), tubeMat);
            var th = Cyl("TubeHoriz", root.transform, new Vector3(0.025f,0.072f,0), new Vector3(0.005f,0.030f,0.005f), tubeMat);
            th.transform.localEulerAngles = new Vector3(0,0,90f);
            Label3D(root.transform, "Cork + Tube", new Vector3(0,0.10f,0), 8, new Color(0.9f,0.75f,0.45f));
            return root;
        }

        // ---------------------------------------------------------
        //  Particle systems
        // ---------------------------------------------------------
        // Bubble effect: rises from zinc granules at the flask base up through the acid liquid.
        // Parented to ZincBubbleOrigin (child of flaskContainer) so it follows the flask.
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
                float e = Mathf.Clamp01((1f - r) * 6f); e = e * e * (3f - 2f * e);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(a, 0.6f) * e));
            }
            t.Apply();
            return _softDotTex = t;
        }

        private Material ParticleMat(Color c)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var m = new Material(sh);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", SoftDotTex());
            m.mainTexture = SoftDotTex();
            SetProp(m, "_BaseColor", c);
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 5;
            }
            return m;
        }

        private ParticleSystem BuildBubbles(Transform parent)
        {
            var go = new GameObject("H2Bubbles");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;   // sits at the zinc pile inside the flask base
            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop           = true;
            main.playOnAwake    = false;
            main.maxParticles   = 120;
            main.startLifetime  = new ParticleSystem.MinMaxCurve(0.30f, 0.50f);      // stays inside the acid column
            main.startSpeed     = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
            main.startSize      = new ParticleSystem.MinMaxCurve(0.008f, 0.016f);
            main.startColor     = new Color(0.95f, 1.0f, 1.0f, 1.0f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var em = ps.emission;
            em.enabled     = false;
            em.rateOverTime = 0f;

            // Matches the spread of zinc granules at the flask base
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 6f;
            sh.radius    = 0.032f;
            sh.rotation  = new Vector3(-90f, 0f, 0f);      // shoot upward

            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material   = ParticleMat(new Color(0.95f, 1.0f, 1.0f, 1.0f));

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            return ps;
        }

        // Gas/smoke effect: visible plume rising from the open delivery tube outlet after reaction starts.
        // Parented to HydrogenTubeOutlet on the Cork + Glass Tube model so it follows the tube.
        private ParticleSystem BuildGasFlow(Transform parent)
        {
            var go = new GameObject("H2GasFlow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = Vector3.zero;   // exactly at tube outlet
            var ps   = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop         = true;
            main.playOnAwake  = false;
            main.maxParticles = 80;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(1.3f, 2.1f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.05f, 0.09f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.022f, 0.040f);
            // grey-blue, not white: the light app background would swallow a white plume
            main.startColor      = new Color(0.50f, 0.62f, 0.74f, 0.80f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            var em = ps.emission;
            em.enabled      = false;
            em.rateOverTime = 0f;

            // Narrow cone pointing upward from tube outlet
            var sh = ps.shape;
            sh.shapeType = ParticleSystemShapeType.Cone;
            sh.angle     = 10f;    // narrow column emerging from open tube end
            sh.radius    = 0.004f; // matches inner tube aperture
            sh.rotation  = new Vector3(-90f, 0f, 0f);   // cone axis up

            var sol = ps.sizeOverLifetime;                    // the plume spreads out as it rises
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 2.2f)));
            var col = ps.colorOverLifetime;                   // and fades in / out softly
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                         new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = grad;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material   = ParticleMat(new Color(0.50f, 0.62f, 0.74f, 0.80f));

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Clear(true);
            return ps;
        }

        // ---------------------------------------------------------
        //  Geometry helpers
        // ---------------------------------------------------------
        private GameObject Box(string name, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(transform, true);
            go.transform.position   = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }

        private GameObject Cyl(string name, Transform parent, Vector3 localPos, Vector3 localScale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = localScale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private void WorldPos(GameObject go, Vector3 pos) => go.transform.position = pos;

        private Material SimpleMat(Color color)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default");
            var mat   = new Material(sh);
            SetProp(mat, "_BaseColor", color);
            if (color.a < 1f)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            return mat;
        }

        private TextMeshPro Label3D(Transform parent, string text, Vector3 localPos, float size, Color color)
        {
            var go = new GameObject("Lbl");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale    = Vector3.one * 0.015f;
            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text      = text;
            tmp.fontSize  = size;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.GetComponent<MeshRenderer>().sortingOrder = 2;
            return tmp;
        }

        private void EnsureBoxCollider(GameObject go, Vector3 size)
        {
            var col = go.GetComponentInChildren<BoxCollider>();
            if (col == null) col = go.AddComponent<BoxCollider>();
            col.size = size;
        }

        private void Tag(GameObject go, HydrogenTag tag)
        {
            var hi = go.GetComponent<HydrogenInteractable>();
            if (hi == null) hi = go.AddComponent<HydrogenInteractable>();
            hi.htag = tag;
        }

        private GameObject Zone(string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            var col = go.AddComponent<BoxCollider>();
            col.size = size; col.isTrigger = true;
            return go;
        }

        // =========================================================
        //  Touch Interaction
        // =========================================================
        private void HandleTouchInput()
        {
            if (ARVirtualLab.AR.ARExperimentMode.BlockSceneInput) return;    // AR: lab not placed yet
            if (_step == HydrogenStep.Intro || _step == HydrogenStep.Completed) return;

            bool down = false, held = false, up = false;
            Vector2 pos = Vector2.zero;

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                down = t.phase == TouchPhase.Began;
                held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                up   = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
                pos  = t.position;
            }
            else
            {
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0);
                up   = Input.GetMouseButtonUp(0);
                pos  = Input.mousePosition;
            }

            // Skip if touching UI
            if (UnityEngine.EventSystems.EventSystem.current != null &&
                UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;

            if (down) PointerDown(pos);
            if (held) PointerMove(pos);
            if (up)   PointerUp(pos);
        }

        private void PointerDown(Vector2 screen)
        {
            if (_animating) return;
            _pointerDownScreen = screen;
            Ray ray = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            // placement zones are triggers: they must not swallow taps meant for the apparatus
            if (!Physics.Raycast(ray, out var hit, 8f, ~0, QueryTriggerInteraction.Ignore)) return;

            var hi = hit.collider.GetComponentInParent<HydrogenInteractable>();
            if (hi == null) return;

            switch (_step)
            {
                case HydrogenStep.Step1_SelectZinc:
                    if (hi.htag == HydrogenTag.Zinc && !_zincSelected)
                    {
                        _zincSelected = true;
                        HighlightObject(_zincGranules, true);
                        ShowFeedback("Zinc granules selected! Drag them into the flask.", true);
                        SetStep(HydrogenStep.Step2_PlaceZinc);
                    }
                    break;
                case HydrogenStep.Step2_PlaceZinc:
                    if (hi.htag == HydrogenTag.Zinc)
                        StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case HydrogenStep.Step3_AddAcid:
                    if (hi.htag == HydrogenTag.Acid)
                        StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case HydrogenStep.Step4_CloseCork:
                    if (hi.htag == HydrogenTag.Cork)
                        StartDrag(RootOf(hi.gameObject), ray);
                    break;
            }
        }

        private void PointerMove(Vector2 screen)
        {
            if (_draggingObject == null) return;
            Ray ray = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            if (_dragPlane.Raycast(ray, out float dist))
                _draggingObject.transform.position = ray.GetPoint(dist) - _dragOffset;
        }

        private void PointerUp(Vector2 screen)
        {
            if (_draggingObject == null) return;
            var dropped = _draggingObject;
            _draggingObject = null;

            switch (_step)
            {
                case HydrogenStep.Step2_PlaceZinc:
                    if (Tapped(screen) || Near(dropped, _flaskZone)) PlaceZinc();
                    else { StartCoroutine(MoveRot(dropped.transform, _zincOriginPos, Quaternion.identity, 0.3f)); ShowFeedback("Drop zinc INTO the flask!", false); }
                    break;
                case HydrogenStep.Step3_AddAcid:
                    if (Tapped(screen) || Near(dropped, _acidZone)) AddAcid();
                    else { StartCoroutine(MoveRot(dropped.transform, _acidBottleOriginPos, Quaternion.identity, 0.3f)); ShowFeedback("Tip the acid over the flask opening.", false); }
                    break;
                case HydrogenStep.Step4_CloseCork:
                    if (Tapped(screen) || Near(dropped, _corkZone)) PlaceCork();
                    else { StartCoroutine(MoveRot(dropped.transform, _corkOriginPos, Quaternion.identity, 0.3f)); ShowFeedback("Place the stopper on the flask neck.", false); }
                    break;
            }
        }

        private bool Tapped(Vector2 screen) => Vector2.Distance(screen, _pointerDownScreen) < 12f;

        private void StartDrag(GameObject go, Ray ray)
        {
            _draggingObject = go;
            _dragPlane = new Plane(Vector3.forward, go.transform.position);
            if (_dragPlane.Raycast(ray, out float dist))
                _dragOffset = ray.GetPoint(dist) - go.transform.position;
            else _dragOffset = Vector3.zero;
        }

        private bool Near(GameObject obj, GameObject zone) =>
            zone != null && Vector3.Distance(obj.transform.position, zone.transform.position) < 0.22f;

        private GameObject RootOf(GameObject go)
        {
            // Walk up until parent is this scene root
            Transform t = go.transform;
            while (t.parent != null && t.parent != transform)
                t = t.parent;
            return t.gameObject;
        }

        // =========================================================
        //  Step Actions
        // =========================================================
        private IEnumerator MoveRot(Transform t, Vector3 pos, Quaternion rot, float dur)
        {
            Vector3 p0 = t.position; Quaternion r0 = t.rotation;
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                t.position = Vector3.Lerp(p0, pos, k);
                t.rotation = Quaternion.Slerp(r0, rot, k);
                yield return null;
            }
            t.position = pos; t.rotation = rot;
        }

        private void SetFlaskLiquidHeight(float h)
        {
            if (_flaskLiquid == null) return;
            h = Mathf.Max(0.0005f, h);
            var t = _flaskLiquid.transform;
            t.localScale    = new Vector3(0.110f, h * 0.5f, 0.110f);      // Unity cylinder is 2 units tall
            t.localPosition = new Vector3(0f, 0.012f + h * 0.5f, 0f);     // grows upward from the flask base
        }

        private void SetEffervescence(float rate)
        {
            if (_bubblePS == null) return;
            var em = _bubblePS.emission;
            if (rate <= 0f) { em.enabled = false; em.rateOverTime = 0f; _bubblePS.Stop(true, ParticleSystemStopBehavior.StopEmitting); return; }
            em.enabled = true; em.rateOverTime = rate;
            if (!_bubblePS.isPlaying) _bubblePS.Play();
        }

        private Vector3 FlaskNeckTop => new Vector3(-0.040f, _flaskNeckY, 0.000f);

        private void PlaceZinc()
        {
            if (_animating) return;
            StartCoroutine(ZincDropRoutine());
        }

        private IEnumerator ZincDropRoutine()
        {
            _animating = true;
            var zt = _zincGranules.transform;
            Vector3 full = zt.localScale;
            yield return MoveRot(zt, FlaskNeckTop + new Vector3(0f, 0.07f, 0f), zt.rotation, 0.4f);

            // granules shrink into the neck and fall to the flask base
            Vector3 p0 = zt.position, inside = new Vector3(-0.040f, TABLE_TOP_Y + 0.03f, 0f);
            float e = 0f, dur = 0.5f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                zt.position = Vector3.Lerp(p0, inside, k * k);
                zt.localScale = Vector3.Lerp(full, full * 0.25f, k);
                yield return null;
            }
            zt.localScale = full;
            _zincPlaced = true;
            _zincGranules.SetActive(false);
            _zincInFlask.SetActive(true);
            HighlightObject(_flask, true);
            ShowFeedback("\u2705 Zinc granules placed in the flask!", true);
            _animating = false;
            SetStep(HydrogenStep.Step3_AddAcid);
        }

        private void AddAcid()
        {
            if (_animating) return;
            StartCoroutine(PourAcidRoutine());
        }

        private IEnumerator PourAcidRoutine()
        {
            _animating = true;
            var bt = _acidBottle.transform;
            Quaternion tilt = Quaternion.Euler(0f, 0f, 78f);
            Vector3 neckTarget = FlaskNeckTop + new Vector3(0f, 0.03f, 0f);
            Vector3 pourPos = neckTarget - tilt * new Vector3(0f, _acidTopOffset, 0f);
            yield return MoveRot(bt, pourPos, tilt, 0.6f);

            _flaskLiquid.SetActive(true);
            float e = 0f, dur = 1.4f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                float h = LIQUID_FULL_H * k;
                SetFlaskLiquidHeight(h);
                Vector3 top = new Vector3(-0.040f, TABLE_TOP_Y + 0.012f + h, 0f);
                Vector3 mid = (neckTarget + top) * 0.5f;
                float len = Mathf.Max(0.001f, (neckTarget - top).magnitude);
                _pourStream.transform.position = mid;
                _pourStream.transform.localScale = new Vector3(0.0045f, len * 0.5f, 0.0045f);
                _pourStream.transform.rotation = Quaternion.FromToRotation(Vector3.up, (neckTarget - top).normalized);
                _pourStream.SetActive(true);
                if (k > 0.4f) SetEffervescence(Mathf.Lerp(8f, 22f, (k - 0.4f) / 0.6f));   // zinc starts to fizz as acid reaches it
                yield return null;
            }
            _pourStream.SetActive(false);
            yield return MoveRot(bt, _acidBottleOriginPos, Quaternion.identity, 0.55f);

            _acidAdded = true;
            HighlightObject(_flask, false);
            ShowFeedback("\u2705 Dilute sulphuric acid added! The zinc starts to fizz.", true);
            _animating = false;
            SetStep(HydrogenStep.Step4_CloseCork);
        }

        private void PlaceCork()
        {
            if (_animating) return;
            StartCoroutine(SealCorkRoutine());
        }

        private IEnumerator SealCorkRoutine()
        {
            _animating = true;
            var ct = _cork.transform;
            float sealedY = _flaskNeckY - _corkSealedYOffset;
            Vector3 sealed_ = new Vector3(-0.040f, sealedY, 0.000f);
            yield return MoveRot(ct, sealed_ + new Vector3(0f, 0.06f, 0f), Quaternion.identity, 0.45f);
            yield return MoveRot(ct, sealed_, Quaternion.identity, 0.25f);
            _corkPlaced = true;
            ShowFeedback("\u2705 Flask sealed with cork and glass delivery tube!", true);
            _animating = false;
            SetStep(HydrogenStep.Step5_ObserveReaction);
            if (_reactionCoroutine != null) StopCoroutine(_reactionCoroutine);
            _reactionCoroutine = StartCoroutine(RunReaction());
        }

        private IEnumerator RunReaction()
        {
            yield return new WaitForSeconds(0.8f);
            _reactionStarted = true;

            // Step 5: Observe Reaction — vigorous effervescence starts around zinc granules inside flask
            // Bubbles form around zinc granules ONLY after acid is added and flask is sealed with cork.
            SetStep(HydrogenStep.Step5_ObserveReaction);
            SetEffervescence(70f);
            ShowFeedback("Reaction active! Effervescence and bubbles forming around zinc granules.", true);
            yield return new WaitForSeconds(3.5f);

            // Step 6: Hydrogen Gas Evolution — gas moves upward through the delivery tube
            SetStep(HydrogenStep.Step6_GasEvolution);
            if (_gasPS != null)
            {
                var em = _gasPS.emission;
                em.enabled = true;
                em.rateOverTime = 45f;
                _gasPS.Play();
            }
            ShowFeedback("Hydrogen gas (H\u2082) moves upward through the glass delivery tube.", true);
            yield return new WaitForSeconds(3.5f);

            // Step 7: Hydrogen Gas Formation — steady stream of hydrogen gas discharging from the open delivery tube outlet
            SetStep(HydrogenStep.Step7_GasFormation);
            ShowFeedback("Continuous formation of hydrogen gas discharging from the open delivery tube outlet.", true);
            yield return new WaitForSeconds(3.5f);

            // Step 8: Observe Gas / Reaction Completion — effervescence subsides, reaction nears completion
            SetStep(HydrogenStep.Step8_ObserveCompletion);
            ShowFeedback("Reaction completion observed. Effervescence subsides as zinc reacts with the acid.", true);
            yield return new WaitForSeconds(3.0f);

            // Step 9: Observation / Equation / Result
            SetStep(HydrogenStep.Step9_Results);
            StopAndClearAllVFX();
            _isCompleted = true;
        }

        private void HighlightObject(GameObject go, bool on)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r.material == null) continue;
                if (on)  r.material.EnableKeyword("_EMISSION");
                else     r.material.DisableKeyword("_EMISSION");
                if (r.material.HasProperty("_EmissionColor"))
                    r.material.SetColor("_EmissionColor", on ? new Color(0.1f, 0.4f, 0.6f) : Color.black);
            }
        }

        // =========================================================
        //  Public Navigation (called by UI buttons)
        // =========================================================
        public void NextStep()
        {
            if (_step == HydrogenStep.Intro) { SetStep(HydrogenStep.Step1_SelectZinc); return; }
            if (_isCompleted || _animating) return;
            switch (_step)          // Next performs the current action (with its animation)
            {
                case HydrogenStep.Step1_SelectZinc: _zincSelected = true; break;
                case HydrogenStep.Step2_PlaceZinc:  PlaceZinc(); return;
                case HydrogenStep.Step3_AddAcid:    AddAcid();   return;
                case HydrogenStep.Step4_CloseCork:  PlaceCork(); return;
            }
            int next = (int)_step + 1;
            if (next <= (int)HydrogenStep.Completed) SetStep((HydrogenStep)next);
        }

        public void PrevStep()
        {
            if (_step <= HydrogenStep.Step1_SelectZinc) return;
            if (_step >= HydrogenStep.Step5_ObserveReaction) return;
            _step = (HydrogenStep)((int)_step - 1);
            RefreshUI();
        }

        public void ResetExperiment()
        {
            StopAllCoroutines();            // cancel any running drop / pour / seal animation
            _reactionCoroutine = null; _feedbackCoroutine = null; _animating = false;
            if (_pourStream != null) _pourStream.SetActive(false);
            SetEffervescence(0f);
            StopAndClearAllVFX();

            _zincGranules.SetActive(true);
            _zincGranules.transform.localScale = Vector3.one;
            _zincGranules.transform.position = _zincOriginPos;
            _zincInFlask.SetActive(false);
            _flaskLiquid.SetActive(false);
            _cork.transform.position        = _corkOriginPos;
            _cork.transform.rotation        = Quaternion.identity;
            _acidBottle.transform.SetPositionAndRotation(_acidBottleOriginPos, Quaternion.identity);
            SetFlaskLiquidHeight(0f);

            HighlightObject(_flask, false);
            HighlightObject(_zincGranules, false);

            _zincSelected = _zincPlaced = _acidAdded = _corkPlaced = false;
            _reactionStarted = _isCompleted = false;
            _draggingObject = null;

            if (_safetyPanel != null) _safetyPanel.SetActive(false);
            if (_obsPanel != null) _obsPanel.SetActive(false);
            if (_eqPanel != null) _eqPanel.SetActive(false);
            if (_resultPanel != null) _resultPanel.SetActive(false);
            if (_aboutPanel != null) _aboutPanel.SetActive(false);
            if (_resetConfirmDialog != null) _resetConfirmDialog.SetActive(false);

            SetStep(HydrogenStep.Step1_SelectZinc);
            ShowFeedback("Experiment reset. Tap the zinc granules on the table.", true);
        }

        // =========================================================
        //  UI Builder — Matches Magnesium Ribbon UI Design & Layout
        //  1080 x 1920 portrait canvas, dark navy theme
        // =========================================================
        private void BuildUI()
        {
            BuildUIRaw();
            if (_canvas != null) ARVirtualLab.UI.EduTheme.Apply(_canvas.transform);   // light, textbook-style theme
        }

        private void BuildUIRaw()
        {
            var canvasGO = new GameObject("HydrogenCanvas");
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

            // 3. Equipment Labels
            BuildEquipmentLabels(root);

            // 4. Modal Panels
            BuildModalPanels(root);

            // 5. AR Component Tray
            var trayGO = new GameObject("HydrogenARTrayRoot");
            trayGO.transform.SetParent(root, false);
            _arTray = trayGO.AddComponent<HydrogenARTray>();
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
            MakeText(cardRT, "Title", "HYDROGEN GAS FROM ZINC",
                new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.89f),
                36, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            // Subtitle
            MakeText(cardRT, "Subtitle", "Interactive Chemistry Experiment",
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
                "1. Prepare Zinc\n" +
                "2. Place Zinc in Flask\n" +
                "3. Add Dilute Sulphuric Acid\n" +
                "4. Seal with Cork & Delivery Tube\n" +
                "5. Observe Reaction & Bubbling\n" +
                "6. Hydrogen Gas Evolution\n" +
                "7. Hydrogen Gas Formation\n" +
                "8. Observe Gas / Reaction Completion\n" +
                "9. Observation / Equation / Result",
                new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.84f),
                16, new Color(0.82f, 0.88f, 0.96f), FontStyles.Normal, TextAlignmentOptions.Left);

            // Primary Button: START EXPERIMENT
            _startBtn = MakeButton(cardRT, "StartBtn", "Start Experiment",
                new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.25f),
                new Color(0.02f, 0.50f, 0.88f));
            var sbOut = _startBtn.gameObject.AddComponent<Outline>();
            sbOut.effectColor = new Color(0.30f, 0.80f, 1f, 0.80f);
            sbOut.effectDistance = new Vector2(1.5f, -1.5f);
            _startBtn.onClick.AddListener(() => SetStep(HydrogenStep.Step1_SelectZinc));

            // Secondary Button: AR LAB
            var arIntroBtn = MakeButton(cardRT, "ARIntroBtn", "AR Lab",
                new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.145f),
                new Color(0.32f, 0.24f, 0.72f));
            arIntroBtn.onClick.AddListener(() => {
                SetStep(HydrogenStep.Step1_SelectZinc);
                ToggleUnifiedAR();
            });

            // Footer Options: Home, Safety & About
            var homeLink = MakeButton(cardRT, "HomeLink", "\u2190 Home",
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

            // ── Top Header Card ───────────────────────────────
            var topBar = MakePanel(_hudPanel.GetComponent<RectTransform>(), "TopHeaderCard",
                new Color(0.04f, 0.08f, 0.16f, 0.95f));
            var topRT = topBar.GetComponent<RectTransform>();
            topRT.anchorMin = new Vector2(0.03f, 0.81f);
            topRT.anchorMax = new Vector2(0.97f, 0.985f);
            var tbOut = topBar.AddComponent<Outline>();
            tbOut.effectColor = new Color(0.15f, 0.45f, 0.85f, 0.60f);
            tbOut.effectDistance = new Vector2(1.5f, -1.5f);

            // Row 1: Back Button, App Header & AR Button
            var backBtn = MakeButton(topRT, "BackButton", "\u2190 Back",
                new Vector2(0.03f, 0.79f), new Vector2(0.24f, 0.97f),
                new Color(0.12f, 0.22f, 0.38f));
            backBtn.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());

            MakeText(topRT, "AppLabel", "HYDROGEN GAS FROM ZINC",
                new Vector2(0.26f, 0.82f), new Vector2(0.70f, 0.97f),
                20, new Color(0.35f, 0.85f, 1.00f), FontStyles.Bold, TextAlignmentOptions.Left);

            var arBtn = MakeButton(topRT, "ARButton", "AR LAB",
                new Vector2(0.72f, 0.79f), new Vector2(0.97f, 0.97f),
                new Color(0.32f, 0.24f, 0.72f));
            arBtn.onClick.AddListener(ToggleUnifiedAR);

            // Row 2: 9-Step Progress Indicators (Pills)
            var pillRow = new GameObject("StepPillRow", typeof(RectTransform));
            pillRow.transform.SetParent(topRT, false);
            var prRT = pillRow.GetComponent<RectTransform>();
            prRT.anchorMin = new Vector2(0.04f, 0.69f);
            prRT.anchorMax = new Vector2(0.96f, 0.76f);
            prRT.offsetMin = prRT.offsetMax = Vector2.zero;

            _stepPillIndicators.Clear();
            float stepWidth = 1.0f / 9.0f;
            for (int i = 0; i < 9; i++)
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
            _stepNumberBadge = MakeText(sbrt, "BadgeText", "STEP 1 OF 9",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                14, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            _stepTitle = MakeText(topRT, "StepTitle", "Step 1: Prepare Zinc",
                new Vector2(0.35f, 0.44f), new Vector2(0.96f, 0.65f),
                21, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);

            // Row 4: Instruction Description
            _stepDesc = MakeText(topRT, "StepDesc", "Tap the zinc granules on the table.",
                new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.42f),
                18, new Color(0.85f, 0.92f, 0.98f), FontStyles.Normal, TextAlignmentOptions.Left);

            // ── Toast Feedback ────────────────────────────────
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

            // ── Bottom Action Bar ─────────────────────────────
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
            _nextBtn.gameObject.SetActive(false);

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

        private void BuildEquipmentLabels(RectTransform root)
        {
            Color labelBg = new Color(0.06f, 0.10f, 0.20f, 0.85f);
            Color labelText = Color.white;

            CreateEquipLabel(root, "FlaskLabel",  _flask != null ? _flask.transform : null,             new Vector3(-0.040f, 0.120f, 0.000f), new Vector2(0f, 40f), "Conical Flask",  labelBg, labelText);
            CreateEquipLabel(root, "ZincLabel",   _zincGranules != null ? _zincGranules.transform : null, new Vector3(0.220f, 0.020f, 0.050f),  new Vector2(0f, 25f), "Zinc Granules",  labelBg, labelText);
            CreateEquipLabel(root, "AcidLabel",   _acidBottle != null ? _acidBottle.transform : null,   new Vector3(0.220f, 0.110f, -0.090f), new Vector2(0f, 35f), "Dil. H\u2082SO\u2084", labelBg, labelText);
            CreateEquipLabel(root, "CorkLabel",   _cork != null ? _cork.transform : null,               new Vector3(0.220f, 0.080f, 0.170f),  new Vector2(0f, 30f), "Tube with Cork", labelBg, labelText);
        }

        private void CreateEquipLabel(RectTransform root, string name, Transform target, Vector3 fallbackWorldPos, Vector2 screenOffset,
            string text, Color bg, Color col)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(130f, 42f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);

            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;

            var outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(0.20f, 0.50f, 0.85f, 0.60f);
            outl.effectDistance = new Vector2(1f, -1f);

            MakeText(rt, "Lbl", text,
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                15f, col, FontStyles.Bold, TextAlignmentOptions.Center);

            var pos = go.AddComponent<HydrogenLabelPositioner>();
            if (target != null)
                pos.Init(_cam, target, Vector3.up * 0.06f, screenOffset, root);
            else
                pos.Init(_cam, fallbackWorldPos, screenOffset, root);

            _equipLabels.Add(go);
        }

        private void BuildModalPanels(RectTransform root)
        {
            _obsPanel = MakeInfoPanel(root, "ObsPanel", "OBSERVATION",
                "\u2022 Rapid bubbling (brisk effervescence) occurs around the zinc granules upon adding dilute sulphuric acid.\n\n" +
                "\u2022 A colorless, odorless hydrogen gas (H\u2082) evolves continuously from the flask.\n\n" +
                "\u2022 The reaction is exothermic \u2014 thermal energy is released and the conical flask warms up.\n\n" +
                "\u2022 Hydrogen gas travels upward through the glass delivery tube and discharges smoothly into the air from the tube outlet.\n\n" +
                "\u2022 Zinc granules gradually dissolve as zinc sulphate (ZnSO\u2084) is formed in the aqueous solution.");
            _obsPanel.SetActive(false);

            _eqPanel = MakeInfoPanel(root, "EqPanel", "CHEMICAL EQUATION",
                "Zinc + Dilute Sulphuric Acid \u2192 Zinc Sulphate + Hydrogen Gas\n\n" +
                "Zn(s)   +   H\u2082SO\u2084(aq)   \u2192   ZnSO\u2084(aq)   +   H\u2082(g)\u2191\n\n" +
                "Reactants:\n" +
                "  \u2022 Zinc (Zn) \u2014 Silvery-grey metallic granules\n" +
                "  \u2022 Sulphuric Acid (H\u2082SO\u2084) \u2014 Colorless dilute acid solution\n\n" +
                "Products:\n" +
                "  \u2022 Zinc Sulphate (ZnSO\u2084) \u2014 Colorless aqueous solution\n" +
                "  \u2022 Hydrogen (H\u2082) \u2014 Colorless, flammable diatomic gas\n\n" +
                "Classification: Single Displacement (Redox) & Exothermic Reaction");
            _eqPanel.SetActive(false);

            _resultPanel = MakeInfoPanel(root, "ResultPanel", "EXPERIMENT RESULT",
                "EXPERIMENT CONCLUSION:\n\n" +
                "Dilute sulphuric acid reacts with reactive zinc metal to produce aqueous zinc sulphate and evolve hydrogen gas.\n\n" +
                "Key Conclusions:\n\n" +
                "1. Single Displacement Reaction: Zinc displaces hydrogen from dilute acid as it is higher in the reactivity series.\n\n" +
                "2. Hydrogen Gas Evolution: Characterised by vigorous effervescence and continuous upward gas flow through the delivery tube.\n\n" +
                "3. Exothermic Reaction: Thermal energy is released into the solution during the reaction.\n\n" +
                "4. Reaction Completion: Effervescence subsides as the available zinc reacts with the acid.\n\n" +
                "Equation: Zn(s) + H\u2082SO\u2084(aq) \u2192 ZnSO\u2084(aq) + H\u2082(g)\u2191");
            _resultPanel.SetActive(false);

            _safetyPanel = MakeInfoPanel(root, "SafetyPanel", "SAFETY PRECAUTIONS",
                "LABORATORY SAFETY GUIDELINES:\n\n" +
                "1. Wear certified safety goggles and a lab coat at all times.\n\n" +
                "2. Dilute sulphuric acid (H\u2082SO\u2084) is corrosive \u2014 avoid contact with skin and eyes.\n\n" +
                "3. In case of accidental acid contact, rinse immediately with plenty of running water.\n\n" +
                "4. Handle glassware carefully to avoid cuts from cracked flasks or delivery tubes.\n\n" +
                "5. Ensure the cork fits tightly to prevent gas leakage.\n\n" +
                "6. Always handle acids under adequate laboratory ventilation.");
            _safetyPanel.SetActive(false);

            _aboutPanel = MakeInfoPanel(root, "AboutPanel", "ABOUT EXPERIMENT",
                "FORMATION OF HYDROGEN GAS EXPERIMENT\n\n" +
                "Curriculum Topic: Acids, Bases, and Salts / Reactivity Series\n" +
                "Subject: Secondary School Chemistry (Grade 9-10)\n\n" +
                "Learning Objectives:\n" +
                "\u2022 Demonstrate the displacement reaction of metals with acids\n" +
                "\u2022 Observe the physical signs of chemical change (effervescence, gas evolution)\n" +
                "\u2022 Understand the formation and flow of hydrogen gas\n\n" +
                "Developed for Virtual & Augmented Reality Science Education.");
            _aboutPanel.SetActive(false);

            // Reset Confirmation Dialog
            BuildResetConfirmationDialog(root);
        }

        private void BuildResetConfirmationDialog(RectTransform root)
        {
            _resetConfirmDialog = MakePanel(root, "ResetConfirmOverlay", new Color(0f, 0f, 0f, 0.70f));
            var oImg = _resetConfirmDialog.GetComponent<Image>();
            if (oImg != null) oImg.raycastTarget = true;

            var bg = MakePanel(_resetConfirmDialog.GetComponent<RectTransform>(), "Bg",
                new Color(0.06f, 0.10f, 0.20f, 0.98f));
            var bgRT = bg.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0.08f, 0.35f);
            bgRT.anchorMax = new Vector2(0.92f, 0.65f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;

            var bgOut = bg.AddComponent<Outline>();
            bgOut.effectColor = new Color(0.20f, 0.60f, 1.0f, 0.70f);
            bgOut.effectDistance = new Vector2(2f, -2f);

            MakeText(bgRT, "Heading", "Reset Experiment?",
                new Vector2(0.05f, 0.65f), new Vector2(0.95f, 0.92f),
                26, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            MakeText(bgRT, "Message", "Your current experiment progress will be cleared.\nAre you sure you want to restart?",
                new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.65f),
                18, new Color(0.82f, 0.88f, 0.96f), FontStyles.Normal, TextAlignmentOptions.Center);

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

            var closeBtn = MakeButton(bgRT, "Close", "Close",
                new Vector2(0.30f, 0.02f), new Vector2(0.70f, 0.09f),
                new Color(0.15f, 0.45f, 0.85f));
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

        private static GameObject MakePanel(RectTransform parent, string name, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            if (col.a > 0.001f)
            {
                var img = go.AddComponent<Image>();
                img.color = col;
                img.raycastTarget = false;
            }
            return go;
        }

        private static TMP_Text MakeText(RectTransform parent, string name, string text,
            Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color col, FontStyles style,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = text;
            tmp.fontSize = fontSize;
            tmp.color    = col;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.overflowMode = TextOverflowModes.Overflow;
            var f = GetFont();
            if (f != null) tmp.font = f;
            MobileText.Setup(tmp, name, fontSize);
            return tmp;
        }

        private static Button MakeButton(RectTransform parent, string name, string label,
            Vector2 anchorMin, Vector2 anchorMax, Color bgCol)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            var img = go.AddComponent<Image>();
            img.color = bgCol;
            img.raycastTarget = true;

            var btn = go.AddComponent<Button>();
            var cb  = btn.colors;
            cb.highlightedColor = bgCol * 1.3f;
            cb.pressedColor     = bgCol * 0.7f;
            btn.colors          = cb;
            btn.targetGraphic   = img;

            var txtGO = new GameObject("Label");
            txtGO.transform.SetParent(go.transform, false);
            var trt = txtGO.AddComponent<RectTransform>();
            trt.anchorMin = new Vector2(0.02f, 0.02f);
            trt.anchorMax = new Vector2(0.98f, 0.98f);
            trt.offsetMin = trt.offsetMax = Vector2.zero;

            var tmp = txtGO.AddComponent<TextMeshProUGUI>();
            tmp.text      = label;
            tmp.fontSize  = 20f;
            tmp.color     = Color.white;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            var f = GetFont();
            if (f != null) tmp.font = f;
            MobileText.Setup(tmp, "Lbl", 20f);
            return btn;
        }

        private static void TogglePanel(GameObject panel)
        {
            if (panel != null) panel.SetActive(!panel.activeSelf);
        }

        public void ShowFeedback(string msg, bool ok = true)
        {
            if (_feedbackText == null) return;
            _feedbackText.text = MobileText.Clean(msg);
            var toastGO = _feedbackText.transform.parent.gameObject;
            toastGO.SetActive(true);
            if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = StartCoroutine(HideFeedback(3.5f));
        }

        private IEnumerator HideFeedback(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_feedbackText != null)
                _feedbackText.transform.parent.gameObject.SetActive(false);
        }

        private void SetStep(HydrogenStep s)
        {
            _step = s;
            RefreshUI();
        }

        private void RefreshUI()
        {
            bool isIntro   = (_step == HydrogenStep.Intro);
            bool isResults = (_step == HydrogenStep.Step9_Results || _step == HydrogenStep.Completed);

            if (_introPanel != null) _introPanel.SetActive(isIntro);
            if (_hudPanel   != null) _hudPanel.SetActive(!isIntro);

            foreach (var lbl in _equipLabels)
            {
                if (lbl != null) lbl.SetActive(!isIntro);
            }

            if (isIntro) return;

            int stepIdx = (int)_step;

            // 9-pill indicator progress bar
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
                    _stepPillIndicators[i].color = new Color(0.14f, 0.20f, 0.32f, 0.7f); // Inactive
                }
            }

            // Step badge
            if (_stepNumberBadge != null)
            {
                if (stepIdx >= 1 && stepIdx <= 9)
                    _stepNumberBadge.text = MobileText.Clean($"STEP {stepIdx} OF 9");
                else if (_step == HydrogenStep.Completed)
                    _stepNumberBadge.text = MobileText.Clean("COMPLETE");
                else
                    _stepNumberBadge.text = MobileText.Clean("START");
            }

            // Title and Description
            if (_stepTitle != null) _stepTitle.text = MobileText.Clean(StepTitle(_step));
            if (_stepDesc  != null) _stepDesc.text  = MobileText.Clean(StepDesc(_step));

            // Previous button visibility
            bool canGoBack = (stepIdx > 1 && _step < HydrogenStep.Step5_ObserveReaction);
            if (_prevBtn != null) _prevBtn.gameObject.SetActive(canGoBack);

            // Results / completion buttons
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
                // "Next" performs the current drag/tap action for the student (fallback for small screens / accessibility)
                if (_nextBtn   != null) _nextBtn.gameObject.SetActive(_step >= HydrogenStep.Step1_SelectZinc && _step <= HydrogenStep.Step4_CloseCork);
            }
        }

        private string StepTitle(HydrogenStep s)
        {
            switch (s)
            {
                case HydrogenStep.Step1_SelectZinc:        return "Step 1: Prepare Zinc";
                case HydrogenStep.Step2_PlaceZinc:         return "Step 2: Place Zinc in Flask";
                case HydrogenStep.Step3_AddAcid:           return "Step 3: Add Dilute Sulphuric Acid";
                case HydrogenStep.Step4_CloseCork:         return "Step 4: Seal with Cork";
                case HydrogenStep.Step5_ObserveReaction:   return "Step 5: Observe Reaction";
                case HydrogenStep.Step6_GasEvolution:      return "Step 6: Hydrogen Gas Evolution";
                case HydrogenStep.Step7_GasFormation:      return "Step 7: Hydrogen Gas Formation";
                case HydrogenStep.Step8_ObserveCompletion: return "Step 8: Observe Gas / Reaction Completion";
                case HydrogenStep.Step9_Results:           return "Step 9: Observation / Equation / Result";
                case HydrogenStep.Completed:               return "Experiment Completed";
                default: return "";
            }
        }

        private string StepDesc(HydrogenStep s)
        {
            switch (s)
            {
                case HydrogenStep.Step1_SelectZinc:
                    return "Tap the zinc granules on the table to inspect them.";
                case HydrogenStep.Step2_PlaceZinc:
                    return "Drag the zinc granules into the conical flask.";
                case HydrogenStep.Step3_AddAcid:
                    return "Drag the dilute sulphuric acid bottle to add acid into the flask.";
                case HydrogenStep.Step4_CloseCork:
                    return "Drag the cork with delivery tube onto the flask neck to seal it.";
                case HydrogenStep.Step5_ObserveReaction:
                    return "Chemical reaction begins! Observe effervescence and bubbles forming around the zinc granules.";
                case HydrogenStep.Step6_GasEvolution:
                    return "Hydrogen gas (H\u2082) moves upward through the glass delivery tube.";
                case HydrogenStep.Step7_GasFormation:
                    return "Continuous formation of hydrogen gas discharging from the open end of the delivery tube.";
                case HydrogenStep.Step8_ObserveCompletion:
                    return "Observe the reaction completing as effervescence subsides.";
                case HydrogenStep.Step9_Results:
                    return "Experiment complete! Tap Observation, Equation, or Result below to review.";
                case HydrogenStep.Completed:
                    return "Experiment complete! Tap Reset to repeat or Back to return to Home.";
                default: return "";
            }
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Helper MonoBehaviour: projects 3D world-space equipment
    //  positions to screen-space label positions each frame.
    // ─────────────────────────────────────────────────────────
    public class HydrogenLabelPositioner : MonoBehaviour
    {
        private Camera _cam;
        private Transform _target;
        private Vector3 _worldPos;
        private Vector2 _offset;
        private RectTransform _canvasRT;
        private RectTransform _rt;

        public void Init(Camera cam, Transform target, Vector3 worldOffset, Vector2 screenOffset, RectTransform canvasRT)
        {
            _cam      = cam;
            _target   = target;
            _worldPos = worldOffset;
            _offset   = screenOffset;
            _canvasRT = canvasRT;
            _rt       = GetComponent<RectTransform>();
        }

        public void Init(Camera cam, Vector3 worldPos, Vector2 screenOffset, RectTransform canvasRT)
        {
            _cam      = cam;
            _target   = null;
            _worldPos = worldPos;
            _offset   = screenOffset;
            _canvasRT = canvasRT;
            _rt       = GetComponent<RectTransform>();
        }

        private CanvasGroup _cg;

        public void SetCamera(Camera cam) => _cam = cam;

        private void LateUpdate()
        {
            if (_cg == null)
            {
                _cg = GetComponent<CanvasGroup>();
                if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
                _cg.blocksRaycasts = false; _cg.interactable = false;
            }
            // fade instead of SetActive: an inactive label would never get another LateUpdate to come back
            if ((_target != null && !_target.gameObject.activeInHierarchy) || ARVirtualLab.AR.ARExperimentMode.BlockSceneInput)
            {
                _cg.alpha = 0f;
                return;
            }

            if (_cam == null || !_cam.gameObject.activeInHierarchy) _cam = Camera.main;
            if (_cam == null || _rt == null) return;

            Vector3 wPos = (_target != null) ? (_target.position + _worldPos) : _worldPos;
            Vector3 screen = _cam.WorldToScreenPoint(wPos);

            if (screen.z < 0) { _cg.alpha = 0f; return; }
            _cg.alpha = 1f;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screen, null, out Vector2 local);
            _rt.anchoredPosition = local + _offset;
        }
    }
}
