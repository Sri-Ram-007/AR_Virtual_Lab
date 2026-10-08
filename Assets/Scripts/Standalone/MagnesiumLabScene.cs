using ARVirtualLab.AR;
﻿// ============================================================
// MagnesiumLabScene.cs  —  AR Virtual Lab Standalone Experiment
// Complete interactive Virtual Chemistry Laboratory
// Visual overhaul matching reference images:
//   - Dark tongs, silver ribbon, brown sandpaper, transparent watch glass
//   - Portrait Android camera framing (fov=58°, pos=(0,0.55,-0.42), pitch=52°)
//   - Extended burning (6 s), heating (2.5 s), cleaning (0.45 acc)
//   - Smoke particle system during burning
//   - Cleaning progress bar, equipment labels, burning info panel
//   - MgO labeled correctly as solid ash / white particulate
//   - Reaction equation: 2Mg + O₂ → 2MgO
//   - Previous / Next navigation buttons
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARVirtualLab.UI;

namespace ARVirtualLab.Lab
{
    public enum LabState
    {
        Intro = 0,
        Step1_SelectRibbon,
        Step2_CleanRibbon,
        Step3_PickWithTongs,
        Step4_LightBurner,
        Step5_MoveIntoFlame,
        Step6_Heating,
        Step7_Burning,
        Step8_Cooling,
        Step9_Collect,
        Completed
    }

    public enum LabTag { None, Ribbon, Sandpaper, Tongs, Burner, WatchGlass }

    public class LabInteractable : MonoBehaviour
    {
        public new LabTag tag;
    }

    [RequireComponent(typeof(AudioSource))]
    public class MagnesiumLabScene : MonoBehaviour
    {
        // ── 3D Scene Objects ──────────────────────────────────
        private GameObject _table, _sandpaper, _ribbon, _tongs;
        private GameObject _burner, _flameVFX, _flameHeatZone;
        private GameObject _outerFlameMeshGO, _innerFlameMeshGO;
        private GameObject _watchGlass, _collectionZone, _mgoResidue, _mgoPowder;
        private Transform  _gripPoint, _flamePoint, _collectionPoint;
        private ParticleSystem _sparkPS, _baseFlamePS, _mainFlamePS, _smokePS;
        private Light _burnLight, _flameLight;
        private Material _ribbonMat, _burnerMat, _tongsMat, _glassMat, _tableMat, _sandpaperMat, _mgoMat;
        private Material _outerFlameMat, _innerFlameMat;
        private Renderer _ribbonRenderer;
        private Camera _cam;

        // ── Equipment Accessors for AR Mode Placement ──────────
        public GameObject TableObject => _table;
        public GameObject SandpaperObject => _sandpaper;
        public GameObject RibbonObject => _ribbon;
        public GameObject TongsObject => _tongs;
        public GameObject BurnerObject => _burner;
        public GameObject WatchGlassObject => _watchGlass;

        // ── AR Tabletop Layout Positions (relative to ARLabRoot on detected ARPlane) ─────
        // Layout:
        //        SANDPAPER (-0.14, 0, 0.12)              TONGS (0.14, 0, 0.12)
        //        MAGNESIUM RIBBON (-0.12, 0, 0.0)       WATCH GLASS (0.12, 0, 0.0)
        //                        BUNSEN BURNER (0.0, 0, -0.10)
        public static readonly Vector3 AR_POS_SANDPAPER  = new Vector3(-0.140f, 0.000f,  0.120f);
        public static readonly Vector3 AR_POS_TONGS      = new Vector3( 0.140f, 0.003f,  0.120f);
        public static readonly Vector3 AR_POS_RIBBON     = new Vector3(-0.120f, 0.002f,  0.000f);
        public static readonly Vector3 AR_POS_WATCHGLASS = new Vector3( 0.120f, 0.001f,  0.000f);
        public static readonly Vector3 AR_POS_BURNER     = new Vector3( 0.000f, 0.000f, -0.100f);

        private bool _isARMode = false;
        public bool IsARMode => _isARMode;

        public void PositionEquipmentInAR(LabTag tag, Vector3 worldPos, Quaternion worldRot)
        {
            GameObject target = tag switch
            {
                LabTag.Burner     => _burner,
                LabTag.Ribbon     => _ribbon,
                LabTag.Tongs      => _tongs,
                LabTag.Sandpaper  => _sandpaper,
                LabTag.WatchGlass => _watchGlass,
                _ => null
            };

            if (target == null)
            {
                Debug.LogWarning($"[ARLab] PositionEquipmentInAR: no object for tag {tag}");
                return;
            }

            // ── 1. Make the object active so renderer bounds are valid ────────
            target.SetActive(true);

            // ── 2. Place at the hit position first so bounds are in world space
            target.transform.rotation = worldRot;
            target.transform.position = worldPos;

            // ── 3. Compute how far the pivot is above the bottom of the mesh ──
            //       This lifts the object so its base sits on the detected plane.
            float bottomCorrection = GetBottomBoundsOffset(target);

            // ── 4. Apply corrected world position ─────────────────────────────
            target.transform.position = worldPos + Vector3.up * bottomCorrection;

            Debug.Log($"[ARLab] PositionEquipmentInAR: {tag} → " +
                      $"worldPos={worldPos} bottomCorrection={bottomCorrection:F4} " +
                      $"finalPos={target.transform.position}");
        }

        public void PlaceAllEquipmentInAR(Pose anchorPose)
        {
            transform.position = anchorPose.position;
            Vector3 camFwd = _cam != null ? (_cam.transform.position - anchorPose.position) : Vector3.forward;
            camFwd.y = 0f;
            transform.rotation = camFwd.sqrMagnitude > 0.001f ? Quaternion.LookRotation(-camFwd.normalized, Vector3.up) : anchorPose.rotation;
            transform.localScale = Vector3.one * 0.90f;

            if (_burner != null)
            {
                _burner.transform.SetParent(transform, false);
                _burner.transform.localPosition = AR_POS_BURNER + Vector3.up * GetBottomBoundsOffset(_burner);
                _burner.transform.localRotation = Quaternion.identity;
                _burner.SetActive(true);
            }
            if (_sandpaper != null)
            {
                _sandpaper.transform.SetParent(transform, false);
                _sandpaper.transform.localPosition = AR_POS_SANDPAPER + Vector3.up * GetBottomBoundsOffset(_sandpaper);
                _sandpaper.transform.localRotation = Quaternion.identity;
                _sandpaper.SetActive(true);
            }
            if (_ribbon != null && !_tongsHoldingRibbon)
            {
                _ribbon.transform.SetParent(transform, false);
                _ribbon.transform.localPosition = AR_POS_RIBBON + Vector3.up * GetBottomBoundsOffset(_ribbon);
                _ribbon.transform.localRotation = Quaternion.identity;
                _ribbon.SetActive(true);
            }
            if (_tongs != null)
            {
                _tongs.transform.SetParent(transform, false);
                _tongs.transform.localPosition = AR_POS_TONGS + Vector3.up * GetBottomBoundsOffset(_tongs);
                _tongs.transform.localRotation = Quaternion.identity;
                _tongs.SetActive(true);
            }
            if (_watchGlass != null)
            {
                _watchGlass.transform.SetParent(transform, false);
                _watchGlass.transform.localPosition = AR_POS_WATCHGLASS + Vector3.up * GetBottomBoundsOffset(_watchGlass);
                _watchGlass.transform.localRotation = Quaternion.identity;
                _watchGlass.SetActive(true);
            }

            if (_table != null) _table.SetActive(false);
        }

        public static float GetBottomBoundsOffset(GameObject obj)
        {
            if (obj == null) return 0f;
            var renderers = obj.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                var col = obj.GetComponentInChildren<Collider>(true);
                if (col != null)
                {
                    float offset = obj.transform.position.y - col.bounds.min.y;
                    return Mathf.Max(0f, offset);
                }
                return 0f;
            }

            float minY = float.MaxValue;
            foreach (var r in renderers)
            {
                if (r.bounds.min.y < minY) minY = r.bounds.min.y;
            }
            float bOffset = obj.transform.position.y - minY;
            return Mathf.Max(0f, bOffset);
        }

        // ── UI References ──────────────────────────────────────
        private Canvas          _canvas;
        private GameObject      _introPanel, _hudPanel;
        private GameObject      _obsPanel, _eqPanel, _resultPanel, _safetyPanel, _aboutPanel, _resetConfirmDialog;
        private TMP_Text        _stepTitle, _stepDesc, _progressLabel;
        private TMP_Text        _stepNumberBadge;
        private readonly List<Image> _stepPillIndicators = new List<Image>();
        private TMP_Text        _feedbackText;
        private Button          _startBtn, _resetBtn, _safetyBtn;
        private Button          _obsBtn, _eqBtn, _resultBtn;
        private Button          _prevBtn, _nextBtn;
        private Coroutine       _feedbackCoroutine;

        // Cleaning progress bar
        private Image           _cleaningBarFill;
        private TMP_Text        _cleaningPctText;
        private GameObject      _cleaningBarRoot;

        // Burning status info panel
        private GameObject      _burningInfoPanel;
        private TMP_Text        _burnerStatusText, _ribbonStatusText, _observationText;
        private Image           _heatingBarFill;
        private TMP_Text        _heatingTimerText;

        // Equation panel (persistent bottom)
        private GameObject      _equationPanel;

        // Landscape HUD (floating cards at the edges; the middle of the screen stays clear for the lab)
        private const float     HudMargin = 40f;                 // canvas units on the 1920 x 1080 reference
        private RectTransform   _stepCardRT, _rightColumnRT;
        private GameObject      _stepPillRow;
        private TMP_Text        _stepToggleLabel;
        private bool            _stepCardCollapsed;
        private bool            _hudInAR;
        private readonly List<Image> _arTranslucent = new List<Image>();

        // Equipment label GameObjects
        private readonly List<GameObject> _equipLabels = new List<GameObject>();

        // ── Experiment State ──────────────────────────────────
        private LabState _state = LabState.Intro;
        private bool     _burnerOn;
        private bool     _tongsHoldingRibbon;
        private bool     _productCooled;
        private bool     _heatingOrBurning;
        private float    _cleaningProgress;
        private float    _cleaningAcc;
        private Vector3  _lastRibbonPos;

        // ── Drag & Layout Coordinates ─────────────────────────
        private GameObject _dragging;
        private Plane      _dragPlane;
        private Vector3    _dragOffset;
        private Transform  _ribbonOriginalParent;

        // ── Layout: clean lab-bench spacing — portrait 9:16 ──────────────
        //
        //  Screen-space map (camera at y=0.55, pitch=52°, fov=58°)
        //  +Z  = further into scene  → higher on screen
        //  -Z  = toward camera       → lower on screen
        //  +X  = screen-right        -X = screen-left
        //
        //   SANDPAPER (-0.115, +0.065)          WATCH GLASS (+0.120, +0.120)
        //
        //   RIBBON    (-0.110, -0.040)          TONGS (+0.115, -0.040)  ← right of the burner, never hidden behind it
        //
        //                       BUNSEN BURNER (+0.010, -0.115)  ← lower-centre
        //                                  🔥
        private readonly Vector3 POS_TABLE      = new Vector3( 0.000f, 0.000f,  0.000f);
        private readonly Vector3 POS_SANDPAPER  = new Vector3(-0.115f, 0.002f,  0.065f);
        private readonly Vector3 POS_RIBBON     = new Vector3(-0.110f, 0.004f, -0.040f);
        private readonly Vector3 POS_TONGS      = new Vector3( 0.115f, 0.006f, -0.040f);   // right of the burner: it used to sit behind it and could not be tapped
        private readonly Vector3 POS_WATCHGLASS = new Vector3( 0.120f, 0.002f,  0.120f);
        private readonly Vector3 POS_BURNER     = new Vector3( 0.010f, 0.000f, -0.115f);

        // ═════════════════════════════════════════════════════
        //  LIFECYCLE
        // ═════════════════════════════════════════════════════
        private void Awake()
        {
            ARVirtualLab.AppShell.LabOrientation.Landscape(this);
            BuildCameraAndLighting();
            CreateMaterials();
            BuildSceneEquipment();
            BuildBurnVFX();
            BuildSmokeVFX();
            BuildUI();
        }

        private void Start()
        {
            if (ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToAR)
            {
                ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToAR = false;
                SetState(LabState.Step1_SelectRibbon);
                ToggleUnifiedAR();
                Debug.Log("[MagnesiumLab] Direct AR launch initiated from App Shell");
            }
            else if (ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToExperiment)
            {
                ARVirtualLab.AppShell.AppNavigation.LaunchDirectlyToExperiment = false;
                SetState(LabState.Step1_SelectRibbon);
                Debug.Log("[MagnesiumLab] Direct experiment start initiated from App Shell");
            }
            else
            {
                SetState(LabState.Intro);
                Debug.Log("[MagnesiumLab] Experiment started in Intro state");
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
                BenchSurfaceY = 0f,
                BenchCenterZ = 0f,
                DefaultScale = 2.6f,        // its apparatus is much smaller than the other experiments
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
            switch (_state)
            {
                case LabState.Step1_SelectRibbon:
                case LabState.Step2_CleanRibbon:   t = _ribbon; break;
                case LabState.Step3_PickWithTongs:
                case LabState.Step5_MoveIntoFlame: t = _tongs;  break;
                case LabState.Step4_LightBurner:   t = _burner; break;
                case LabState.Step9_Collect:       t = _productCooled ? _tongs : null; break;
            }
            _hint.Target(t);
            _hint.SetVisible(_dragging == null && !_heatingOrBurning);
        }

        private void OnDestroy()
        {
            ARVirtualLab.AppShell.LabOrientation.Portrait();
        }

        private void Update()
        {
            UpdateHint();
            ARVirtualLab.UI.CameraFit.Apply(_cam, 48f);   // keep the same width of view on tall phones
            UpdateHudForAR();
            HandleInput();
            AnimateFlame();
        }

        /// <summary>In AR the cards become see-through and Reset/Safety step up to make room for the AR buttons.</summary>
        private void UpdateHudForAR()
        {
            bool inAR = ARVirtualLab.AR.ARExperimentMode.Active;
            if (inAR == _hudInAR) return;
            _hudInAR = inAR;
            foreach (var img in _arTranslucent)
            {
                if (img == null) continue;
                var c = img.color; c.a = inAR ? 0.86f : 1f; img.color = c;
            }
            if (_rightColumnRT != null) _rightColumnRT.anchoredPosition = new Vector2(-HudMargin, inAR ? 32f + 96f : 32f);
        }

        private void AnimateFlame()
        {
            if (_burnerOn && _flameVFX != null && _flameVFX.activeSelf)
            {
                float t = Time.time * 8.5f;
                float noise = Mathf.PerlinNoise(t, 0.5f);
                float noiseX = Mathf.PerlinNoise(t * 1.3f, 2.1f) - 0.5f;
                float noiseZ = Mathf.PerlinNoise(t * 1.1f, 4.7f) - 0.5f;

                float scaleY = Mathf.Lerp(0.88f, 1.14f, noise);
                float scaleXZ = Mathf.Lerp(0.92f, 1.08f, 1f - noise);

                if (_outerFlameMeshGO != null)
                {
                    _outerFlameMeshGO.transform.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);
                    _outerFlameMeshGO.transform.localRotation = Quaternion.Euler(noiseX * 7f, 0f, noiseZ * 7f);
                }
                if (_innerFlameMeshGO != null)
                {
                    _innerFlameMeshGO.transform.localScale = new Vector3(scaleXZ * 0.95f, scaleY * 1.02f, scaleXZ * 0.95f);
                    _innerFlameMeshGO.transform.localRotation = Quaternion.Euler(noiseX * 5f, 0f, noiseZ * 5f);
                }
                if (_flameLight != null)
                    _flameLight.intensity = Mathf.Lerp(1.6f, 2.4f, noise);
            }
        }

        // ═════════════════════════════════════════════════════
        //  CAMERA & LIGHTING — Portrait Android 9:16
        //  Framed to show complete equipment spread clearly
        // ═════════════════════════════════════════════════════
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
            // Clear, uncropped portrait framing - zoomed out for complete laboratory view with comfortable margins
            _cam.transform.position = new Vector3(0f, 1.05f, -0.84f);
            _cam.transform.rotation = Quaternion.Euler(51f, 0f, 0f);
            _cam.fieldOfView        = 48f;
            _cam.backgroundColor    = ARVirtualLab.UI.EduTheme.Dark
                ? new Color(0.13f, 0.17f, 0.20f, 1f)
                : new Color(0.70f, 0.77f, 0.80f, 1f);   // a softer, less glaring backdrop than the other labs
            _cam.clearFlags         = CameraClearFlags.SolidColor;
            _cam.nearClipPlane      = 0.01f;
            _cam.farClipPlane       = 100f;

            RenderSettings.ambientMode  = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.70f, 1f);

            // Key Light
            var keyGO   = new GameObject("KeyLight");
            var keyL    = keyGO.AddComponent<Light>();
            keyL.type   = LightType.Directional;
            keyL.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            keyL.color  = new Color(1f, 0.98f, 0.94f);
            keyL.intensity = 1.4f;

            // Fill Light
            var fillGO  = new GameObject("FillLight");
            var fillL   = fillGO.AddComponent<Light>();
            fillL.type  = LightType.Directional;
            fillL.transform.rotation = Quaternion.Euler(35f, 155f, 0f);
            fillL.color = new Color(0.82f, 0.90f, 1f);
            fillL.intensity = 0.85f;

            // Rim / Overhead
            var rimGO   = new GameObject("RimLight");
            var rimL    = rimGO.AddComponent<Light>();
            rimL.type   = LightType.Directional;
            rimL.transform.rotation = Quaternion.Euler(85f, 0f, 0f);
            rimL.color  = Color.white;
            rimL.intensity = 0.55f;

            // Dedicated Burner Soft Fill Light — illuminates metallic/brass details
            var burnerFillGO = new GameObject("BurnerFillLight");
            burnerFillGO.transform.position = new Vector3(0.010f, 0.35f, -0.32f);
            var bfLight = burnerFillGO.AddComponent<Light>();
            bfLight.type = LightType.Point;
            bfLight.color = new Color(1.0f, 0.96f, 0.90f);
            bfLight.intensity = 1.1f;
            bfLight.range = 0.75f;
        }

        // ═════════════════════════════════════════════════════
        //  PBR & URP-COMPATIBLE MATERIALS
        // ═════════════════════════════════════════════════════
        private void CreateMaterials()
        {
            Shader lit = GetValidUrpShader(false);

            // Table: grey laminate
            _tableMat     = CreatePbrMaterial(lit, new Color(0.68f, 0.72f, 0.76f, 1f), 0.05f, 0.30f);

            // Burner: classic laboratory warm brass
            _burnerMat    = CreatePbrMaterial(lit, new Color(0.82f, 0.70f, 0.38f, 1f), 0.65f, 0.55f);

            // Tongs: dark charcoal steel — matches reference (nearly black lab tongs)
            _tongsMat     = CreatePbrMaterial(lit, new Color(0.10f, 0.10f, 0.12f, 1f), 0.95f, 0.65f);

            // Ribbon: bright silver metallic
            _ribbonMat    = CreatePbrMaterial(lit, new Color(0.80f, 0.83f, 0.88f, 1f), 0.92f, 0.60f);

            // Sandpaper: warm brown/orange abrasive
            _sandpaperMat = CreatePbrMaterial(lit, new Color(0.68f, 0.40f, 0.14f, 1f), 0.02f, 0.12f);

            // Watch glass: highly transparent blue-tinted glass
            _glassMat = CreatePbrMaterial(lit, new Color(0.82f, 0.92f, 1.0f, 0.18f), 0.02f, 0.98f);
            _glassMat.SetFloat("_Surface", 1f);
            _glassMat.SetFloat("_Blend",   0f);
            _glassMat.SetInt("_SrcBlend",  (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            _glassMat.SetInt("_DstBlend",  (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            _glassMat.SetInt("_ZWrite",    0);
            _glassMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            _glassMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            // MgO: white powdery ash
            _mgoMat = CreatePbrMaterial(lit, new Color(0.97f, 0.97f, 0.99f, 1f), 0.0f, 0.08f);

            // Outer flame: Red-orange translucent mesh
            _outerFlameMat = CreateFlameMeshMaterial(
                new Color(1.00f, 0.22f, 0.01f, 0.78f),
                new Color(1.00f, 0.35f, 0.02f) * 2.8f);

            // Inner flame: bright yellow-orange core
            _innerFlameMat = CreateFlameMeshMaterial(
                new Color(1.00f, 0.82f, 0.10f, 0.92f),
                new Color(1.00f, 0.78f, 0.08f) * 3.5f);
        }

        private static Shader GetValidUrpShader(bool forParticles = false)
        {
            Shader s = null;
            if (forParticles)
            {
                // For particles: Unlit is safest — guaranteed in URP and Standard builds
                s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (s == null) s = Shader.Find("Particles/Standard Unlit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
                if (s == null) s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("Unlit/Color");
                if (s == null) s = Shader.Find("Mobile/Particles/Additive");
                if (s == null) s = Shader.Find("Standard");
            }
            else
            {
                s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Simple Lit");
                if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
                if (s == null) s = Shader.Find("Mobile/Diffuse");
                if (s == null) s = Shader.Find("Unlit/Color");
                if (s == null) s = Shader.Find("Sprites/Default");
                if (s == null) s = Shader.Find("Standard");
            }
            return s;
        }

        // Dedicated unlit-type shader for flame/glow — never causes magenta
        private static Shader GetFlameShader()
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (s == null) s = Shader.Find("Particles/Standard Unlit");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null) s = Shader.Find("Mobile/Particles/Additive");
            if (s == null) s = Shader.Find("Sprites/Default");
            if (s == null) s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Unlit/Color");
            if (s == null) s = Shader.Find("Standard");
            return s;
        }

        private static Material CreatePbrMaterial(Shader shader, Color col, float metallic, float smoothness)
        {
            var mat = new Material(shader);
            mat.color = col;
            if (mat.HasProperty("_BaseColor"))  mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color"))      mat.SetColor("_Color",     col);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic",  metallic);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        private static Material CreateFlameMeshMaterial(Color col, Color emissionCol)
        {
            // Use unlit shader to guarantee orange/red on Android (no magenta)
            Shader s = GetFlameShader();
            var mat  = new Material(s);
            mat.name = "FlameMeshMat";
            mat.color = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     col);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", col);

            // Transparent alpha-blend, double-sided
            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend"))   mat.SetFloat("_Blend",   0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite",   0);
            mat.SetInt("_Cull",     (int)UnityEngine.Rendering.CullMode.Off);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            // Only enable emission if shader explicitly supports it (URP Lit has _Metallic; Unlit doesn't)
            if (mat.HasProperty("_EmissionColor") && mat.HasProperty("_Metallic"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionCol);
            }
            return mat;
        }

        private static Material CreateParticleMaterial(Color col, bool additive = true)
        {
            Shader s   = GetValidUrpShader(true);
            var mat    = new Material(s);
            mat.name   = "ParticleMat";
            mat.color  = col;
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", col);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     col);
            if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", col);

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (additive)
            {
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 1f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            }
            else
            {
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            }
            mat.SetInt("_ZWrite", 0);
            mat.SetInt("_Cull",   (int)UnityEngine.Rendering.CullMode.Off);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            var tex = GetSoftCircleTexture();
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);

            // Safe emission — only if shader supports it
            if (mat.HasProperty("_EmissionColor") && mat.HasProperty("_Metallic"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", col * 1.5f);
            }
            return mat;
        }

        private static Texture2D _softCircleTex;
        private static Texture2D GetSoftCircleTexture()
        {
            if (_softCircleTex != null) return _softCircleTex;
            const int size = 64;
            _softCircleTex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            _softCircleTex.wrapMode = TextureWrapMode.Clamp;
            var center    = new Vector2(size * 0.5f, size * 0.5f);
            float maxR    = size * 0.5f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center) / maxR;
                    float a = Mathf.Clamp01(1f - d);
                    a = a * a;
                    _softCircleTex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            _softCircleTex.Apply();
            return _softCircleTex;
        }

        // ═════════════════════════════════════════════════════
        //  SCENE EQUIPMENT — positions match reference layout
        // ═════════════════════════════════════════════════════
        private void BuildSceneEquipment()
        {
            // 1. Table
            _table = CreateObjectWithMesh("LaboratoryTable", transform, POS_TABLE,
                LoadObjMesh("LaboratoryTable"), _tableMat);

            // 2. Sandpaper Pad (Far Left)
            _sandpaper = CreateObjectWithMesh("Sandpaper", transform, POS_SANDPAPER,
                LoadObjMesh("Sandpaper"), _sandpaperMat);
            AddInteractable(_sandpaper, LabTag.Sandpaper);
            var spCol    = _sandpaper.AddComponent<BoxCollider>();
            spCol.size   = new Vector3(0.075f, 0.020f, 0.075f);
            spCol.center = new Vector3(0f, 0.005f, 0f);

            // 3. Magnesium Ribbon (Centre-Left) ── Uses Meshy 3D Model
            _ribbon = new GameObject("MagnesiumRibbon");
            _ribbon.transform.SetParent(transform, false);
            _ribbon.transform.localPosition = POS_RIBBON;
            _ribbon.transform.localRotation = Quaternion.identity;
            _ribbonOriginalParent = _ribbon.transform.parent;
            AddInteractable(_ribbon, LabTag.Ribbon);

            GameObject ribbonModelPrefab = Resources.Load<GameObject>("Models/MagnesiumRibbonModel");
            if (ribbonModelPrefab != null)
            {
                GameObject ribbonModelGO = Instantiate(ribbonModelPrefab);
                ribbonModelGO.name = "MagnesiumRibbonModel";
                ribbonModelGO.transform.SetParent(_ribbon.transform, false);
                // Rotate -90 on Y so length aligns with Z axis (+Z = free tip, -Z = held end)
                ribbonModelGO.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                // Scale: ~0.055m length, realistic thin metallic ribbon
                ribbonModelGO.transform.localScale = new Vector3(0.0435f, 0.0525f, 0.060f);   // 1.5x: readable on a phone
                ribbonModelGO.transform.localPosition = new Vector3(0f, 0.001f, 0f);

                foreach (var c in ribbonModelGO.GetComponentsInChildren<Collider>(true))
                {
                    if (!c.isTrigger) Destroy(c);
                }
                EnhanceModelMaterials(ribbonModelGO, new Color(0.85f, 0.88f, 0.92f, 1f), 0.90f, 0.85f);
                _ribbonRenderer = ribbonModelGO.GetComponentInChildren<Renderer>(true);
            }
            else
            {
                GameObject fallbackGO = CreateObjectWithMesh("MagnesiumRibbonModel", _ribbon.transform,
                    Vector3.zero, LoadObjMesh("MagnesiumRibbon"), _ribbonMat);
                fallbackGO.transform.localScale = new Vector3(1.55f, 1.20f, 1.55f);
                _ribbonRenderer = fallbackGO.GetComponent<Renderer>();
            }

            if (_ribbonRenderer != null) _ribbonMat = _ribbonRenderer.material;

            // Interaction collider: slightly larger than visual for easy mobile tap/drag
            var rbCol = _ribbon.AddComponent<BoxCollider>();
            rbCol.size   = new Vector3(0.085f, 0.045f, 0.120f);
            rbCol.center = Vector3.zero;

            // 4. Bunsen Burner (Lower Centre) ── uses imported GLB visual
            //    Parent container holds collider + interactions + FlamePoint
            _burner = new GameObject("BunsenBurner");
            _burner.transform.SetParent(transform, false);
            _burner.transform.localPosition = POS_BURNER;
            AddInteractable(_burner, LabTag.Burner);

            const float TARGET_BURNER_HEIGHT = 0.230f;  // 23 cm
            float modelTopY = TARGET_BURNER_HEIGHT;

            GameObject burnerModelPrefab = Resources.Load<GameObject>("Models/BunsenBurnerModel");
            GameObject burnerModelGO = null;
            Vector3 nozzleCenterMeshLocal = new Vector3(-0.103f, 0.9512f, -0.130f);
            bool foundMeshNozzle = false;

            if (burnerModelPrefab != null)
            {
                burnerModelGO = Instantiate(burnerModelPrefab);
                burnerModelGO.name = "BunsenBurnerModel";
                burnerModelGO.transform.SetParent(_burner.transform, false);
                burnerModelGO.transform.localPosition = Vector3.zero;
                burnerModelGO.transform.localRotation = Quaternion.identity;
                burnerModelGO.transform.localScale    = Vector3.one;

                // Activate all renderers and remove non-trigger child colliders
                foreach (var r in burnerModelGO.GetComponentsInChildren<Renderer>(true))
                {
                    r.gameObject.SetActive(true);
                    var c = r.GetComponent<Collider>();
                    if (c != null && !c.isTrigger) UnityEngine.Object.Destroy(c);
                }

                // Enhance materials so brass/metal surfaces are clearly visible and realistic
                EnhanceBurnerModelMaterials(burnerModelGO);

                // Find exact nozzle rim center directly from actual mesh vertices
                var mf = burnerModelGO.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    var verts = mf.sharedMesh.vertices;
                    float maxY = float.MinValue;
                    for (int i = 0; i < verts.Length; i++)
                        if (verts[i].y > maxY) maxY = verts[i].y;

                    float cutoff = maxY - (mf.sharedMesh.bounds.size.y * 0.015f);
                    Vector3 sum = Vector3.zero;
                    int count = 0;
                    for (int i = 0; i < verts.Length; i++)
                    {
                        if (verts[i].y >= cutoff)
                        {
                            sum += verts[i];
                            count++;
                        }
                    }
                    if (count > 0)
                    {
                        Vector3 centroid = sum / count;
                        nozzleCenterMeshLocal = new Vector3(centroid.x, maxY, centroid.z);
                        foundMeshNozzle = true;
                    }
                }

                // ─── Auto-scale to TARGET_BURNER_HEIGHT ───────────────────────
                var bounds = new Bounds(burnerModelGO.transform.position, Vector3.zero);
                bool hasBounds = false;
                foreach (var r in burnerModelGO.GetComponentsInChildren<Renderer>(true))
                {
                    if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                    else bounds.Encapsulate(r.bounds);
                }

                if (hasBounds && bounds.size.y > 0.001f)
                {
                    float naturalHeight = bounds.size.y;
                    float scaleFactor   = TARGET_BURNER_HEIGHT / naturalHeight;
                    scaleFactor = Mathf.Clamp(scaleFactor, 0.01f, 100f);
                    burnerModelGO.transform.localScale = Vector3.one * scaleFactor;

                    // Place the base of the mesh flush with the table surface (Y = 0)
                    float bottomOffset = (bounds.min.y - burnerModelGO.transform.position.y) * scaleFactor;
                    burnerModelGO.transform.localPosition = new Vector3(0f, -bottomOffset, 0f);

                    modelTopY = (bounds.max.y - burnerModelGO.transform.position.y) * scaleFactor;

                    Debug.Log($"[MagnesiumLab] GLB auto-scaled: naturalH={naturalHeight:F4} scale={scaleFactor:F4} topY={modelTopY:F4} nozzleLocal={nozzleCenterMeshLocal:F4}");
                }
                else
                {
                    burnerModelGO.transform.localScale = Vector3.one * 1.0f;
                    modelTopY = TARGET_BURNER_HEIGHT;
                    Debug.LogWarning("[MagnesiumLab] GLB bounds empty - using scale=1. Open Unity Editor to reimport.");
                }

                Debug.Log("[MagnesiumLab] Meshy GLB BunsenBurner loaded successfully.");
            }
            else
            {
                Debug.LogWarning("[MagnesiumLab] BunsenBurnerModel not found in Resources - using procedural fallback. Run: AR Lab > Rebuild Bunsen Burner Prefab");
                burnerModelGO = CreateObjectWithMesh("BunsenBurnerModel", _burner.transform,
                    Vector3.zero, LoadObjMesh("BunsenBurner"), _burnerMat);
                modelTopY = TARGET_BURNER_HEIGHT;
            }

            // ─── Interaction Collider (CapsuleCollider on parent stand) ─────────
            var bCol    = _burner.AddComponent<CapsuleCollider>();
            bCol.radius = 0.055f;
            bCol.height = 0.245f;
            bCol.center = new Vector3(0f, 0.115f, 0f);
            bCol.direction = 1;  // Y axis

            // ─── FlamePoint - EXACTLY at center of top nozzle opening ─────────
            _flamePoint = new GameObject("FlamePoint").transform;
            if (burnerModelGO != null && foundMeshNozzle)
            {
                // Parent directly to burnerModelGO so scale/rotation/position track perfectly
                _flamePoint.SetParent(burnerModelGO.transform, false);
                _flamePoint.localPosition = nozzleCenterMeshLocal;
                _flamePoint.localRotation = Quaternion.identity;
                _flamePoint.localScale    = Vector3.one;
            }
            else
            {
                _flamePoint.SetParent(_burner.transform, false);
                _flamePoint.localPosition = new Vector3(0f, modelTopY, 0f);
                _flamePoint.localRotation = Quaternion.identity;
                _flamePoint.localScale    = Vector3.one;
            }

            // ─── FlameTrigger / FlameHeatZone ──────────────────────────────
            // Centered around the flame using FlamePoint as origin
            _flameHeatZone = new GameObject("FlameTrigger");
            _flameHeatZone.transform.SetParent(_flamePoint, false);
            _flameHeatZone.transform.localPosition = new Vector3(0f, 0.025f, 0f); // centered inside flame
            var hzCol    = _flameHeatZone.AddComponent<SphereCollider>();
            hzCol.radius = 0.060f;   // matches visible flame envelope
            hzCol.isTrigger = true;

            // ─── Flame VFX ─────────────────────────────────────────────────────
            _flameVFX = new GameObject("Flame");
            _flameVFX.transform.SetParent(_flamePoint, false);
            _flameVFX.transform.localPosition = Vector3.zero;
            _flameVFX.transform.localRotation = Quaternion.identity;
            _flameVFX.transform.localScale    = Vector3.one;
            BuildFlameVFX(_flameVFX);
            _flameVFX.SetActive(false);

            // 5. Crucible Tongs (Upper Centre) ── Uses Meshy 3D Model
            _tongs = new GameObject("CrucibleTongs");
            _tongs.transform.SetParent(transform, false);
            _tongs.transform.localPosition = POS_TONGS;
            _tongs.transform.localRotation = Quaternion.identity;
            AddInteractable(_tongs, LabTag.Tongs);

            GameObject tongsModelPrefab = Resources.Load<GameObject>("Models/CrucibleTongsModel");
            if (tongsModelPrefab != null)
            {
                GameObject tongsModelGO = Instantiate(tongsModelPrefab);
                tongsModelGO.name = "CrucibleTongsModel";
                tongsModelGO.transform.SetParent(_tongs.transform, false);
                // Rotate -90 on Y so +X (tips) points to +Z, -X (handles) points to -Z
                tongsModelGO.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
                // Scale: realistic lab tongs length ~0.20m (20 cm)
                float tongsScale = 0.20f / 1.9037f; // ~0.105f
                tongsModelGO.transform.localScale = Vector3.one * tongsScale;
                tongsModelGO.transform.localPosition = new Vector3(0f, 0.003f, 0f);

                foreach (var c in tongsModelGO.GetComponentsInChildren<Collider>(true))
                {
                    if (!c.isTrigger) Destroy(c);
                }
                EnhanceModelMaterials(tongsModelGO, new Color(0.25f, 0.26f, 0.28f, 1f), 0.85f, 0.65f);
            }
            else
            {
                GameObject fallbackGO = CreateObjectWithMesh("CrucibleTongsModel", _tongs.transform,
                    Vector3.zero, LoadObjMesh("CrucibleTongs"), _tongsMat);
                fallbackGO.transform.localScale = new Vector3(1.20f, 1.20f, 1.20f);
            }

            var tCol = _tongs.AddComponent<BoxCollider>();
            tCol.size   = new Vector3(0.080f, 0.040f, 0.220f);
            tCol.center = new Vector3(0f, 0.005f, 0f);

            // GripPoint at jaw tip (+Z = +0.098m)
            _gripPoint = new GameObject("GripPoint").transform;
            _gripPoint.SetParent(_tongs.transform, false);
            _gripPoint.localPosition = new Vector3(0f, 0.003f, 0.098f);

            // 6. Watch Glass (Far Right) ── Uses Meshy 3D Model
            _watchGlass = new GameObject("WatchGlass");
            _watchGlass.transform.SetParent(transform, false);
            _watchGlass.transform.localPosition = POS_WATCHGLASS;
            _watchGlass.transform.localRotation = Quaternion.identity;
            AddInteractable(_watchGlass, LabTag.WatchGlass);

            GameObject watchGlassModelPrefab = Resources.Load<GameObject>("Models/WatchGlassModel");
            if (watchGlassModelPrefab != null)
            {
                GameObject watchGlassModelGO = Instantiate(watchGlassModelPrefab);
                watchGlassModelGO.name = "WatchGlassModel";
                watchGlassModelGO.transform.SetParent(_watchGlass.transform, false);
                // Scale: realistic watch glass diameter ~0.095m (9.5 cm)
                float wgScale = 0.095f / 1.9025f; // ~0.050f
                watchGlassModelGO.transform.localScale = Vector3.one * wgScale;
                watchGlassModelGO.transform.localPosition = new Vector3(0f, 0.001f, 0f);
                watchGlassModelGO.transform.localRotation = Quaternion.identity;

                foreach (var c in watchGlassModelGO.GetComponentsInChildren<Collider>(true))
                {
                    if (!c.isTrigger) Destroy(c);
                }
                EnhanceModelMaterials(watchGlassModelGO, new Color(0.92f, 0.96f, 1.0f, 0.40f), 0.1f, 0.95f, true);
            }
            else
            {
                GameObject fallbackGO = CreateObjectWithMesh("WatchGlassModel", _watchGlass.transform,
                    Vector3.zero, LoadObjMesh("WatchGlass"), _glassMat);
            }

            var wgCol = _watchGlass.AddComponent<SphereCollider>();
            wgCol.radius = 0.055f;
            wgCol.center = new Vector3(0f, 0.008f, 0f);

            // CollectionPoint inside dish
            _collectionPoint = new GameObject("CollectionPoint").transform;
            _collectionPoint.SetParent(_watchGlass.transform, false);
            _collectionPoint.localPosition = new Vector3(0f, 0.004f, 0f);

            // CollectionZone ── forgiving trigger
            _collectionZone = new GameObject("CollectionZone");
            _collectionZone.transform.SetParent(_watchGlass.transform, false);
            _collectionZone.transform.localPosition = new Vector3(0f, 0.012f, 0f);
            var czCol = _collectionZone.AddComponent<SphereCollider>();
            czCol.radius = 0.095f;
            czCol.isTrigger = true;

            // MgO Powder heap (hidden initially)
            _mgoPowder = new GameObject("MgOPowder");
            _mgoPowder.transform.SetParent(_collectionPoint, false);
            _mgoPowder.transform.localPosition = Vector3.zero;
            {
                // A squashed sphere: the imported heap mesh is only a few millimetres tall and was never visible.
                var heap = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                heap.name = "MgOHeap";
                Destroy(heap.GetComponent<Collider>());
                heap.transform.SetParent(_mgoPowder.transform, false);
                heap.transform.localPosition = new Vector3(0f, 0.003f, 0f);
                heap.transform.localScale = new Vector3(0.050f, 0.018f, 0.050f);
                heap.GetComponent<Renderer>().sharedMaterial = _mgoMat;
            }
            _mgoPowder.SetActive(false);

            Debug.Log("[MagnesiumLab] Scene equipment built");
        }

        // ── Burn VFX: Sparks + Burn Light ────────────────────
                private void BuildBurnVFX()
        {
            var psGO = new GameObject("BurnParticles");
            Transform parentTarget = _ribbon != null ? _ribbon.transform : (_gripPoint != null ? _gripPoint : transform);
            psGO.transform.SetParent(parentTarget, false);
            psGO.transform.localPosition = new Vector3(0f, 0f, 0.0275f);
            _sparkPS = psGO.AddComponent<ParticleSystem>();

            var psr  = psGO.GetComponent<ParticleSystemRenderer>();
            var pMat = CreateParticleMaterial(new Color(1f, 0.98f, 0.90f, 1f), true);
            psr.material = psr.sharedMaterial = pMat;

            var main = _sparkPS.main;

            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.playOnAwake     = false;
            main.duration        = 4f;
            main.loop            = true;
            main.startLifetime   = 0.45f;
            main.startSpeed      = 0.65f;
            main.startSize       = 0.005f;
            main.startColor      = new Color(1f, 0.98f, 0.90f);
            main.maxParticles    = 120;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _sparkPS.emission;
            emission.rateOverTime = 90f;

            var shape       = _sparkPS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 35f;
            shape.radius    = 0.006f;

            var colorOL = _sparkPS.colorOverLifetime;
            colorOL.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.7f, 0.2f), 0.5f),
                    new GradientColorKey(new Color(0.5f, 0.5f, 0.5f), 1f)
                },
                new[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.8f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = grad;
            _sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            // Burn Point Light (intense white during combustion)
            var lightGO  = new GameObject("BurnLight");
            lightGO.transform.SetParent(_flamePoint != null ? _flamePoint : transform, false);
            _burnLight       = lightGO.AddComponent<Light>();
            _burnLight.type  = LightType.Point;
            _burnLight.color = new Color(1f, 0.98f, 0.95f);
            _burnLight.intensity = 0f;
            _burnLight.range = 0.7f;
        }

        // ── White Smoke Particle System ───────────────────────
                private void BuildSmokeVFX()
        {
            var smokeGO = new GameObject("SmokeParticles");
            Transform parentTarget = _ribbon != null ? _ribbon.transform : (_gripPoint != null ? _gripPoint : transform);
            smokeGO.transform.SetParent(parentTarget, false);
            smokeGO.transform.localPosition = new Vector3(0f, 0.015f, 0.0275f);

            _smokePS = smokeGO.AddComponent<ParticleSystem>();

            var smokePsr  = smokeGO.GetComponent<ParticleSystemRenderer>();
            var smokeMat  = CreateParticleMaterial(new Color(0.90f, 0.90f, 0.92f, 0.55f), false);
            smokePsr.material = smokePsr.sharedMaterial = smokeMat;
            smokePsr.sortingOrder = 1;

            var main = _smokePS.main;

            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.playOnAwake     = false;
            main.duration        = 99f;
            main.loop            = true;
            main.startLifetime   = new ParticleSystem.MinMaxCurve(2.0f, 3.5f);
            main.startSpeed      = new ParticleSystem.MinMaxCurve(0.025f, 0.06f);
            main.startSize       = new ParticleSystem.MinMaxCurve(0.010f, 0.022f);
            main.startColor      = new ParticleSystem.MinMaxGradient(
                new Color(0.85f, 0.85f, 0.88f, 0.35f),
                new Color(0.95f, 0.95f, 0.97f, 0.50f));
            main.maxParticles    = 70;
            main.gravityModifier = -0.04f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = _smokePS.emission;
            emission.rateOverTime = 18f;

            var shape       = _smokePS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle     = 12f;
            shape.radius    = 0.004f;

            var colorOL = _smokePS.colorOverLifetime;
            colorOL.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(
                new[] {
                    new GradientColorKey(new Color(0.9f, 0.9f, 0.92f), 0f),
                    new GradientColorKey(new Color(0.85f, 0.85f, 0.87f), 0.5f),
                    new GradientColorKey(new Color(0.8f, 0.8f, 0.82f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.55f, 0.25f),
                    new GradientAlphaKey(0.40f, 0.65f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            colorOL.color = sg;

            var sizeOL = _smokePS.sizeOverLifetime;
            sizeOL.enabled = true;
            var sizeCurve = new AnimationCurve(
                new Keyframe(0f, 0.5f),
                new Keyframe(0.4f, 1.2f),
                new Keyframe(1f, 2.2f));
            sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            // Uniform TwoConstants mode across all axes to prevent velocity curve mode error
            var vel = _smokePS.velocityOverLifetime;
            vel.enabled = true;
            vel.space   = ParticleSystemSimulationSpace.Local;
            vel.x       = new ParticleSystem.MinMaxCurve(-0.008f, 0.008f);
            vel.y       = new ParticleSystem.MinMaxCurve(0.015f, 0.045f);
            vel.z       = new ParticleSystem.MinMaxCurve(-0.005f, 0.005f);

            _smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ═════════════════════════════════════════════════════
        //  GUARANTEED URP RED / ORANGE / YELLOW BUNSEN FLAME
        // ═════════════════════════════════════════════════════
        private void BuildFlameVFX(GameObject parent)
        {
            // 1. Outer translucent red/orange tapered flame cone mesh (scaled another +18%: 0.091m height)
            Mesh outerMesh = CreateTaperedFlameMesh(0.091f, 0.0091f, 0.0181f, 16);
            _outerFlameMeshGO = CreateObjectWithMesh("OuterFlameMesh", parent.transform,
                Vector3.zero, outerMesh, _outerFlameMat);

            // 2. Inner glowing yellow/orange core (scaled another +18%: 0.055m height)
            Mesh innerMesh = CreateTaperedFlameMesh(0.055f, 0.0048f, 0.0098f, 16);
            _innerFlameMeshGO = CreateObjectWithMesh("InnerFlameMesh", parent.transform,
                Vector3.zero, innerMesh, _innerFlameMat);

            // 3. Base dark-orange crimson emitter (nozzle base)
            var baseGO = new GameObject("BaseFlameParticles");
            baseGO.transform.SetParent(parent.transform, false);
            baseGO.transform.localPosition = Vector3.zero;
            _baseFlamePS = baseGO.AddComponent<ParticleSystem>();
            var bpsr  = baseGO.GetComponent<ParticleSystemRenderer>();
            var bpMat = CreateParticleMaterial(new Color(1.00f, 0.18f, 0.01f, 0.9f), true);
            bpsr.material = bpsr.sharedMaterial = bpMat;

            var baseMain = _baseFlamePS.main;
            baseMain.playOnAwake = false;
            baseMain.duration    = 99f; baseMain.loop = true;
            baseMain.startLifetime = 0.19f;
            baseMain.startSpeed  = 0.10f;
            baseMain.startSize   = 0.0195f;
            baseMain.maxParticles = 28;
            baseMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            var baseEm = _baseFlamePS.emission; baseEm.rateOverTime = 28f;
            var bShape  = _baseFlamePS.shape;
            bShape.shapeType = ParticleSystemShapeType.Cone;
            bShape.angle = 5f; bShape.radius = 0.0041f;

            // 4. Orange / warm-yellow main plume (scaled another +18%)
            _mainFlamePS = parent.AddComponent<ParticleSystem>();
            var mpsr  = parent.GetComponent<ParticleSystemRenderer>();
            var mpMat = CreateParticleMaterial(new Color(1.00f, 0.55f, 0.03f, 0.85f), true);
            mpsr.material = mpsr.sharedMaterial = mpMat;

            var main = _mainFlamePS.main;

            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.duration    = 99f; main.loop = true;
            main.startLifetime = 0.28f;
            main.startSpeed  = 0.13f;
            main.startSize   = 0.028f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = _mainFlamePS.emission; emission.rateOverTime = 35f;
            var shape    = _mainFlamePS.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 6f; shape.radius = 0.0048f;

            var colorOL = _mainFlamePS.colorOverLifetime;
            colorOL.enabled = true;
            var fg = new Gradient();
            fg.SetKeys(
                new[] {
                    new GradientColorKey(new Color(1.00f, 0.20f, 0.01f), 0f),
                    new GradientColorKey(new Color(1.00f, 0.60f, 0.04f), 0.35f),
                    new GradientColorKey(new Color(1.00f, 0.88f, 0.15f), 0.65f),
                    new GradientColorKey(new Color(0.85f, 0.25f, 0.02f), 1f)
                },
                new[] {
                    new GradientAlphaKey(0.0f, 0f),
                    new GradientAlphaKey(0.9f, 0.15f),
                    new GradientAlphaKey(0.8f, 0.60f),
                    new GradientAlphaKey(0.0f, 1.0f)
                }
            );
            colorOL.color = fg;

            // 5. Flame Point Light (warm amber)
            var lGO = new GameObject("FlamePointLight");
            lGO.transform.SetParent(parent.transform, false);
            _flameLight       = lGO.AddComponent<Light>();
            _flameLight.type  = LightType.Point;
            _flameLight.color = new Color(1.0f, 0.50f, 0.12f);
            _flameLight.intensity = 2.0f;
            _flameLight.range = 0.45f;
        }

        private static void EnhanceBurnerModelMaterials(GameObject burnerModelGO)
        {
            EnhanceModelMaterials(burnerModelGO, new Color(0.84f, 0.72f, 0.40f, 1f), 0.65f, 0.60f, false);
        }

        private static void EnhanceModelMaterials(GameObject modelGO, Color defaultColor, float metallic = 0.5f, float smoothness = 0.6f, bool transparent = false)
        {
            if (modelGO == null) return;
            Shader litShader = GetValidUrpShader(transparent);
            foreach (var r in modelGO.GetComponentsInChildren<Renderer>(true))
            {
                r.gameObject.SetActive(true);
                var mats = r.materials;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;

                    if (m.shader == null || m.shader.name.Contains("Error") || !m.shader.isSupported)
                    {
                        m.shader = litShader;
                    }

                    Texture tex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : null;
                    if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");

                    if (tex != null)
                    {
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
                        if (m.HasProperty("_Color"))     m.SetColor("_Color",     Color.white);
                        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic",   metallic);
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                    }
                    else
                    {
                        m.color = defaultColor;
                        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", defaultColor);
                        if (m.HasProperty("_Color"))     m.SetColor("_Color",     defaultColor);
                        if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic",   metallic);
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smoothness);
                    }
                }
            }
        }

        private Vector3 GetRibbonTipPosition()
        {
            if (_ribbon == null) return _gripPoint != null ? _gripPoint.position : (_tongs != null ? _tongs.transform.position : Vector3.zero);
            // The free ribbon tip extends forward (+Z in ribbon local space) ~2.75cm from the ribbon center
            return _ribbon.transform.TransformPoint(new Vector3(0f, 0f, 0.0275f));
        }

        private static Mesh CreateTaperedFlameMesh(float height, float baseR, float midR, int segments)
        {
            var rings = new[]
            {
                new Vector2(0.000f * height, baseR),
                new Vector2(0.200f * height, midR),
                new Vector2(0.550f * height, midR * 0.75f),
                new Vector2(0.850f * height, midR * 0.35f),
                new Vector2(1.000f * height, 0.0f)
            };

            var verts   = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs     = new List<Vector2>();

            for (int rIdx = 0; rIdx < rings.Length; rIdx++)
            {
                float h = rings[rIdx].x, r = rings[rIdx].y;
                if (r == 0f)
                {
                    verts.Add(new Vector3(0f, h, 0f));
                    normals.Add(Vector3.up);
                    uvs.Add(new Vector2(0.5f, 1f));
                }
                else
                {
                    for (int i = 0; i < segments; i++)
                    {
                        float ang = i * 2f * Mathf.PI / segments;
                        float x   = r * Mathf.Cos(ang), z = r * Mathf.Sin(ang);
                        verts.Add(new Vector3(x, h, z));
                        normals.Add(new Vector3(x, 0.2f, z).normalized);
                        uvs.Add(new Vector2((float)i / segments, h / height));
                    }
                }
            }

            var tris     = new List<int>();
            int numRings = rings.Length;
            for (int rIdx = 0; rIdx < numRings - 2; rIdx++)
            {
                int sA = rIdx * segments, sB = (rIdx + 1) * segments;
                for (int i = 0; i < segments; i++)
                {
                    int ni = (i + 1) % segments;
                    int a1 = sA + i, a2 = sA + ni, b1 = sB + i, b2 = sB + ni;
                    tris.Add(a1); tris.Add(b1); tris.Add(a2);
                    tris.Add(a2); tris.Add(b1); tris.Add(b2);
                }
            }
            int tip = verts.Count - 1, lrs = (numRings - 2) * segments;
            for (int i = 0; i < segments; i++)
            {
                int ni = (i + 1) % segments;
                tris.Add(lrs + i); tris.Add(tip); tris.Add(lrs + ni);
            }

            var mesh = new Mesh();
            mesh.name = "TaperedFlame";
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        // ═════════════════════════════════════════════════════
        //  INPUT HANDLING
        // ═════════════════════════════════════════════════════
        private void HandleInput()
        {
            if (ARVirtualLab.AR.ARExperimentMode.BlockSceneInput) return;    // AR: lab not placed yet
            Vector3 screenPos = Vector3.zero;
            bool down = false, held = false, up = false;

            if (Input.touchCount > 0)
            {
                var t     = Input.GetTouch(0);
                screenPos = t.position;
                down = t.phase == TouchPhase.Began;
                held = t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary;
                up   = t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled;
            }
            else
            {
                screenPos = Input.mousePosition;
                down = Input.GetMouseButtonDown(0);
                held = Input.GetMouseButton(0) && !Input.GetMouseButtonDown(0);
                up   = Input.GetMouseButtonUp(0);
            }

            if (down && IsPointerOverBlockingUI(screenPos)) return;
            if (down) OnPointerDown(screenPos);
            if (held) OnPointerHeld(screenPos);
            if (up)   OnPointerUp();
        }

        private bool IsPointerOverBlockingUI(Vector3 screen)
        {
            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null) return false;
            var pd  = new UnityEngine.EventSystems.PointerEventData(
                UnityEngine.EventSystems.EventSystem.current) { position = screen };
            var res = new List<UnityEngine.EventSystems.RaycastResult>();
            UnityEngine.EventSystems.EventSystem.current.RaycastAll(pd, res);
            foreach (var r in res)
            {
                if (r.gameObject == null) continue;
                if (r.gameObject.GetComponentInParent<Button>() != null) return true;
                if (r.gameObject.name.EndsWith("_Overlay") || r.gameObject.name == "IntroPanel") return true;
            }
            return false;
        }

        private void OnPointerDown(Vector3 screen)
        {
            var ray = _cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            if (hits.Length == 0) return;
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            LabInteractable li = null;
            foreach (var h in hits)
            {
                var cand = h.collider.GetComponentInParent<LabInteractable>();
                if (cand != null) { li = cand; break; }
            }
            if (li == null) return;

            // The tall burner can hide the tongs from the camera. When the step is about the tongs, a tap that also
            // passes through the tongs picks the tongs instead of toggling the burner.
            bool tongsStep = _state == LabState.Step3_PickWithTongs || _state == LabState.Step5_MoveIntoFlame ||
                             (_state == LabState.Step9_Collect && _productCooled);
            if (tongsStep && li.tag == LabTag.Burner)
            {
                foreach (var h in hits)
                {
                    var cand = h.collider.GetComponentInParent<LabInteractable>();
                    if (cand != null && cand.tag == LabTag.Tongs) { li = cand; break; }
                }
            }

            switch (li.tag)
            {
                case LabTag.Ribbon:    OnRibbonTapped(li.gameObject, screen);    break;
                case LabTag.Tongs:     OnTongsTapped(li.gameObject, screen);     break;
                case LabTag.Burner:    OnBurnerTapped();                         break;
                case LabTag.Sandpaper: OnSandpaperTapped(li.gameObject, screen); break;
            }
        }

        private void OnPointerHeld(Vector3 screen)
        {
            if (_dragging == null || _heatingOrBurning) return;

            var ray = _cam.ScreenPointToRay(screen);
            if (!_dragPlane.Raycast(ray, out float enter)) return;

            Vector3 hitWorld = ray.GetPoint(enter);
            Vector3 targetWorld = hitWorld + _dragOffset;
            Vector3 local = transform.InverseTransformPoint(targetWorld);
            local.x = Mathf.Clamp(local.x, -0.18f, 0.18f);
            local.z = Mathf.Clamp(local.z, -0.16f, 0.16f);

            // Auto-elevate tongs+ribbon as they approach the burner flame.
            if (_dragging == _tongs && _tongsHoldingRibbon)
            {
                float burnerDistZ = Mathf.Clamp01((0.020f - local.z) / 0.135f);
                float targetY = Mathf.Lerp(0.006f, 0.235f, burnerDistZ * burnerDistZ);
                local.y = targetY;

                if (_flamePoint != null)
                {
                    Vector3 toFlame = (_flamePoint.position - _dragging.transform.position);
                    toFlame.y = 0f;
                    if (toFlame.sqrMagnitude > 0.001f)
                    {
                        Quaternion targetRot = Quaternion.LookRotation(toFlame.normalized, transform.up);
                        _tongs.transform.rotation = Quaternion.Slerp(_tongs.transform.rotation, targetRot, Time.deltaTime * 12f);
                    }
                }
            }
            else
            {
                local.y = _dragging.transform.localPosition.y;
            }

            _dragging.transform.localPosition = local;
            OnDragging();
        }

        private void OnPointerUp()
        {
            if (_dragging != null)
            {
                SetHighlight(_dragging, false);
                _dragging = null;
            }
        }

        // ── Tap Handlers ─────────────────────────────────────
        private void OnRibbonTapped(GameObject obj, Vector3 screen)
        {
            Debug.Log("[ARLab] Ribbon selected");
            if (_tongsHoldingRibbon) { OnTongsTapped(_tongs, screen); return; }

            if (_state == LabState.Intro || _state == LabState.Step1_SelectRibbon)
            {
                SetState(LabState.Step2_CleanRibbon);
                ShowFeedback("Drag the ribbon back and forth on the sandpaper to clean it!");
                StartDragging(obj, screen);
            }
            else if (_state == LabState.Step2_CleanRibbon)
            {
                StartDragging(obj, screen);
            }
            else
            {
                StartDragging(obj, screen);
            }
        }

        private void OnTongsTapped(GameObject obj, Vector3 screen)
        {
            Debug.Log("[ARLab] Tongs selected");
            if (_heatingOrBurning) return;
            StartDragging(obj, screen);

            if (_state == LabState.Step3_PickWithTongs && !_tongsHoldingRibbon)
            {
                float dist = Vector3.Distance(_gripPoint.position, _ribbon.transform.position);
                if (dist < 0.115f) GripRibbonWithTongs();
                else               ShowFeedback("Drag the tongs toward the ribbon to grip it!");
            }
            else if (_state == LabState.Step2_CleanRibbon || _state == LabState.Step1_SelectRibbon)
            {
                ShowFeedback("Clean the ribbon with sandpaper first, then grip with tongs.");
            }
        }

        private void OnBurnerTapped()
        {
            if (_state == LabState.Step4_LightBurner || _state == LabState.Step5_MoveIntoFlame)
                ToggleBurner();
            else if (_state == LabState.Step3_PickWithTongs)
                ShowFeedback("First grip the cleaned ribbon with tongs!");
            else if (_state < LabState.Step4_LightBurner)
                ShowFeedback("Follow the steps — clean and grip the ribbon first.");
        }

        private void OnSandpaperTapped(GameObject obj, Vector3 screen)
        {
            if (_state == LabState.Step1_SelectRibbon || _state == LabState.Intro)
            {
                SetState(LabState.Step2_CleanRibbon);
                ShowFeedback("Drag the ribbon across the sandpaper to remove the oxide layer.");
            }
        }

        // ── Drag & Step Progression ───────────────────────────
        private void StartDragging(GameObject obj, Vector3 screen)
        {
            _dragging = obj;
            SetHighlight(obj, true);
            _dragPlane = new Plane(transform.up, obj.transform.position);
            var ray = _cam.ScreenPointToRay(screen);
            _dragOffset = _dragPlane.Raycast(ray, out float enter)
                ? obj.transform.position - ray.GetPoint(enter)
                : Vector3.zero;
            _lastRibbonPos = _ribbon.transform.position;
        }

        private void OnDragging()
        {
            // Step 2: Clean ribbon on sandpaper
            if (_state == LabState.Step2_CleanRibbon && _dragging == _ribbon)
            {
                float dist = Vector3.Distance(
                    new Vector3(_ribbon.transform.position.x, 0, _ribbon.transform.position.z),
                    new Vector3(_sandpaper.transform.position.x, 0, _sandpaper.transform.position.z));

                if (dist < 0.085f)
                {
                    float moved = Vector3.Distance(_ribbon.transform.position, _lastRibbonPos);
                    if (moved > 0.0004f)   // only count real dragging motion
                    {
                        _cleaningAcc     += moved;
                        // 0.45f divisor → requires ~9-12 s of actual rubbing motion
                        _cleaningProgress = Mathf.Min(1f, _cleaningAcc / 0.45f);
                        int pct           = Mathf.RoundToInt(_cleaningProgress * 100f);

                        // Update progress bar fill
                        if (_cleaningBarFill != null)
                            _cleaningBarFill.fillAmount = _cleaningProgress;
                        if (_cleaningPctText != null)
                            _cleaningPctText.text = $"CLEANING: {pct}%";

                        SetStepInfo("Step 2: Clean Ribbon",
                            $"Drag the magnesium ribbon back and forth on the sandpaper\npad to remove the oxide layer.\nCleaning Progress: {pct}%", "2 / 9");

                        // Ribbon colour brightens as it cleans
                        if (_ribbonMat != null)
                        {
                            Color polished = Color.Lerp(
                                new Color(0.80f, 0.83f, 0.88f, 1f),
                                new Color(0.95f, 0.97f, 1.00f, 1f),
                                _cleaningProgress);
                            _ribbonMat.color = polished;
                            _ribbonMat.SetColor("_BaseColor", polished);
                            _ribbonMat.SetFloat("_Smoothness", Mathf.Lerp(0.60f, 0.96f, _cleaningProgress));
                        }

                        if (_cleaningProgress >= 1f) FinishCleaning();
                    }
                }
                _lastRibbonPos = _ribbon.transform.position;
            }

            // Tongs dragging close to ribbon → grip
            else if (_dragging == _tongs && !_tongsHoldingRibbon && _ribbon != null)
            {
                float dist = Vector3.Distance(_gripPoint.position, _ribbon.transform.position);
                if (dist < 0.100f) GripRibbonWithTongs();
            }

            // Step 5: Move tongs + ribbon into flame zone — detect ribbon TIP entering FlameTrigger
            else if (_state == LabState.Step5_MoveIntoFlame
                  && _dragging == _tongs && _burnerOn && _tongsHoldingRibbon)
            {
                Vector3 ribbonTip = GetRibbonTipPosition();
                Vector3 triggerCenter = _flameHeatZone != null
                    ? _flameHeatZone.transform.position
                    : (_flamePoint != null ? _flamePoint.position + Vector3.up * 0.045f : POS_BURNER + Vector3.up * 0.25f);

                float distToTrigger = Vector3.Distance(ribbonTip, triggerCenter);
                float distXZ = Vector2.Distance(
                    new Vector2(ribbonTip.x, ribbonTip.z),
                    new Vector2(triggerCenter.x, triggerCenter.z));
                float distY = Mathf.Abs(ribbonTip.y - triggerCenter.y);

                // Detected when the ribbon TIP enters FlameTrigger volume
                if (distToTrigger <= 0.085f || (distXZ <= 0.070f && distY <= 0.085f))
                {
                    _dragging = null;
                    SetHighlight(_tongs, false);
                    Debug.Log($"[MagnesiumLab] Ribbon TIP ({ribbonTip}) entered FlameTrigger! dist={distToTrigger:F3}m. Starting heating/burning...");
                    Debug.Log("[ARLab] Ribbon entered flame");
                    StartCoroutine(DoHeatingAndBurning());
                }
            }

            // Step 9: Collect MgO in watch glass
            else if (_state == LabState.Step9_Collect && _dragging == _tongs && _productCooled)
            {
                float distGripXZ = Vector2.Distance(
                    new Vector2(_gripPoint.position.x, _gripPoint.position.z),
                    new Vector2(_collectionPoint.position.x, _collectionPoint.position.z));
                float distGrip3D = Vector3.Distance(_gripPoint.position, _collectionPoint.position);
                float distTongsXZ = Vector2.Distance(
                    new Vector2(_tongs.transform.position.x, _tongs.transform.position.z),
                    new Vector2(_watchGlass.transform.position.x, _watchGlass.transform.position.z));

                if (distGripXZ < 0.090f || distGrip3D < 0.115f || distTongsXZ < 0.100f)
                {
                    _dragging = null;
                    SetHighlight(_tongs, false);
                    CompleteCollection();
                }
            }
        }

        // ═════════════════════════════════════════════════════
        //  EXPERIMENT ACTIONS
        // ═════════════════════════════════════════════════════
        private void FinishCleaning()
        {
            _dragging = null;
            SetHighlight(_ribbon, false);
            StartCoroutine(MoveWorldRoutine(_ribbon.transform, POS_RIBBON + transform.position, 0.5f));
            ShowFeedback("Ribbon cleaned! Drag the tongs to the ribbon to grip it.");
            SetState(LabState.Step3_PickWithTongs);
        }

        private void GripRibbonWithTongs()
        {
            if (_tongsHoldingRibbon && _ribbon.transform.parent == _gripPoint) return;
            _tongsHoldingRibbon = true;

            Vector3 gripStartPos = _ribbon.transform.position;
            Quaternion gripStartRot = _ribbon.transform.rotation;
            _ribbon.transform.SetParent(_gripPoint, true);            // keep the world pose, then glide into the jaws
            _ribbon.transform.localScale = Vector3.one * 0.90f;
            _ribbon.SetActive(true);
            StartCoroutine(SnapRibbonIntoJaws(gripStartPos, gripStartRot));

            var rbCol = _ribbon.GetComponent<Collider>();
            if (rbCol != null) rbCol.enabled = false;

            ShowFeedback("Magnesium ribbon gripped in tongs! Tap the Bunsen burner to ignite the flame.");
            SetState(LabState.Step4_LightBurner);
        }

        private void ToggleBurner()
        {
            _burnerOn = !_burnerOn;
            SetFlame(_burnerOn);
            if (_burnerOn) Debug.Log("[ARLab] Burner turned on");

            UpdateBurnerStatusUI();

            if (_burnerOn)
            {
                if (_tongsHoldingRibbon)
                {
                    ShowFeedback("Bunsen burner ON! Drag the tongs to place the ribbon into the flame.");
                    if (_state == LabState.Step4_LightBurner) SetState(LabState.Step5_MoveIntoFlame);
                }
                else
                {
                    ShowFeedback("Bunsen burner ON! Grip the cleaned ribbon with tongs first.");
                }
            }
            else
            {
                ShowFeedback("Bunsen burner OFF.");
                if (_state == LabState.Step5_MoveIntoFlame) SetState(LabState.Step4_LightBurner);
            }
        }

        // ── Heating & Burning Coroutine ───────────────────────
                private IEnumerator DoHeatingAndBurning()
        {
            // Strict safety guards against early burning
            if (_state != LabState.Step5_MoveIntoFlame) yield break;
            if (!_burnerOn || !_tongsHoldingRibbon || _cleaningProgress < 0.99f || _heatingOrBurning) yield break;

            _heatingOrBurning = true;
            Debug.Log("[ARLab] Burning started");
            SetState(LabState.Step6_Heating);

            // Show burning info panel
            if (_burningInfoPanel != null) _burningInfoPanel.SetActive(true);
            UpdateBurnerStatusUI();

            // Phase 1 — Heating: orange thermal glow on ribbon (2.5 s)
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / 2.5f;   // extended: was 2.0
                if (_ribbonMat != null)
                {
                    _ribbonMat.EnableKeyword("_EMISSION");
                    Color emColor = new Color(1f, 0.40f, 0.05f) * (t * 3.5f);
                    _ribbonMat.SetColor("_EmissionColor", emColor);
                }

                int pct = Mathf.RoundToInt(t * 100f);
                SetStepInfo("Step 6: Heat in Flame",
                    $"Hold the magnesium ribbon with tongs and place the\nribbon tip into the Bunsen burner flame.\nObserve the burning.", "6 / 9");

                // Update heating bar in info panel
                if (_heatingBarFill != null) _heatingBarFill.fillAmount = t;
                if (_heatingTimerText != null)
                    _heatingTimerText.text = t < 0.85f
                        ? "Keep the ribbon in the flame for a little longer..."
                        : "Almost there...";

                // Update ribbon/burner status
                if (_ribbonStatusText != null) _ribbonStatusText.text = "Ribbon: Heating...";
                yield return null;
            }

            // Phase 2 — Burning: intense white dazzling combustion (6 s)
            SetState(LabState.Step7_Burning);
            _sparkPS.Play();
            _smokePS.Play();
            if (_burnLight) _burnLight.enabled = true;

            if (_ribbonStatusText != null) _ribbonStatusText.text = "Ribbon: Burning...";
            if (_observationText  != null) _observationText.text  = "Observation: Bright white light with white smoke";

            // Show the MgO smoke callout annotation
            ShowMgOCallout(true);

            float elapsed = 0f;
            float burnDuration = 6.0f;  // extended from 3.5 s
            while (elapsed < burnDuration)
            {
                elapsed += Time.deltaTime;
                // the ribbon is used up from the free end while it burns
                float left = Mathf.Lerp(1f, 0.15f, elapsed / burnDuration);
                _ribbon.transform.localScale = new Vector3(0.90f, 0.90f, 0.90f * left);
                _ribbon.transform.localPosition = new Vector3(0f, 0f, 0.022f - 0.037f * (1f - left));
                float glow = 4.5f + Mathf.PingPong(elapsed * 14f, 5.5f);
                if (_burnLight) _burnLight.intensity = glow * 0.55f;   // was blinding on a phone screen
                if (_ribbonMat) _ribbonMat.SetColor("_EmissionColor", Color.white * glow * 0.6f);

                // Update progress bar during burning
                if (_heatingBarFill != null) _heatingBarFill.fillAmount = elapsed / burnDuration;
                if (_heatingTimerText != null)
                {
                    int secLeft = Mathf.CeilToInt(burnDuration - elapsed);
                    _heatingTimerText.text = secLeft > 1
                        ? $"Burning... ({secLeft}s remaining)"
                        : "Combustion complete!";
                }
                yield return null;
            }

            // ── Extinguish burning ──
            _sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _smokePS.Stop(false, ParticleSystemStopBehavior.StopEmitting); // let existing particles fade
            ShowMgOCallout(false);
            if (_burnLight) { _burnLight.intensity = 0f; _burnLight.enabled = false; }
            if (_ribbonMat) { _ribbonMat.DisableKeyword("_EMISSION"); _ribbonMat.SetColor("_EmissionColor", Color.black); }
            if (_burningInfoPanel != null) _burningInfoPanel.SetActive(false);

            // Ribbon transforms into white MgO ash on tongs
            _ribbon.SetActive(false);

            Mesh meshResidue = LoadObjMesh("MgOResidue");
            _mgoResidue = CreateObjectWithMesh("MgOResidue", _gripPoint, Vector3.zero, meshResidue, _mgoMat);

            SetState(LabState.Step8_Cooling);
            ShowFeedback("Magnesium oxide (MgO) ash formed! Allow to cool...");

            yield return new WaitForSeconds(2.5f);

            _productCooled    = true;
            _heatingOrBurning = false;
            SetState(LabState.Step9_Collect);
            ShowFeedback("Product cooled. Drag the tongs over the watch glass to collect the MgO ash.");
        }

        private void CompleteCollection()
        {
            StartCoroutine(CollectRoutine());
        }

        private static float Ease(float k) { k = Mathf.Clamp01(k); return k * k * (3f - 2f * k); }

        private IEnumerator MoveWorldRoutine(Transform t, Vector3 world, float dur)
        {
            Vector3 p0 = t.position;
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Ease(e / dur);
                Vector3 p = Vector3.Lerp(p0, world, k);
                p.y += Mathf.Sin(k * Mathf.PI) * 0.02f;               // small arc so it looks lifted, not slid
                t.position = p;
                yield return null;
            }
            t.position = world;
        }

        private IEnumerator SnapRibbonIntoJaws(Vector3 startPos, Quaternion startRot)
        {
            var rt = _ribbon.transform;
            float e = 0f, dur = 0.35f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Ease(e / dur);
                rt.position = Vector3.Lerp(startPos, _gripPoint.TransformPoint(new Vector3(0f, 0f, 0.022f)), k);   // target follows the tongs
                rt.rotation = Quaternion.Slerp(startRot, _gripPoint.rotation, k);
                yield return null;
            }
            rt.localPosition = new Vector3(0f, 0f, 0.022f);
            rt.localRotation = Quaternion.identity;
        }

        private Coroutine _flameCo;
        private void SetFlame(bool on)
        {
            if (_flameVFX == null) return;
            if (_flameCo != null) StopCoroutine(_flameCo);
            _flameCo = StartCoroutine(FlameRoutine(on));
        }

        private IEnumerator FlameRoutine(bool on)
        {
            var ft = _flameVFX.transform;
            if (on) _flameVFX.SetActive(true);
            float from = on ? 0.05f : ft.localScale.x, to = on ? 1f : 0f;
            float e = 0f, dur = on ? 0.4f : 0.25f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Ease(e / dur);
                float sc = Mathf.Lerp(from, to, k) * (on ? 1f + 0.18f * Mathf.Sin(k * Mathf.PI) : 1f);   // "whoosh" overshoot on ignition
                ft.localScale = Vector3.one * sc;
                yield return null;
            }
            ft.localScale = Vector3.one;
            if (!on) _flameVFX.SetActive(false);
        }

        /// <summary>Tongs swing over the watch glass, tip down; the MgO ash pours in and the tongs return.</summary>
        private IEnumerator CollectRoutine()
        {
            Debug.Log("[ARLab] MgO collected");
            Debug.Log("[MagnesiumLab] Magnesium oxide collected");
            _heatingOrBurning = true;                     // ignore input while it plays
            _dragging = null;

            var tt = _tongs.transform;
            Vector3 pos0 = tt.localPosition;
            Quaternion rot0 = tt.localRotation;

            // 1. glide so the jaw tip is above the dish, tip tilted down
            Vector3 over = _collectionPoint.position + Vector3.up * 0.05f;
            Vector3 posOver = pos0 + transform.InverseTransformVector(over - _gripPoint.position);
            Quaternion tilt = rot0 * Quaternion.Euler(38f, 0f, 0f);
            float e = 0f, dur = 0.6f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Ease(e / dur);
                tt.localPosition = Vector3.Lerp(pos0, posOver, k);
                tt.localRotation = Quaternion.Slerp(rot0, tilt, k);
                yield return null;
            }

            // 2. pour: white ash grains fall into the dish, the heap grows, the residue on the tongs shrinks away
            if (_mgoPowder != null) { _mgoPowder.SetActive(true); _mgoPowder.transform.localScale = new Vector3(0.05f, 0.05f, 0.05f); }
            Vector3 heapFull = Vector3.one;
            Vector3 residueScale = _mgoResidue != null ? _mgoResidue.transform.localScale : Vector3.one;
            var grains = new List<Transform>(); var gFrom = new List<Vector3>(); var gTo = new List<Vector3>(); var gT = new List<float>();
            float pourDur = 1.5f, spawnTimer = 0f; e = 0f;
            while (e < pourDur || grains.Count > 0)
            {
                e += Time.deltaTime;
                float frac = Mathf.Clamp01(e / pourDur);
                if (e < pourDur)
                {
                    spawnTimer -= Time.deltaTime;
                    while (spawnTimer <= 0f)
                    {
                        spawnTimer += 0.04f;
                        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                        Destroy(g.GetComponent<Collider>());
                        g.transform.SetParent(transform, true);
                        g.transform.localScale = Vector3.one * UnityEngine.Random.Range(0.004f, 0.008f);
                        g.GetComponent<Renderer>().sharedMaterial = _mgoMat;
                        Vector3 start = _gripPoint.position + UnityEngine.Random.insideUnitSphere * 0.006f;
                        g.transform.position = start;
                        grains.Add(g.transform); gFrom.Add(start);
                        gTo.Add(_collectionPoint.position + new Vector3(UnityEngine.Random.Range(-0.012f, 0.012f), 0.004f, UnityEngine.Random.Range(-0.012f, 0.012f)));
                        gT.Add(0f);
                    }
                    if (_mgoPowder != null) _mgoPowder.transform.localScale = Vector3.Lerp(new Vector3(0.05f, 0.05f, 0.05f), heapFull, Ease(frac));
                    if (_mgoResidue != null) _mgoResidue.transform.localScale = residueScale * Mathf.Lerp(1f, 0.05f, Ease(frac));
                }
                for (int i = grains.Count - 1; i >= 0; i--)
                {
                    gT[i] += Time.deltaTime / 0.28f;
                    float t = Mathf.Clamp01(gT[i]);
                    grains[i].position = Vector3.Lerp(gFrom[i], gTo[i], t * t);      // accelerate as they fall
                    if (gT[i] >= 1f) { Destroy(grains[i].gameObject); grains.RemoveAt(i); gFrom.RemoveAt(i); gTo.RemoveAt(i); gT.RemoveAt(i); }
                }
                yield return null;
            }
            if (_mgoPowder != null) _mgoPowder.transform.localScale = heapFull;
            if (_mgoResidue != null)
            {
                _mgoResidue.transform.SetParent(_collectionPoint, false);
                _mgoResidue.transform.localScale = residueScale;
                _mgoResidue.SetActive(false);
            }

            // 3. tongs swing back to where they started
            Vector3 restPos = _isARMode ? (AR_POS_TONGS + Vector3.up * GetBottomBoundsOffset(_tongs)) : POS_TONGS;
            Vector3 posNow = tt.localPosition; Quaternion rotNow = tt.localRotation;
            e = 0f; dur = 0.6f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Ease(e / dur);
                tt.localPosition = Vector3.Lerp(posNow, restPos, k);
                tt.localRotation = Quaternion.Slerp(rotNow, Quaternion.identity, k);
                yield return null;
            }

            _heatingOrBurning = false;
            SetState(LabState.Completed);
            ShowFeedback("Magnesium oxide (MgO) ash collected in watch glass!");
            if (_obsPanel != null) _obsPanel.SetActive(true);
            SetPanelButtonsVisible(true);
        }

        // ── Burner status UI update helper ────────────────────
        private void UpdateBurnerStatusUI()
        {
            if (_burnerStatusText == null) return;
            if (_burnerOn)
            {
                _burnerStatusText.text  = "Bunsen Burner: <color=#2E8B4E>ON</color>";
            }
            else
            {
                _burnerStatusText.text  = "Bunsen Burner: <color=#C4473A>OFF</color>";
                if (_ribbonStatusText != null)  _ribbonStatusText.text = "Ribbon: Waiting...";
                if (_observationText  != null)  _observationText.text  = "Observation: —";
            }
        }

        // ═════════════════════════════════════════════════════
        //  RESET & AR MODE INTEGRATION
        // ═════════════════════════════════════════════════════
        private Camera _standaloneCam;

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

            // Hide all equipment — they become visible only when explicitly placed
            // by dragging from the tray onto a detected real surface.
            if (_burner    != null) _burner.SetActive(false);
            if (_sandpaper != null) _sandpaper.SetActive(false);
            if (_ribbon    != null) _ribbon.SetActive(false);
            if (_tongs     != null) _tongs.SetActive(false);
            if (_watchGlass != null) _watchGlass.SetActive(false);
            Debug.Log("[ARLab] OnEnterARMode: all equipment hidden — awaiting user placement.");

            foreach (var l in _equipLabels)
            {
                if (l != null)
                {
                    var pos = l.GetComponent<EquipmentLabelPositioner>();
                    if (pos != null) pos.SetCamera(_cam);
                }
            }
        }

        public void OnExitARMode()
        {
            _isARMode = false;
            if (_standaloneCam != null)
            {
                _standaloneCam.gameObject.SetActive(true);
                _cam = _standaloneCam;
            }
            else
            {
                var mainCam = Camera.main;
                if (mainCam != null) { mainCam.gameObject.SetActive(true); _cam = mainCam; }
            }

            transform.position = Vector3.zero;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            // Restore standalone laboratory table and exact coordinates
            if (_table != null) _table.SetActive(true);
            if (_burner != null) { _burner.transform.SetParent(transform, false); _burner.transform.localPosition = POS_BURNER; _burner.transform.localRotation = Quaternion.identity; }
            if (_sandpaper != null) { _sandpaper.transform.SetParent(transform, false); _sandpaper.transform.localPosition = POS_SANDPAPER; _sandpaper.transform.localRotation = Quaternion.identity; }
            if (_ribbon != null && !_tongsHoldingRibbon) { _ribbon.transform.SetParent(transform, false); _ribbon.transform.localPosition = POS_RIBBON; _ribbon.transform.localRotation = Quaternion.identity; }
            if (_tongs != null) { _tongs.transform.SetParent(transform, false); _tongs.transform.localPosition = POS_TONGS; _tongs.transform.localRotation = Quaternion.identity; }
            if (_watchGlass != null) { _watchGlass.transform.SetParent(transform, false); _watchGlass.transform.localPosition = POS_WATCHGLASS; _watchGlass.transform.localRotation = Quaternion.identity; }

            foreach (var l in _equipLabels)
            {
                if (l != null)
                {
                    var pos = l.GetComponent<EquipmentLabelPositioner>();
                    if (pos != null) pos.SetCamera(_cam);
                }
            }
        }

        public void ResetExperiment()
        {
            StopAllCoroutines();
            Debug.Log("[MagnesiumLab] Experiment reset");
            Debug.Log("[ARLab] AR experiment reset");

            _burnerOn           = false;
            _tongsHoldingRibbon = false;
            _productCooled      = false;
            _heatingOrBurning   = false;
            _cleaningProgress   = 0f;
            _cleaningAcc        = 0f;

            if (_flameVFX != null) { _flameVFX.SetActive(false); _flameVFX.transform.localScale = Vector3.one; }
            if (_sparkPS != null) _sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_smokePS != null) _smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_baseFlamePS != null) _baseFlamePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_mainFlamePS != null) _mainFlamePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_burnLight != null) _burnLight.intensity = 0f;
            if (_flameLight != null) _flameLight.intensity = 0f;

            if (_ribbonMat != null)
            {
                Color uncleaned = new Color(0.70f, 0.72f, 0.76f, 1f);
                _ribbonMat.color = uncleaned;
                _ribbonMat.SetColor("_BaseColor", uncleaned);
                _ribbonMat.SetFloat("_Metallic", 0.70f);
                _ribbonMat.SetFloat("_Smoothness", 0.55f);
                _ribbonMat.DisableKeyword("_EMISSION");
                _ribbonMat.SetColor("_EmissionColor", Color.black);
            }

            Vector3 ribbonResetPos = _isARMode ? (AR_POS_RIBBON + Vector3.up * GetBottomBoundsOffset(_ribbon)) : POS_RIBBON;
            Vector3 tongsResetPos  = _isARMode ? (AR_POS_TONGS + Vector3.up * GetBottomBoundsOffset(_tongs)) : POS_TONGS;

            if (_ribbon != null)
            {
                _ribbon.transform.SetParent(transform, false);
                _ribbon.transform.localPosition = ribbonResetPos;
                _ribbon.transform.localRotation = Quaternion.identity;
                _ribbon.transform.localScale = Vector3.one;
                _ribbon.SetActive(true);
                var rbCol = _ribbon.GetComponent<Collider>();
                if (rbCol != null) rbCol.enabled = true;
                SetHighlight(_ribbon, false);
            }
            if (_tongs != null)
            {
                _tongs.transform.SetParent(transform, false);
                _tongs.transform.localPosition = tongsResetPos;
                _tongs.transform.localRotation = Quaternion.identity;
                SetHighlight(_tongs, false);
            }

            if (_isARMode)
            {
                if (_burner != null) { _burner.transform.SetParent(transform, false); _burner.transform.localPosition = AR_POS_BURNER + Vector3.up * GetBottomBoundsOffset(_burner); _burner.transform.localRotation = Quaternion.identity; }
                if (_sandpaper != null) { _sandpaper.transform.SetParent(transform, false); _sandpaper.transform.localPosition = AR_POS_SANDPAPER + Vector3.up * GetBottomBoundsOffset(_sandpaper); _sandpaper.transform.localRotation = Quaternion.identity; }
                if (_watchGlass != null) { _watchGlass.transform.SetParent(transform, false); _watchGlass.transform.localPosition = AR_POS_WATCHGLASS + Vector3.up * GetBottomBoundsOffset(_watchGlass); _watchGlass.transform.localRotation = Quaternion.identity; }
            }

            if (_mgoResidue != null) _mgoResidue.SetActive(false);
            if (_mgoPowder != null) { _mgoPowder.SetActive(false); _mgoPowder.transform.localScale = Vector3.one; }

            UpdateBurnerStatusUI();
            if (_heatingBarFill != null) _heatingBarFill.fillAmount = 0f;
            if (_heatingTimerText != null) _heatingTimerText.text = "";
            if (_cleaningBarFill != null) _cleaningBarFill.fillAmount = 0f;
            if (_burningInfoPanel != null) _burningInfoPanel.SetActive(false);
            ShowMgOCallout(false);

            if (_obsPanel)    _obsPanel.SetActive(false);
            if (_eqPanel)     _eqPanel.SetActive(false);
            if (_resultPanel) _resultPanel.SetActive(false);
            if (_safetyPanel) _safetyPanel.SetActive(false);
            if (_aboutPanel)  _aboutPanel.SetActive(false);
            if (_resetConfirmDialog) _resetConfirmDialog.SetActive(false);
            SetPanelButtonsVisible(false);

            SetState(LabState.Step1_SelectRibbon);
        }

        // ═════════════════════════════════════════════════════
        //  STATE MACHINE
        // ═════════════════════════════════════════════════════
        public void SetState(LabState newState)
        {
            _state = newState;

            // Show/hide cleaning bar
            if (_cleaningBarRoot != null)
                _cleaningBarRoot.SetActive(newState == LabState.Step2_CleanRibbon);

            // Show/hide equation panel
            if (_equationPanel != null)
                _equationPanel.SetActive(
                    newState == LabState.Step7_Burning ||
                    newState == LabState.Step8_Cooling ||
                    newState == LabState.Step9_Collect  ||
                    newState == LabState.Completed);

            if (newState == LabState.Intro)
            {
                if (_introPanel != null) _introPanel.SetActive(true);
                if (_hudPanel   != null) _hudPanel.SetActive(false);
                return;
            }

            if (_introPanel != null) _introPanel.SetActive(false);
            if (_hudPanel   != null) _hudPanel.SetActive(true);

            // Update nav buttons
            UpdateNavButtons(newState);

            switch (newState)
            {
                                case LabState.Step1_SelectRibbon:
                    _burnerOn = false;
                    if (_flameVFX != null) _flameVFX.SetActive(false);
                    if (_sparkPS != null) _sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (_smokePS != null) _smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (_baseFlamePS != null) _baseFlamePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (_mainFlamePS != null) _mainFlamePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    if (_burnLight != null) _burnLight.intensity = 0f;
                    if (_flameLight != null) _flameLight.intensity = 0f;
                    _tongsHoldingRibbon = false;
                    _productCooled = false;
                    _heatingOrBurning = false;
                    _cleaningProgress = 0f;
                    _cleaningAcc = 0f;
                    UpdateBurnerStatusUI();
                    SetStepInfo("Step 1: Select Ribbon",
                        "Tap the silver magnesium ribbon on the table.", "1 / 9");
                    break;
                case LabState.Step2_CleanRibbon:
                    SetStepInfo("Step 2: Clean Ribbon",
                        "Drag the magnesium ribbon back and forth on the sandpaper\npad to remove the oxide layer.\nCleaning Progress: 0%", "2 / 9");
                    break;
                case LabState.Step3_PickWithTongs:
                    SetStepInfo("Step 3: Pick Up with Tongs",
                        "Drag the crucible tongs to the ribbon to grip it.", "3 / 9");
                    break;
                case LabState.Step4_LightBurner:
                    if (!_tongsHoldingRibbon || _ribbon.transform.parent != _gripPoint)
                    {
                        _tongsHoldingRibbon = true;
                        _ribbon.transform.SetParent(_gripPoint, false);
                        _ribbon.transform.localPosition = new Vector3(0f, 0f, 0.022f);
                        _ribbon.transform.localRotation = Quaternion.identity;
                        _ribbon.transform.localScale = Vector3.one * 0.90f;
                        _ribbon.SetActive(true);
                        var rCol = _ribbon.GetComponent<Collider>();
                        if (rCol != null) rCol.enabled = false;
                    }
                    SetStepInfo("Step 4: Light Bunsen Burner",
                        "Tap the Bunsen burner to ignite the flame.", "4 / 9");
                    break;
                case LabState.Step5_MoveIntoFlame:
                    if (!_tongsHoldingRibbon || _ribbon.transform.parent != _gripPoint)
                    {
                        _tongsHoldingRibbon = true;
                        _ribbon.transform.SetParent(_gripPoint, false);
                        _ribbon.transform.localPosition = new Vector3(0f, 0f, 0.022f);
                        _ribbon.transform.localRotation = Quaternion.identity;
                        _ribbon.transform.localScale = Vector3.one * 0.90f;
                        _ribbon.SetActive(true);
                        var rCol = _ribbon.GetComponent<Collider>();
                        if (rCol != null) rCol.enabled = false;
                    }
                    if (!_burnerOn)
                    {
                        _burnerOn = true;
                        _flameVFX.SetActive(true);
                    }
                    SetStepInfo("Step 5: Heat in Flame",
                        "Drag the tongs to place the ribbon tip into the flame.", "5 / 9");
                    break;
                case LabState.Step6_Heating:
                    SetStepInfo("Step 6: Heat in Flame",
                        "Hold the magnesium ribbon with tongs and place the\nribbon tip into the Bunsen burner flame.\nObserve the burning.", "6 / 9");
                    break;
                case LabState.Step7_Burning:
                    SetStepInfo("Step 7: Combustion Reaction",
                        "Magnesium burns with an intense white flame!\n2Mg + O\u2082 \u2192 2MgO", "7 / 9");
                    break;
                case LabState.Step8_Cooling:
                    SetStepInfo("Step 8: Cooling Product",
                        "Magnesium Oxide (MgO) ash formed — a white powdery solid.\nAllow to cool before collection.", "8 / 9");
                    break;
                case LabState.Step9_Collect:
                    SetStepInfo("Step 9: Collect MgO",
                        "Drag tongs over the watch glass to collect\nthe Magnesium Oxide (MgO) ash.", "9 / 9");
                    break;
                case LabState.Completed:
                    SetStepInfo("Experiment Complete!",
                        "MgO ash collected! Review Observation, Equation, and Result.", "Done");
                    SetPanelButtonsVisible(true);
                    break;
            }
        }

        private void UpdateNavButtons(LabState state)
        {
            // Previous button
            bool showPrev = state > LabState.Step1_SelectRibbon && state < LabState.Step6_Heating;
            if (_prevBtn != null) _prevBtn.gameObject.SetActive(showPrev);

            // Next button — only at clean-complete, pick-complete, and burner steps
            bool showNext = state == LabState.Step3_PickWithTongs
                         || state == LabState.Step4_LightBurner
                         || state == LabState.Step5_MoveIntoFlame
                         || state == LabState.Completed;
            if (_nextBtn != null) _nextBtn.gameObject.SetActive(showNext);
        }

        private void AdvanceStep()
        {
            switch (_state)
            {
                case LabState.Step2_CleanRibbon:
                    // Only allow skip if partially cleaned
                    if (_cleaningProgress >= 0.5f) FinishCleaning();
                    else ShowFeedback("Keep rubbing! Clean the ribbon more before proceeding.");
                    break;
                case LabState.Step3_PickWithTongs:
                    ShowFeedback("Drag the tongs to the ribbon to grip it first.");
                    break;
                case LabState.Step4_LightBurner:
                    ShowFeedback("Tap the Bunsen burner to light it.");
                    break;
                case LabState.Step5_MoveIntoFlame:
                    ShowFeedback("Move the tongs into the flame.");
                    break;
                case LabState.Completed:
                    ShowFeedback("Experiment complete! Press Reset to start again.");
                    break;
            }
        }

        private void GoBack()
        {
            if (_state == LabState.Step2_CleanRibbon)
                SetState(LabState.Step1_SelectRibbon);
            else if (_state == LabState.Step3_PickWithTongs)
            {
                _cleaningProgress = 0f; _cleaningAcc = 0f;
                SetState(LabState.Step2_CleanRibbon);
            }
            else if (_state == LabState.Step4_LightBurner)
                SetState(LabState.Step3_PickWithTongs);
            else if (_state == LabState.Step5_MoveIntoFlame)
            {
                if (_burnerOn) ToggleBurner();
                SetState(LabState.Step4_LightBurner);
            }
        }

        // ── MgO Smoke Callout annotation ─────────────────────
        private GameObject _mgoCallout;
        private void ShowMgOCallout(bool show)
        {
            if (show && _mgoCallout == null)
            {
                // Build a small world-space canvas callout
                _mgoCallout = new GameObject("MgOCallout");
                _mgoCallout.transform.SetParent(_flamePoint, false);
                _mgoCallout.transform.localPosition = new Vector3(0.06f, 0.10f, 0f);
                _mgoCallout.transform.localScale    = Vector3.one * 0.0009f;

                var cvs = _mgoCallout.AddComponent<Canvas>();
                cvs.renderMode   = RenderMode.WorldSpace;
                cvs.sortingOrder = 20;
                _mgoCallout.AddComponent<CanvasScaler>();
                _mgoCallout.AddComponent<GraphicRaycaster>();

                var rt = _mgoCallout.GetComponent<RectTransform>();
                rt.sizeDelta = new Vector2(220f, 80f);

                // Dark background
                var bgGO = new GameObject("Bg");
                bgGO.transform.SetParent(_mgoCallout.transform, false);
                var bgRT = bgGO.AddComponent<RectTransform>();
                bgRT.anchorMin = Vector2.zero;
                bgRT.anchorMax = Vector2.one;
                bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
                var bgImg = bgGO.AddComponent<Image>();
                bgImg.color = new Color(0.08f, 0.12f, 0.22f, 0.88f);

                // Label text
                var lGO = new GameObject("Label");
                lGO.transform.SetParent(_mgoCallout.transform, false);
                var lRT = lGO.AddComponent<RectTransform>();
                lRT.anchorMin = new Vector2(0.05f, 0.1f);
                lRT.anchorMax = new Vector2(0.95f, 0.9f);
                lRT.offsetMin = lRT.offsetMax = Vector2.zero;
                var lTmp = lGO.AddComponent<TextMeshProUGUI>();
                lTmp.text      = "White Smoke:\nMagnesium Oxide\n(MgO)";
                lTmp.fontSize  = 18f;
                lTmp.color     = Color.white;
                lTmp.fontStyle = FontStyles.Bold;
                lTmp.alignment = TextAlignmentOptions.Center;
                lTmp.raycastTarget = false;
                var f = GetFont();
                if (f != null) lTmp.font = f;

                _mgoCallout.transform.LookAt(_cam.transform);
                _mgoCallout.transform.Rotate(0f, 180f, 0f);
            }

            if (_mgoCallout != null) _mgoCallout.SetActive(show);
        }

        private static void SetHighlight(GameObject go, bool on)
        {
            if (go == null) return;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                if (r.material == null) continue;
                if (on) { r.material.EnableKeyword("_EMISSION");  r.material.SetColor("_EmissionColor", new Color(0.25f, 0.55f, 1f) * 0.8f); }
                else    { r.material.DisableKeyword("_EMISSION"); r.material.SetColor("_EmissionColor", Color.black); }
            }
        }

        // ═════════════════════════════════════════════════════
        //  UI — Matches Reference Image Style
        //  1080 × 1920 portrait canvas, dark navy theme
        // ═════════════════════════════════════════════════════
        private void BuildUI()
        {
            BuildUIRaw();
            if (_canvas != null) ARVirtualLab.UI.EduTheme.Apply(_canvas.transform);   // light, textbook-style theme
        }

        private void BuildUIRaw()
        {
            var canvasGO = new GameObject("ExperimentCanvas");
            canvasGO.transform.SetParent(transform, false);
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 10;

            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);   // the experiment runs in landscape
            scaler.matchWidthOrHeight  = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGO = new GameObject("EventSystem");
                esGO.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGO.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            var root = canvasGO.GetComponent<RectTransform>();

            // ── 1. Intro Panel ────────────────────────────────
            BuildIntroPanel(root);

            // ── 2. HUD Panel ──────────────────────────────────
            BuildHUDPanel(root);

            // ── 3. Burning Status Info Panel (hidden by default)
            BuildBurningInfoPanel(root);

            // ── 4. Reaction Equation Panel (hidden by default)
            BuildEquationPanel(root);

            // ── 5. Equipment Labels ───────────────────────────
            BuildEquipmentLabels(root);

            // ── 6. Modal Info Panels ──────────────────────────
            BuildModalPanels(root);
        }

        private void BuildIntroPanel(RectTransform root)
        {
            _introPanel = MakePanel(root, "IntroPanel", new Color(0.02f, 0.04f, 0.09f, 0.95f));
            var rootRT = _introPanel.GetComponent<RectTransform>();

            // Center card
            var card = MakePanel(rootRT, "Card", new Color(0.06f, 0.10f, 0.20f, 0.98f));
            var cardRT = card.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.22f, 0.04f);
            cardRT.anchorMax = new Vector2(0.78f, 0.96f);
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
            MakeText(cardRT, "Title", "MAGNESIUM RIBBON",
                new Vector2(0.05f, 0.79f), new Vector2(0.95f, 0.89f),
                38, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);

            // Subtitle
            MakeText(cardRT, "Subtitle", "Interactive Chemistry Experiment",
                new Vector2(0.05f, 0.73f), new Vector2(0.95f, 0.79f),
                20, new Color(0.70f, 0.82f, 0.98f), FontStyles.Normal, TextAlignmentOptions.Center);

            // Reaction Equation Pill
            var eqPill = MakePanel(cardRT, "EqPill", new Color(0.08f, 0.15f, 0.30f, 0.95f));
            var eqRT = eqPill.GetComponent<RectTransform>();
            eqRT.anchorMin = new Vector2(0.12f, 0.63f);
            eqRT.anchorMax = new Vector2(0.88f, 0.71f);
            var eqOut = eqPill.AddComponent<Outline>();
            eqOut.effectColor = new Color(0.15f, 0.60f, 1.0f, 0.50f);
            eqOut.effectDistance = new Vector2(1.5f, -1.5f);
            MakeText(eqRT, "EqText", "2Mg(s) + O\u2082(g) \u2192 2MgO(s)",
                new Vector2(0f, 0f), new Vector2(1f, 1f),
                22, new Color(0.35f, 0.90f, 1f), FontStyles.Bold, TextAlignmentOptions.Center);

            // Experiment Overview Summary Box
            var infoBox = MakePanel(cardRT, "InfoBox", new Color(0.04f, 0.07f, 0.15f, 0.90f));
            var ibRT = infoBox.GetComponent<RectTransform>();
            ibRT.anchorMin = new Vector2(0.06f, 0.28f);
            ibRT.anchorMax = new Vector2(0.94f, 0.61f);

            MakeText(ibRT, "InfoTitle", "EXPERIMENT OVERVIEW",
                new Vector2(0.06f, 0.85f), new Vector2(0.94f, 0.98f),
                16, new Color(0.40f, 0.75f, 1f), FontStyles.Bold, TextAlignmentOptions.Left);

            MakeText(ibRT, "InfoBody",
                "1. Clean ribbon with sandpaper to expose reactive metal\n" +
                "2. Hold securely using laboratory crucible tongs\n" +
                "3. Ignite Bunsen burner and place ribbon into flame\n" +
                "4. Observe dazzling combustion & energy release\n" +
                "5. Collect white Magnesium Oxide (MgO) powder",
                new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.84f),
                17, new Color(0.82f, 0.88f, 0.96f), FontStyles.Normal, TextAlignmentOptions.Left);

            // Primary Button: START EXPERIMENT
            _startBtn = MakeButton(cardRT, "StartBtn", "Start Experiment",
                new Vector2(0.10f, 0.16f), new Vector2(0.90f, 0.25f),
                new Color(0.02f, 0.50f, 0.88f));
            var sbOut = _startBtn.gameObject.AddComponent<Outline>();
            sbOut.effectColor = new Color(0.30f, 0.80f, 1f, 0.80f);
            sbOut.effectDistance = new Vector2(1.5f, -1.5f);
            _startBtn.onClick.AddListener(() => SetState(LabState.Step1_SelectRibbon));

            // Secondary Button: AR LAB
            var arIntroBtn = MakeButton(cardRT, "ARIntroBtn", "AR Lab",
                new Vector2(0.10f, 0.07f), new Vector2(0.90f, 0.145f),
                new Color(0.32f, 0.24f, 0.72f));
            arIntroBtn.onClick.AddListener(() => {
                SetState(LabState.Step1_SelectRibbon);
                ToggleUnifiedAR();
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

        private const float StepCardExpandedH = 340f, StepCardCollapsedH = 152f;
        private const float StepTextWidth = 592f, StepTextMaxH = 220f;

        // Landscape layout on a 1920 x 1080 canvas: Back + name and the step card top-left, AR toggle and equation
        // top-right, Reset/Safety bottom-right (above the AR buttons when in AR), step buttons bottom-centre.
        private void BuildHUDPanel(RectTransform root)
        {
            _hudPanel = MakePanel(root, "HUD", Color.clear);
            _hudPanel.AddComponent<ARVirtualLab.AppShell.SafeAreaHelper>();
            _hudPanel.SetActive(false);
            var hud = _hudPanel.GetComponent<RectTransform>();
            const float M = HudMargin;

            // ── Top-left: Back + experiment name ──────────────
            var backBtn = HudButton(hud, "BackButton", "Back", new Color(0.12f, 0.22f, 0.38f), 30f);
            UiKit.Pin(Rt(backBtn), 0f, 1f, M, -32f, 170f, 80f);
            backBtn.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());

            // name on its own chip so it stays readable over the camera feed in AR
            // (layout components size the chip to its text whenever it is shown; measuring while the HUD was hidden gave 0)
            var chip = MakePanel(hud, "AppLabelChip", new Color(0.04f, 0.08f, 0.16f, 0.95f));
            var chipRT = UiKit.Pin(chip.GetComponent<RectTransform>(), 0f, 1f, M + 186f, -44f, 300f, 56f);
            AddBorder(chip);
            _arTranslucent.Add(chip.GetComponent<Image>());
            var chipLayout = chip.AddComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(20, 20, 0, 0);
            chipLayout.childAlignment = TextAnchor.MiddleCenter;
            chipLayout.childControlWidth = chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
            chip.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var appLabel = Fixed(MakeText(chipRT, "AppLabel", "MAGNESIUM RIBBON", Vector2.zero, Vector2.one,
                24, new Color(0.35f, 0.85f, 1f), FontStyles.Bold, TextAlignmentOptions.Center), 24f);
            appLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // ── Top-right: AR toggle ──────────────────────────
            var arBtn = HudButton(hud, "ARButton", "AR LAB", new Color(0.32f, 0.24f, 0.72f), 30f);
            UiKit.Pin(Rt(arBtn), 1f, 1f, -M, -32f, 220f, 80f);
            arBtn.onClick.AddListener(ToggleUnifiedAR);

            // ── Step card: badge + Hide/Show, title, progress pills, instruction ──
            var card = MakePanel(hud, "StepCard", new Color(0.04f, 0.08f, 0.16f, 0.95f));
            _stepCardRT = UiKit.Pin(card.GetComponent<RectTransform>(), 0f, 1f, M, -128f, 640f, StepCardExpandedH);
            AddBorder(card);
            _arTranslucent.Add(card.GetComponent<Image>());

            var stepBadge = MakePanel(_stepCardRT, "StepBadge", new Color(0.10f, 0.28f, 0.58f, 0.95f));
            UiKit.Pin(stepBadge.GetComponent<RectTransform>(), 0f, 1f, 20f, -18f, 220f, 52f);
            _stepNumberBadge = Fixed(MakeText(stepBadge.GetComponent<RectTransform>(), "BadgeText", "STEP 1 OF 9",
                Vector2.zero, Vector2.one, 22, Color.white, FontStyles.Bold, TextAlignmentOptions.Center), 22f);

            var toggle = HudButton(_stepCardRT, "StepToggle", "Hide", new Color(0.18f, 0.24f, 0.36f), 24f);
            UiKit.Pin(Rt(toggle), 1f, 1f, -20f, -18f, 120f, 52f);
            _stepToggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            toggle.onClick.AddListener(() => SetStepCardCollapsed(!_stepCardCollapsed));

            _stepTitle = MakeText(_stepCardRT, "StepTitle", "Select Magnesium Ribbon", Vector2.zero, Vector2.zero,
                32, Color.white, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            FitText(_stepTitle, 24f, 32f);
            UiKit.Pin(_stepTitle.rectTransform, 0f, 1f, 24f, -80f, 592f, 60f);

            _stepPillRow = new GameObject("StepPillRow", typeof(RectTransform));
            _stepPillRow.transform.SetParent(_stepCardRT, false);
            var prRT = UiKit.Pin(_stepPillRow.GetComponent<RectTransform>(), 0f, 1f, 24f, -152f, 592f, 12f);

            _stepPillIndicators.Clear();
            float stepWidth = 1.0f / 9.0f;
            for (int i = 0; i < 9; i++)
            {
                var pill = new GameObject($"Pill_{i + 1}", typeof(RectTransform), typeof(Image));
                pill.transform.SetParent(prRT, false);
                var pRT = pill.GetComponent<RectTransform>();
                pRT.anchorMin = new Vector2(i * stepWidth + 0.008f, 0f);
                pRT.anchorMax = new Vector2((i + 1) * stepWidth - 0.008f, 1f);
                pRT.offsetMin = pRT.offsetMax = Vector2.zero;
                var pImg = pill.GetComponent<Image>();
                pImg.color = (i == 0) ? ARVirtualLab.UI.EduTheme.PillActive : ARVirtualLab.UI.EduTheme.PillIdle;
                _stepPillIndicators.Add(pImg);
            }

            _stepDesc = Fixed(MakeText(_stepCardRT, "StepDesc", "Tap the silver magnesium ribbon on the table.", Vector2.zero, Vector2.zero,
                26, new Color(0.85f, 0.92f, 0.98f), FontStyles.Normal, TextAlignmentOptions.TopLeft), 26f);
            UiKit.Pin(_stepDesc.rectTransform, 0f, 1f, 24f, -180f, StepTextWidth, 144f);

            // ── Cleaning progress bar (Step 2 only), top-centre ──
            _cleaningBarRoot = new GameObject("CleaningBarRoot", typeof(RectTransform));
            _cleaningBarRoot.transform.SetParent(hud, false);
            var cbRootRT = UiKit.Pin(_cleaningBarRoot.GetComponent<RectTransform>(), 0.5f, 1f, 0f, -40f, 600f, 56f);

            var cbBg = new GameObject("BarBg", typeof(RectTransform), typeof(Image));
            cbBg.transform.SetParent(cbRootRT, false);
            var cbBgRT = cbBg.GetComponent<RectTransform>();
            cbBgRT.anchorMin = Vector2.zero; cbBgRT.anchorMax = Vector2.one;
            cbBgRT.offsetMin = cbBgRT.offsetMax = Vector2.zero;
            cbBg.GetComponent<Image>().color = new Color(0.08f, 0.14f, 0.25f, 0.95f);

            var cbFill = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            cbFill.transform.SetParent(cbRootRT, false);
            var cbFillRT = cbFill.GetComponent<RectTransform>();
            cbFillRT.anchorMin = new Vector2(0.005f, 0.12f);
            cbFillRT.anchorMax = new Vector2(0.995f, 0.88f);
            cbFillRT.offsetMin = cbFillRT.offsetMax = Vector2.zero;
            _cleaningBarFill = cbFill.GetComponent<Image>();
            _cleaningBarFill.color = new Color(0.05f, 0.65f, 1.00f, 1f);
            _cleaningBarFill.type = Image.Type.Filled;
            _cleaningBarFill.fillMethod = Image.FillMethod.Horizontal;
            _cleaningBarFill.fillAmount = 0f;

            _cleaningPctText = Fixed(MakeText(cbRootRT, "PctText", "CLEANING: 0%",
                Vector2.zero, Vector2.one, 22, Color.white, FontStyles.Bold, TextAlignmentOptions.Center), 22f);

            _cleaningBarRoot.SetActive(false);

            // ── Toast feedback, bottom-centre above the step buttons ──
            var toastBg = MakePanel(hud, "Toast", new Color(0.04f, 0.10f, 0.22f, 0.94f));
            var toastRT = UiKit.Pin(toastBg.GetComponent<RectTransform>(), 0.5f, 0f, 60f, 130f, 700f, 100f);
            AddBorder(toastBg);
            _arTranslucent.Add(toastBg.GetComponent<Image>());
            _feedbackText = MakeText(toastRT, "FeedbackText", "",
                new Vector2(0.04f, 0.05f), new Vector2(0.96f, 0.95f),
                26, new Color(0.92f, 0.97f, 1f), FontStyles.Normal, TextAlignmentOptions.Center);
            FitText(_feedbackText, 20f, 26f);
            toastBg.SetActive(false);

            // ── Bottom-centre: step navigation and end-of-experiment buttons (only the active ones show) ──
            var row = new GameObject("BottomRow", typeof(RectTransform));
            row.transform.SetParent(hud, false);
            var rowRT = UiKit.Pin(row.GetComponent<RectTransform>(), 0.5f, 0f, 60f, 32f, 960f, 80f);
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;

            _prevBtn = SizedButton(rowRT, "Prev", "Previous", new Color(0.18f, 0.24f, 0.36f), 220f);
            _prevBtn.onClick.AddListener(GoBack);
            _prevBtn.gameObject.SetActive(false);

            _nextBtn = SizedButton(rowRT, "Next", "Next", new Color(0.02f, 0.50f, 0.88f), 220f);
            _nextBtn.onClick.AddListener(AdvanceStep);
            _nextBtn.gameObject.SetActive(false);

            _obsBtn = SizedButton(rowRT, "Obs", "Observation", new Color(0.06f, 0.45f, 0.28f), 220f);
            _obsBtn.onClick.AddListener(() => TogglePanel(_obsPanel));
            _obsBtn.gameObject.SetActive(false);

            _eqBtn = SizedButton(rowRT, "Eq", "Equation", new Color(0.35f, 0.20f, 0.65f), 220f);
            _eqBtn.onClick.AddListener(() => TogglePanel(_eqPanel));
            _eqBtn.gameObject.SetActive(false);

            _resultBtn = SizedButton(rowRT, "Result", "Result", new Color(0.08f, 0.48f, 0.62f), 220f);
            _resultBtn.onClick.AddListener(() => TogglePanel(_resultPanel));
            _resultBtn.gameObject.SetActive(false);

            // ── Bottom-right: Reset / Safety (lifted above Move lab / Exit AR while in AR) ──
            var column = new GameObject("RightColumn", typeof(RectTransform));
            column.transform.SetParent(hud, false);
            _rightColumnRT = UiKit.Pin(column.GetComponent<RectTransform>(), 1f, 0f, -M, 32f, 200f, 172f);
            var vlg = column.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f;
            vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlWidth = vlg.childControlHeight = false;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;

            _resetBtn = SizedButton(_rightColumnRT, "Reset", "Reset", new Color(0.48f, 0.12f, 0.16f), 200f);
            _resetBtn.onClick.AddListener(() => {
                if (_resetConfirmDialog != null) _resetConfirmDialog.SetActive(true);
                else ResetExperiment();
            });

            _safetyBtn = SizedButton(_rightColumnRT, "Safety", "Safety", new Color(0.55f, 0.35f, 0.05f), 200f);
            _safetyBtn.onClick.AddListener(() => TogglePanel(_safetyPanel));
        }

        private void SetStepCardCollapsed(bool collapsed)
        {
            _stepCardCollapsed = collapsed;
            if (_stepPillRow != null) _stepPillRow.SetActive(!collapsed);
            if (_stepDesc != null) _stepDesc.gameObject.SetActive(!collapsed);
            if (_stepToggleLabel != null) _stepToggleLabel.text = collapsed ? "Show" : "Hide";
            RefreshStepCardHeight();
        }

        // The card grows with the instruction text instead of reserving space for the longest one
        private void RefreshStepCardHeight()
        {
            if (_stepCardRT == null || _stepDesc == null) return;
            float h = StepCardCollapsedH;
            if (!_stepCardCollapsed)
            {
                float textH = Mathf.Clamp(_stepDesc.GetPreferredValues(_stepDesc.text, StepTextWidth, 0f).y, 34f, StepTextMaxH);
                _stepDesc.rectTransform.sizeDelta = new Vector2(StepTextWidth, textH);
                h = 180f + textH + 22f;
            }
            _stepCardRT.sizeDelta = new Vector2(_stepCardRT.sizeDelta.x, h);
        }

        private static RectTransform Rt(Component c) { return c.GetComponent<RectTransform>(); }

        // MobileText auto-grows every label to fill its box, which made neighbouring labels different sizes.
        private static TMP_Text Fixed(TMP_Text t, float size)
        {
            t.enableAutoSizing = false;
            t.fontSize = size;
            return t;
        }

        private static void FitText(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
        }

        private static Button HudButton(RectTransform parent, string name, string label, Color col, float fontSize)
        {
            var b = MakeButton(parent, name, label, Vector2.zero, Vector2.one, col);
            Fixed(b.GetComponentInChildren<TMP_Text>(), fontSize);
            return b;
        }

        private static Button SizedButton(RectTransform parent, string name, string label, Color col, float width)
        {
            var b = HudButton(parent, name, label, col, 28f);
            Rt(b).sizeDelta = new Vector2(width, 80f);
            return b;
        }

        private static void AddBorder(GameObject panel)
        {
            var o = panel.AddComponent<Outline>();
            o.effectColor = new Color(0.15f, 0.45f, 0.85f, 0.60f);
            o.effectDistance = new Vector2(1.5f, -1.5f);
        }

        // Heating status, bottom-left (steps 6-7)
        private void BuildBurningInfoPanel(RectTransform root)
        {
            _burningInfoPanel = MakePanel(_hudPanel.GetComponent<RectTransform>(), "BurningInfoPanel",
                new Color(0.04f, 0.08f, 0.18f, 0.95f));
            var bRT = UiKit.Pin(_burningInfoPanel.GetComponent<RectTransform>(), 0f, 0f, HudMargin, 32f, 640f, 290f);
            AddBorder(_burningInfoPanel);
            _arTranslucent.Add(_burningInfoPanel.GetComponent<Image>());

            _burnerStatusText = Fixed(MakeText(bRT, "BurnerStatus", "Bunsen Burner: <color=#2E8B4E>ON</color>",
                new Vector2(0.04f, 0.76f), new Vector2(0.96f, 0.95f),
                28, Color.white, FontStyles.Bold, TextAlignmentOptions.Left), 28f);

            _ribbonStatusText = Fixed(MakeText(bRT, "RibbonStatus", "Ribbon: Heating...",
                new Vector2(0.04f, 0.58f), new Vector2(0.96f, 0.76f),
                26, Color.white, FontStyles.Normal, TextAlignmentOptions.Left), 26f);

            _observationText = Fixed(MakeText(bRT, "Observation", "Observation: —",
                new Vector2(0.04f, 0.40f), new Vector2(0.96f, 0.58f),
                24, new Color(0.85f, 0.92f, 0.98f), FontStyles.Normal, TextAlignmentOptions.Left), 24f);

            _heatingTimerText = MakeText(bRT, "TimerText",
                "Keep the ribbon in the flame for a little longer...",
                new Vector2(0.04f, 0.20f), new Vector2(0.96f, 0.40f),
                22, new Color(0.75f, 0.85f, 0.98f), FontStyles.Italic, TextAlignmentOptions.Left);
            FitText(_heatingTimerText, 18f, 22f);

            var barTrack = new GameObject("BarTrack", typeof(RectTransform), typeof(Image));
            barTrack.transform.SetParent(bRT, false);
            var btRT = barTrack.GetComponent<RectTransform>();
            btRT.anchorMin = new Vector2(0.04f, 0.06f);
            btRT.anchorMax = new Vector2(0.96f, 0.16f);
            btRT.offsetMin = btRT.offsetMax = Vector2.zero;
            barTrack.GetComponent<Image>().color = new Color(0.08f, 0.14f, 0.28f, 1f);

            var barFillGO = new GameObject("BarFill", typeof(RectTransform), typeof(Image));
            barFillGO.transform.SetParent(btRT, false);
            var bfRT = barFillGO.GetComponent<RectTransform>();
            bfRT.anchorMin = new Vector2(0.005f, 0.10f);
            bfRT.anchorMax = new Vector2(0.995f, 0.90f);
            bfRT.offsetMin = bfRT.offsetMax = Vector2.zero;
            _heatingBarFill = barFillGO.GetComponent<Image>();
            _heatingBarFill.color = new Color(0.95f, 0.55f, 0.05f, 1f);
            _heatingBarFill.type = Image.Type.Filled;
            _heatingBarFill.fillMethod = Image.FillMethod.Horizontal;
            _heatingBarFill.fillAmount = 0f;

            _burningInfoPanel.SetActive(false);
        }

        // Reaction equation, top-right under the AR toggle (steps 7-9)
        private void BuildEquationPanel(RectTransform root)
        {
            _equationPanel = MakePanel(_hudPanel.GetComponent<RectTransform>(), "EquationPanel",
                new Color(0.04f, 0.07f, 0.16f, 0.95f));
            var eRT = UiKit.Pin(_equationPanel.GetComponent<RectTransform>(), 1f, 1f, -HudMargin, -128f, 560f, 150f);
            AddBorder(_equationPanel);
            _arTranslucent.Add(_equationPanel.GetComponent<Image>());

            Fixed(MakeText(eRT, "ReactionLabel", "Reaction:",
                new Vector2(0.05f, 0.70f), new Vector2(0.95f, 0.94f),
                22, new Color(0.40f, 0.85f, 1f), FontStyles.Bold, TextAlignmentOptions.Left), 22f);

            var eq = MakeText(eRT, "Equation", "2Mg(s) + O₂(g) → 2MgO(s)",
                new Vector2(0.05f, 0.30f), new Vector2(0.95f, 0.70f),
                32, Color.white, FontStyles.Bold, TextAlignmentOptions.Left);
            FitText(eq, 24f, 32f);

            var sub = MakeText(eRT, "Sub", "(Magnesium + Oxygen → Magnesium Oxide)",
                new Vector2(0.05f, 0.06f), new Vector2(0.95f, 0.30f),
                20, new Color(0.75f, 0.85f, 0.95f), FontStyles.Normal, TextAlignmentOptions.Left);
            FitText(sub, 16f, 20f);

            _equationPanel.SetActive(false);
        }

        private void BuildEquipmentLabels(RectTransform root)
        {
            Color labelBg = new Color(0.06f, 0.10f, 0.20f, 0.85f);
            CreateEquipLabel(root, "TongsLabel",      _tongs,      "Tongs",            labelBg);
            CreateEquipLabel(root, "SandpaperLabel",  _sandpaper,  "Sandpaper",        labelBg);
            CreateEquipLabel(root, "RibbonLabel",     _ribbon,     "Magnesium Ribbon", labelBg);
            CreateEquipLabel(root, "WatchGlassLabel", _watchGlass, "Watch Glass",      labelBg);
            CreateEquipLabel(root, "BurnerLabel",     _burner,     "Bunsen Burner",    labelBg);
        }

        // One-line label of a fixed text size, sitting just above its object and following it when it moves
        private void CreateEquipLabel(RectTransform root, string name, GameObject target, string text, Color bg)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0f);

            var img = go.GetComponent<Image>();
            img.color = bg;
            img.raycastTarget = false;

            var outl = go.AddComponent<Outline>();
            outl.effectColor = new Color(0.20f, 0.50f, 0.85f, 0.60f);
            outl.effectDistance = new Vector2(1f, -1f);

            var lbl = Fixed(MakeText(rt, "Lbl", text, Vector2.zero, Vector2.one,
                22f, Color.white, FontStyles.Bold, TextAlignmentOptions.Center), 22f);
            lbl.textWrappingMode = TextWrappingModes.NoWrap;
            rt.sizeDelta = new Vector2(lbl.GetPreferredValues(text, 2000f, 100f).x + 28f, 42f);

            go.AddComponent<EquipmentLabelPositioner>().Init(_cam, target != null ? target.transform : null, root);
            go.transform.SetAsFirstSibling();       // drawn behind the HUD cards and dialogs, never on top of them
            _equipLabels.Add(go);
        }

        private void BuildModalPanels(RectTransform root)
        {
            _obsPanel = MakeInfoPanel(root, "ObsPanel", "OBSERVATION",
                "• The magnesium ribbon burns with a dazzling, intense bright white flame.\n\n" +
                "• A large amount of thermal and light energy (UV) is released.\n\n" +
                "• A white powder / ash — Magnesium Oxide (MgO) — forms and is collected in the watch glass.\n\n" +
                "• White smoke particulate: finely dispersed MgO solid particles rise into the air.\n\n" +
                "• Reaction Type: Combination / Oxidation reaction.\n\n" +
                "NOTE: MgO is a WHITE SOLID (ash), not a gas.");
            _obsPanel.SetActive(false);

            _eqPanel = MakeInfoPanel(root, "EqPanel", "CHEMICAL EQUATION",
                "Magnesium + Oxygen \u2192 Magnesium Oxide\n\n" +
                "2Mg(s)   +   O\u2082(g)   \u2192   2MgO(s)\n\n" +
                "Reactants:\n" +
                "  \u2022 Magnesium (Mg) \u2014 Silvery-white metal strip\n" +
                "  \u2022 Oxygen (O\u2082) \u2014 Atmospheric gas\n\n" +
                "Product:\n" +
                "  \u2022 Magnesium Oxide (MgO) \u2014 White solid ash\n\n" +
                "Classification: Exothermic Synthesis / Oxidation Reaction");
            _eqPanel.SetActive(false);

            _resultPanel = MakeInfoPanel(root, "ResultPanel", "EXPERIMENT RESULT",
                "EXPERIMENT CONCLUSION:\n\n" +
                "Magnesium reacts vigorously with atmospheric oxygen at high temperatures to form magnesium oxide.\n\n" +
                "Key Conclusions:\n\n" +
                "1. Combination Reaction: Mg + O\u2082 combine to produce MgO.\n\n" +
                "2. Exothermic Reaction: Dazzling white light and heat are released.\n\n" +
                "3. Product: Magnesium Oxide (MgO) \u2014 basic white solid ash.\n\n" +
                "4. White smoke = finely dispersed MgO particulate, NOT a gas.\n\n" +
                "Equation: 2Mg(s) + O\u2082(g) \u2192 2MgO(s)");
            _resultPanel.SetActive(false);

            _safetyPanel = MakeInfoPanel(root, "SafetyPanel", "SAFETY PRECAUTIONS",
                "LABORATORY SAFETY GUIDELINES:\n\n" +
                "1. Wear certified safety goggles at all times to protect your eyes.\n\n" +
                "2. Perform this experiment only under teacher / adult supervision.\n\n" +
                "3. Do NOT look directly at the intense white flame (UV radiation).\n\n" +
                "4. Always hold the burning ribbon with crucible tongs — never bare hands.\n\n" +
                "5. Allow the hot ash to cool completely before collecting.\n\n" +
                "6. Keep flammable substances away from the Bunsen burner flame.");
            _safetyPanel.SetActive(false);

            _aboutPanel = MakeInfoPanel(root, "AboutPanel", "ABOUT EXPERIMENT",
                "MAGNESIUM RIBBON BURNING EXPERIMENT\n\n" +
                "Curriculum Topic: Chemical Reactions & Equations\n" +
                "Subject: Secondary School Chemistry\n\n" +
                "Learning Objectives:\n" +
                "• Demonstrate combination & combustion reactions\n" +
                "• Understand oxidation of metals in air\n" +
                "• Observe physical & chemical transformations\n\n" +
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
            bgRT.anchorMin = new Vector2(0.30f, 0.28f);
            bgRT.anchorMax = new Vector2(0.70f, 0.72f);
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
            bgRT.anchorMin = new Vector2(0.22f, 0.06f);
            bgRT.anchorMax = new Vector2(0.78f, 0.94f);
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
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.color = col;
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

        private void SetPanelButtonsVisible(bool show)
        {
            if (_obsBtn)    _obsBtn.gameObject.SetActive(show);
            if (_eqBtn)     _eqBtn.gameObject.SetActive(show);
            if (_resultBtn) _resultBtn.gameObject.SetActive(show);
            if (show) { if (_prevBtn) _prevBtn.gameObject.SetActive(false); if (_nextBtn) _nextBtn.gameObject.SetActive(false); }
        }

        private static void TogglePanel(GameObject panel)
        {
            if (panel != null) panel.SetActive(!panel.activeSelf);
        }

        private void ShowFeedback(string msg)
        {
            if (_feedbackText == null) return;
            _feedbackText.text = MobileText.Clean(msg);
            _feedbackText.transform.parent.gameObject.SetActive(true);
            if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
            _feedbackCoroutine = StartCoroutine(HideFeedback(4.0f));
        }

        private IEnumerator HideFeedback(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_feedbackText != null)
                _feedbackText.transform.parent.gameObject.SetActive(false);
        }

        private void SetStepInfo(string title, string desc, string progress)
        {
            if (_stepTitle)     _stepTitle.text     = MobileText.Clean(title);
            if (_stepDesc)      _stepDesc.text      = MobileText.Clean(desc);
            if (_progressLabel) _progressLabel.text = MobileText.Clean(progress);
            RefreshStepCardHeight();

            int stepIdx = (int)_state;
            if (_stepNumberBadge != null)
            {
                if (stepIdx >= 1 && stepIdx <= 9)
                    _stepNumberBadge.text = MobileText.Clean($"STEP {stepIdx} OF 9");
                else if (_state == LabState.Completed)
                    _stepNumberBadge.text = MobileText.Clean("COMPLETE");
                else
                    _stepNumberBadge.text = MobileText.Clean("START");
            }

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
        }

        // ═════════════════════════════════════════════════════
        //  UI HELPERS
        // ═════════════════════════════════════════════════════
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

        private static void AddInteractable(GameObject go, LabTag tag)
        {
            var li = go.GetComponent<LabInteractable>() ?? go.AddComponent<LabInteractable>();
            li.tag = tag;
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
            Vector2 anchorMin, Vector2 anchorMax, float fontSize, Color col, FontStyles style)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text     = text;
            tmp.fontSize = fontSize;
            tmp.color    = col;
            tmp.fontStyle = style;
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


        // ═════════════════════════════════════════════════════
        //  RESOURCES & OBJ MESH LOADER
        // ═════════════════════════════════════════════════════
        private static Mesh LoadObjMesh(string modelName)
        {
            string cleanName = Path.GetFileNameWithoutExtension(modelName);

            // 1. Resources.Load (works in APK, iOS, WebGL)
            var ta = Resources.Load<TextAsset>("Models/" + cleanName);
            if (ta != null && !string.IsNullOrEmpty(ta.text))
                return ParseObjString(ta.text, cleanName);

            // 2. File read (Editor fallback)
            string rel = modelName.StartsWith("Assets/") ? modelName : "Assets/Models/" + modelName;
            if (!rel.EndsWith(".obj")) rel += ".obj";
            string full = Path.Combine(Application.dataPath, rel.Replace("Assets/", ""));
            if (File.Exists(full))
                return ParseObjString(File.ReadAllText(full), cleanName);

            Debug.LogWarning($"[MagnesiumLab] Model '{cleanName}' not found — using procedural fallback.");
            return GenerateProceduralFallback(cleanName);
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
                    if (line.StartsWith("#") || string.IsNullOrEmpty(line)) continue;
                    var parts = line.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length < 2) continue;

                    if (parts[0] == "v" && parts.Length >= 4)
                    {
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
                        positions.Add(new Vector3(x, y, z));
                    }
                    else if (parts[0] == "vn" && parts.Length >= 4)
                    {
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float x);
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float y);
                        float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float z);
                        normals.Add(new Vector3(x, y, z));
                    }
                    else if (parts[0] == "vt" && parts.Length >= 3)
                    {
                        float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float u);
                        float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float v);
                        uvs.Add(new Vector2(u, v));
                    }
                    else if (parts[0] == "f" && parts.Length >= 4)
                    {
                        for (int i = 1; i < parts.Length - 1; i++)
                        {
                            string[] fTokens = { parts[1], parts[i], parts[i + 1] };
                            foreach (var token in fTokens)
                            {
                                if (!vertexMap.TryGetValue(token, out int idx))
                                {
                                    var segs = token.Split('/');
                                    int posIdx = int.Parse(segs[0]) - 1;
                                    Vector3 pos = (posIdx >= 0 && posIdx < positions.Count) ? positions[posIdx] : Vector3.zero;
                                    Vector3 nrm = Vector3.up;
                                    Vector2 uv  = Vector2.zero;
                                    if (segs.Length > 1 && !string.IsNullOrEmpty(segs[1]))
                                    { int ui = int.Parse(segs[1]) - 1; if (ui >= 0 && ui < uvs.Count) uv = uvs[ui]; }
                                    if (segs.Length > 2 && !string.IsNullOrEmpty(segs[2]))
                                    { int ni = int.Parse(segs[2]) - 1; if (ni >= 0 && ni < normals.Count) nrm = normals[ni]; }
                                    idx = finalVerts.Count;
                                    finalVerts.Add(pos); finalNormals.Add(nrm); finalUvs.Add(uv);
                                    vertexMap[token] = idx;
                                }
                                triIndices.Add(idx);
                            }
                        }
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

        private static Mesh GenerateProceduralFallback(string name)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m  = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            m.name = name + "_Fallback";
            Destroy(go);
            return m;
        }
    }

    // ─────────────────────────────────────────────────────────
    //  Helper MonoBehaviour: projects 3D world-space equipment
    //  positions to screen-space label positions each frame.
    // ─────────────────────────────────────────────────────────
    public class EquipmentLabelPositioner : MonoBehaviour
    {
        private Camera _cam;
        private Transform _target;
        private readonly List<Renderer> _renderers = new List<Renderer>();
        private RectTransform _canvasRT;
        private RectTransform _rt;
        private const float LabelGap = 10f;      // canvas units between the object's top and the label

        public void Init(Camera cam, Transform target, RectTransform canvasRT)
        {
            _cam      = cam;
            _target   = target;
            _canvasRT = canvasRT;
            _rt       = GetComponent<RectTransform>();
        }

        // Screen point just above the object's on-screen outline, so the label never covers the object itself.
        // Particles are excluded: smoke bounds would push the label off screen.
        private bool TryGetScreenAnchor(out Vector3 screen)
        {
            screen = Vector3.zero;
            if (_target == null || !_target.gameObject.activeInHierarchy) return false;
            if (_renderers.Count == 0)
                foreach (var r in _target.GetComponentsInChildren<Renderer>(true))
                    if (r is MeshRenderer || r is SkinnedMeshRenderer) _renderers.Add(r);

            bool any = false;
            Bounds b = default;
            foreach (var r in _renderers)
            {
                if (r == null || !r.enabled || !r.gameObject.activeInHierarchy) continue;
                if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
            }
            if (!any) return false;

            Vector3 centre = _cam.WorldToScreenPoint(b.center);
            if (centre.z < 0f) return false;
            float top = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(
                    (i & 1) == 0 ? b.min.x : b.max.x,
                    (i & 2) == 0 ? b.min.y : b.max.y,
                    (i & 4) == 0 ? b.min.z : b.max.z);
                Vector3 s = _cam.WorldToScreenPoint(corner);
                if (s.z > 0f && s.y > top) top = s.y;
            }
            screen = new Vector3(centre.x, top, centre.z);
            return true;
        }

        private CanvasGroup _cg;

        public void SetCamera(Camera cam)
        {
            _cam = cam;
        }

        private void LateUpdate()
        {
            if (_cam == null || !_cam.gameObject.activeInHierarchy) _cam = Camera.main;
            if (_cam == null || _rt == null) return;
            // fade instead of SetActive: an inactive label would never get another LateUpdate to come back
            if (_cg == null)
            {
                _cg = GetComponent<CanvasGroup>();
                if (_cg == null) _cg = gameObject.AddComponent<CanvasGroup>();
                _cg.blocksRaycasts = false; _cg.interactable = false;
            }
            bool visible = TryGetScreenAnchor(out Vector3 screen)
                           && !ARVirtualLab.AR.ARExperimentMode.BlockSceneInput;    // hidden until the AR lab is placed
            _cg.alpha = visible ? 1f : 0f;
            if (!visible) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _canvasRT, screen, null, out Vector2 local);
            _rt.anchoredPosition = local + new Vector2(0f, LabelGap);
        }
    }
}
