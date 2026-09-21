// ============================================================
// StandaloneMagnesiumExperiment.cs  —  AR Virtual Lab Standalone
// Full interactive educational experiment controller for the
// Magnesium Ribbon burning reaction: 2Mg + O2 -> 2MgO
// Optimized for Mobile Touch & Android Portrait Laboratory.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ARVirtualLab.Standalone
{
    public class StandaloneMagnesiumExperiment : MonoBehaviour
    {
        [Header("State Tracking")]
        public StandaloneState currentState = StandaloneState.Introduction;

        [Header("Equipment GameObjects")]
        public GameObject tableObject;
        public GameObject standObject;
        public GameObject burnerObject;
        public GameObject flameObject;
        public GameObject flameHeatZoneObject;
        public GameObject sandpaperObject;
        public GameObject ribbonObject;
        public GameObject tongsObject;
        public GameObject watchGlassObject;
        public GameObject mgoResidueObject;
        public GameObject mgoInGlassObject;

        [Header("Interaction Transforms")]
        public Transform tongsGripPoint;
        public Transform flamePoint;
        public Transform watchGlassCollectionPoint;

        [Header("VFX & Lighting")]
        public ParticleSystem burnParticles;
        public Light burnLight;

        [Header("UI Controller")]
        public StandaloneUIController uiController;

        // Interaction internal fields
        private GameObject _draggingObject = null;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private Vector3 _lastRibbonLocalPos;
        private float _cleaningProgress = 0f;
        private float _cleaningAccumulator = 0f;

        private Vector3 _ribbonStartPosLocal;
        private Vector3 _tongsStartPosLocal;
        private Quaternion _ribbonStartRotLocal;
        private Quaternion _tongsStartRotLocal;
        private Transform _originalRibbonParent;
        private Material _ribbonMaterial;

        // Flags
        private bool _ribbonSelected = false;
        private bool _ribbonCleaned = false;
        private bool _tongsHoldingRibbon = false;
        private bool _burnerOn = false;
        private bool _ribbonBurned = false;
        private bool _productCooled = false;
        private bool _productCollected = false;
        private bool _isHeatingOrBurning = false;

        private void Awake()
        {
            ValidateAndAutoAssignReferences();
            EnsureColliders();

            if (uiController != null)
            {
                if (uiController.startButton != null)
                    uiController.startButton.onClick.AddListener(StartButtonTriggered);
                if (uiController.uiResetButton != null)
                    uiController.uiResetButton.onClick.AddListener(ResetButtonTriggered);
                if (uiController.uiNextButton != null)
                    uiController.uiNextButton.onClick.AddListener(ManualNextStepAttempted);
            }
        }

        private void Start()
        {
            InitializeExperiment();
        }

        public void ValidateAndAutoAssignReferences()
        {
            if (tableObject == null) tableObject = transform.Find("Table")?.gameObject;
            if (sandpaperObject == null) sandpaperObject = transform.Find("Sandpaper")?.gameObject;
            if (burnerObject == null) burnerObject = transform.Find("BunsenBurner")?.gameObject;
            if (flameObject == null) flameObject = transform.Find("BunsenFlame")?.gameObject ?? burnerObject?.transform.Find("FlamePoint/BunsenFlame")?.gameObject ?? burnerObject?.transform.Find("BunsenFlame")?.gameObject;
            if (flameHeatZoneObject == null) flameHeatZoneObject = transform.Find("FlameHeatZone")?.gameObject ?? burnerObject?.transform.Find("HeatingZone")?.gameObject;
            if (watchGlassObject == null) watchGlassObject = transform.Find("WatchGlass")?.gameObject;
            if (tongsObject == null) tongsObject = transform.Find("Tongs")?.gameObject;
            if (ribbonObject == null) ribbonObject = transform.Find("MagnesiumRibbon")?.gameObject;
            if (mgoResidueObject == null) mgoResidueObject = transform.Find("MgOResidue")?.gameObject ?? tongsObject?.transform.Find("MgOResidue")?.gameObject ?? tongsGripPoint?.Find("MgOResidue")?.gameObject;
            if (mgoInGlassObject == null) mgoInGlassObject = transform.Find("MgOPowderInWatchGlass")?.gameObject ?? watchGlassObject?.transform.Find("CollectionPoint/MgOPowderInWatchGlass")?.gameObject ?? watchGlassObject?.transform.Find("MgOPowderInWatchGlass")?.gameObject;
            if (standObject == null) standObject = transform.Find("SupportStand")?.gameObject;
            
            if (tongsGripPoint == null && tongsObject != null)
                tongsGripPoint = tongsObject.transform.Find("GripPoint");

            if (flamePoint == null && burnerObject != null)
                flamePoint = burnerObject.transform.Find("FlamePoint");

            if (watchGlassCollectionPoint == null && watchGlassObject != null)
                watchGlassCollectionPoint = watchGlassObject.transform.Find("CollectionPoint");

            if (burnParticles == null) burnParticles = GetComponentInChildren<ParticleSystem>(true);
            if (burnLight == null) burnLight = GetComponentInChildren<Light>(true);
            if (uiController == null) uiController = FindFirstObjectByType<StandaloneUIController>();
        }

        private void EnsureColliders()
        {
            EnsureColliderOn(ribbonObject, typeof(BoxCollider), new Vector3(0.06f, 0.04f, 0.08f), StandaloneObjectType.MagnesiumRibbon);
            EnsureColliderOn(tongsObject, typeof(CapsuleCollider), Vector3.zero, StandaloneObjectType.Tongs);
            EnsureColliderOn(sandpaperObject, typeof(BoxCollider), new Vector3(0.08f, 0.02f, 0.08f), StandaloneObjectType.Sandpaper);
            EnsureColliderOn(burnerObject, typeof(CapsuleCollider), Vector3.zero, StandaloneObjectType.BunsenBurner);
            EnsureColliderOn(watchGlassObject, typeof(SphereCollider), Vector3.zero, StandaloneObjectType.WatchGlass);
        }

        private void EnsureColliderOn(GameObject go, System.Type colType, Vector3 customSize, StandaloneObjectType objType)
        {
            if (go == null) return;
            var mc = go.GetComponent<MeshCollider>();
            if (mc != null) Destroy(mc);

            Collider col = go.GetComponent(colType) as Collider;
            if (col == null)
            {
                col = (Collider)go.AddComponent(colType);
            }
            col.enabled = true;
            col.isTrigger = true;

            if (col is BoxCollider box && customSize != Vector3.zero)
            {
                box.size = customSize;
                box.center = Vector3.zero;
            }
            else if (col is CapsuleCollider cap && go == tongsObject)
            {
                cap.radius = 0.04f;
                cap.height = 0.16f;
                cap.direction = 2; // Z-axis along length
                cap.center = Vector3.zero;
            }
            else if (col is CapsuleCollider capBurner && go == burnerObject)
            {
                capBurner.radius = 0.04f;
                capBurner.height = 0.14f;
                capBurner.direction = 1; // Y-axis
                capBurner.center = new Vector3(0, 0.055f, 0);
            }
            else if (col is SphereCollider sph && go == watchGlassObject)
            {
                sph.radius = 0.045f;
                sph.center = new Vector3(0, 0.005f, 0);
            }

            var io = go.GetComponent<StandaloneInteractable>();
            if (io == null) io = go.AddComponent<StandaloneInteractable>();
            io.objectType = objType;
            io.isInteractable = true;
        }

        public void InitializeExperiment()
        {
            StopAllCoroutines();

            _ribbonSelected = false;
            _ribbonCleaned = false;
            _tongsHoldingRibbon = false;
            _burnerOn = false;
            _ribbonBurned = false;
            _productCooled = false;
            _productCollected = false;
            _isHeatingOrBurning = false;
            _cleaningProgress = 0f;
            _cleaningAccumulator = 0f;
            _draggingObject = null;

            // Start positions on table (Logical educational layout)
            _ribbonStartPosLocal = new Vector3(-0.10f, 0.006f, 0.03f);
            _ribbonStartRotLocal = Quaternion.identity;

            _tongsStartPosLocal = new Vector3(-0.02f, 0.008f, -0.06f);
            _tongsStartRotLocal = Quaternion.Euler(0f, 15f, 0f);

            if (_originalRibbonParent == null && ribbonObject != null)
                _originalRibbonParent = ribbonObject.transform.parent;

            ResetVisuals();

            currentState = StandaloneState.Introduction;
            if (uiController != null)
            {
                uiController.ShowPanel(uiController.introPanel, true);
                uiController.ShowPanel(uiController.experimentPanel, false);
                uiController.TogglePanelButtons(false, false, false);
            }

            Debug.Log("[StandaloneExperiment] Initialized to Introduction state.");
        }

        private void ResetVisuals()
        {
            if (tongsObject != null)
            {
                tongsObject.transform.SetParent(transform, false);
                tongsObject.transform.localPosition = _tongsStartPosLocal;
                tongsObject.transform.localRotation = _tongsStartRotLocal;
                var io = tongsObject.GetComponent<StandaloneInteractable>();
                if (io != null)
                {
                    io.ResetColor();
                    io.Highlight(false);
                    io.isInteractable = true;
                }
            }

            if (ribbonObject != null)
            {
                ribbonObject.transform.SetParent(_originalRibbonParent != null ? _originalRibbonParent : transform, false);
                ribbonObject.transform.localPosition = _ribbonStartPosLocal;
                ribbonObject.transform.localRotation = _ribbonStartRotLocal;
                ribbonObject.SetActive(true);
                var io = ribbonObject.GetComponent<StandaloneInteractable>();
                if (io != null)
                {
                    io.ResetColor();
                    io.Highlight(false);
                    io.isInteractable = true;
                }
                
                Renderer rend = ribbonObject.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    _ribbonMaterial = rend.material;
                    _ribbonMaterial.color = new Color(0.65f, 0.66f, 0.70f); // Dull grey initial coat
                    _ribbonMaterial.DisableKeyword("_EMISSION");
                    _ribbonMaterial.SetColor("_EmissionColor", Color.black);
                }
            }

            if (flameObject != null) flameObject.SetActive(false);
            if (mgoResidueObject != null) mgoResidueObject.SetActive(false);
            if (mgoInGlassObject != null) mgoInGlassObject.SetActive(false);

            if (uiController != null)
            {
                uiController.ShowPanel(uiController.observationPanel, false);
                uiController.ShowPanel(uiController.equationPanel, false);
                uiController.ShowPanel(uiController.resultPanel, false);
                uiController.ShowPanel(uiController.safetyPanel, false);
                uiController.ShowPanel(uiController.instructionsPanel, false);
            }

            StopBurnEffect();
        }

        // ── UI Button Actions ──────────────────────────────────────
        public void StartButtonTriggered()
        {
            if (uiController != null)
            {
                uiController.ShowPanel(uiController.introPanel, false);
                uiController.ShowPanel(uiController.experimentPanel, true);
            }
            currentState = StandaloneState.Ready;
            UpdateStepUI();
            Debug.Log("[StandaloneExperiment] Started! Ready to select Magnesium ribbon.");
        }

        public void ResetButtonTriggered()
        {
            InitializeExperiment();
            StartButtonTriggered();
            uiController?.ShowFeedback("Experiment reset.", true);
            Debug.Log("[StandaloneExperiment] Reset completed.");
        }

        public void ManualNextStepAttempted()
        {
            switch (currentState)
            {
                case StandaloneState.Introduction:
                case StandaloneState.Ready:
                    uiController?.ShowFeedback("Tap the Magnesium Ribbon to select it!", false);
                    break;
                case StandaloneState.CleaningRibbon:
                    uiController?.ShowFeedback("Drag the ribbon onto the Sandpaper and rub it to clean off the oxide layer!", false);
                    break;
                case StandaloneState.RibbonReady:
                    uiController?.ShowFeedback("Tap the Tongs to grip the cleaned magnesium ribbon!", false);
                    break;
                case StandaloneState.TongsReady:
                    uiController?.ShowFeedback("Tap the Bunsen burner to turn it ON!", false);
                    break;
                case StandaloneState.BurnerPreparation:
                    uiController?.ShowFeedback("Move the tongs into the flame.", true);
                    break;
                case StandaloneState.Heating:
                    uiController?.ShowFeedback("Heating magnesium ribbon...", false);
                    break;
                case StandaloneState.Burning:
                    uiController?.ShowFeedback("Magnesium is burning in air with an intense white flame!", false);
                    break;
                case StandaloneState.Cooling:
                    uiController?.ShowFeedback("Wait for the Magnesium Oxide (MgO) to cool down!", false);
                    break;
                case StandaloneState.Collection:
                    uiController?.ShowFeedback("Drag the tongs over the watch glass to collect the white powder!", false);
                    break;
                case StandaloneState.Completed:
                    uiController?.ShowFeedback("Experiment completed! View Observation, Equation, and Results.", true);
                    break;
            }
        }

        // ── Input & Drag Handlers ──────────────────────────────────
        private void Update()
        {
            HandleInput();
        }

        private void HandleInput()
        {
            if (_isHeatingOrBurning) return; // Prevent movement during heating/burning

            Vector3 inputPos = Vector3.zero;
            bool isDown = false;
            bool isHeld = false;
            bool isUp = false;
            bool hasPointer = false;

            // 1. Touch Input (Android & Mobile)
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                inputPos = t.position;
                hasPointer = true;
                if (t.phase == TouchPhase.Began) isDown = true;
                else if (t.phase == TouchPhase.Moved || t.phase == TouchPhase.Stationary) isHeld = true;
                else if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled) isUp = true;
            }
            // 2. Mouse Input (Unity Editor & Desktop)
            else if (Input.mousePresent || Input.GetMouseButtonDown(0) || Input.GetMouseButton(0) || Input.GetMouseButtonUp(0))
            {
                inputPos = Input.mousePosition;
                hasPointer = true;
                if (Input.GetMouseButtonDown(0)) isDown = true;
                else if (Input.GetMouseButton(0)) isHeld = true;
                else if (Input.GetMouseButtonUp(0)) isUp = true;
            }

            if (!hasPointer) return;

            // UI Raycast Blocker
            if (isDown && EventSystem.current != null)
            {
                var pointerData = new PointerEventData(EventSystem.current) { position = inputPos };
                var results = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointerData, results);
                foreach (var r in results)
                {
                    if (r.gameObject.GetComponentInParent<UnityEngine.UI.Button>() != null ||
                        r.gameObject.GetComponentInParent<UnityEngine.UI.ScrollRect>() != null)
                    {
                        return;
                    }
                }
            }

            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam == null) return;

            // 1. Raycast Tap / Selection
            if (isDown)
            {
                Ray ray = cam.ScreenPointToRay(inputPos);
                RaycastHit[] hits = Physics.RaycastAll(ray, 10f);
                if (hits.Length > 0)
                {
                    System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
                    foreach (var hit in hits)
                    {
                        var io = hit.collider.GetComponentInParent<StandaloneInteractable>();
                        if (io != null && io.isInteractable && io.gameObject.activeInHierarchy)
                        {
                            OnObjectTapped(io);
                            break;
                        }
                    }
                }
            }

            // 2. Object Dragging (Smooth table plane movement)
            if (isHeld && _draggingObject != null)
            {
                Ray ray = cam.ScreenPointToRay(inputPos);
                float enter;
                if (_dragPlane.Raycast(ray, out enter))
                {
                    Vector3 worldPoint = ray.GetPoint(enter);
                    Vector3 localPoint = transform.InverseTransformPoint(worldPoint);
                    Vector3 targetLocalPos = localPoint + _dragOffset;
                    
                    // Keep height stable on table
                    targetLocalPos.y = _draggingObject.transform.localPosition.y;
                    targetLocalPos.x = Mathf.Clamp(targetLocalPos.x, -0.22f, 0.22f);
                    targetLocalPos.z = Mathf.Clamp(targetLocalPos.z, -0.16f, 0.16f);
                    
                    _draggingObject.transform.localPosition = targetLocalPos;

                    OnObjectDragged();
                }
            }

            // 3. Release Dragging
            if (isUp)
            {
                if (_draggingObject != null)
                {
                    var io = _draggingObject.GetComponent<StandaloneInteractable>();
                    if (io != null) io.Highlight(false);
                    _draggingObject = null;
                }
            }
        }

        private void OnObjectTapped(StandaloneInteractable io)
        {
            switch (io.objectType)
            {
                case StandaloneObjectType.MagnesiumRibbon:
                    if (_tongsHoldingRibbon && tongsObject != null)
                    {
                        // Redirect selection to tongs
                        var tongsIO = tongsObject.GetComponent<StandaloneInteractable>();
                        if (tongsIO != null) OnObjectTapped(tongsIO);
                        return;
                    }

                    Debug.Log("Ribbon tapped");
                    if (currentState == StandaloneState.Introduction || currentState == StandaloneState.Ready)
                    {
                        if (currentState == StandaloneState.Introduction) StartButtonTriggered();
                        _ribbonSelected = true;
                        io.Highlight(true);
                        Debug.Log("Ribbon selected");
                        uiController?.ShowFeedback("Ribbon selected. Rub it on the Sandpaper.", true);
                        currentState = StandaloneState.CleaningRibbon;
                        _lastRibbonLocalPos = ribbonObject.transform.localPosition;
                        UpdateStepUI();
                        
                        StartDragging(ribbonObject);
                    }
                    else if (currentState == StandaloneState.CleaningRibbon)
                    {
                        Debug.Log("Ribbon selected");
                        StartDragging(ribbonObject);
                    }
                    break;

                case StandaloneObjectType.Tongs:
                    Debug.Log("Tongs tapped");
                    if (currentState == StandaloneState.RibbonReady)
                    {
                        AttachRibbonToTongs();
                    }
                    else if (_tongsHoldingRibbon && (currentState == StandaloneState.BurnerPreparation || currentState == StandaloneState.Collection || currentState == StandaloneState.TongsReady))
                    {
                        Debug.Log("Tongs selected");
                        StartDragging(tongsObject);
                    }
                    else if (currentState == StandaloneState.Introduction || currentState == StandaloneState.Ready)
                    {
                        uiController?.ShowFeedback("Select and clean the Magnesium Ribbon with sandpaper first!", false);
                    }
                    break;

                case StandaloneObjectType.BunsenBurner:
                    Debug.Log("Burner tapped");
                    ToggleBurner();
                    break;

                case StandaloneObjectType.Sandpaper:
                    if (currentState == StandaloneState.Ready || currentState == StandaloneState.Introduction)
                    {
                        if (currentState == StandaloneState.Introduction) StartButtonTriggered();
                        if (ribbonObject != null)
                        {
                            var ribbonIO = ribbonObject.GetComponent<StandaloneInteractable>();
                            if (ribbonIO != null) OnObjectTapped(ribbonIO);
                        }
                    }
                    break;
            }
        }

        private void StartDragging(GameObject obj)
        {
            _draggingObject = obj;
            Vector3 planeNormal = transform.up;
            _dragPlane = new Plane(planeNormal, obj.transform.position);

            Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                Vector3 inputPos = Input.touchCount > 0 ? (Vector3)Input.GetTouch(0).position : Input.mousePosition;
                Ray ray = cam.ScreenPointToRay(inputPos);
                float enter;
                if (_dragPlane.Raycast(ray, out enter))
                {
                    Vector3 hitPoint = ray.GetPoint(enter);
                    Vector3 localHitPoint = transform.InverseTransformPoint(hitPoint);
                    _dragOffset = obj.transform.localPosition - localHitPoint;
                }
                else
                {
                    _dragOffset = Vector3.zero;
                }
            }

            var io = obj.GetComponent<StandaloneInteractable>();
            if (io != null) io.Highlight(true);
        }

        private void OnObjectDragged()
        {
            // 1. Cleaning ribbon rubbing calculation
            if (currentState == StandaloneState.CleaningRibbon)
            {
                Vector3 spPos = sandpaperObject != null ? sandpaperObject.transform.localPosition : new Vector3(-0.14f, 0.003f, 0.05f);
                float distToSandpaper = Vector3.Distance(
                    new Vector3(ribbonObject.transform.localPosition.x, 0, ribbonObject.transform.localPosition.z),
                    new Vector3(spPos.x, 0, spPos.z)
                );

                if (distToSandpaper < 0.12f)
                {
                    float movement = Vector3.Distance(ribbonObject.transform.localPosition, _lastRibbonLocalPos);
                    if (movement > 0.0008f)
                    {
                        _cleaningAccumulator += movement;
                        _cleaningProgress = Mathf.Min(100f, (_cleaningAccumulator / 0.22f) * 100f);
                        
                        uiController?.SetStepInfo(
                            "Step 1: Clean Magnesium Ribbon",
                            $"Rub the ribbon against sandpaper to remove magnesium oxide layer.\nCleaning: {Mathf.RoundToInt(_cleaningProgress)}%",
                            "Step 1 / 8"
                        );

                        if (_ribbonMaterial != null)
                        {
                            // Shiny polished metallic silver as cleaned
                            _ribbonMaterial.color = Color.Lerp(
                                new Color(0.65f, 0.66f, 0.70f),
                                new Color(0.92f, 0.94f, 0.98f),
                                _cleaningProgress / 100f
                            );
                            _ribbonMaterial.SetFloat("_Smoothness", Mathf.Lerp(0.55f, 0.90f, _cleaningProgress / 100f));
                        }

                        if (_cleaningProgress >= 100f)
                        {
                            FinishCleaning();
                        }
                    }
                }
                _lastRibbonLocalPos = ribbonObject.transform.localPosition;
            }
            
            // 2. Heating zone trigger & Snap Assist
            else if (currentState == StandaloneState.BurnerPreparation && _tongsHoldingRibbon && _burnerOn)
            {
                Vector3 targetFlamePos = burnerObject != null ? burnerObject.transform.localPosition + new Vector3(0f, 0.11f, 0f) : new Vector3(0.12f, 0.11f, 0.06f);
                float distToFlame = Vector3.Distance(tongsObject.transform.localPosition, targetFlamePos);

                if (distToFlame < 0.14f) // Snap assist radius
                {
                    Debug.Log("Tongs entered FlameHeatZone");
                    
                    // Snap assist: Guide and position tongs holding ribbon firmly into flame
                    _draggingObject = null;
                    tongsObject.transform.localPosition = targetFlamePos + new Vector3(-0.065f, -0.015f, 0f);
                    tongsObject.transform.localRotation = Quaternion.Euler(0f, 25f, 25f);
                    
                    var io = tongsObject.GetComponent<StandaloneInteractable>();
                    if (io != null) io.Highlight(false);

                    uiController?.ShowFeedback("Ribbon is in the flame.", true);
                    StartCoroutine(TriggerHeatingAndBurning());
                }
            }
            
            // 3. MgO Collection trigger checking
            else if (currentState == StandaloneState.Collection && _productCooled && !_productCollected)
            {
                if (watchGlassObject != null && mgoResidueObject != null)
                {
                    Vector3 wgPos = watchGlassObject.transform.localPosition;
                    float dist = Vector3.Distance(tongsObject.transform.localPosition, wgPos);
                    if (dist < 0.12f)
                    {
                        // Snap tongs over watch glass
                        _draggingObject = null;
                        tongsObject.transform.localPosition = wgPos + new Vector3(0f, 0.035f, 0f);
                        tongsObject.transform.localRotation = Quaternion.Euler(0f, 0f, 45f);

                        var io = tongsObject.GetComponent<StandaloneInteractable>();
                        if (io != null) io.Highlight(false);
                        
                        CompleteCollection();
                    }
                }
            }
        }

        // ── State Processors ───────────────────────────────────────
        private void FinishCleaning()
        {
            _ribbonCleaned = true;
            currentState = StandaloneState.RibbonReady;
            _draggingObject = null;

            ribbonObject.transform.localPosition = _ribbonStartPosLocal;
            var io = ribbonObject.GetComponent<StandaloneInteractable>();
            if (io != null)
            {
                io.Highlight(false);
                io.isInteractable = false;
            }

            uiController?.ShowFeedback("Magnesium ribbon cleaned and ready for heating. Tap the Tongs.", true);
            UpdateStepUI();
            Debug.Log("[StandaloneExperiment] Ribbon cleaned successfully.");
        }

        private void AttachRibbonToTongs()
        {
            _tongsHoldingRibbon = true;
            currentState = StandaloneState.TongsReady;

            if (ribbonObject != null && tongsObject != null)
            {
                Transform parentTarget = tongsGripPoint != null ? tongsGripPoint : tongsObject.transform;
                ribbonObject.transform.SetParent(parentTarget, false);
                ribbonObject.transform.localPosition = new Vector3(0f, 0f, 0.022f);
                ribbonObject.transform.localRotation = Quaternion.identity;
                ribbonObject.SetActive(true);

                var ioRibbon = ribbonObject.GetComponent<StandaloneInteractable>();
                if (ioRibbon != null) ioRibbon.Highlight(false);
            }

            uiController?.ShowFeedback("Tongs gripping ribbon. Tap the Bunsen Burner to light it.", true);
            UpdateStepUI();
            Debug.Log("[StandaloneExperiment] Ribbon attached to tongs.");
        }

        private void ToggleBurner()
        {
            if (_isHeatingOrBurning)
            {
                uiController?.ShowFeedback("Cannot turn burner off during active heating or combustion!", false);
                return;
            }

            _burnerOn = !_burnerOn;
            if (flameObject != null)
            {
                flameObject.SetActive(_burnerOn);
            }

            if (_burnerOn)
            {
                Debug.Log("Burner ON");
                if (currentState == StandaloneState.TongsReady)
                {
                    currentState = StandaloneState.BurnerPreparation;
                }
                uiController?.ShowFeedback("Bunsen burner ON. Move ribbon into the flame.", true);
                UpdateStepUI();
            }
            else
            {
                Debug.Log("Burner OFF");
                if (currentState == StandaloneState.BurnerPreparation)
                {
                    currentState = StandaloneState.TongsReady;
                }
                uiController?.ShowFeedback("Bunsen burner OFF", true);
                UpdateStepUI();
            }
        }

        private IEnumerator TriggerHeatingAndBurning()
        {
            _isHeatingOrBurning = true;

            currentState = StandaloneState.Heating;
            UpdateStepUI();
            uiController?.ShowFeedback("Heating the magnesium ribbon...", true);
            Debug.Log("Heating started");

            // Gradual heating effect: ribbon glows orange/yellow
            float t = 0f;
            while (t < 1.8f)
            {
                t += Time.deltaTime;
                float factor = t / 1.8f;
                if (_ribbonMaterial != null)
                {
                    _ribbonMaterial.EnableKeyword("_EMISSION");
                    _ribbonMaterial.SetColor("_EmissionColor", new Color(1f, 0.40f, 0.05f) * factor * 2.5f);
                }
                yield return null;
            }

            // Transition to intense white burning reaction
            currentState = StandaloneState.Burning;
            UpdateStepUI();
            uiController?.ShowFeedback("Magnesium is burning in air with an intense white flame!", true);
            Debug.Log("Burning started");

            if (burnParticles != null) burnParticles.Play();
            if (burnLight != null) burnLight.enabled = true;

            float elapsed = 0f;
            float duration = 4.0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float glow = 4f + Mathf.PingPong(elapsed * 15f, 6f);
                if (burnLight != null) burnLight.intensity = glow;

                if (_ribbonMaterial != null)
                {
                    _ribbonMaterial.SetColor("_EmissionColor", Color.white * glow);
                }
                yield return null;
            }

            StopBurnEffect();
            Debug.Log("Burning completed");

            // Form magnesium oxide: transform original ribbon appearance into white MgO residue
            if (ribbonObject != null) ribbonObject.SetActive(false);
            if (mgoResidueObject != null)
            {
                mgoResidueObject.SetActive(true);
                Transform parentTarget = tongsGripPoint != null ? tongsGripPoint : tongsObject.transform;
                mgoResidueObject.transform.SetParent(parentTarget, false);
                mgoResidueObject.transform.localPosition = Vector3.zero;
                mgoResidueObject.transform.localRotation = Quaternion.identity;
            }

            currentState = StandaloneState.Cooling;
            UpdateStepUI();
            uiController?.ShowFeedback("Magnesium oxide has formed. Allow the product to cool before collection.", true);
            Debug.Log("[StandaloneExperiment] Product cooling...");

            yield return new WaitForSeconds(2.5f);
            
            _isHeatingOrBurning = false;
            _productCooled = true;
            currentState = StandaloneState.Collection;
            UpdateStepUI();
            uiController?.ShowFeedback("Product cooled. Collect the magnesium oxide in the watch glass.", true);
            Debug.Log("[StandaloneExperiment] Product cooled.");
        }

        private void CompleteCollection()
        {
            _productCollected = true;
            
            if (mgoResidueObject != null) mgoResidueObject.SetActive(false);
            if (mgoInGlassObject != null) mgoInGlassObject.SetActive(true);

            Debug.Log("MgO collected");
            Debug.Log("Experiment completed");

            uiController?.ShowFeedback("Magnesium oxide collected.", true);

            currentState = StandaloneState.Completed;
            
            if (uiController != null)
            {
                uiController.TogglePanelButtons(true, true, true);
                uiController.ToggleNextButton(false);
                uiController.ShowPanel(uiController.observationPanel, true);
            }
            UpdateStepUI();
        }

        private void UpdateStepUI()
        {
            if (uiController == null) return;

            string title = "";
            string desc = "";
            string prog = "";

            switch (currentState)
            {
                case StandaloneState.Introduction:
                case StandaloneState.Ready:
                    title = "Step 1: Select Magnesium Ribbon";
                    desc = "Tap the dull grey magnesium ribbon lying on the table.";
                    prog = "Step 1 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.CleaningRibbon:
                    title = "Step 2: Clean Magnesium Ribbon";
                    desc = $"Rub the ribbon against sandpaper to remove the oxide layer.\nCleaning: {Mathf.RoundToInt(_cleaningProgress)}%";
                    prog = "Step 2 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.RibbonReady:
                    title = "Step 3: Hold with Tongs";
                    desc = "Magnesium ribbon cleaned and ready. Tap the tongs to grip the ribbon.";
                    prog = "Step 3 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.TongsReady:
                    title = "Step 4: Light Bunsen Burner";
                    desc = "Tap the Bunsen burner to turn it ON.";
                    prog = "Step 4 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.BurnerPreparation:
                    title = "Step 5: Move Ribbon into Flame";
                    desc = "Drag the tongs to hold the magnesium ribbon inside the burner flame.";
                    prog = "Step 5 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.Heating:
                    title = "Step 6: Heating Ribbon";
                    desc = "Heating the magnesium ribbon in the flame...";
                    prog = "Step 6 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.Burning:
                    title = "Step 7: Magnesium Burning";
                    desc = "Magnesium is burning in air with an intense white flame!";
                    prog = "Step 7 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.Cooling:
                    title = "Step 8: Cooling Product";
                    desc = "Magnesium oxide has formed. Allow the product to cool before collection.";
                    prog = "Step 8 / 8";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.Collection:
                    title = "Step 9: Collect Magnesium Oxide";
                    desc = "Drag the tongs over the watch glass to collect the magnesium oxide.";
                    prog = "Step 9 / 9";
                    uiController.ToggleNextButton(false);
                    break;

                case StandaloneState.Completed:
                    title = "Experiment Completed!";
                    desc = "Magnesium oxide collected! View Observation, Chemical Equation (2Mg + O₂ → 2MgO), and Result.";
                    prog = "Complete";
                    uiController.ToggleNextButton(false);
                    break;
            }

            uiController.SetStepInfo(title, desc, prog);
        }

        private void StopBurnEffect()
        {
            if (burnParticles != null)
                burnParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            if (burnLight != null)
            {
                burnLight.intensity = 0f;
                burnLight.enabled = false;
            }
        }
    }
}
