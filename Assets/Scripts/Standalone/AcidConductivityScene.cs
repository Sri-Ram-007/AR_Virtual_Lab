// ============================================================
// AcidConductivityScene.cs  —  AR Virtual Lab
// Experiment: Do Solutions Conduct Electricity? (Class 10 Physical Science, Andhra Pradesh, Activity 2.8, page 48)
// Two nail electrodes fixed on a cork are dipped into a beaker wired to a 6-volt battery, a bulb and a switch.
// The bulb glows for dilute acids (which ionise and conduct) but stays dark for glucose and alcohol solutions
// (which do not ionise), even though both contain hydrogen — showing that "contains hydrogen" is not what makes
// a solution an acid; conducting a current by ionising in water is.
// Built with the shared light-theme kit (ExperimentHud, UiKit, EduTheme, ModelLoader, HintRing, CameraFit).
// ============================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ARVirtualLab.UI;
using ARVirtualLab.AppShell;

namespace ARVirtualLab.AcidConductivity
{
    public enum AcTag { None, ElectrodeAssembly, BottleHCl, BottleH2SO4, BottleGlucose, BottleAlcohol, SwitchObj, Beaker }

    public class AcInteractable : MonoBehaviour { public AcTag tag = AcTag.None; }

    public enum ConductStep
    {
        Intro = 0,
        Step1_PlaceElectrodes = 1,
        Step2_PourHCl = 2,
        Step3_SwitchOn = 3,
        Step4_TryOthers = 4,
        Step5_Results = 5,
        Completed = 6
    }

    [RequireComponent(typeof(AudioSource))]
    public class AcidConductivityScene : MonoBehaviour
    {
        private const float TABLE_TOP_Y = -0.032f;

        private ConductStep _step = ConductStep.Intro;
        private AudioSource _audio;
        private Camera _cam;
        private GameObject _table;
        private ExperimentHud _hud;
        private HintRing _hint;
        private bool _animating;
        private Vector2 _pointerDownScreen;
        private Camera _arStandaloneCam;

        private GameObject _beaker;
        private LiquidFill _beakerFill;
        private Vector3 _beakerCentre;                          // world position of the beaker cavity's centre axis
        private const float FillFrac = 0.50f;                  // how full the beaker gets (fraction of its inner height)
        private GameObject _stream;
        private readonly Dictionary<AcTag, Vector3> _bottleTipLocal = new Dictionary<AcTag, Vector3>();
        private readonly Dictionary<AcTag, LiquidFill> _bottleFill = new Dictionary<AcTag, LiquidFill>();   // solution inside a test tube
        private const float TubeFull = 0.72f;

        // student-made circuit
        private readonly List<CircuitWire> _wires = new List<CircuitWire>();
        private CircuitWire _dragWire;
        private Plane _wirePlane;
        private Vector3 _wireOffset;
        private readonly List<GameObject> _nails = new List<GameObject>();

        // emptying the beaker between solutions
        private GameObject _beakerContainer, _wasteJar;
        private LiquidFill _wasteFill;
        private Vector3 _beakerHome;
        private Vector3 _beakerSpoutLocal;
        private float _wasteLevel;
        private bool _beakerNeedsEmptying;

        // glow around a lit bulb
        private GameObject _bulbHalo;
        private Material _haloMat;
        private GameObject _electrodeAssembly;                 // cork + two nails, one object
        private GameObject _battery, _bulb, _bulbGlow, _switchObj;
        private Light _bulbLight;
        private readonly Dictionary<AcTag, GameObject> _bottles = new Dictionary<AcTag, GameObject>();
        private readonly Dictionary<AcTag, Vector3> _bottleOrigins = new Dictionary<AcTag, Vector3>();
        private readonly Dictionary<AcTag, bool> _bottleConducts = new Dictionary<AcTag, bool>
        {
            { AcTag.BottleHCl, true }, { AcTag.BottleH2SO4, true }, { AcTag.BottleGlucose, false }, { AcTag.BottleAlcohol, false }
        };
        private readonly Dictionary<AcTag, string> _bottleLabel = new Dictionary<AcTag, string>
        {
            { AcTag.BottleHCl, "Dilute Hydrochloric Acid" }, { AcTag.BottleH2SO4, "Dilute Sulphuric Acid" },
            { AcTag.BottleGlucose, "Glucose Solution" }, { AcTag.BottleAlcohol, "Alcohol Solution" }
        };

        private Vector3 _electrodeOrigin, _electrodeTarget;
        private readonly Vector3 POS_BEAKER = new Vector3(-0.03f, TABLE_TOP_Y, 0.07f);
        private readonly Vector3 POS_BATTERY = new Vector3(0.22f, TABLE_TOP_Y, -0.16f);
        private readonly Vector3 POS_BULB = new Vector3(0.03f, TABLE_TOP_Y, -0.22f);
        private readonly Vector3 POS_SWITCH = new Vector3(-0.19f, TABLE_TOP_Y, -0.19f);

        private float _beakerTopY;
        private bool _electrodePlaced, _switchOn;
        private AcTag _currentLiquid = AcTag.None;
        private int _testsCompleted;
        // dilute HCl is already tested in Steps 2-3; Step 4 repeats the test with the remaining three liquids
        private readonly List<AcTag> _testOrder = new List<AcTag> { AcTag.BottleH2SO4, AcTag.BottleGlucose, AcTag.BottleAlcohol };

        private Material _glassMat, _liquidMat, _metalMat;
        private readonly Dictionary<AcTag, Color> _liquidColor = new Dictionary<AcTag, Color>
        {
            { AcTag.BottleHCl, new Color(0.45f, 0.75f, 1.00f, 0.60f) },
            { AcTag.BottleH2SO4, new Color(0.98f, 0.85f, 0.30f, 0.60f) },
            { AcTag.BottleGlucose, new Color(0.55f, 0.88f, 0.45f, 0.60f) },
            { AcTag.BottleAlcohol, new Color(0.95f, 0.55f, 0.78f, 0.60f) }
        };

        private GameObject _draggingObject;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private GameObject _electrodeZone, _pourZone;

        // ═════════════════════════════════════════════════════
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
            BuildScene();
            BuildUI();

            if (AppNavigation.LaunchDirectlyToAR)
            {
                AppNavigation.LaunchDirectlyToAR = false;
                SetStep(ConductStep.Step1_PlaceElectrodes);
                ToggleUnifiedAR();
            }
            else if (AppNavigation.LaunchDirectlyToExperiment)
            {
                AppNavigation.LaunchDirectlyToExperiment = false;
                SetStep(ConductStep.Step1_PlaceElectrodes);
            }
            else SetStep(ConductStep.Intro);
        }

        private void Update()
        {
            UpdateHint();
            CameraFit.Apply(_cam, 48f, 1.40f);
            UpdateInteraction();
            UpdateBulbHalo();
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
            _cam.enabled = true; _cam.cullingMask = -1;
            _cam.transform.position = new Vector3(0f, 1.05f, -0.84f);
            _cam.transform.rotation = Quaternion.Euler(51f, 0f, 0f);
            _cam.fieldOfView = 48f;
            _cam.backgroundColor = EduTheme.CameraBackground;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.nearClipPlane = 0.01f; _cam.farClipPlane = 100f;

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
            _glassMat = new Material(lit); ApplyTransparent(_glassMat, new Color(0.78f, 0.90f, 1.00f, 0.22f)); _glassMat.SetFloat("_Smoothness", 0.95f);
            _liquidMat = new Material(lit); ApplyTransparent(_liquidMat, new Color(0.80f, 0.92f, 1.0f, 0.70f)); _liquidMat.SetFloat("_Smoothness", 0.85f);
            _metalMat = Simple(new Color(0.75f, 0.76f, 0.78f), 0.75f, 0.85f);
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
            Box("LabTable", _table.transform, new Vector3(0f, -0.06f, 0.05f), new Vector3(1.20f, 0.04f, 0.70f), Simple(new Color(0.30f, 0.18f, 0.10f), 0.35f, 0.02f));
            Box("BenchSurface", _table.transform, new Vector3(0f, -0.038f, 0.05f), new Vector3(1.18f, 0.012f, 0.68f), Simple(new Color(0.42f, 0.26f, 0.15f), 0.45f, 0.02f));

            // ── Beaker ─────────────────────────────────────────
            var beakerContainer = new GameObject("Beaker");
            beakerContainer.transform.SetParent(transform, false);
            beakerContainer.transform.localPosition = POS_BEAKER;
            if (!ModelLoader.TryLoad(beakerContainer.transform, "GlassBeakerModel", out _beaker))
                _beaker = Cyl("Beaker_Proc", beakerContainer.transform, Vector3.zero, new Vector3(0.17f, 0.115f, 0.17f), _glassMat);
            else
            {
                ModelLoader.AutoScaleToHeight(_beaker, 0.23f);
                foreach (var r in _beaker.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = _glassMat;
            }
            AlignToTableSurface(beakerContainer, TABLE_TOP_Y);
            _beakerTopY = ModelLoader.GetTopY(_beaker);
            ModelLoader.FitCollider(beakerContainer);
            // the liquid is a lathe mesh fitted to the beaker's real interior, standing on its real floor
            _beakerFill = LiquidFill.Create(beakerContainer.transform, _beaker, _liquidMat);
            _beakerCentre = _beakerFill.CentreWorld(0.5f);
            Tag(beakerContainer, AcTag.Beaker);
            _beakerContainer = beakerContainer;
            _beakerHome = beakerContainer.transform.position;
            _beakerSpoutLocal = _beakerFill.SpoutLocal;

            // ── Waste jar: the student pours the previous solution out into it before testing the next one ──
            BuildWasteJar();

            // ── Electrode assembly: cork + two nails ─────────────
            _electrodeOrigin = new Vector3(0.17f, TABLE_TOP_Y, 0.12f);
            var elContainer = new GameObject("ElectrodeAssembly");
            elContainer.transform.SetParent(transform, false);
            elContainer.transform.localPosition = _electrodeOrigin;
            if (!ModelLoader.TryLoad(elContainer.transform, "RubberCorkStandModel", out _electrodeAssembly))
                _electrodeAssembly = Cyl("Cork_Proc", elContainer.transform, Vector3.zero, new Vector3(0.06f, 0.02f, 0.06f), Simple(new Color(0.76f, 0.54f, 0.32f), 0.25f, 0f));
            else ModelLoader.AutoScaleToWidth(_electrodeAssembly, 0.09f);
            AlignToTableSurface(elContainer, TABLE_TOP_Y);
            // two nail electrodes poking up through the cork
            for (int i = 0; i < 2; i++)
            {
                GameObject nail;
                if (ModelLoader.TryLoad(elContainer.transform, "NailElectrodeModel", out nail))
                {
                    ModelLoader.OrientLongestAxisUp(nail);             // stand it up if it was exported lying on its side
                    ModelLoader.AutoScaleToLongestAxis(nail, 0.10f);
                    nail.transform.localPosition = new Vector3(i == 0 ? -0.022f : 0.022f, 0f, 0f);
                    // sink the nail's point into the middle of the cork so it stands firmly in it
                    var corkB = ModelLoader.GetBounds(_electrodeAssembly);
                    nail.transform.position += new Vector3(0f, corkB.center.y - ModelLoader.GetBounds(nail).min.y, 0f);
                    _nails.Add(nail);
                }
            }
            AlignToTableSurface(elContainer, TABLE_TOP_Y);
            ModelLoader.FitCollider(elContainer, 0.012f);
            Tag(elContainer, AcTag.ElectrodeAssembly);
            _electrodeAssembly = elContainer;
            // Fixed pose: the cork stands on the FLOOR of the beaker with both nails rising through the liquid.
            float corkBottomRel = elContainer.transform.position.y - ModelLoader.GetBounds(elContainer).min.y;
            _electrodeTarget = new Vector3(_beakerCentre.x, _beakerFill.FloorWorldY + corkBottomRel + 0.001f, _beakerCentre.z);
            _electrodeZone = Zone("Zone_Electrode", new Vector3(_beakerCentre.x, _beakerTopY, _beakerCentre.z), new Vector3(0.16f, 0.18f, 0.16f));

            // ── Battery, bulb, switch (decorative circuit) ──────
            var batteryContainer = new GameObject("Battery");
            batteryContainer.transform.SetParent(transform, false);
            batteryContainer.transform.localPosition = POS_BATTERY;
            if (!ModelLoader.TryLoad(batteryContainer.transform, "SixVoltBatteryModel", out _battery))
                _battery = Box("Battery_Proc", batteryContainer.transform, Vector3.zero, new Vector3(0.10f, 0.05f, 0.05f), Simple(new Color(0.15f, 0.15f, 0.15f), 0.4f, 0.1f));
            else ModelLoader.AutoScaleToWidth(_battery, 0.115f);
            AlignToTableSurface(batteryContainer, TABLE_TOP_Y);

            var bulbContainer = new GameObject("Bulb");
            bulbContainer.transform.SetParent(transform, false);
            bulbContainer.transform.localPosition = POS_BULB;
            if (!ModelLoader.TryLoad(bulbContainer.transform, "LightBulbModel", out _bulb))
                _bulb = Cyl("Bulb_Proc", bulbContainer.transform, Vector3.zero, new Vector3(0.035f, 0.042f, 0.035f), Simple(Color.white, 0.9f, 0f));
            else ModelLoader.AutoScaleToHeight(_bulb, 0.085f);
            AlignToTableSurface(bulbContainer, TABLE_TOP_Y);
            foreach (var r in _bulb.GetComponentsInChildren<Renderer>(true))
            {
                var glowMat = new Material(r.sharedMaterial);
                glowMat.EnableKeyword("_EMISSION");
                glowMat.SetColor("_EmissionColor", Color.black);
                r.sharedMaterial = glowMat;
            }
            var blGO = new GameObject("BulbLight"); blGO.transform.SetParent(bulbContainer.transform, false);
            blGO.transform.localPosition = new Vector3(0f, ModelLoader.GetTopY(_bulb) - bulbContainer.transform.position.y, 0f);
            _bulbLight = blGO.AddComponent<Light>(); _bulbLight.type = LightType.Point; _bulbLight.range = 0.22f; _bulbLight.intensity = 0f;
            _bulbLight.color = new Color(1f, 0.92f, 0.65f);
            // A point light this close, at this scene's centimetre scale, blows out into a huge soft disc with no
            // tonemapping to compress it (confirmed by disabling it). The bulb's own emissive material already
            // reads clearly as "lit" without it, so the light stays off; UpdateBulb() only ever sets its intensity,
            // never re-enables it.
            _bulbLight.enabled = false;

            var switchContainer = new GameObject("Switch");
            switchContainer.transform.SetParent(transform, false);
            switchContainer.transform.localPosition = POS_SWITCH;
            if (!ModelLoader.TryLoad(switchContainer.transform, "ElectricalSwitchModel", out _switchObj))
                _switchObj = Box("Switch_Proc", switchContainer.transform, Vector3.zero, new Vector3(0.07f, 0.042f, 0.042f), Simple(new Color(0.6f, 0.1f, 0.1f), 0.5f, 0.1f));
            else ModelLoader.AutoScaleToWidth(_switchObj, 0.085f);
            AlignToTableSurface(switchContainer, TABLE_TOP_Y);
            ModelLoader.FitCollider(switchContainer);
            Tag(switchContainer, AcTag.SwitchObj);
            _switchObj = switchContainer;

            // ── The circuit, left for the student to complete: four leads, each fixed at one device and ending in a
            //    coloured clip that must be dragged to the next device. Battery(+) -> bulb -> switch -> nail A, and
            //    nail B -> battery(-), so the current runs through the solution between the two nails. ──
            var batB = ModelLoader.GetBounds(batteryContainer);
            var bulbB = ModelLoader.GetBounds(bulbContainer);
            var swB = ModelLoader.GetBounds(switchContainer);
            Vector3 batPlus  = new Vector3(batB.min.x + batB.size.x * 0.28f, batB.max.y - 0.004f, batB.center.z);
            Vector3 batMinus = new Vector3(batB.min.x + batB.size.x * 0.72f, batB.max.y - 0.004f, batB.center.z);
            Vector3 bulbIn   = new Vector3(bulbB.center.x - 0.012f, bulbB.min.y + bulbB.size.y * 0.16f, bulbB.center.z);
            Vector3 bulbOut  = new Vector3(bulbB.center.x + 0.012f, bulbB.min.y + bulbB.size.y * 0.16f, bulbB.center.z);
            Vector3 swIn     = new Vector3(swB.min.x + swB.size.x * 0.18f, swB.min.y + swB.size.y * 0.30f, swB.center.z);
            Vector3 swOut    = new Vector3(swB.max.x - swB.size.x * 0.18f, swB.min.y + swB.size.y * 0.30f, swB.center.z);
            float restY = TABLE_TOP_Y + 0.011f;
            _wires.Add(CircuitWire.Create(transform, "Red", new Color(0.86f, 0.15f, 0.15f), () => batPlus, () => bulbIn,
                new Vector3(batPlus.x + 0.05f, restY, batPlus.z - 0.10f), TABLE_TOP_Y));
            _wires.Add(CircuitWire.Create(transform, "Orange", new Color(0.95f, 0.55f, 0.10f), () => bulbOut, () => swIn,
                new Vector3(bulbOut.x + 0.07f, restY, bulbOut.z - 0.07f), TABLE_TOP_Y));
            _wires.Add(CircuitWire.Create(transform, "Green", new Color(0.18f, 0.62f, 0.28f), () => swOut, () => NailTop(0),
                new Vector3(swOut.x - 0.06f, restY, swOut.z - 0.08f), TABLE_TOP_Y));
            _wires.Add(CircuitWire.Create(transform, "Blue", new Color(0.15f, 0.35f, 0.85f), () => batMinus, () => NailTop(1),
                new Vector3(batMinus.x + 0.09f, restY, batMinus.z - 0.07f), TABLE_TOP_Y));

            // ── Glow shown around a lit bulb ──
            BuildBulbHalo(bulbContainer);

            // ── Four bottles ─────────────────────────────────────
            BuildBottle(AcTag.BottleHCl, "DiluteHClBottleModel", new Vector3(-0.34f, TABLE_TOP_Y, -0.03f));
            BuildBottle(AcTag.BottleH2SO4, "SulfuricAcidBottleModel", new Vector3(-0.34f, TABLE_TOP_Y, 0.19f));
            BuildBottle(AcTag.BottleGlucose, null, new Vector3(0.32f, TABLE_TOP_Y, -0.03f));
            BuildBottle(AcTag.BottleAlcohol, null, new Vector3(0.32f, TABLE_TOP_Y, 0.19f));
        }

        private void BuildWasteJar()
        {
            float side = _beakerContainer.transform.TransformPoint(_beakerSpoutLocal).x < _beakerCentre.x ? -1f : 1f;   // jar goes on the spout's side
            _wasteJar = new GameObject("WasteJar");
            _wasteJar.transform.SetParent(transform, false);
            _wasteJar.transform.localPosition = new Vector3(_beakerCentre.x + side * 0.20f, TABLE_TOP_Y, _beakerCentre.z - 0.02f);
            var jarGlass = new Material(_glassMat);
            ApplyTransparent(jarGlass, new Color(0.78f, 0.82f, 0.88f, 0.30f));
            GameObject body;
            PitcherBuilder.BuildJar(_wasteJar.transform, jarGlass, out body);
            AlignToTableSurface(_wasteJar, TABLE_TOP_Y);
            ModelLoader.FitCollider(_wasteJar);
            var wasteMat = new Material(_liquidMat);
            ApplyTransparent(wasteMat, new Color(0.62f, 0.56f, 0.38f, 0.75f));
            _wasteFill = LiquidFill.Create(_wasteJar.transform, body, wasteMat);
        }

        private void BuildBottle(AcTag tag, string prefabName, Vector3 origin)
        {
            var container = new GameObject(tag.ToString());
            container.transform.SetParent(transform, false);
            container.transform.localPosition = origin;
            GameObject visual = null;
            bool isTube = false;
            if (!string.IsNullOrEmpty(prefabName) && ModelLoader.TryLoad(container.transform, prefabName, out visual))
                ModelLoader.AutoScaleToHeight(visual, 0.20f);
            else
            {
                // glucose / alcohol solutions come in glass test tubes (the same imported tube model the other experiments use)
                isTube = ModelLoader.TryLoad(container.transform, "TestTubeModel", out visual);
                if (isTube)
                {
                    ModelLoader.AutoScaleToHeight(visual, 0.20f);
                    foreach (var r in visual.GetComponentsInChildren<Renderer>(true))
                    {
                        var mats = new Material[r.sharedMaterials.Length];
                        for (int i = 0; i < mats.Length; i++) mats[i] = _glassMat;
                        r.sharedMaterials = mats;
                    }
                }
                else visual = Cyl("Bottle_Proc", container.transform, Vector3.zero, new Vector3(0.05f, 0.095f, 0.05f), Simple(_liquidColor[tag], 0.6f, 0f));
            }
            AlignToTableSurface(container, TABLE_TOP_Y);
            var bb = ModelLoader.GetBounds(container);
            _bottleTipLocal[tag] = container.transform.InverseTransformPoint(new Vector3(bb.center.x, bb.max.y, bb.center.z));
            ModelLoader.FitCollider(container, isTube ? 0.022f : 0.008f);       // thin tubes get a roomier touch target
            if (isTube)
            {
                var solMat = new Material(_liquidMat);
                ApplyTransparent(solMat, _liquidColor[tag]);
                var fill = LiquidFill.Create(container.transform, visual, solMat);
                fill.SetFraction(TubeFull);
                _bottleFill[tag] = fill;
            }
            Tag(container, tag);
            _bottles[tag] = container;
            _bottleOrigins[tag] = container.transform.position;
        }

        /// <summary>The top end of one of the two nails (where a lead clips on).</summary>
        private Vector3 NailTop(int i)
        {
            if (i >= _nails.Count || _nails[i] == null) return _electrodeAssembly.transform.position;
            var b = ModelLoader.GetBounds(_nails[i]);
            return new Vector3(b.center.x, b.max.y - 0.004f, b.center.z);
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
                int left = 0; foreach (var x in _wires) if (!x.Connected) left++;
                _hud.ShowFeedback(left == 0 ? "All four wires connected — the circuit is complete. Now tap the switch." : "Wire connected — " + left + " to go.");
            }
            else _hud.ShowFeedback("Drag the coloured clip onto the glowing dot of the same colour.");
        }

        private IEnumerator AutoConnectWires()
        {
            _animating = true;
            foreach (var w in _wires) { if (!w.Connected) { w.ConnectNow(); yield return new WaitForSeconds(0.3f); } }
            _hud.ShowFeedback("Wires connected — the circuit is complete.");
            _animating = false;
        }

        // ---- glow around a lit bulb (a point light would blow out at this scale, so a soft additive halo is used) ----
        private void BuildBulbHalo(GameObject bulbContainer)
        {
            const int N = 64;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    tex.SetPixel(x, y, new Color(1f, 0.85f, 0.4f, a));
                }
            tex.Apply();
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _haloMat = new Material(s);
            if (_haloMat.HasProperty("_BaseMap")) _haloMat.SetTexture("_BaseMap", tex);
            _haloMat.mainTexture = tex;
            if (_haloMat.HasProperty("_BaseColor")) _haloMat.SetColor("_BaseColor", Color.white);
            if (_haloMat.HasProperty("_Surface"))
            {
                _haloMat.SetFloat("_Surface", 1f); _haloMat.SetFloat("_Blend", 2f);   // additive
                _haloMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _haloMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
                _haloMat.SetInt("_ZWrite", 0);
                _haloMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                _haloMat.renderQueue = 3300;
            }
            _bulbHalo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _bulbHalo.name = "BulbHalo";
            _bulbHalo.transform.SetParent(transform, false);
            Destroy(_bulbHalo.GetComponent<Collider>());
            var r = _bulbHalo.GetComponent<Renderer>(); r.sharedMaterial = _haloMat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var b = ModelLoader.GetBounds(bulbContainer);
            _bulbHalo.transform.position = new Vector3(b.center.x, b.min.y + b.size.y * 0.62f, b.center.z);   // around the glass globe
            _bulbHalo.SetActive(false);
        }

        private void UpdateBulbHalo()
        {
            if (_bulbHalo == null) return;
            bool on = _bulbHalo.activeSelf;
            if (!on || _cam == null) return;
            _bulbHalo.transform.rotation = _cam.transform.rotation;
            float s = 0.20f + Mathf.Sin(Time.time * 9f) * 0.008f;
            _bulbHalo.transform.localScale = new Vector3(s, s, 1f);
        }

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

        private static void Tag(GameObject go, AcTag tag)
        {
            var it = go.GetComponent<AcInteractable>() ?? go.AddComponent<AcInteractable>();
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
                case ConductStep.Step1_PlaceElectrodes: t = _electrodeAssembly; break;
                case ConductStep.Step2_PourHCl: t = _bottles[AcTag.BottleHCl]; break;
                case ConductStep.Step3_SwitchOn: { var lw = NextLooseWire(); t = lw != null ? lw.Clip : _switchObj; } break;
                case ConductStep.Step4_TryOthers:
                    if (_beakerNeedsEmptying) t = _beakerContainer;                       // pour the old solution out first
                    else if (_testsCompleted < _testOrder.Count) t = _bottles[_testOrder[_testsCompleted]];
                    break;
            }
            _hint.Target(t);
            _hint.SetVisible(!_animating && _draggingObject == null && _dragWire == null);
        }

        private void UpdateInteraction()
        {
            if (ARVirtualLab.AR.ARExperimentMode.BlockSceneInput) return;
            if (_step == ConductStep.Intro || _step == ConductStep.Completed) return;

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
                if (_step == ConductStep.Step3_SwitchOn && !_switchOn && !clip.Wire.Connected) BeginWireDrag(clip.Wire, ray);
                else if (_step != ConductStep.Step3_SwitchOn) _hud.ShowFeedback("The wires get connected in Step 3.");
                return;
            }

            var hi = hit.collider.GetComponentInParent<AcInteractable>();
            if (hi == null) return;

            switch (_step)
            {
                case ConductStep.Step1_PlaceElectrodes:
                    if (hi.tag == AcTag.ElectrodeAssembly && !_electrodePlaced) StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case ConductStep.Step2_PourHCl:
                    if (hi.tag == AcTag.BottleHCl) StartDrag(RootOf(hi.gameObject), ray);
                    break;
                case ConductStep.Step3_SwitchOn:
                    if (hi.tag == AcTag.SwitchObj) SwitchOn();
                    break;
                case ConductStep.Step4_TryOthers:
                    if (_beakerNeedsEmptying)
                    {
                        if (hi.tag == AcTag.Beaker || hi.tag == AcTag.ElectrodeAssembly) StartDrag(_beakerContainer, ray);
                        else if (hi.tag >= AcTag.BottleHCl && hi.tag <= AcTag.BottleAlcohol)
                            _hud.ShowFeedback("The beaker still holds the last solution — drag the beaker to the waste jar and pour it out first.");
                    }
                    else if (_testsCompleted < _testOrder.Count && hi.tag == _testOrder[_testsCompleted]) StartDrag(RootOf(hi.gameObject), ray);
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

        // "close enough" is judged on screen, where the student is looking, so it works at any height/depth
        private bool NearPoint(GameObject obj, Vector3 world)
        {
            if (obj == null || _cam == null) return false;
            float lim = Screen.height * 0.20f;
            Vector2 z = _cam.WorldToScreenPoint(world);
            var b = ModelLoader.GetBounds(obj);
            return Vector2.Distance(_cam.WorldToScreenPoint(obj.transform.position), z) < lim
                || Vector2.Distance(_cam.WorldToScreenPoint(b.center), z) < lim;
        }

        private void PointerUp(Vector2 screen)
        {
            if (_dragWire != null) { FinishWireDrag(); return; }
            if (_draggingObject == null) return;
            var dropped = _draggingObject; _draggingObject = null;
            bool tapped = Vector2.Distance(screen, _pointerDownScreen) < 12f;
            Vector3 beakerTop = new Vector3(_beakerCentre.x, _beakerTopY, _beakerCentre.z);

            switch (_step)
            {
                case ConductStep.Step1_PlaceElectrodes:
                    if (tapped || NearPoint(dropped, beakerTop)) PlaceElectrode();
                    else { StartCoroutine(MoveRot(dropped.transform, _electrodeOrigin, Quaternion.identity, 0.3f)); _hud.ShowFeedback("Place the cork with the two nails into the beaker."); }
                    break;
                case ConductStep.Step2_PourHCl:
                    if (tapped || NearPoint(dropped, beakerTop)) PourLiquid(AcTag.BottleHCl);
                    else { StartCoroutine(MoveRot(dropped.transform, _bottleOrigins[AcTag.BottleHCl], Quaternion.identity, 0.3f)); _hud.ShowFeedback("Pour the dilute HCl into the beaker."); }
                    break;
                case ConductStep.Step4_TryOthers:
                    if (dropped == _beakerContainer)
                    {
                        Vector3 jarTop = new Vector3(_wasteJar.transform.position.x, ModelLoader.GetTopY(_wasteJar), _wasteJar.transform.position.z);
                        if (tapped || NearPoint(dropped, jarTop)) EmptyBeaker();
                        else { StartCoroutine(MoveRot(dropped.transform, _beakerHome, Quaternion.identity, 0.3f)); _hud.ShowFeedback("Carry the beaker over the waste jar to pour the old solution out."); }
                        break;
                    }
                    var tag = _testOrder[_testsCompleted];
                    if (tapped || NearPoint(dropped, beakerTop)) PourLiquid(tag);
                    else { StartCoroutine(MoveRot(dropped.transform, _bottleOrigins[tag], Quaternion.identity, 0.3f)); _hud.ShowFeedback("Pour " + _bottleLabel[tag] + " into the beaker."); }
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
        private void PlaceElectrode()
        {
            if (_animating) return;
            StartCoroutine(PlaceElectrodeRoutine());
        }

        private IEnumerator PlaceElectrodeRoutine()
        {
            _animating = true;
            yield return MoveRot(_electrodeAssembly.transform, _electrodeTarget + Vector3.up * 0.05f, Quaternion.identity, 0.3f);
            yield return MoveRot(_electrodeAssembly.transform, _electrodeTarget, Quaternion.identity, 0.25f);
            _electrodeAssembly.transform.SetParent(_beakerContainer.transform, true);   // from now on it travels with the beaker
            _electrodePlaced = true;
            _hud.ShowFeedback("Electrodes fixed in the beaker on their cork.");
            _animating = false;
            SetStep(ConductStep.Step2_PourHCl);
        }

        private void PourLiquid(AcTag tag)
        {
            if (_animating) return;
            if (_beakerNeedsEmptying)
            {
                _hud.ShowFeedback("The beaker still holds the last solution — pour it out into the waste jar first.");
                StartCoroutine(MoveRot(_bottles[tag].transform, _bottleOrigins[tag], Quaternion.identity, 0.3f));
                return;
            }
            StartCoroutine(PourLiquidRoutine(tag));
        }

        // Pour the used solution away into the waste jar so the next one is tested on its own.
        private void EmptyBeaker()
        {
            if (_animating || !_beakerNeedsEmptying) { if (!_animating) StartCoroutine(MoveRot(_beakerContainer.transform, _beakerHome, Quaternion.identity, 0.25f)); return; }
            StartCoroutine(EmptyBeakerRoutine());
        }

        private IEnumerator EmptyBeakerRoutine()
        {
            _animating = true;
            var bt = _beakerContainer.transform;
            // the spout side decides which way the beaker tips (the waste jar stands on that side)
            Vector3 spoutNow = bt.TransformPoint(_beakerSpoutLocal);
            float dir = spoutNow.x < _beakerCentre.x ? 1f : -1f;                  // +z rotation lowers the -x side
            Quaternion tilt = Quaternion.Euler(0f, 0f, 105f * dir);
            Vector3 jarTop = new Vector3(_wasteJar.transform.position.x, ModelLoader.GetTopY(_wasteJar), _wasteJar.transform.position.z);
            Vector3 lip = jarTop + Vector3.up * 0.035f;
            yield return MoveRot(bt, lip - tilt * _beakerSpoutLocal, tilt, 0.6f);

            float start = _beakerFill.Fraction;
            var liqMat = _beakerFill.CurrentMaterial;
            float e = 0f, dur = 1.3f;
            float wasteFrom = _wasteLevel;
            _wasteLevel = Mathf.Min(0.8f, _wasteLevel + 0.18f);
            while (e < dur)
            {
                e += Time.deltaTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                _beakerFill.SetFraction(Mathf.Lerp(start, 0f, k));
                _wasteFill.SetFraction(Mathf.Lerp(wasteFrom, _wasteLevel, k));
                ShowStream(liqMat != null ? liqMat : _liquidMat, lip, Mathf.Max(_wasteFill.LevelWorldY, _wasteFill.FloorWorldY));
                yield return null;
            }
            _beakerFill.SetFraction(0f);
            HideStream();

            yield return MoveRot(bt, _beakerHome, Quaternion.identity, 0.6f);
            _beakerNeedsEmptying = false;
            _currentLiquid = AcTag.None;
            UpdateBulb();
            _hud.ShowFeedback("Beaker emptied. Now pour the next solution in.");
            _animating = false;
            RefreshUI();
        }

        private IEnumerator PourLiquidRoutine(AcTag tag)
        {
            _animating = true;
            var bottle = _bottles[tag];
            var bt = bottle.transform;
            // tip the bottle towards the beaker so its mouth hangs just over the middle of it
            float side = _bottleOrigins[tag].x > _beakerCentre.x ? 1f : -1f;
            Quaternion tilt = Quaternion.Euler(0f, 0f, 105f * side);
            Vector3 lip = new Vector3(_beakerCentre.x, _beakerTopY + 0.05f, _beakerCentre.z);
            yield return MoveRot(bt, lip - tilt * _bottleTipLocal[tag], tilt, 0.5f);

            // empty the beaker of whatever was in it (the bulb goes dark), then fill with the new liquid
            _currentLiquid = AcTag.None;
            UpdateBulb();
            var liqMat = new Material(_liquidMat);
            ApplyTransparent(liqMat, _liquidColor[tag]);
            _beakerFill.SetMaterial(liqMat);
            float e = 0f, dur = 1.1f;
            while (e < dur)
            {
                e += Time.deltaTime;
                float kk = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(e / dur));
                _beakerFill.SetFraction(kk * FillFrac);
                if (_bottleFill.TryGetValue(tag, out var tf)) tf.SetFraction(Mathf.Lerp(TubeFull, 0.10f, kk));   // the tube runs low as it is poured
                ShowStream(liqMat, lip, _beakerFill.LevelWorldY);
                yield return null;
            }
            _beakerFill.SetFraction(FillFrac);
            HideStream();

            yield return MoveRot(bt, _bottleOrigins[tag], Quaternion.identity, 0.45f);
            if (_bottleFill.TryGetValue(tag, out var refill)) refill.SetFraction(TubeFull);
            _currentLiquid = tag;
            _animating = false;

            if (_step == ConductStep.Step2_PourHCl)
            {
                _hud.ShowFeedback("Dilute HCl poured into the beaker.");
                SetStep(ConductStep.Step3_SwitchOn);
            }
            else
            {
                UpdateBulb();
                _hud.ShowFeedback(_bottleLabel[tag] + ": bulb " + (_bottleConducts[tag] ? "glows - this solution conducts electricity!" : "stays dark - this solution does not conduct electricity."));
                _testsCompleted++;
                _beakerNeedsEmptying = _testsCompleted < _testOrder.Count;     // pour this one out before testing the next
                RefreshUI();
                if (_testsCompleted >= _testOrder.Count) { StartCoroutine(FinishAfterDelay()); }
            }
        }

        private IEnumerator FinishAfterDelay()
        {
            yield return new WaitForSeconds(1.5f);
            SetStep(ConductStep.Step5_Results);
        }

        // thin falling column of liquid between a pouring mouth and the surface below it
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
            _stream.transform.localScale = new Vector3(0.010f, len * 0.5f, 0.010f);
        }

        private void HideStream() { if (_stream != null) _stream.SetActive(false); }

        private void SwitchOn()
        {
            if (_animating || _switchOn) return;
            if (!AllWired) { _hud.ShowFeedback("The circuit is open — connect all four wires first."); return; }
            _switchOn = true;
            UpdateBulb();
            _hud.ShowFeedback(_bulbConductsCurrent() ? "The bulb glows! Dilute HCl conducts electricity." : "The bulb stays dark.");
            _beakerNeedsEmptying = true;                       // the HCl has to be poured away before the next solution is tried
            SetStep(ConductStep.Step4_TryOthers);
        }

        private bool _bulbConductsCurrent() => _currentLiquid != AcTag.None && _bottleConducts.TryGetValue(_currentLiquid, out bool c) && c;

        private void UpdateBulb()
        {
            // the bulb only lights when the loop is complete: wires clipped on, switch closed, and a conducting solution
            bool glow = _switchOn && AllWired && _electrodePlaced && _bulbConductsCurrent();
            _bulbLight.intensity = glow ? 1.4f : 0f;
            if (_bulbHalo != null) _bulbHalo.SetActive(glow);
            foreach (var r in _bulb.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", glow ? new Color(2.2f, 1.7f, 0.5f) : Color.black);
                // a lit bulb is plainly warm yellow even without a point light (which would blow out at this scale)
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", glow ? new Color(1f, 0.92f, 0.45f) : Color.white);
            }
        }

        // =========================================================
        //  Navigation, reset, AR
        // =========================================================
        public void NextStep()
        {
            if (_step == ConductStep.Intro) { SetStep(ConductStep.Step1_PlaceElectrodes); return; }
            if (_animating) return;
            switch (_step)
            {
                case ConductStep.Step1_PlaceElectrodes: PlaceElectrode(); return;
                case ConductStep.Step2_PourHCl: PourLiquid(AcTag.BottleHCl); return;
                case ConductStep.Step3_SwitchOn:
                    if (!AllWired) StartCoroutine(AutoConnectWires()); else SwitchOn();
                    return;
                case ConductStep.Step4_TryOthers:
                    if (_beakerNeedsEmptying) EmptyBeaker();
                    else if (_testsCompleted < _testOrder.Count) PourLiquid(_testOrder[_testsCompleted]);
                    return;
            }
        }

        public void PrevStep()
        {
            if (_step <= ConductStep.Step1_PlaceElectrodes) return;
            _step = (ConductStep)((int)_step - 1);
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
                DefaultScale = 2.6f,
                OnStarted = c => { _cam = c; },
                OnStopped = c => { _cam = c; }
            });
        }

        public void ResetExperiment()
        {
            StopAllCoroutines();
            _animating = false;

            _beakerContainer.transform.SetPositionAndRotation(_beakerHome, Quaternion.identity);
            _electrodeAssembly.transform.SetParent(transform, true);
            _electrodeAssembly.transform.SetPositionAndRotation(_electrodeOrigin, Quaternion.identity);
            foreach (var kv in _bottleOrigins) _bottles[kv.Key].transform.SetPositionAndRotation(kv.Value, Quaternion.identity);
            HideStream();
            foreach (var bf in _bottleFill.Values) bf.SetFraction(TubeFull);
            _beakerFill.SetFraction(0f);
            _wasteLevel = 0f; _wasteFill.SetFraction(0f);
            _beakerNeedsEmptying = false;
            foreach (var w in _wires) w.ResetWire();
            _dragWire = null;
            _switchOn = false;
            _currentLiquid = AcTag.None;
            UpdateBulb();

            _electrodePlaced = false;
            _testsCompleted = 0;
            _draggingObject = null;
            if (_hud != null) _hud.CloseAllModals();

            SetStep(ConductStep.Step1_PlaceElectrodes);
            _hud.ShowFeedback("Experiment reset. Apparatus returned to the starting state.");
        }

        // =========================================================
        //  UI
        // =========================================================
        private void BuildUI()
        {
            var canvasGO = new GameObject("AcidConductivityCanvas", typeof(RectTransform));
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<UnityEngine.UI.CanvasScaler>();
            scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            var root = canvasGO.GetComponent<RectTransform>();

            _hud = ExperimentHud.Build(this, root, new HudConfig
            {
                AppLabel = "DO SOLUTIONS CONDUCT ELECTRICITY?",
                IntroTitle = "Do Solutions Conduct Electricity?",
                IntroSubtitle = "Acids, Bases and Salts · Activity 2.8",
                Equation = "Acids ionise in water -> free ions -> current flows",
                OverviewSteps = new[]
                {
                    "Fix the two nail electrodes on their cork",
                    "Pour dilute HCl into the beaker",
                    "Connect the wires, switch on and watch the bulb",
                    "Pour the old solution out, then try sulphuric acid, glucose and alcohol",
                    "Compare the results",
                },
                StepCount = 4,
                SafetyBody = "LABORATORY SAFETY GUIDELINES:\n\n" +
                    "1. Handle dilute acids carefully - avoid skin and eye contact.\n\n" +
                    "2. Keep the battery circuit away from spilled liquid.\n\n" +
                    "3. Rinse the beaker before pouring in a different solution.\n\n" +
                    "4. This activity is an educational simulation only.",
                AboutBody = "DO SOLUTIONS CONDUCT ELECTRICITY?\n\n" +
                    "Subject: Class 10 Chemistry / Virtual Science Laboratory\n\n" +
                    "Concept: A solution conducts electricity only if it contains free ions. Acids ionise in water; glucose and alcohol do not, even though both contain hydrogen.\n\n" +
                    "Apparatus:\n  - 100 mL beaker, cork with two nail electrodes\n  - 6 volt battery, bulb, switch\n  - Dilute HCl, dilute H2SO4, glucose solution, alcohol",
                ObservationBody = "- The bulb glows brightly with dilute HCl and with dilute sulphuric acid.\n\n" +
                    "- The bulb stays dark with glucose solution and with alcohol solution.\n\n" +
                    "- Glucose and alcohol both contain hydrogen, yet neither conducts electricity - so containing hydrogen alone does not make a compound an acid.",
                EquationBody = "What Makes a Solution Conduct?\n\n" +
                    "Acids form ions when dissolved in water:\n  HCl(aq) -> H+(aq) + Cl-(aq)\n\n" +
                    "These free-moving ions carry the current through the solution, lighting the bulb.\n" +
                    "Glucose and alcohol dissolve as neutral molecules - no free ions form, so no current flows.",
                ResultBody = "EXPERIMENT CONCLUSION:\n\n" +
                    "1. Dilute HCl and dilute sulphuric acid conduct electricity - the bulb glows for both.\n\n" +
                    "2. Glucose solution and alcohol solution do NOT conduct electricity - the bulb stays dark for both.\n\n" +
                    "3. All acids conduct electricity in water because they release hydrogen ions (H+). This, not the presence of hydrogen atoms alone, is what defines an acid.",
                OnStart = () => SetStep(ConductStep.Step1_PlaceElectrodes),
                OnArToggle = ToggleUnifiedAR,
                OnPrev = PrevStep,
                OnNext = NextStep,
                OnReset = ResetExperiment,
            });

            BuildEquipmentLabels(root);
            CircuitWire.UiRoot = root;                 // wire target rings are drawn on this UI layer, above the 3D scene
            CircuitWire.UiCamera = () => _cam;
            ARVirtualLab.AI.AIMentorUI.Attach(this, ARVirtualLab.AI.MentorKnowledge.Acid,
                () => _step == ConductStep.Intro ? null : StepTitle(_step) + ". " + StepDesc(_step));
        }

        /// <summary>The small white name-tags floating over each piece of apparatus, matching the earlier experiments.</summary>
        private void BuildEquipmentLabels(RectTransform canvasRoot)
        {
            System.Func<Camera> cam = () => _cam;
            // Tags are small, only show while the lab is running (not over the intro / results screens), and the
            // beaker's own tag sits under it so it never covers the liquid or the electrodes inside.
            System.Func<bool> inLab = () => _step != ConductStep.Intro && _step != ConductStep.Step5_Results && _step != ConductStep.Completed
                                            && !_animating && _draggingObject == null;
            System.Func<bool> electrodesVisible = () => inLab() && _step <= ConductStep.Step2_PourHCl;

            EquipmentLabel.Create(canvasRoot, cam, _beaker.transform, "Beaker", LabelAnchor.Bottom, new Vector2(0f, -4f), inLab);
            EquipmentLabel.Create(canvasRoot, cam, _electrodeAssembly.transform, "Nail Electrodes", LabelAnchor.Top, null, electrodesVisible);
            EquipmentLabel.Create(canvasRoot, cam, _wasteJar.transform, "Waste Jar", LabelAnchor.Top, null, () => inLab() && _step == ConductStep.Step4_TryOthers);
            EquipmentLabel.Create(canvasRoot, cam, _battery.transform, "6V Battery", LabelAnchor.Top, null, inLab);
            EquipmentLabel.Create(canvasRoot, cam, _bulb.transform, "Bulb", LabelAnchor.Bottom, new Vector2(0f, -4f), inLab);
            EquipmentLabel.Create(canvasRoot, cam, _switchObj.transform, "Switch", LabelAnchor.Bottom, new Vector2(0f, -4f), inLab);
            EquipmentLabel.Create(canvasRoot, cam, _bottles[AcTag.BottleHCl].transform, "Dil. HCl", LabelAnchor.Top, null, inLab);
            EquipmentLabel.Create(canvasRoot, cam, _bottles[AcTag.BottleH2SO4].transform, "Dil. H2SO4", LabelAnchor.Top, null, inLab);
            EquipmentLabel.Create(canvasRoot, cam, _bottles[AcTag.BottleGlucose].transform, "Glucose", LabelAnchor.Top, null, inLab);
            EquipmentLabel.Create(canvasRoot, cam, _bottles[AcTag.BottleAlcohol].transform, "Alcohol", LabelAnchor.Top, null, inLab);
        }

        private void SetStep(ConductStep s)
        {
            _step = s;
            foreach (var w in _wires) w.ShowTarget = s == ConductStep.Step3_SwitchOn && !_switchOn;
            RefreshUI();
        }

        private void RefreshUI()
        {
            bool isIntro = _step == ConductStep.Intro;
            _hud.ShowIntro(isIntro);
            if (isIntro) return;
            bool isResults = _step == ConductStep.Step5_Results || _step == ConductStep.Completed;
            int idx = Mathf.Clamp((int)_step, 1, 4);
            _hud.SetProgress(idx, StepTitle(_step), StepDesc(_step), isResults);
        }

        private string StepTitle(ConductStep s)
        {
            switch (s)
            {
                case ConductStep.Step1_PlaceElectrodes: return "Step 1: Fix the Electrodes";
                case ConductStep.Step2_PourHCl: return "Step 2: Add Dilute HCl";
                case ConductStep.Step3_SwitchOn: return "Step 3: Connect the Circuit";
                case ConductStep.Step4_TryOthers: return "Step 4: Test Other Solutions";
                case ConductStep.Step5_Results: return "Results & Observations";
                default: return "";
            }
        }

        private string StepDesc(ConductStep s)
        {
            switch (s)
            {
                case ConductStep.Step1_PlaceElectrodes: return "Drag the cork with its two nail electrodes into the beaker.";
                case ConductStep.Step2_PourHCl: return "Drag the dilute HCl bottle over the beaker to pour it in.";
                case ConductStep.Step3_SwitchOn: return "Drag each coloured wire clip to the glowing dot of its colour, then tap the switch.";
                case ConductStep.Step4_TryOthers:
                    if (_beakerNeedsEmptying) return "Drag the beaker over the waste jar and pour the used solution out first.";
                    if (_testsCompleted < _testOrder.Count) return "Pour " + _bottleLabel[_testOrder[_testsCompleted]] + " into the beaker and see if the bulb glows.";
                    return "All solutions tested.";
                case ConductStep.Step5_Results: return "Tap Observation, Equation, or Result below to review what happened.";
                default: return "";
            }
        }
    }
}
