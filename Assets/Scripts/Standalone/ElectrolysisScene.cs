// ============================================================
// ElectrolysisScene.cs  —  AR Virtual Lab
// Experiment: Electrolysis of Water (Class 10 Physical Science, Andhra Pradesh, Activity 1.7, page 20)
// Passing current through acidified water splits it into hydrogen (at the cathode, twice the volume) and
// oxygen (at the anode). Hydrogen pops when a flame is brought near; oxygen makes the flame flare brighter.
//   2H2O(l)  --electricity-->  2H2(g) + O2(g)
// Built with the shared light-theme kit (ExperimentHud, UiKit, EduTheme, ModelLoader, HintRing, CameraFit) so it
// matches the three earlier experiments without re-deriving their UI code.
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ARVirtualLab.UI;
using ARVirtualLab.AppShell;

namespace ARVirtualLab.Electrolysis
{
    public enum ElectrolysisStep
    {
        Intro = 0,
        Step1_PlaceElectrodes = 1,
        Step2_FillWater = 2,
        Step3_AddAcid = 3,
        Step4_PlaceTestTubes = 4,
        Step5_SwitchOn = 5,
        Step6_TestGases = 6,
        Step7_Results = 7,
        Completed = 8
    }

    public enum ElTag { None, ElectrodeLeft, ElectrodeRight, AcidBottle, TubeAnode, TubeCathode, SwitchBox, Candle, Mug, WaterJug }

    public class ElInteractable : MonoBehaviour { public ElTag tag = ElTag.None; }

    [RequireComponent(typeof(AudioSource))]
    public class ElectrolysisScene : MonoBehaviour
    {
        private const float TABLE_TOP_Y = -0.032f;

        private ElectrolysisStep _step = ElectrolysisStep.Intro;
        private bool _isCompleted;
        private AudioSource _audio;
        private Camera _cam;
        private GameObject _table;
        private Bounds _apparatus;                 // measured once, for the camera framing
        private bool _haveApparatus;
        private ExperimentHud _hud;
        private HintRing _hint;
        private bool _animating;
        private Vector2 _pointerDownScreen;
        private Camera _arStandaloneCam;

        // apparatus
        private GameObject _mug;
        private LiquidFill _mugFill;
        private Vector3 _mugCentre;                              // world position of the mug cavity's centre axis
        private const float WaterFrac = 0.60f;                   // how full the mug gets (fraction of its inner height)
        private GameObject _electrodeLeft, _electrodeRight;      // rod + stopper, anode (left) / cathode (right)
        private GameObject _acidBottle;
        private GameObject _tubeAnode, _tubeCathode;             // inverted test tubes over the electrodes
        private GameObject _tubeAnodeModel, _tubeCathodeModel;   // the tube mesh itself (without the liquid child)
        private GameObject _tubeAnodeLiquid, _tubeCathodeLiquid;
        private float _tubeBottomRel, _tubeTopRel;               // tube mesh extents relative to its container origin (upright)
        private LiquidFill _jugFill;
        private readonly List<CircuitWire> _wires = new List<CircuitWire>();
        private CircuitWire _dragWire;
        private Plane _wirePlane;
        private Vector3 _wireOffset;
        private GameObject _liftedTube;                          // the gas-filled tube currently held up out of the mug for the flame test
        private bool _liftedAnode;
        private Vector3 _liftedMouth;                            // world position of that tube's open mouth
        private const float TubeLift = 0.24f;
        private const float JugFull = 0.78f;
        private Vector3 _acidTipLocal;                         // mouth of the acid bottle, in its container's space
        private Vector3 _jugSpoutLocal;                        // pour tip of the water jug, in its container's space
        private float _tubeAnodeMouthY, _tubeAnodeClosedY, _tubeCathodeMouthY, _tubeCathodeClosedY;
        private GameObject _switchBox;
        private GameObject _waterJug;
        private Vector3 _waterJugOrigin;
        private GameObject _candle, _candleFlame;
        private Vector3 _candleHome;
        private Light _candleLight;

        private Vector3 _electrodeLeftOrigin, _electrodeRightOrigin, _acidOrigin, _tubeAnodeOrigin, _tubeCathodeOrigin;
        private Vector3 _electrodeLeftTarget, _electrodeRightTarget;   // where each electrode is fixed inside the mug
        private readonly Vector3 POS_MUG      = new Vector3(-0.03f, TABLE_TOP_Y, 0.03f);
        private readonly Vector3 POS_SWITCH   = new Vector3(0.32f, TABLE_TOP_Y, -0.10f);
        private readonly Vector3 POS_ACID     = new Vector3(0.27f, TABLE_TOP_Y, 0.08f);
        private readonly Vector3 POS_CANDLE   = new Vector3(-0.32f, TABLE_TOP_Y, -0.13f);
        private readonly Vector3 POS_WATER_JUG = new Vector3(-0.31f, TABLE_TOP_Y, 0.10f);

        private float _mugTopY;
        private float _electrodeGap = 0.058f;
        private float _tubeH = 0.21f, _tubeDia = 0.026f;
        private bool _waterAdded, _leftPlaced, _rightPlaced, _acidAdded, _anodeTubePlaced, _cathodeTubePlaced, _switchedOn, _anodeTested, _cathodeTested;

        private Material _glassMat, _liquidMat, _acidTintMat, _metalMat, _bubbleMat, _tableMat, _benchMat;
        private ParticleSystem _anodeBubblesPS, _cathodeBubblesPS;
        private GameObject _draggingObject;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private GameObject _anodeZone, _cathodeZone;

        // ═════════════════════════════════════════════════════
        private void Awake()
        {
            LabOrientation.Landscape(this);
            BuildCameraAndLighting();
            EnsureEventSystem();
            _audio = GetComponent<AudioSource>();
            if (_audio == null) _audio = gameObject.AddComponent<AudioSource>();
        }

        private void OnDestroy()
        {
            LabOrientation.Portrait();
        }

        private void Start()
        {
            CreateMaterials();
            BuildScene();
            _haveApparatus = ApparatusBounds.TryGet(transform, _table, out _apparatus);
            BuildUI();

            if (AppNavigation.LaunchDirectlyToAR)
            {
                AppNavigation.LaunchDirectlyToAR = false;
                SetStep(ElectrolysisStep.Step1_PlaceElectrodes);
                ToggleUnifiedAR();
            }
            else if (AppNavigation.LaunchDirectlyToExperiment)
            {
                AppNavigation.LaunchDirectlyToExperiment = false;
                SetStep(ElectrolysisStep.Step1_PlaceElectrodes);
            }
            else SetStep(ElectrolysisStep.Intro);
        }

        private void Update()
        {
            UpdateHint();
            if (_haveApparatus) CameraFit.Frame(_cam, _apparatus);   // the whole bench in the clear middle of the screen
            UpdateInteraction();
            CheckFlameContact();
        }

        // while the student holds the candle, the moment its flame reaches the lifted tube's mouth (on screen) the test fires
        private void CheckFlameContact()
        {
            if (_step != ElectrolysisStep.Step6_TestGases || _liftedTube == null || _animating || _draggingObject != _candle || _cam == null) return;
            Vector2 f = _cam.WorldToScreenPoint(_candleFlame.transform.position);
            Vector2 m = _cam.WorldToScreenPoint(_liftedMouth);
            if (Vector2.Distance(f, m) < Screen.height * 0.075f)
            {
                _draggingObject = null;
                StartCoroutine(FlameTestRoutine(_liftedAnode, false));
            }
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private void BuildCameraAndLighting()
        {
            _cam = Camera.main;
            if (_cam == null)
            {
                var go = new GameObject("Main Camera");
                _cam = go.AddComponent<Camera>();
                go.tag = "MainCamera";
                go.AddComponent<AudioListener>();
            }
            _cam.enabled = true;
            _cam.cullingMask = -1;
            _cam.transform.position = new Vector3(0f, 1.05f, -0.84f);
            _cam.transform.rotation = Quaternion.Euler(51f, 0f, 0f);
            _cam.fieldOfView = 48f;
            _cam.backgroundColor = EduTheme.CameraBackground;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.nearClipPlane = 0.01f;
            _cam.farClipPlane = 100f;

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.60f, 0.70f);

            var key = new GameObject("KeyLight").AddComponent<Light>();
            key.type = LightType.Directional; key.transform.rotation = Quaternion.Euler(50f, -25f, 0f);
            key.color = new Color(1f, 0.98f, 0.94f); key.intensity = 1.35f;
            var fill = new GameObject("FillLight").AddComponent<Light>();
            fill.type = LightType.Directional; fill.transform.rotation = Quaternion.Euler(35f, 155f, 0f);
            fill.color = new Color(0.85f, 0.90f, 1f); fill.intensity = 0.8f;

            try
            {
                var urp = _cam.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                if (urp == null) urp = _cam.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                urp.renderPostProcessing = false;
            }
            catch { }
        }

        private void CreateMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            // Glass is deliberately more visible than a real window pane (a faint 0.20-alpha rim all but disappears
            // at phone size): a soft blue-white tint with a bright rim reads as "glass" while still showing the
            // liquid clearly through it.
            _glassMat = new Material(lit); ApplyTransparent(_glassMat, new Color(0.78f, 0.90f, 1.00f, 0.26f)); _glassMat.SetFloat("_Smoothness", 0.95f);
            _liquidMat = new Material(lit); ApplyTransparent(_liquidMat, new Color(0.45f, 0.75f, 1.00f, 0.70f)); _liquidMat.SetFloat("_Smoothness", 0.85f);
            _acidTintMat = new Material(lit); ApplyTransparent(_acidTintMat, new Color(0.50f, 0.85f, 0.65f, 0.70f)); _acidTintMat.SetFloat("_Smoothness", 0.85f);
            _metalMat = Simple(new Color(0.30f, 0.32f, 0.35f), 0.55f, 0.7f);
            _bubbleMat = new Material(lit); ApplyTransparent(_bubbleMat, new Color(0.9f, 0.97f, 1f, 0.85f));
            _tableMat = Simple(new Color(0.30f, 0.18f, 0.10f), 0.35f, 0.02f);
            _benchMat = Simple(new Color(0.42f, 0.26f, 0.15f), 0.45f, 0.02f);
        }

        private Material Simple(Color c, float smooth, float metal)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metal);
            return m;
        }

        private void ApplyTransparent(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        // =========================================================
        //  SCENE
        // =========================================================
        private void BuildScene()
        {
            _table = new GameObject("LabPlatform");
            _table.transform.SetParent(transform, false);
            Box("LabTable", _table.transform, new Vector3(0f, -0.06f, 0.05f), new Vector3(1.20f, 0.04f, 0.70f), _tableMat);
            Box("BenchSurface", _table.transform, new Vector3(0f, -0.038f, 0.05f), new Vector3(1.18f, 0.012f, 0.68f), _benchMat);

            // ── Mug ────────────────────────────────────────────
            var mugContainer = new GameObject("PlasticMug");
            mugContainer.transform.SetParent(transform, false);
            mugContainer.transform.localPosition = POS_MUG;
            if (!ModelLoader.TryLoad(mugContainer.transform, "PlasticMugModel", out _mug))
                _mug = Cyl("Mug_Proc", mugContainer.transform, Vector3.zero, new Vector3(0.24f, 0.135f, 0.24f), Simple(new Color(0.95f, 0.85f, 0.90f), 0.5f, 0f));
            else ModelLoader.AutoScaleToHeight(_mug, 0.27f);
            AlignToTableSurface(mugContainer, TABLE_TOP_Y);
            // (the mug is never picked up, and a box around it would swallow every tap meant for the electrodes
            // and test tubes standing inside it, so it gets no collider)
            Tag(mugContainer, ElTag.Mug);
            _mugTopY = ModelLoader.GetTopY(_mug);
            // the water is a lathe mesh fitted to the mug's real interior, standing on its real floor
            _mugFill = LiquidFill.Create(mugContainer.transform, _mug, _liquidMat);
            _mugCentre = _mugFill.CentreWorld(0.5f);

            // ── Two electrode assemblies (graphite rod through a rubber stopper) ──
            BuildElectrode(true);
            BuildElectrode(false);

            // ── Dilute sulphuric acid bottle (reused from the Hydrogen experiments) ──
            _acidOrigin = POS_ACID;
            var acidContainer = new GameObject("AcidBottle");
            acidContainer.transform.SetParent(transform, false);
            acidContainer.transform.localPosition = _acidOrigin;
            if (!ModelLoader.TryLoad(acidContainer.transform, "SulfuricAcidBottleModel", out _acidBottle))
                _acidBottle = Cyl("Acid_Proc", acidContainer.transform, Vector3.zero, new Vector3(0.065f, 0.12f, 0.065f), Simple(new Color(0.85f, 0.55f, 0.15f), 0.7f, 0f));
            else ModelLoader.AutoScaleToHeight(_acidBottle, 0.24f);
            AlignToTableSurface(acidContainer, TABLE_TOP_Y);
            _acidOrigin = acidContainer.transform.position;
            var acidBounds = ModelLoader.GetBounds(acidContainer);
            _acidTipLocal = acidContainer.transform.InverseTransformPoint(new Vector3(acidBounds.center.x, acidBounds.max.y, acidBounds.center.z));
            ModelLoader.FitCollider(acidContainer);
            Tag(acidContainer, ElTag.AcidBottle);

            // ── Water jug (procedural — no student-supplied model for this one) ──
            _waterJugOrigin = POS_WATER_JUG;
            var jugContainer = new GameObject("WaterJug");
            jugContainer.transform.SetParent(transform, false);
            jugContainer.transform.localPosition = _waterJugOrigin;
            GameObject jugBody;
            PitcherBuilder.Build(jugContainer.transform, _glassMat, Simple(new Color(0.55f, 0.78f, 0.95f), 0.5f, 0f), out _jugSpoutLocal, out jugBody);
            AlignToTableSurface(jugContainer, TABLE_TOP_Y);
            _waterJugOrigin = jugContainer.transform.position;
            ModelLoader.FitCollider(jugContainer, 0.012f);
            Tag(jugContainer, ElTag.WaterJug);
            _waterJug = jugContainer;
            _jugFill = LiquidFill.Create(jugContainer.transform, jugBody, _liquidMat);
            _jugFill.SetFraction(JugFull);

            // ── Switch + battery control box ───────────────────
            var switchContainer = new GameObject("SwitchBox");
            switchContainer.transform.SetParent(transform, false);
            switchContainer.transform.localPosition = POS_SWITCH;
            if (!ModelLoader.TryLoad(switchContainer.transform, "SwitchBatteryBoxModel", out _switchBox))
                _switchBox = Box("SwitchBox_Proc", switchContainer.transform, Vector3.zero, new Vector3(0.13f, 0.07f, 0.07f), Simple(new Color(0.75f, 0.15f, 0.12f), 0.5f, 0.1f));
            else ModelLoader.AutoScaleToWidth(_switchBox, 0.18f);
            AlignToTableSurface(switchContainer, TABLE_TOP_Y);
            ModelLoader.FitCollider(switchContainer);
            Tag(switchContainer, ElTag.SwitchBox);

            // ── Two loose leads from the battery box: the student clips each onto an electrode ──
            var bb = ModelLoader.GetBounds(switchContainer);
            Vector3 termL = new Vector3(bb.min.x + 0.008f, bb.min.y + bb.size.y * 0.55f, bb.center.z);
            Vector3 termR = new Vector3(bb.max.x - 0.008f, bb.min.y + bb.size.y * 0.55f, bb.center.z);
            _wires.Add(CircuitWire.Create(transform, "Red", new Color(0.86f, 0.15f, 0.15f), () => termL, () => RodClip(_electrodeLeft),
                new Vector3(bb.min.x - 0.03f, TABLE_TOP_Y + 0.011f, bb.center.z - 0.09f), TABLE_TOP_Y));
            _wires.Add(CircuitWire.Create(transform, "Black", new Color(0.12f, 0.12f, 0.14f), () => termR, () => RodClip(_electrodeRight),
                new Vector3(bb.max.x + 0.01f, TABLE_TOP_Y + 0.011f, bb.center.z - 0.10f), TABLE_TOP_Y));

            // ── Two test tubes (reuse the model from the other experiments) ──
            BuildTestTube(true);
            BuildTestTube(false);

            // ── Candle for the gas tests ─────────────────────────
            var candleContainer = new GameObject("Candle");
            candleContainer.transform.SetParent(transform, false);
            candleContainer.transform.localPosition = POS_CANDLE;
            if (!ModelLoader.TryLoad(candleContainer.transform, "CandleModel", out _candle))
                _candle = Cyl("Candle_Proc", candleContainer.transform, Vector3.zero, new Vector3(0.026f, 0.12f, 0.026f), Simple(new Color(0.96f, 0.94f, 0.88f), 0.3f, 0f));
            else ModelLoader.AutoScaleToHeight(_candle, 0.16f);
            AlignToTableSurface(candleContainer, TABLE_TOP_Y);
            ModelLoader.FitCollider(candleContainer);
            Tag(candleContainer, ElTag.Candle);
            _candle = candleContainer;
            _candleHome = candleContainer.transform.position;

            _candleFlame = ProcFlame();
            float flameY = ModelLoader.GetTopY(candleContainer.gameObject) + 0.01f;
            _candleFlame.transform.SetParent(candleContainer.transform, true);
            _candleFlame.transform.position = new Vector3(candleContainer.transform.position.x, flameY, candleContainer.transform.position.z);
            var clGO = new GameObject("CandleLight"); clGO.transform.SetParent(_candleFlame.transform, false);
            _candleLight = clGO.AddComponent<Light>(); _candleLight.type = LightType.Point; _candleLight.range = 0.22f; _candleLight.intensity = 0.35f;
            _candleLight.color = new Color(1f, 0.75f, 0.35f);
            // A point light this close, at this scene's centimetre scale, blows out into a huge soft disc with no
            // tonemapping to compress it (confirmed by disabling it). The flame's own sprite already reads clearly
            // without it, so the light stays off; other code may still change its intensity, but it never renders.
            _candleLight.enabled = false;
            _candleFlame.SetActive(true);   // the candle starts lit, ready for the gas test

            // ── bubble particle systems at each electrode ───────
            var anodeAnchor = new GameObject("AnodeBubbleAnchor"); anodeAnchor.transform.SetParent(_electrodeLeft.transform, false);
            anodeAnchor.transform.localPosition = BubbleBase(_electrodeLeft);
            _anodeBubblesPS = BuildBubbles(anodeAnchor.transform);
            var cathodeAnchor = new GameObject("CathodeBubbleAnchor"); cathodeAnchor.transform.SetParent(_electrodeRight.transform, false);
            cathodeAnchor.transform.localPosition = BubbleBase(_electrodeRight);
            _cathodeBubblesPS = BuildBubbles(cathodeAnchor.transform);

            // drop zones hover at the mug's rim above each electrode's spot (the electrodes themselves end up on the floor)
            _anodeZone = Zone("Zone_Anode", new Vector3(_electrodeLeftTarget.x, _mugTopY, _electrodeLeftTarget.z), new Vector3(0.16f, 0.20f, 0.16f));
            _cathodeZone = Zone("Zone_Cathode", new Vector3(_electrodeRightTarget.x, _mugTopY, _electrodeRightTarget.z), new Vector3(0.16f, 0.20f, 0.16f));
        }

        private void BuildElectrode(bool left)
        {
            // Starts laid on the bench in front of the mug; PlaceElectrode() moves it into the mug (see _electrodeXTarget).
            float startX = left ? POS_MUG.x - _electrodeGap : POS_MUG.x + _electrodeGap;
            // (the camera looks along +z, so a SMALLER z is nearer the viewer: the loose electrodes wait in front of the mug)
            var startPos = new Vector3(startX, TABLE_TOP_Y, POS_MUG.z - 0.24f);
            var container = new GameObject(left ? "ElectrodeAnode" : "ElectrodeCathode");
            container.transform.SetParent(transform, false);
            container.transform.localPosition = startPos;

            // The rod is taller than the mug so its top end sticks out above the rim, like the textbook figure.
            GameObject rod;
            if (!ModelLoader.TryLoad(container.transform, "GraphiteRodModel", out rod))
                rod = Cyl("Rod_Proc", container.transform, Vector3.zero, new Vector3(0.014f, 0.15f, 0.014f), _metalMat);
            else { ModelLoader.AutoScaleToHeight(rod, 0.28f); }
            GameObject stopper;
            if (ModelLoader.TryLoad(container.transform, "RubberStopperModel", out stopper))
                ModelLoader.AutoScaleToWidth(stopper, 0.05f);
            AlignToTableSurface(container, TABLE_TOP_Y);
            ModelLoader.FitCollider(container, 0.012f);
            Tag(container, left ? ElTag.ElectrodeLeft : ElTag.ElectrodeRight);

            // Fixed pose: standing on the FLOOR of the mug (the stoppers sit in holes in the base), one on each side
            // of the centre, the rod rising up through the water and out past the rim.
            float bottomRel = container.transform.position.y - ModelLoader.GetBounds(container).min.y;
            float innerGap = Mathf.Min(0.05f, _mugFill.FloorRadiusWorld * 0.45f);
            float targetX = left ? _mugCentre.x - innerGap : _mugCentre.x + innerGap;
            var targetPos = new Vector3(targetX, _mugFill.FloorWorldY + bottomRel + 0.001f, _mugCentre.z);

            if (left) { _electrodeLeft = container; _electrodeLeftOrigin = startPos; _electrodeLeftTarget = targetPos; }
            else { _electrodeRight = container; _electrodeRightOrigin = startPos; _electrodeRightTarget = targetPos; }
        }

        private void BuildTestTube(bool anode)
        {
            var container = new GameObject(anode ? "TubeAnode" : "TubeCathode");
            container.transform.SetParent(transform, false);
            var start = new Vector3(anode ? -0.21f : 0.16f, TABLE_TOP_Y, -0.26f);
            container.transform.localPosition = start;

            GameObject tube;
            if (!ModelLoader.TryLoad(container.transform, "TestTubeModel", out tube))
                tube = Cyl("Tube_Proc", container.transform, Vector3.zero, new Vector3(0.026f, _tubeH * 0.5f, 0.026f), _glassMat);
            else
            {
                ModelLoader.AutoScaleToHeight(tube, _tubeH);
                foreach (var r in tube.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = new Material[r.sharedMaterials.Length];
                    for (int i = 0; i < mats.Length; i++) mats[i] = _glassMat;
                    r.sharedMaterials = mats;
                }
            }
            AlignToTableSurface(container, TABLE_TOP_Y);
            var b = ModelLoader.GetBounds(tube);
            _tubeDia = Mathf.Max(0.015f, Mathf.Min(b.size.x, b.size.z));
            // measured AFTER the container was lifted onto the bench, so these are true offsets from its origin
            _tubeBottomRel = b.min.y - container.transform.position.y;
            _tubeTopRel = b.max.y - container.transform.position.y;
            ModelLoader.FitCollider(container, 0.012f);

            // While the tube stands upright on the bench the water fills it from the bench up. Once the tube is
            // inverted over an electrode, FinalizeTubeForGas() takes over and drives this cylinder from measured
            // world bounds instead — inversion changes which physical end is "up", and world bounds do not depend
            // on knowing the model's pivot in advance.
            var liquid = Cyl(anode ? "AnodeLiquid" : "CathodeLiquid", container.transform, Vector3.zero, Vector3.one, _liquidMat);
            PlaceRestLiquid(liquid);
            Tag(container, anode ? ElTag.TubeAnode : ElTag.TubeCathode);

            if (anode) { _tubeAnode = container; _tubeAnodeModel = tube; _tubeAnodeOrigin = start; _tubeAnodeLiquid = liquid; }
            else { _tubeCathode = container; _tubeCathodeModel = tube; _tubeCathodeOrigin = start; _tubeCathodeLiquid = liquid; }
        }

        private void PlaceRestLiquid(GameObject liquid)
        {
            float h = (_tubeTopRel - _tubeBottomRel) * 0.90f;
            liquid.transform.localRotation = Quaternion.identity;
            liquid.transform.localPosition = new Vector3(0f, _tubeBottomRel + 0.008f + h * 0.5f, 0f);
            liquid.transform.localScale = new Vector3(_tubeDia * 0.7f, h * 0.5f, _tubeDia * 0.7f);
        }

        /// <summary>Just above the foot of a rod, in the electrode container's own space (where the bubbles start).</summary>
        private static Vector3 BubbleBase(GameObject electrode)
        {
            var b = ModelLoader.GetBounds(electrode);
            return electrode.transform.InverseTransformPoint(new Vector3(electrode.transform.position.x, b.min.y + 0.05f, electrode.transform.position.z));
        }

        /// <summary>Where the lead clips onto a rod: just below its top end.</summary>
        private static Vector3 RodClip(GameObject electrode)
        {
            return new Vector3(electrode.transform.position.x, ModelLoader.GetTopY(electrode) - 0.025f, electrode.transform.position.z);
        }

        private bool AllWired { get { foreach (var w in _wires) if (!w.Connected) return false; return true; } }

        private CircuitWire NextLooseWire() { foreach (var w in _wires) if (!w.Connected) return w; return null; }

        private void BeginWireDrag(CircuitWire w, Ray ray)
        {
            Vector3 fwd = _cam != null ? _cam.transform.forward : Vector3.forward;
            Vector3 p = w.Clip.transform.position;
            _wirePlane = new Plane(-fwd, p);
            _wireOffset = _wirePlane.Raycast(ray, out float d) ? ray.GetPoint(d) - p : Vector3.zero;
            _dragWire = w;
            w.BeginDrag(p);
        }

        private void FinishWireDrag()
        {
            var w = _dragWire; _dragWire = null;
            if (w == null) return;
            if (w.Drop(_cam, Screen.height * 0.10f))
            {
                _hud.ShowFeedback(AllWired ? "Both wires connected — the circuit is complete. Now tap the switch." : "Wire connected. Now clip the other one on.");
            }
            else _hud.ShowFeedback("Drag the coloured clip onto the glowing dot on its electrode.");
        }

        private IEnumerator AutoConnectWires()
        {
            _animating = true;
            foreach (var w in _wires) { if (!w.Connected) { w.ConnectNow(); yield return new WaitForSeconds(0.35f); } }
            _hud.ShowFeedback("Wires connected — the circuit is complete.");
            _animating = false;
        }

        private ParticleSystem BuildBubbles(Transform parent)
        {
            var go = new GameObject("Bubbles");
            go.transform.SetParent(parent, false);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            main.loop = true; main.playOnAwake = false; main.maxParticles = 60;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.18f, 0.26f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.006f, 0.012f);
            main.startColor = new Color(0.92f, 0.98f, 1f, 0.85f);
            main.gravityModifier = 0f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var em = ps.emission; em.enabled = false; em.rateOverTime = 0f;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Cone; sh.angle = 8f; sh.radius = 0.006f;
            sh.rotation = new Vector3(-90f, 0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.material = ParticleMat(new Color(0.92f, 0.98f, 1f, 0.85f));
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }

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
                    float e = Mathf.Clamp01((1f - r) * 6f); e = e * e * (3f - 2f * e);
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Pow(Mathf.Clamp01(1f - r), 0.6f) * e));
                }
            t.Apply();
            return _softDotTex = t;
        }

        private Material ParticleMat(Color c)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", SoftDotTex());
            m.mainTexture = SoftDotTex();
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            return m;
        }

        private GameObject ProcFlame()
        {
            var root = new GameObject("Flame");
            var inner = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            inner.transform.SetParent(root.transform, false);
            inner.transform.localPosition = new Vector3(0, 0.006f, 0);
            inner.transform.localScale = new Vector3(0.010f, 0.016f, 0.010f);
            inner.GetComponent<Renderer>().material = Simple(new Color(1f, 0.95f, 0.5f, 0.9f), 0.3f, 0f);
            Destroy(inner.GetComponent<Collider>());
            var outer = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            outer.transform.SetParent(root.transform, false);
            outer.transform.localScale = new Vector3(0.018f, 0.024f, 0.018f);
            outer.GetComponent<Renderer>().material = Simple(new Color(1f, 0.45f, 0.05f, 0.7f), 0.3f, 0f);
            Destroy(outer.GetComponent<Collider>());
            return root;
        }

        // ---------------------------------------------------------
        //  Geometry helpers
        // ---------------------------------------------------------
        private static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject Cyl(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name; go.transform.SetParent(parent, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            Destroy(go.GetComponent<Collider>());
            return go;
        }

        private GameObject Zone(string name, Vector3 pos, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, true);
            go.transform.position = pos;
            var c = go.AddComponent<BoxCollider>();
            c.size = size; c.isTrigger = true;
            return go;
        }

        private static void AlignToTableSurface(GameObject go, float targetY)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return;
            float minY = float.MaxValue;
            foreach (var r in rs) if (r.bounds.min.y < minY) minY = r.bounds.min.y;
            go.transform.position += new Vector3(0f, targetY - minY, 0f);
        }

        private static void Tag(GameObject go, ElTag tag)
        {
            var it = go.GetComponent<ElInteractable>() ?? go.AddComponent<ElInteractable>();
            it.tag = tag;
        }

        // =========================================================
        //  Interaction
        // =========================================================
        private void UpdateHint()
        {
            if (_hint == null) _hint = HintRing.Create(transform);
            GameObject t = null;
            switch (_step)
            {
                case ElectrolysisStep.Step1_PlaceElectrodes: t = !_leftPlaced ? _electrodeLeft : !_rightPlaced ? _electrodeRight : null; break;
                case ElectrolysisStep.Step2_FillWater: t = _waterJug; break;
                case ElectrolysisStep.Step3_AddAcid: t = _acidBottle; break;
                case ElectrolysisStep.Step4_PlaceTestTubes: t = !_anodeTubePlaced ? _tubeAnode : !_cathodeTubePlaced ? _tubeCathode : null; break;
                case ElectrolysisStep.Step5_SwitchOn: { var lw = NextLooseWire(); t = lw != null ? lw.Clip : _switchBox; } break;
                case ElectrolysisStep.Step6_TestGases:
                    if (_liftedTube != null) t = _candle;                       // tube is up: now bring the flame to it
                    else t = !_anodeTested ? _tubeAnode : !_cathodeTested ? _tubeCathode : null;
                    break;
            }
            _hint.Target(t);
            _hint.SetVisible(!_animating && _draggingObject == null && _dragWire == null);
        }

        private void UpdateInteraction()
        {
            if (ARVirtualLab.AR.ARExperimentMode.BlockSceneInput) return;
            if (_step == ElectrolysisStep.Intro || _step == ElectrolysisStep.Completed) return;

            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) PointerDown(t.position);
                else if (t.phase == UnityEngine.TouchPhase.Moved) PointerMove(t.position);
                else if (t.phase == UnityEngine.TouchPhase.Ended || t.phase == UnityEngine.TouchPhase.Canceled) PointerUp(t.position);
                return;
            }
            if (Input.GetMouseButtonDown(0)) PointerDown(Input.mousePosition);
            else if (Input.GetMouseButton(0)) PointerMove(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) PointerUp(Input.mousePosition);
        }

        private void PointerDown(Vector2 screen)
        {
            if (_animating) return;
            _pointerDownScreen = screen;
            if (UnityEngine.EventSystems.EventSystem.current != null && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()) return;
            Ray ray = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            if (!Physics.Raycast(ray, out RaycastHit hit, 20f, ~0, QueryTriggerInteraction.Ignore)) return;

            // a loose wire clip takes priority over anything behind it
            var clip = hit.collider.GetComponentInParent<WireClip>();
            if (clip != null)
            {
                if (_step == ElectrolysisStep.Step5_SwitchOn && !_switchedOn && !clip.Wire.Connected) BeginWireDrag(clip.Wire, ray);
                else if (_step != ElectrolysisStep.Step5_SwitchOn) _hud.ShowFeedback("These wires get connected in Step 5.");
                return;
            }

            var hi = hit.collider.GetComponentInParent<ElInteractable>();
            if (hi == null) return;

            switch (_step)
            {
                case ElectrolysisStep.Step1_PlaceElectrodes:
                    if ((hi.tag == ElTag.ElectrodeLeft && !_leftPlaced) || (hi.tag == ElTag.ElectrodeRight && !_rightPlaced))
                        StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case ElectrolysisStep.Step2_FillWater:
                    if (hi.tag == ElTag.WaterJug) StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case ElectrolysisStep.Step3_AddAcid:
                    if (hi.tag == ElTag.AcidBottle) StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case ElectrolysisStep.Step4_PlaceTestTubes:
                    if ((hi.tag == ElTag.TubeAnode && !_anodeTubePlaced) || (hi.tag == ElTag.TubeCathode && !_cathodeTubePlaced))
                        StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case ElectrolysisStep.Step5_SwitchOn:
                    if (hi.tag == ElTag.SwitchBox) SwitchOn();
                    break;
                case ElectrolysisStep.Step6_TestGases:
                    // (the electrode rod stands inside its tube, so tapping either one lifts that tube)
                    if ((hi.tag == ElTag.TubeAnode || hi.tag == ElTag.ElectrodeLeft) && !_anodeTested && _liftedTube == null) StartCoroutine(LiftTubeRoutine(true));
                    else if ((hi.tag == ElTag.TubeCathode || hi.tag == ElTag.ElectrodeRight) && !_cathodeTested && _liftedTube == null) StartCoroutine(LiftTubeRoutine(false));
                    else if (hi.tag == ElTag.Candle)
                    {
                        if (_liftedTube != null) StartDrag(_candle, ray);
                        else _hud.ShowFeedback("Tap a test tube first to lift it out of the mug, then bring the flame to its mouth.");
                    }
                    break;
            }
        }

        private void PointerMove(Vector2 screen)
        {
            if (_dragWire != null)
            {
                Ray wr = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
                if (_wirePlane.Raycast(wr, out float wd)) _dragWire.DragTo(wr.GetPoint(wd) - _wireOffset);
                return;
            }
            if (_draggingObject == null) return;
            Ray ray = _cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0));
            if (_dragPlane.Raycast(ray, out float dist)) _draggingObject.transform.position = ray.GetPoint(dist) - _dragOffset;
        }

        private void PointerUp(Vector2 screen)
        {
            if (_dragWire != null) { FinishWireDrag(); return; }
            if (_draggingObject == null) return;
            var dropped = _draggingObject; _draggingObject = null;
            bool tapped = Vector2.Distance(screen, _pointerDownScreen) < 12f;

            switch (_step)
            {
                case ElectrolysisStep.Step1_PlaceElectrodes:
                    if (dropped == _electrodeLeft && (tapped || Near(dropped, _anodeZone))) PlaceElectrode(true);
                    else if (dropped == _electrodeRight && (tapped || Near(dropped, _cathodeZone))) PlaceElectrode(false);
                    else { StartCoroutine(MoveRot(dropped.transform, dropped == _electrodeLeft ? _electrodeLeftOrigin : _electrodeRightOrigin, Quaternion.identity, 0.3f)); _hud.ShowFeedback("Fit the electrode into its hole in the mug."); }
                    break;
                case ElectrolysisStep.Step2_FillWater:
                    if (Vector3.Distance(dropped.transform.position, new Vector3(_mugCentre.x, _mugTopY, _mugCentre.z)) < 0.36f || tapped) FillWater();
                    else { StartCoroutine(MoveRot(dropped.transform, _waterJugOrigin, Quaternion.identity, 0.3f)); _hud.ShowFeedback("Pour the water jug over the mug."); }
                    break;
                case ElectrolysisStep.Step3_AddAcid:
                    if (Vector3.Distance(dropped.transform.position, new Vector3(_mugCentre.x, _mugTopY, _mugCentre.z)) < 0.36f || tapped) AddAcid();
                    else { StartCoroutine(MoveRot(dropped.transform, _acidOrigin, Quaternion.identity, 0.3f)); _hud.ShowFeedback("Add a few drops of dilute sulphuric acid to the water."); }
                    break;
                case ElectrolysisStep.Step4_PlaceTestTubes:
                    if (dropped == _tubeAnode && (tapped || Near(dropped, _anodeZone))) PlaceTube(true);
                    else if (dropped == _tubeCathode && (tapped || Near(dropped, _cathodeZone))) PlaceTube(false);
                    else { StartCoroutine(MoveRot(dropped.transform, dropped == _tubeAnode ? _tubeAnodeOrigin : _tubeCathodeOrigin, Quaternion.identity, 0.3f)); _hud.ShowFeedback("Invert the test tube over the electrode."); }
                    break;
                case ElectrolysisStep.Step6_TestGases:
                    if (dropped == _candle)
                    {
                        StartCoroutine(MoveRot(_candle.transform, _candleHome, Quaternion.identity, 0.35f));
                        _hud.ShowFeedback("Hold the flame right at the mouth of the lifted test tube.");
                    }
                    break;
            }
        }

        private void StartDrag(GameObject go, Ray ray)
        {
            _draggingObject = go;
            Vector3 fwd = _cam != null ? _cam.transform.forward : Vector3.forward;
            _dragPlane = new Plane(-fwd, go.transform.position);
            if (_dragPlane.Raycast(ray, out float dist)) _dragOffset = ray.GetPoint(dist) - go.transform.position;
            else _dragOffset = Vector3.zero;
        }

        private bool Near(GameObject obj, GameObject zone) => obj != null && zone != null && Vector3.Distance(obj.transform.position, zone.transform.position) < 0.26f;

        private GameObject RootOf(GameObject go)
        {
            Transform t = go.transform;
            while (t.parent != null && t.parent != transform) t = t.parent;
            return t.gameObject;
        }

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

        // =========================================================
        //  Step actions
        // =========================================================
        private void PlaceElectrode(bool left)
        {
            StartCoroutine(PlaceElectrodeRoutine(left));
        }

        private IEnumerator PlaceElectrodeRoutine(bool left)
        {
            _animating = true;
            var container = left ? _electrodeLeft : _electrodeRight;
            var target = left ? _electrodeLeftTarget : _electrodeRightTarget;
            yield return MoveRot(container.transform, target, Quaternion.identity, 0.35f);
            if (left) _leftPlaced = true; else _rightPlaced = true;
            _hud.ShowFeedback((left ? "Anode" : "Cathode") + " electrode fixed in the mug.");
            _animating = false;
            if (_leftPlaced && _rightPlaced) SetStep(ElectrolysisStep.Step2_FillWater);
        }

        private void FillWater()
        {
            if (_animating) return;
            StartCoroutine(FillWaterRoutine());
        }

        private IEnumerator FillWaterRoutine()
        {
            _animating = true;
            var jt = _waterJug.transform;
            // tip the jug (spout is on its +x side) so the lip hangs just over the middle of the mug
            Quaternion tilt = Quaternion.Euler(0f, 0f, -80f);
            Vector3 lip = new Vector3(_mugCentre.x, _mugTopY + 0.05f, _mugCentre.z);
            // the jug's water drains down as it tips and pours
            Coroutine tiltDrain = StartCoroutine(DrainJug(JugFull, JugFull * 0.55f, 0.55f));
            yield return MoveRot(jt, lip - tilt * _jugSpoutLocal, tilt, 0.55f);
            yield return tiltDrain;

            _mugFill.SetMaterial(_liquidMat);
            float e = 0f, dur = 1.4f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                _mugFill.SetFraction(k * WaterFrac);
                _jugFill.SetFraction(Mathf.Lerp(JugFull * 0.55f, 0.04f, k));
                ShowStream(_liquidMat, lip, _mugFill.LevelWorldY);
                yield return null;
            }
            _mugFill.SetFraction(WaterFrac);
            _jugFill.SetFraction(0.04f);
            HideStream();
            _waterAdded = true;

            yield return MoveRot(jt, _waterJugOrigin, Quaternion.identity, 0.45f);
            _hud.ShowFeedback("Mug filled with water — the electrodes are now immersed.");
            _animating = false;
            SetStep(ElectrolysisStep.Step3_AddAcid);
        }

        private IEnumerator DrainJug(float from, float to, float dur)
        {
            float e = 0f;
            while (e < dur) { e += Time.deltaTime; _jugFill.SetFraction(Mathf.Lerp(from, to, Mathf.Clamp01(e / dur))); yield return null; }
            _jugFill.SetFraction(to);
        }

        // thin falling column of liquid between a pouring lip and the surface below it
        private GameObject _stream;
        private void ShowStream(Material mat, Vector3 lip, float surfaceY)
        {
            if (_stream == null)
            {
                _stream = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                _stream.name = "PourStream";
                _stream.transform.SetParent(transform, false);
                Destroy(_stream.GetComponent<Collider>());
            }
            _stream.SetActive(true);
            _stream.GetComponent<Renderer>().sharedMaterial = mat;
            float len = Mathf.Max(0.002f, lip.y - surfaceY);
            _stream.transform.position = new Vector3(lip.x, surfaceY + len * 0.5f, lip.z);
            _stream.transform.rotation = Quaternion.identity;
            _stream.transform.localScale = new Vector3(0.011f, len * 0.5f, 0.011f);
        }

        private void HideStream() { if (_stream != null) _stream.SetActive(false); }

        private void AddAcid()
        {
            if (_animating) return;
            StartCoroutine(AddAcidRoutine());
        }

        private IEnumerator AddAcidRoutine()
        {
            _animating = true;
            var bt = _acidBottle.transform;
            float side = _acidOrigin.x > _mugCentre.x ? 1f : -1f;      // bottle stands to the right of the mug -> tips leftwards
            Quaternion tilt = Quaternion.Euler(0f, 0f, 105f * side);
            Vector3 lip = new Vector3(_mugCentre.x, _mugTopY + 0.05f, _mugCentre.z);
            yield return MoveRot(bt, lip - tilt * _acidTipLocal, tilt, 0.55f);
            float e = 0f, dur = 0.9f;
            while (e < dur)
            {
                e += Time.deltaTime;
                ShowStream(_acidTintMat, lip, _mugFill.LevelWorldY);
                if (e > dur * 0.5f) _mugFill.SetMaterial(_acidTintMat);
                yield return null;
            }
            HideStream();
            _mugFill.SetMaterial(_acidTintMat);
            yield return MoveRot(bt, _acidOrigin, Quaternion.identity, 0.45f);
            _acidAdded = true;
            _hud.ShowFeedback("Dilute sulphuric acid added — the water can now conduct electricity.");
            _animating = false;
            SetStep(ElectrolysisStep.Step4_PlaceTestTubes);
        }

        private void PlaceTube(bool anode)
        {
            StartCoroutine(PlaceTubeRoutine(anode));
        }

        private IEnumerator PlaceTubeRoutine(bool anode)
        {
            _animating = true;
            var container = anode ? _tubeAnode : _tubeCathode;
            var electrode = anode ? _electrodeLeft : _electrodeRight;
            // Inverted, the tube's old top becomes its mouth. Put that mouth a little below the water surface so the
            // tube dips into the water while its closed end rises well above the mug, enclosing the rod's top end.
            float mouthY = _mugFill.LevelWorldY - 0.03f;
            Vector3 target = new Vector3(electrode.transform.position.x, mouthY + _tubeTopRel, electrode.transform.position.z);
            yield return MoveRot(container.transform, target + Vector3.up * 0.12f, Quaternion.Euler(180f, 0f, 0f), 0.45f);
            yield return MoveRot(container.transform, target, Quaternion.Euler(180f, 0f, 0f), 0.35f);
            FinalizeTubeForGas(anode);
            if (anode) _anodeTubePlaced = true; else _cathodeTubePlaced = true;
            _hud.ShowFeedback((anode ? "Anode" : "Cathode") + " test tube placed, mouth down and full of water.");
            _animating = false;
            if (_anodeTubePlaced && _cathodeTubePlaced) SetStep(ElectrolysisStep.Step5_SwitchOn);
        }

        /// <summary>
        /// Once a tube is inverted, its liquid stops following the tube's own (rotated) local space and instead
        /// tracks the tube's measured WORLD bounds directly: the highest point is always the closed end (now on
        /// top) and the lowest point is always the submerged mouth, regardless of the model's original pivot.
        /// </summary>
        private void FinalizeTubeForGas(bool anode)
        {
            var container = anode ? _tubeAnode : _tubeCathode;
            var liquid = anode ? _tubeAnodeLiquid : _tubeCathodeLiquid;
            var b = ModelLoader.GetBounds(anode ? _tubeAnodeModel : _tubeCathodeModel);
            liquid.transform.SetParent(transform, true);
            if (anode) { _tubeAnodeMouthY = b.min.y + 0.004f; _tubeAnodeClosedY = b.max.y - 0.006f; }
            else { _tubeCathodeMouthY = b.min.y + 0.004f; _tubeCathodeClosedY = b.max.y - 0.006f; }
            SetTubeWaterWorld(liquid, container.transform.position, anode ? _tubeAnodeMouthY : _tubeCathodeMouthY, anode ? _tubeAnodeClosedY : _tubeCathodeClosedY, 1f);
        }

        private void SwitchOn()
        {
            if (_animating || _switchedOn) return;
            if (!AllWired) { _hud.ShowFeedback("The circuit is open — clip both wires onto the electrodes first."); return; }
            _switchedOn = true;
            _hud.ShowFeedback("Current flows! Hydrogen collects at the cathode, oxygen at the anode.");
            if (_anodeBubblesPS != null) { var em = _anodeBubblesPS.emission; em.enabled = true; em.rateOverTime = 18f; _anodeBubblesPS.Play(); }
            if (_cathodeBubblesPS != null) { var em = _cathodeBubblesPS.emission; em.enabled = true; em.rateOverTime = 34f; _cathodeBubblesPS.Play(); }
            StartCoroutine(GasEvolutionRoutine());
        }

        private IEnumerator GasEvolutionRoutine()
        {
            float dur = 6.0f, e = 0f;
            Vector3 anodeXZ = _tubeAnode.transform.position, cathodeXZ = _tubeCathode.transform.position;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.Clamp01(e / dur);
                // the water surface retreats from the closed (top) end down towards the submerged mouth as gas collects
                SetTubeWaterWorld(_tubeAnodeLiquid, anodeXZ, _tubeAnodeMouthY, _tubeAnodeClosedY, Mathf.Lerp(1f, 0.55f, k));    // anode: oxygen, less gas
                SetTubeWaterWorld(_tubeCathodeLiquid, cathodeXZ, _tubeCathodeMouthY, _tubeCathodeClosedY, Mathf.Lerp(1f, 0.15f, k)); // cathode: hydrogen, twice the gas
                yield return null;
            }
            SetStep(ElectrolysisStep.Step6_TestGases);
        }

        /// <summary>retainedFraction 1 = tube still full of water, 0 = water pushed all the way down to the mouth.</summary>
        private void SetTubeWaterWorld(GameObject liquid, Vector3 xz, float mouthY, float closedY, float retainedFraction)
        {
            if (liquid == null) return;
            float interfaceY = Mathf.Lerp(mouthY, closedY, Mathf.Clamp01(retainedFraction));
            float height = Mathf.Max(0.002f, interfaceY - mouthY);
            liquid.transform.position = new Vector3(xz.x, mouthY + height * 0.5f, xz.z);
            liquid.transform.rotation = Quaternion.identity;
            liquid.transform.localScale = new Vector3(_tubeDia * 0.7f, height * 0.5f, _tubeDia * 0.7f);
        }

        private IEnumerator MoveTubeAndWater(Transform tube, Transform liquid, Vector3 delta, float dur)
        {
            Vector3 t0 = tube.position, l0 = liquid.position;
            float e = 0f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                tube.position = t0 + delta * k;
                liquid.position = l0 + delta * k;
                yield return null;
            }
            tube.position = t0 + delta; liquid.position = l0 + delta;
        }

        // Next-button version: does the whole test by itself (lift the tube, carry the candle over, run the flame test).
        private void TestTube(bool anode)
        {
            StartCoroutine(TestTubeRoutine(anode));
        }

        private IEnumerator TestTubeRoutine(bool anode)
        {
            _animating = true;
            if (_liftedTube == null) yield return LiftTubeRoutine(anode, true);
            var candle = _candle.transform;
            Vector3 flameOffset = _candleFlame.transform.position - candle.position;
            Vector3 flameAt = _liftedMouth + Vector3.down * 0.012f;
            Vector3 high = new Vector3(_candleHome.x, flameAt.y - flameOffset.y, _candleHome.z);   // raise the candle first, then slide over
            yield return MoveRot(candle, high, Quaternion.identity, 0.4f);
            yield return MoveRot(candle, flameAt - flameOffset, Quaternion.identity, 0.5f);
            yield return FlameTestRoutine(anode, true);
        }

        /// <summary>Student path: tap a tube and it lifts clear of the mug (the gas stays trapped in it).</summary>
        private IEnumerator LiftTubeRoutine(bool anode) { return LiftTubeRoutine(anode, false); }

        private IEnumerator LiftTubeRoutine(bool anode, bool keepAnimatingFlag)
        {
            _animating = true;
            var tube = anode ? _tubeAnode : _tubeCathode;
            var liquid = anode ? _tubeAnodeLiquid : _tubeCathodeLiquid;
            yield return MoveTubeAndWater(tube.transform, liquid.transform, Vector3.up * TubeLift, 0.5f);
            _liftedTube = tube; _liftedAnode = anode;
            _liftedMouth = new Vector3(tube.transform.position.x, (anode ? _tubeAnodeMouthY : _tubeCathodeMouthY) + TubeLift, tube.transform.position.z);
            if (!keepAnimatingFlag)
            {
                _animating = false;
                _hud.ShowFeedback("Tube lifted out of the mug. Now drag the burning candle so its flame reaches the tube's mouth.");
            }
        }

        /// <summary>The flame is at the tube's mouth: show what that gas does, then put everything back.</summary>
        private IEnumerator FlameTestRoutine(bool anode, bool alreadyAnimating)
        {
            _animating = true;
            var candle = _candle.transform;
            Vector3 flameOffset = _candleFlame.transform.position - candle.position;
            if (!alreadyAnimating)
                yield return MoveRot(candle, _liftedMouth + Vector3.down * 0.012f - flameOffset, Quaternion.identity, 0.2f);   // settle the flame right on the mouth

            var flame = _candleFlame.transform;
            Vector3 flameScale = flame.localScale;
            if (anode)
            {
                // oxygen supports burning: the flame grows brighter and bigger
                float e = 0f;
                while (e < 0.8f) { e += Time.deltaTime; flame.localScale = flameScale * Mathf.Lerp(1f, 1.9f, Mathf.Sin(Mathf.Clamp01(e / 0.8f) * Mathf.PI)); yield return null; }
                _hud.ShowFeedback("The flame flares up brightly — this gas supports burning. It is oxygen.");
                _anodeTested = true;
            }
            else
            {
                // hydrogen burns explosively with a squeaky pop
                PlayPopSound();
                float e = 0f;
                while (e < 0.3f) { e += Time.deltaTime; flame.localScale = flameScale * Mathf.Lerp(1f, 3.2f, Mathf.Sin(Mathf.Clamp01(e / 0.3f) * Mathf.PI)); yield return null; }
                _hud.ShowFeedback("Pop! The gas burns with a squeaky pop — this is hydrogen.");
                _cathodeTested = true;
            }
            flame.localScale = flameScale;

            Vector3 high = new Vector3(_candleHome.x, candle.position.y, _candleHome.z);
            yield return MoveRot(candle, high, Quaternion.identity, 0.35f);
            yield return MoveRot(candle, _candleHome, Quaternion.identity, 0.45f);
            var tube = _liftedTube;
            var liquid = anode ? _tubeAnodeLiquid : _tubeCathodeLiquid;
            yield return MoveTubeAndWater(tube.transform, liquid.transform, Vector3.down * TubeLift, 0.5f);
            _liftedTube = null;
            _animating = false;
            if (_anodeTested && _cathodeTested)
            {
                _isCompleted = true;
                SetStep(ElectrolysisStep.Step7_Results);
            }
        }

        private void PlayPopSound()
        {
            if (_audio == null) return;
            int n = 4410; var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / 44100f;
                float env = Mathf.Exp(-t * 60f);
                float wave = Mathf.Sin(2f * Mathf.PI * 190f * t) * 0.7f + Mathf.Sin(2f * Mathf.PI * 95f * t) * 0.3f;
                data[i] = Mathf.Clamp(wave * env, -1f, 1f);
            }
            var clip = AudioClip.Create("Pop", n, 1, 44100, false);
            clip.SetData(data, 0);
            _audio.PlayOneShot(clip, 1f);
        }

        // =========================================================
        //  Navigation, reset, AR
        // =========================================================
        public void NextStep()
        {
            if (_step == ElectrolysisStep.Intro) { SetStep(ElectrolysisStep.Step1_PlaceElectrodes); return; }
            if (_isCompleted || _animating) return;
            switch (_step)
            {
                case ElectrolysisStep.Step1_PlaceElectrodes:
                    if (!_leftPlaced) PlaceElectrode(true); else if (!_rightPlaced) PlaceElectrode(false);
                    return;
                case ElectrolysisStep.Step2_FillWater: FillWater(); return;
                case ElectrolysisStep.Step3_AddAcid: AddAcid(); return;
                case ElectrolysisStep.Step4_PlaceTestTubes:
                    if (!_anodeTubePlaced) PlaceTube(true); else if (!_cathodeTubePlaced) PlaceTube(false);
                    return;
                case ElectrolysisStep.Step5_SwitchOn:
                    if (!AllWired) StartCoroutine(AutoConnectWires()); else SwitchOn();
                    return;
                case ElectrolysisStep.Step6_TestGases:
                    if (_liftedTube != null) return;
                    if (!_anodeTested) TestTube(true); else if (!_cathodeTested) TestTube(false);
                    return;
            }
        }

        public void PrevStep()
        {
            if (_step <= ElectrolysisStep.Step1_PlaceElectrodes) return;
            _step = (ElectrolysisStep)((int)_step - 1);
            RefreshUI();
        }

        private void ToggleUnifiedAR()
        {
            if (!ARVirtualLab.AR.ARExperimentMode.Active && _arStandaloneCam == null) _arStandaloneCam = _cam;
            ARVirtualLab.AR.ARExperimentMode.Toggle(new ARVirtualLab.AR.ARExperimentSetup
            {
                SceneRoot = transform,
                StandaloneCamera = _arStandaloneCam,
                Bench = _table,
                BenchSurfaceY = TABLE_TOP_Y,
                BenchCenterZ = 0.05f,
                OnStarted = c => { _cam = c; },
                OnStopped = c => { _cam = c; }
            });
        }

        public void ResetExperiment()
        {
            StopAllCoroutines();
            _animating = false;

            if (_anodeBubblesPS != null) _anodeBubblesPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (_cathodeBubblesPS != null) _cathodeBubblesPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            HideStream();
            foreach (var w in _wires) w.ResetWire();
            _dragWire = null;
            _liftedTube = null;
            _jugFill.SetFraction(JugFull);
            _mugFill.SetFraction(0f);
            _mugFill.SetMaterial(_liquidMat);
            _candle.transform.SetPositionAndRotation(_candleHome, Quaternion.identity);
            _candleFlame.transform.localScale = Vector3.one;
            _waterJug.transform.SetPositionAndRotation(_waterJugOrigin, Quaternion.identity);
            _electrodeLeft.transform.SetPositionAndRotation(_electrodeLeftOrigin, Quaternion.identity);
            _electrodeRight.transform.SetPositionAndRotation(_electrodeRightOrigin, Quaternion.identity);
            _acidBottle.transform.SetPositionAndRotation(_acidOrigin, Quaternion.identity);
            _tubeAnode.transform.SetPositionAndRotation(_tubeAnodeOrigin, Quaternion.identity);
            _tubeCathode.transform.SetPositionAndRotation(_tubeCathodeOrigin, Quaternion.identity);
            ResetTubeLiquid(_tubeAnode, ref _tubeAnodeLiquid);
            ResetTubeLiquid(_tubeCathode, ref _tubeCathodeLiquid);
            _candleLight.intensity = 0.35f;

            _waterAdded = _leftPlaced = _rightPlaced = _acidAdded = _anodeTubePlaced = _cathodeTubePlaced = _switchedOn = _anodeTested = _cathodeTested = false;
            _isCompleted = false;
            _draggingObject = null;
            if (_hud != null) _hud.CloseAllModals();

            SetStep(ElectrolysisStep.Step1_PlaceElectrodes);
            _hud.ShowFeedback("Experiment reset. Apparatus returned to the starting state.");
        }

        /// <summary>Puts a tube's water cylinder back to being a plain child of the (now upright, un-rotated) tube.</summary>
        private void ResetTubeLiquid(GameObject tubeContainer, ref GameObject liquid)
        {
            if (liquid == null) return;
            liquid.transform.SetParent(tubeContainer.transform, false);
            PlaceRestLiquid(liquid);
        }

        // =========================================================
        //  UI
        // =========================================================
        private void BuildUI()
        {
            var root = ExperimentHud.CreateCanvas(transform, "ElectrolysisCanvas");

            _hud = ExperimentHud.Build(this, root, new HudConfig
            {
                AppLabel = "ELECTROLYSIS OF WATER",
                IntroTitle = "Electrolysis of Water",
                IntroSubtitle = "Chemical Reactions and Equations · Activity 1.7",
                Equation = "2H2O(l)  --electricity-->  2H2(g) + O2(g)",
                OverviewSteps = new[]
                {
                    "Fix the carbon electrodes in the mug",
                    "Fill the mug with water",
                    "Add dilute sulphuric acid",
                    "Place water-filled test tubes over the electrodes",
                    "Connect the wires and switch on the current",
                    "Lift each tube and test its gas with the flame",
                },
                StepCount = 6,
                SafetyBody = "LABORATORY SAFETY GUIDELINES:\n\n" +
                    "1. Handle dilute sulphuric acid carefully - avoid skin and eye contact.\n\n" +
                    "2. Keep the 6 volt supply away from water spills.\n\n" +
                    "3. Bring the flame near the test tube mouth only, never inside it.\n\n" +
                    "4. This flame test is an educational simulation only.",
                AboutBody = "ELECTROLYSIS OF WATER\n\n" +
                    "Subject: Class 10 Chemistry / Virtual Science Laboratory\n\n" +
                    "Concept: Passing direct current through acidified water decomposes it into hydrogen and oxygen gas.\n\n" +
                    "Apparatus:\n  - Plastic mug with two carbon electrodes\n  - Dilute sulphuric acid\n  - 6 volt battery, switch\n  - Two test tubes",
                ObservationBody = "- Bubbles form at both electrodes as soon as the current is switched on.\n\n" +
                    "- The test tube over the cathode fills with gas about twice as fast as the one over the anode.\n\n" +
                    "- The gas at the cathode (hydrogen) burns with a pop; the gas at the anode (oxygen) makes a flame burn brighter.",
                EquationBody = "Electrolysis of Water:\n\n2H2O(l)  --electricity-->  2H2(g) + O2(g)\n\n" +
                    "Hydrogen forms at the cathode (negative electrode) - twice the volume.\nOxygen forms at the anode (positive electrode) - half the volume.",
                ResultBody = "EXPERIMENT CONCLUSION:\n\n" +
                    "1. Water decomposes into hydrogen and oxygen when a direct current passes through it (electrolysis).\n\n" +
                    "2. Hydrogen collects at the cathode in twice the volume of oxygen, matching the formula H2O.\n\n" +
                    "3. Hydrogen is confirmed by its pop with a flame; oxygen is confirmed because it supports combustion.\n\n" +
                    "Equation: 2H2O(l) -> 2H2(g) + O2(g)",
                OnStart = () => SetStep(ElectrolysisStep.Step1_PlaceElectrodes),
                OnArToggle = ToggleUnifiedAR,
                OnPrev = PrevStep,
                OnNext = NextStep,
                OnReset = ResetExperiment,
            });

            BuildEquipmentLabels(root);
            CircuitWire.UiRoot = root;                 // wire target rings are drawn on this UI layer, above the 3D scene
            CircuitWire.UiCamera = () => _cam;
        }

        /// <summary>The small white name-tags floating over each piece of apparatus, matching the earlier experiments.</summary>
        private void BuildEquipmentLabels(RectTransform canvasRoot)
        {
            System.Func<Camera> cam = () => _cam;
            // Tags only appear while they are useful, so they never pile up over the apparatus: setup items before
            // they are used, the test tubes once they come into play, nothing at all on the intro / results screens.
            // (they also step aside while something is being dragged or poured, so the action itself stays clear)
            System.Func<bool> inLab = () => _step != ElectrolysisStep.Intro && _step != ElectrolysisStep.Step7_Results && _step != ElectrolysisStep.Completed
                                            && !_animating && _draggingObject == null;
            System.Func<bool> upToAcid = () => inLab() && _step <= ElectrolysisStep.Step3_AddAcid;
            System.Func<bool> beforeWater = () => inLab() && !_waterAdded;
            System.Func<bool> beforeAcid = () => inLab() && !_acidAdded;
            System.Func<bool> tubesInPlay = () => inLab() && _step >= ElectrolysisStep.Step4_PlaceTestTubes;

            EquipmentLabel.Create(canvasRoot, cam, _mug.transform, "Plastic Mug", LabelAnchor.Top, new Vector2(120f, -6f), inLab);
            EquipmentLabel.Create(canvasRoot, cam, _waterJug.transform, "Water", LabelAnchor.Top, null, beforeWater);
            EquipmentLabel.Create(canvasRoot, cam, _electrodeLeft.transform, "Anode (Carbon)", LabelAnchor.Top, new Vector2(-62f, 0f), upToAcid);
            EquipmentLabel.Create(canvasRoot, cam, _electrodeRight.transform, "Cathode (Carbon)", LabelAnchor.Top, new Vector2(62f, 0f), upToAcid);
            EquipmentLabel.Create(canvasRoot, cam, _acidBottle.transform, "Dil. H2SO4", LabelAnchor.Top, null, beforeAcid);
            EquipmentLabel.Create(canvasRoot, cam, _switchBox.transform, "Battery & Switch", LabelAnchor.Top, null, inLab);
            EquipmentLabel.Create(canvasRoot, cam, _tubeAnode.transform, "Test Tube (O2)", LabelAnchor.Top, new Vector2(-40f, 0f), tubesInPlay);
            EquipmentLabel.Create(canvasRoot, cam, _tubeCathode.transform, "Test Tube (H2)", LabelAnchor.Top, new Vector2(40f, 0f), tubesInPlay);
            EquipmentLabel.Create(canvasRoot, cam, _candle.transform, "Candle", LabelAnchor.Top, null, inLab);
        }

        private void SetStep(ElectrolysisStep s)
        {
            _step = s;
            foreach (var w in _wires) w.ShowTarget = s == ElectrolysisStep.Step5_SwitchOn && !_switchedOn;
            RefreshUI();
        }

        private void RefreshUI()
        {
            bool isIntro = _step == ElectrolysisStep.Intro;
            _hud.ShowIntro(isIntro);
            if (isIntro) return;
            bool isResults = _step == ElectrolysisStep.Step7_Results || _step == ElectrolysisStep.Completed;
            int idx = Mathf.Clamp((int)_step, 1, 6);
            _hud.SetProgress(idx, StepTitle(_step), StepDesc(_step), isResults);
        }

        private string StepTitle(ElectrolysisStep s)
        {
            switch (s)
            {
                case ElectrolysisStep.Step1_PlaceElectrodes: return "Step 1: Fix the Electrodes";
                case ElectrolysisStep.Step2_FillWater: return "Step 2: Fill with Water";
                case ElectrolysisStep.Step3_AddAcid: return "Step 3: Add Dilute Sulphuric Acid";
                case ElectrolysisStep.Step4_PlaceTestTubes: return "Step 4: Place the Test Tubes";
                case ElectrolysisStep.Step5_SwitchOn: return "Step 5: Connect Wires & Switch On";
                case ElectrolysisStep.Step6_TestGases: return "Step 6: Test the Gases";
                case ElectrolysisStep.Step7_Results: return "Results & Observations";
                default: return "";
            }
        }

        private string StepDesc(ElectrolysisStep s)
        {
            switch (s)
            {
                case ElectrolysisStep.Step1_PlaceElectrodes: return "Drag each carbon-electrode assembly into its hole in the base of the mug.";
                case ElectrolysisStep.Step2_FillWater: return "Drag the water jug and pour it over the mug until the electrodes are immersed.";
                case ElectrolysisStep.Step3_AddAcid: return "Drag the dilute sulphuric acid bottle over the mug to add a few drops.";
                case ElectrolysisStep.Step4_PlaceTestTubes: return "Drag each water-filled test tube and invert it over an electrode.";
                case ElectrolysisStep.Step5_SwitchOn: return "Drag each coloured wire clip onto its electrode (the glowing dot), then tap the battery box.";
                case ElectrolysisStep.Step6_TestGases: return "Tap a test tube to lift it out, then drag the burning candle to its mouth.";
                case ElectrolysisStep.Step7_Results: return "Tap Observation, Equation, or Result below to review what happened.";
                default: return "";
            }
        }
    }
}
