// ============================================================
// MagnesiumExperiment.cs  —  AR Virtual Lab
// Drives the 8-step interactive Magnesium Ribbon experiment.
// Implements full mobile touch drag/select interaction, state limits,
// burner ignition, flame heating zone triggers, and white residue collection.
// ============================================================
using System.Collections;
using UnityEngine;
using ARVirtualLab.Core;
using ARVirtualLab.UI;
using TMPro;

namespace ARVirtualLab.Experiments.MagnesiumRibbon
{
    [DisallowMultipleComponent]
    public class MagnesiumExperiment : MonoBehaviour, IExperiment
    {
        // ── IExperiment ───────────────────────────────────────────
        public string ExperimentName => "Burning Magnesium Ribbon";

        // ── Inspector ─────────────────────────────────────────────
        [Header("Lab Objects (auto-created if null)")]
        public GameObject tableObject;
        public GameObject watchGlassObject;
        public GameObject ribbonObject;
        public GameObject tongsObject;
        public GameObject burnerObject;
        public GameObject flameObject;
        public GameObject mgoObject;      // residue on tongs after burning
        public GameObject mgoInGlassObject; // collected residue in watch glass

        [Header("Particle System")]
        public ParticleSystem burnParticles;

        [Header("Light")]
        public Light burnLight;

        [Header("UI Refs")]
        public ARUIController uiController;

        [Header("Step Info Texts")]
        public TMP_Text stepDescriptionText;
        public TMP_Text equationText;
        public TMP_Text observationText;

        // ── State ─────────────────────────────────────────────────
        private ExperimentStep _currentStep = ExperimentStep.NotStarted;
        private bool _isCompleted = false;
        
        // Interactive Flags
        private bool _isTargetDetected = false;
        private bool _ribbonSelected = false;
        private bool _tongsHoldingRibbon = false;
        private bool _burnerOn = false;
        private bool _ribbonBurned = false;
        private bool _productCooled = false;
        private bool _productCollected = false;

        // Dragging & Interaction State
        private GameObject _draggingObject = null;
        private Plane _dragPlane;
        private Vector3 _dragOffset;
        private Vector3 _tongsStartPosLocal;
        private Vector3 _ribbonStartPosLocal;
        private Transform _originalRibbonParent;

        // Visual / Emission Material caching
        private Material _ribbonMat;

        // ── IExperiment Implementation ────────────────────────────
        public int  GetCurrentStep() => (int)_currentStep;
        public int  GetTotalSteps()  => 8; // Steps 1-8
        public bool IsCompleted()    => _isCompleted;

        public void Initialize()
        {
            Debug.Log("[MagnesiumExperiment] Initializing experiment state");
            _isCompleted = false;
            _currentStep = ExperimentStep.NotStarted;
            
            _ribbonSelected = false;
            _tongsHoldingRibbon = false;
            _burnerOn = false;
            _ribbonBurned = false;
            _productCooled = false;
            _productCollected = false;

            EnsureObjectsExist();
            
            // Set up original parent and positions for reset
            if (_originalRibbonParent == null && ribbonObject != null)
                _originalRibbonParent = ribbonObject.transform.parent;

            _tongsStartPosLocal = new Vector3(0f, 0.015f, -0.08f);
            _ribbonStartPosLocal = new Vector3(0f, 0.015f, 0.08f);

            ResetVisuals();
            UpdateStepText("Tap 'Start Experiment' in the menu to begin.");
        }

        private void ResetVisuals()
        {
            // Reset transforms
            if (tongsObject != null)
            {
                tongsObject.transform.SetParent(transform, false);
                tongsObject.transform.localPosition = _tongsStartPosLocal;
                tongsObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                var io = tongsObject.GetComponent<InteractableObject>();
                if (io != null) io.Drop();
            }

            if (ribbonObject != null)
            {
                ribbonObject.transform.SetParent(_originalRibbonParent, false);
                ribbonObject.transform.localPosition = _ribbonStartPosLocal;
                ribbonObject.transform.localRotation = Quaternion.identity;
                ribbonObject.SetActive(true);
                var io = ribbonObject.GetComponent<InteractableObject>();
                if (io != null)
                {
                    io.Drop();
                    io.isInteractable = true;
                }
                
                // Restore material color to original silver-grey
                if (_ribbonMat != null)
                {
                    _ribbonMat.color = new Color(0.82f, 0.82f, 0.84f);
                    _ribbonMat.DisableKeyword("_EMISSION");
                    _ribbonMat.SetColor("_EmissionColor", Color.black);
                }
            }

            // Hide/Show correct objects
            if (tableObject != null) tableObject.SetActive(true);
            if (watchGlassObject != null) watchGlassObject.SetActive(true);
            if (burnerObject != null) burnerObject.SetActive(true);

            if (flameObject != null) flameObject.SetActive(false);
            if (mgoObject != null) mgoObject.SetActive(false);
            if (mgoInGlassObject != null) mgoInGlassObject.SetActive(false);

            if (equationText != null) equationText.gameObject.SetActive(false);
            if (observationText != null) observationText.gameObject.SetActive(false);

            StopBurnEffect();
        }

        public void NextStep()
        {
            // Handled interactively by user actions, but if they press the manual Next Step button:
            ProcessManualNextStepAttempt();
        }

        public void ResetExperiment()
        {
            StopAllCoroutines();
            Initialize();
            _currentStep = ExperimentStep.ShowMagnesiumRibbon;
            UpdateStepUI();
            uiController?.ShowFeedback("Experiment reset. Start from Step 1.", true);
            Debug.Log("[MagnesiumExperiment] Experiment Reset completed.");
        }

        public void OnTargetLost()
        {
            _isTargetDetected = false;
            if (burnParticles != null && burnParticles.isPlaying)
                burnParticles.Pause();

            // Cancel any active drag
            _draggingObject = null;
        }

        public void OnTargetFound()
        {
            _isTargetDetected = true;
            if (_currentStep == ExperimentStep.NotStarted)
            {
                _currentStep = ExperimentStep.ShowMagnesiumRibbon;
            }

            UpdateStepUI();

            if (_currentStep == ExperimentStep.BurningAnimation && burnParticles != null && !burnParticles.isPlaying)
            {
                burnParticles.Play();
            }
        }

        // ── Interaction System (Called by GameManager update or direct Raycast) ──────────────────
        private void Update()
        {
            if (!_isTargetDetected || _isCompleted || _currentStep == ExperimentStep.NotStarted) return;

            HandleTouchDrag();
        }

        private void HandleTouchDrag()
        {
            // Tapping/Selecting
            if (Input.GetMouseButtonDown(0))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                if (Physics.Raycast(ray, out RaycastHit hit))
                {
                    InteractableObject io = hit.collider.GetComponentInParent<InteractableObject>();
                    if (io != null && io.isInteractable && io.gameObject.activeInHierarchy)
                    {
                        OnObjectTapped(io);
                    }
                }
            }

            // Dragging
            if (Input.GetMouseButton(0) && _draggingObject != null)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                float enter;
                if (_dragPlane.Raycast(ray, out enter))
                {
                    Vector3 point = ray.GetPoint(enter);
                    Vector3 localPoint = transform.InverseTransformPoint(point);
                    
                    Vector3 targetLocalPos = localPoint + _dragOffset;
                    
                    // Keep height constant relative to the ImageTarget table surface
                    targetLocalPos.y = _draggingObject.transform.localPosition.y;
                    
                    // Clamp to table workspace boundaries
                    targetLocalPos.x = Mathf.Clamp(targetLocalPos.x, -0.2f, 0.2f);
                    targetLocalPos.z = Mathf.Clamp(targetLocalPos.z, -0.2f, 0.2f);
                    
                    _draggingObject.transform.localPosition = targetLocalPos;

                    OnObjectDragged();
                }
            }

            // Release Drag
            if (Input.GetMouseButtonUp(0))
            {
                if (_draggingObject != null)
                {
                    var io = _draggingObject.GetComponent<InteractableObject>();
                    if (io != null) io.Drop();
                    
                    // Check if ribbon fell near tongs for attachment if dragging ribbon
                    if (_draggingObject == ribbonObject && !_tongsHoldingRibbon && _ribbonSelected)
                    {
                        CheckRibbonTongsProximity();
                    }

                    _draggingObject = null;
                }
            }
        }

        private void OnObjectTapped(InteractableObject io)
        {
            switch (io.objectType)
            {
                case LabObjectType.MagnesiumRibbon:
                    if (_currentStep == ExperimentStep.ShowMagnesiumRibbon)
                    {
                        _ribbonSelected = true;
                        io.Highlight();
                        uiController?.ShowFeedback("Good! Ribbon selected. Now tap Tongs to pick it up.", true);
                        Debug.Log("[MagnesiumExperiment] Ribbon selected");
                        NextStepTrigger();
                    }
                    break;

                case LabObjectType.Tongs:
                    if (_currentStep == ExperimentStep.ShowLabSetup)
                    {
                        if (_ribbonSelected)
                        {
                            AttachRibbonToTongs();
                        }
                        else
                        {
                            uiController?.ShowFeedback("Select the Magnesium Ribbon first!", false);
                            Debug.Log("[MagnesiumExperiment] Attempted to use tongs before ribbon selection");
                        }
                    }
                    else if (_tongsHoldingRibbon && (_currentStep == ExperimentStep.HeatRibbon || _currentStep == ExperimentStep.ShowMgO))
                    {
                        // Allow dragging the tongs if it holds the ribbon/MgO
                        StartDragging(tongsObject);
                        Debug.Log("[MagnesiumExperiment] Dragging tongs");
                    }
                    break;

                case LabObjectType.Burner:
                    if (_currentStep == ExperimentStep.HeatRibbon)
                    {
                        ToggleBurner();
                    }
                    break;
            }
        }

        private void StartDragging(GameObject obj)
        {
            _draggingObject = obj;
            Vector3 planeNormal = transform.up;
            _dragPlane = new Plane(planeNormal, obj.transform.position);

            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
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

            var io = obj.GetComponent<InteractableObject>();
            if (io != null) io.PickUp();
        }

        private void OnObjectDragged()
        {
            // If dragging tongs holding ribbon over the flame
            if (_currentStep == ExperimentStep.HeatRibbon && _tongsHoldingRibbon && _burnerOn && !_ribbonBurned)
            {
                // Check distance between ribbon tip and burner flame
                if (flameObject != null && ribbonObject != null)
                {
                    // Calculate distance in local space
                    float dist = Vector3.Distance(ribbonObject.transform.position, flameObject.transform.position);
                    if (dist < 0.05f)
                    {
                        _draggingObject = null; // stop dragging
                        var io = tongsObject.GetComponent<InteractableObject>();
                        if (io != null) io.Drop();
                        StartCoroutine(TriggerCombustion());
                    }
                }
            }
            // If dragging tongs with product to watch glass
            else if (_currentStep == ExperimentStep.ShowMgO && _productCooled && !_productCollected)
            {
                if (watchGlassObject != null && mgoObject != null)
                {
                    float dist = Vector3.Distance(mgoObject.transform.position, watchGlassObject.transform.position);
                    if (dist < 0.05f)
                    {
                        _draggingObject = null; // stop dragging
                        var io = tongsObject.GetComponent<InteractableObject>();
                        if (io != null) io.Drop();
                        CollectProduct();
                    }
                }
            }
        }

        private void CheckRibbonTongsProximity()
        {
            if (tongsObject != null && ribbonObject != null)
            {
                float dist = Vector3.Distance(ribbonObject.transform.position, tongsObject.transform.position);
                if (dist < 0.06f)
                {
                    AttachRibbonToTongs();
                }
            }
        }

        // ── Step State Transitions ─────────────────────────────────
        private void UpdateStepUI()
        {
            switch (_currentStep)
            {
                case ExperimentStep.ShowMagnesiumRibbon:
                    UpdateStepText("Step 1: Tap the silver-grey Magnesium Ribbon to select it.");
                    break;
                case ExperimentStep.ShowLabSetup:
                    UpdateStepText("Step 2: Tap the Tongs to pick up and hold the selected Magnesium Ribbon.");
                    break;
                case ExperimentStep.HeatRibbon:
                    if (!_burnerOn)
                        UpdateStepText("Step 3: Tap the Bunsen Burner to turn it ON.");
                    else
                        UpdateStepText("Step 3: Drag the Tongs to move the Magnesium Ribbon into the burner flame.");
                    break;
                case ExperimentStep.BurningAnimation:
                    UpdateStepText("Step 4: Magnesium is reacting with oxygen!\nObservation: Dazzling bright white flame.");
                    break;
                case ExperimentStep.ShowMgO:
                    if (!_productCooled)
                        UpdateStepText("Step 5: Burning complete. Allow the Magnesium Oxide product to cool.");
                    else
                        UpdateStepText("Step 5: Product cooled. Drag the Tongs to transfer the Magnesium Oxide into the watch glass.");
                    break;
                case ExperimentStep.ShowEquation:
                    UpdateStepText("Step 6: Chemical Reaction equation:\n\n2Mg + O₂ → 2MgO\n\nMagnesium reacts with oxygen in air to form Magnesium Oxide.");
                    break;
                case ExperimentStep.ShowObservation:
                    UpdateStepText("Step 7: Observation:\n- Burns with an intense bright white light.\n- Leaves a white powdery ash (Magnesium Oxide).");
                    break;
                case ExperimentStep.Completed:
                    UpdateStepText("Step 8: Results ready!\nTap 'View Results' to see the experiment summary.");
                    break;
            }

            uiController?.UpdateStepCounter((int)_currentStep, GetTotalSteps());
        }

        private void NextStepTrigger()
        {
            _currentStep++;
            UpdateStepUI();
        }

        private void AttachRibbonToTongs()
        {
            _tongsHoldingRibbon = true;
            
            // Parent ribbon to tongs at its tip
            if (ribbonObject != null && tongsObject != null)
            {
                ribbonObject.transform.SetParent(tongsObject.transform, true);
                // Position it at the front tip of the tongs
                ribbonObject.transform.localPosition = new Vector3(0f, 0.08f, 0f);
                ribbonObject.transform.localRotation = Quaternion.identity;
                
                var ioRibbon = ribbonObject.GetComponent<InteractableObject>();
                if (ioRibbon != null)
                {
                    ioRibbon.Unhighlight();
                    ioRibbon.isInteractable = false; // Cannot tap it directly now
                }
            }

            uiController?.ShowFeedback("Good! You correctly placed the magnesium ribbon in the tongs.", true);
            Debug.Log("[MagnesiumExperiment] Ribbon attached to tongs.");
            NextStepTrigger();
        }

        private void ToggleBurner()
        {
            _burnerOn = !_burnerOn;
            if (flameObject != null)
            {
                flameObject.SetActive(_burnerOn);
            }

            if (_burnerOn)
            {
                uiController?.ShowFeedback("Bunsen burner ON. Drag tongs to heat ribbon.", true);
                Debug.Log("[MagnesiumExperiment] Burner turned ON");
                UpdateStepUI(); // refresh description
            }
            else
            {
                uiController?.ShowFeedback("Bunsen burner OFF", true);
                Debug.Log("[MagnesiumExperiment] Burner turned OFF");
            }
        }

        private IEnumerator TriggerCombustion()
        {
            _ribbonBurned = true;
            _currentStep = ExperimentStep.BurningAnimation;
            UpdateStepUI();
            
            Debug.Log("[MagnesiumExperiment] Combustion heating started");
            uiController?.ShowFeedback("Combustion started! Dazzling white flame!", true);

            // Activate Particle burning burst
            if (burnParticles != null)
                burnParticles.Play();

            // Flash burn light
            if (burnLight != null)
            {
                burnLight.enabled = true;
                burnLight.color = Color.white;
            }

            // Animate ribbon glowing bright white/emissive
            float elapsed = 0f;
            float duration = 4.0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float intensity = Mathf.PingPong(elapsed * 12f, 8f);
                if (burnLight != null) burnLight.intensity = intensity + 4f;

                if (_ribbonMat != null)
                {
                    _ribbonMat.EnableKeyword("_EMISSION");
                    Color glowColor = Color.white * (intensity / 8f);
                    _ribbonMat.SetColor("_EmissionColor", glowColor);
                }
                yield return null;
            }

            // Turn off bright burn light and particles
            StopBurnEffect();

            // Transform ribbon into white Magnesium Oxide residue (mgoObject)
            if (ribbonObject != null) ribbonObject.SetActive(false);
            if (mgoObject != null)
            {
                mgoObject.SetActive(true);
                mgoObject.transform.position = tongsObject.transform.position + tongsObject.transform.up * 0.08f;
                mgoObject.transform.SetParent(tongsObject.transform, true);
            }

            Debug.Log("[MagnesiumExperiment] Combustion completed, product cooling");
            _currentStep = ExperimentStep.ShowMgO;
            UpdateStepUI();

            // Cooling delay
            uiController?.ShowFeedback("Reaction finished. Allow product to cool...", true);
            yield return new WaitForSeconds(3.0f);

            _productCooled = true;
            uiController?.ShowFeedback("Product cooled. Drag tongs to collect it in the watch glass.", true);
            UpdateStepUI();
        }

        private void CollectProduct()
        {
            _productCollected = true;
            
            // Remove from tongs, place MgO in the watch glass
            if (mgoObject != null) mgoObject.SetActive(false);
            if (mgoInGlassObject != null) mgoInGlassObject.SetActive(true);

            uiController?.ShowFeedback("Magnesium oxide collected in the watch glass.", true);
            Debug.Log("[MagnesiumExperiment] Magnesium oxide collected.");

            NextStepTrigger(); // Move to Step 6 (ShowEquation)
        }

        private void ProcessManualNextStepAttempt()
        {
            switch (_currentStep)
            {
                case ExperimentStep.ShowMagnesiumRibbon:
                    uiController?.ShowFeedback("Select the Magnesium Ribbon first!", false);
                    break;
                case ExperimentStep.ShowLabSetup:
                    uiController?.ShowFeedback("Try again. Hold the magnesium ribbon with the tongs before heating it.", false);
                    break;
                case ExperimentStep.HeatRibbon:
                    if (!_burnerOn)
                        uiController?.ShowFeedback("Turn ON the Bunsen burner first!", false);
                    else
                        uiController?.ShowFeedback("Drag the tongs to move the ribbon into the flame to heat it.", false);
                    break;
                case ExperimentStep.BurningAnimation:
                    uiController?.ShowFeedback("Wait for the magnesium to finish burning!", false);
                    break;
                case ExperimentStep.ShowMgO:
                    if (!_productCooled)
                        uiController?.ShowFeedback("Wait for the product to cool down!", false);
                    else
                        uiController?.ShowFeedback("Drag the tongs to transfer the product into the watch glass.", false);
                    break;
                case ExperimentStep.ShowEquation:
                    NextStepTrigger(); // Step 7 (ShowObservation)
                    break;
                case ExperimentStep.ShowObservation:
                    NextStepTrigger(); // Step 8 (Completed)
                    break;
                case ExperimentStep.Completed:
                    _isCompleted = true;
                    uiController?.ShowViewResultsButton(true);
                    uiController?.ShowFeedback("Tap 'View Results' to complete.", true);
                    break;
            }
        }

        // ── Auto-create Lab Hierarchy ──────────────────────────────
        private void EnsureObjectsExist()
        {
            // Work Surface / Table base
            tableObject = EnsurePrimitive(tableObject, "LabTable",
                PrimitiveType.Cylinder,
                new Vector3(0f, -0.015f, 0f),
                new Vector3(0.35f, 0.01f, 0.35f),
                new Color(0.15f, 0.15f, 0.18f));

            // Watch Glass (transparent grey cylindrical slice)
            watchGlassObject = EnsurePrimitive(watchGlassObject, "WatchGlass",
                PrimitiveType.Cylinder,
                new Vector3(-0.08f, 0.005f, 0f),
                new Vector3(0.07f, 0.005f, 0.07f),
                new Color(0.9f, 0.9f, 0.95f, 0.4f)); // semi-transparent

            // Bunsen Burner
            burnerObject = EnsurePrimitive(burnerObject, "BunsenBurner",
                PrimitiveType.Cylinder,
                new Vector3(0.08f, 0.02f, 0f),
                new Vector3(0.03f, 0.04f, 0.03f),
                new Color(0.35f, 0.35f, 0.38f));

            // Bunsen Flame
            flameObject = EnsurePrimitive(flameObject, "BunsenFlame",
                PrimitiveType.Sphere,
                new Vector3(0.08f, 0.07f, 0f),
                new Vector3(0.015f, 0.03f, 0.015f),
                new Color(0.2f, 0.6f, 1f, 0.8f));

            // Tongs
            tongsObject = EnsurePrimitive(tongsObject, "Tongs",
                PrimitiveType.Capsule,
                _tongsStartPosLocal,
                new Vector3(0.012f, 0.08f, 0.012f),
                new Color(0.5f, 0.5f, 0.52f));
            
            if (tongsObject != null)
            {
                tongsObject.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Magnesium Ribbon
            ribbonObject = EnsurePrimitive(ribbonObject, "MagnesiumRibbon",
                PrimitiveType.Cube,
                _ribbonStartPosLocal,
                new Vector3(0.012f, 0.09f, 0.003f),
                new Color(0.82f, 0.82f, 0.84f));

            if (ribbonObject != null)
            {
                var rend = ribbonObject.GetComponent<Renderer>();
                if (rend != null) _ribbonMat = rend.material;
            }

            // MgO Residue on Tongs (white cylinder)
            mgoObject = EnsurePrimitive(mgoObject, "MgOResidue",
                PrimitiveType.Cylinder,
                new Vector3(0f, 0.08f, 0f),
                new Vector3(0.01f, 0.02f, 0.01f),
                Color.white);

            // MgO Powder inside Watch Glass
            mgoInGlassObject = EnsurePrimitive(mgoInGlassObject, "MgOPowderInWatchGlass",
                PrimitiveType.Sphere,
                new Vector3(-0.08f, 0.012f, 0f),
                new Vector3(0.03f, 0.01f, 0.03f),
                Color.white);

            // Particle System for Burning
            if (burnParticles == null)
            {
                GameObject psGO = new GameObject("BurnParticles");
                psGO.transform.SetParent(transform, false);
                psGO.transform.localPosition = new Vector3(0.08f, 0.07f, 0f);
                burnParticles = psGO.AddComponent<ParticleSystem>();
                ConfigureBurnParticles(burnParticles);
            }

            // Point Light for Burning Flash
            if (burnLight == null)
            {
                GameObject lightGO = new GameObject("BurnLight");
                lightGO.transform.SetParent(transform, false);
                lightGO.transform.localPosition = new Vector3(0.08f, 0.09f, 0f);
                burnLight = lightGO.AddComponent<Light>();
                burnLight.type = LightType.Point;
                burnLight.color = Color.white;
                burnLight.range = 0.8f;
                burnLight.intensity = 0f;
                burnLight.enabled = false;
            }
        }

        private GameObject EnsurePrimitive(
            GameObject existing, string name,
            PrimitiveType type,
            Vector3 localPos, Vector3 localScale, Color color)
        {
            if (existing != null) return existing;

            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;

            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
            {
                Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                if (mat == null || mat.shader == null || mat.shader.name == "Hidden/InternalErrorShader")
                    mat = new Material(Shader.Find("Standard"));
                mat.color = color;
                
                // Allow transparent glass material for watch glass
                if (color.a < 1.0f)
                {
                    mat.SetFloat("_Surface", 1.0f); // Transparent
                    mat.SetFloat("_Blend", 0.0f); // Alpha blend
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.DisableKeyword("_ALPHATEST_ON");
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
                
                rend.material = mat;
            }

            var collider = go.GetComponent<Collider>();
            if (collider != null) collider.isTrigger = true; // Use triggers for simple raycast hit checking

            InteractableObject io = go.AddComponent<InteractableObject>();
            io.isInteractable = true;

            // Map type
            if (name == "MagnesiumRibbon") io.objectType = LabObjectType.MagnesiumRibbon;
            else if (name == "Tongs") io.objectType = LabObjectType.Tongs;
            else if (name == "BunsenBurner") io.objectType = LabObjectType.Burner;
            else io.objectType = LabObjectType.None;

            go.SetActive(false);
            return go;
        }

        private void ConfigureBurnParticles(ParticleSystem ps)
        {
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white, new Color(1f, 0.9f, 0.5f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.01f, 0.03f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.3f);
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.6f);
            main.maxParticles = 500;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 120f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.02f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(new Color(1f, 0.5f, 0f), 0.7f), new GradientColorKey(Color.black, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(1.0f, 0.8f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(grad);

            ParticleSystemRenderer psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Material partMat = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                if (partMat == null || partMat.shader == null || partMat.shader.name == "Hidden/InternalErrorShader")
                    partMat = new Material(Shader.Find("Particles/Standard Unlit"));
                partMat.color = Color.white;
                psr.material = partMat;
            }
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

        private void UpdateStepText(string text)
        {
            if (stepDescriptionText != null)
                stepDescriptionText.text = text;
        }
    }
}
