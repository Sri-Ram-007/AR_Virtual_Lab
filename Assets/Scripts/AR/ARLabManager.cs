using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using Unity.XR.CoreUtils;
using UnityEngine.InputSystem.XR;
using ARVirtualLab.Lab;

#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARVirtualLab.AR
{
    public enum ARPlacementState
    {
        Inactive,
        WaitingForPermission,
        Scanning,
        PlaneDetected,
        ExperimentActive
    }

    public enum ARComponentType
    {
        None,
        Burner,
        Ribbon,
        Tongs,
        Sandpaper,
        WatchGlass,
        FullTable
    }

    public class ARLabManager : MonoBehaviour
    {
        public static ARLabManager Instance { get; private set; }

        [Header("AR Core References")]
        [SerializeField] private ARSession _arSession;
        [SerializeField] private XROrigin _xrOrigin;
        [SerializeField] private ARCameraManager _arCameraManager;
        [SerializeField] private ARCameraBackground _arCameraBackground;
        [SerializeField] private ARPlaneManager _arPlaneManager;
        [SerializeField] private ARRaycastManager _arRaycastManager;
        [SerializeField] private ARPointCloudManager _arPointCloudManager;
        [SerializeField] private Camera _arCamera;
        [SerializeField] private TrackedPoseDriver _trackedPoseDriver;

        [Header("Lab Reference")]
        [SerializeField] private MagnesiumLabScene _labScene;

        private ARPlacementState _placementState = ARPlacementState.Inactive;
        private GameObject _reticle;
        private GameObject _arOverlayUI;
        private GameObject _diagnosticPanel;
        private TMP_Text _diagnosticText;
        private TMP_Text _promptText;
        private TMP_Text _statusBadge;
        private Button _exitARBtn;
        private Button _resetARBtn;
        private Button _startExperimentBtn;
        private Button _placeAllBtn;
        private GameObject _componentTrayRoot;

        // Tray Card UI elements & state
        private readonly Dictionary<ARComponentType, Image> _cardBackgrounds = new Dictionary<ARComponentType, Image>();
        private readonly Dictionary<ARComponentType, TMP_Text> _cardStatusTexts = new Dictionary<ARComponentType, TMP_Text>();
        private readonly HashSet<ARComponentType> _placedComponents = new HashSet<ARComponentType>();

        // Dragging & Ghost Preview
        private ARComponentType _currentlyDraggingType = ARComponentType.None;
        private int _activeDragFingerId = -1;
        private GameObject _ghostPreviewGO;
        private Pose _lastValidHitPose;
        private bool _hasValidHitPose;

        // Plane Detection Tracking
        private readonly List<ARRaycastHit> _raycastHits = new List<ARRaycastHit>();
        private readonly List<Camera> _disabledCameras = new List<Camera>();
        private readonly HashSet<TrackableId> _loggedPlaneIds = new HashSet<TrackableId>();
        private Pose _currentReticlePose;
        private bool _reticlePoseValid;
        private bool _hasLoggedPlaneFound;
        private bool _hasLoggedARCoreInit;
        private float _scanningTimer;
        private Coroutine _permissionCoroutine;
        private string _lastCameraTrackingState = "None";

        // Placed equipment repositioning & gesture manipulation
        private GameObject _selectedEquipmentGO;
        private Plane _equipmentDragPlane;
        private Vector3 _equipmentDragOffset;
        private float _initialPinchDistance;
        private Vector3 _initialLabScale;
        private float _initialTwistAngle;
        private Quaternion _initialLabRotation;

        public bool IsARModeActive => _placementState != ARPlacementState.Inactive;
        public bool IsExperimentActive => _placementState == ARPlacementState.ExperimentActive;
        public Camera ActiveARCamera => _arCamera;

        private void OnEnable()
        {
            ARSession.stateChanged += OnARSessionStateChanged;
            SubscribePlaneEvents();
        }

        private void OnDisable()
        {
            ARSession.stateChanged -= OnARSessionStateChanged;
            UnsubscribePlaneEvents();
        }

        private void SubscribePlaneEvents()
        {
            if (_arPlaneManager != null)
            {
                _arPlaneManager.trackablesChanged.RemoveListener(OnTrackablesChanged); // guard double-sub
                _arPlaneManager.trackablesChanged.AddListener(OnTrackablesChanged);
                Debug.Log("[ARLab] Subscribed to ARPlaneManager.trackablesChanged");
            }
            else
            {
                Debug.LogWarning("[ARLab] SubscribePlaneEvents: _arPlaneManager is null!");
            }

            if (_arCameraManager != null)
            {
                _arCameraManager.frameReceived -= OnCameraFrameReceived;
                _arCameraManager.frameReceived += OnCameraFrameReceived;
            }
        }

        private void UnsubscribePlaneEvents()
        {
            if (_arPlaneManager != null)
            {
                _arPlaneManager.trackablesChanged.RemoveListener(OnTrackablesChanged);
            }
            if (_arCameraManager != null)
            {
                _arCameraManager.frameReceived -= OnCameraFrameReceived;
            }
        }

        private void OnCameraFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_arSession != null && _arSession.subsystem != null)
            {
                _lastCameraTrackingState = _arSession.subsystem.trackingState.ToString();
            }
            else
            {
                _lastCameraTrackingState = "FrameReceived";
            }
        }

        private void OnARSessionStateChanged(ARSessionStateChangedEventArgs args)
        {
            Debug.Log($"[ARLab] AR Session state: {args.state}");

            if (args.state == ARSessionState.SessionTracking || args.state == ARSessionState.Ready)
            {
                if (!_hasLoggedARCoreInit)
                {
                    _hasLoggedARCoreInit = true;
                    Debug.Log("[ARLab] ARCore running");
                }
            }
            else if (args.state == ARSessionState.Unsupported)
            {
                Debug.LogWarning("[ARLab] AR Session state is Unsupported on this device.");
                SetPrompt("ARCore is not supported on this device.");
            }
            else if (args.state == ARSessionState.NeedsInstall)
            {
                Debug.LogWarning("[ARLab] AR Session needs ARCore installation.");
                SetPrompt("ARCore update/installation required.");
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureARComponents();
            CreatePlacementReticle();
            BuildAROverlayUI();
            SetARHierarchyActive(false);
        }

        private void Start()
        {
            if (_labScene == null)
            {
                _labScene = FindFirstObjectByType<MagnesiumLabScene>();
            }
        }

        private void EnsureARComponents()
        {
            // ── Clean up duplicate AR Sessions / XR Origins in scene ────────────────
            var allSessions = FindObjectsByType<ARSession>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allSessions.Length > 1)
            {
                for (int i = 1; i < allSessions.Length; i++)
                {
                    if (allSessions[i] != null && allSessions[i] != _arSession)
                    {
                        Debug.LogWarning($"[ARLab] Destroying duplicate ARSession: {allSessions[i].gameObject.name}");
                        Destroy(allSessions[i].gameObject);
                    }
                }
            }

            var allOrigins = FindObjectsByType<XROrigin>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (allOrigins.Length > 1)
            {
                for (int i = 1; i < allOrigins.Length; i++)
                {
                    if (allOrigins[i] != null && allOrigins[i] != _xrOrigin)
                    {
                        Debug.LogWarning($"[ARLab] Destroying duplicate XROrigin: {allOrigins[i].gameObject.name}");
                        Destroy(allOrigins[i].gameObject);
                    }
                }
            }

            // ── 1. AR Session ────────────────────────────────────────────────────────
            if (_arSession == null)
            {
                var sessionGO = GameObject.Find("AR Session");
                if (sessionGO == null)
                {
                    sessionGO = new GameObject("AR Session");
                    sessionGO.transform.SetParent(transform, false);
                }
                _arSession = sessionGO.GetComponent<ARSession>();
                if (_arSession == null) _arSession = sessionGO.AddComponent<ARSession>();
                _arSession.attemptUpdate = true;
                _arSession.matchFrameRateRequested = true;

                if (sessionGO.GetComponent<ARInputManager>() == null)
                    sessionGO.AddComponent<ARInputManager>();
            }

            // ── 2. XR Origin ─────────────────────────────────────────────────────────
            if (_xrOrigin == null)
            {
                var originGO = GameObject.Find("XR Origin");
                if (originGO == null)
                {
                    originGO = new GameObject("XR Origin");
                    originGO.transform.SetParent(transform, false);
                }
                _xrOrigin = originGO.GetComponent<XROrigin>();
                if (_xrOrigin == null) _xrOrigin = originGO.AddComponent<XROrigin>();

                // ── ARPlaneManager ────────────────────────────────────────────────
                _arPlaneManager = originGO.GetComponent<ARPlaneManager>();
                if (_arPlaneManager == null) _arPlaneManager = originGO.AddComponent<ARPlaneManager>();

                // ── ARRaycastManager ──────────────────────────────────────────────
                _arRaycastManager = originGO.GetComponent<ARRaycastManager>();
                if (_arRaycastManager == null) _arRaycastManager = originGO.AddComponent<ARRaycastManager>();

                // ── ARPointCloudManager ───────────────────────────────────────────
                _arPointCloudManager = originGO.GetComponent<ARPointCloudManager>();
                if (_arPointCloudManager == null) _arPointCloudManager = originGO.AddComponent<ARPointCloudManager>();

                // ── Camera Offset ─────────────────────────────────────────────────
                var offsetGO = originGO.transform.Find("Camera Offset")?.gameObject;
                if (offsetGO == null)
                {
                    offsetGO = new GameObject("Camera Offset");
                    offsetGO.transform.SetParent(originGO.transform, false);
                }
                _xrOrigin.CameraFloorOffsetObject = offsetGO;

                // ── AR Camera ─────────────────────────────────────────────────────
                var camGO = offsetGO.transform.Find("AR Camera")?.gameObject;
                if (camGO == null)
                {
                    camGO = new GameObject("AR Camera");
                    camGO.transform.SetParent(offsetGO.transform, false);
                }

                _arCamera = camGO.GetComponent<Camera>();
                if (_arCamera == null) _arCamera = camGO.AddComponent<Camera>();
                _arCamera.clearFlags = CameraClearFlags.SolidColor;
                _arCamera.backgroundColor = Color.black;
                _arCamera.nearClipPlane = 0.1f;
                _arCamera.farClipPlane = 100f;
                _arCamera.cullingMask = ~0;

                _arCameraManager = camGO.GetComponent<ARCameraManager>();
                if (_arCameraManager == null) _arCameraManager = camGO.AddComponent<ARCameraManager>();
                _arCameraManager.autoFocusRequested = true;

                _arCameraBackground = camGO.GetComponent<ARCameraBackground>();
                if (_arCameraBackground == null) _arCameraBackground = camGO.AddComponent<ARCameraBackground>();

                _trackedPoseDriver = camGO.GetComponent<TrackedPoseDriver>();
                if (_trackedPoseDriver == null) _trackedPoseDriver = camGO.AddComponent<TrackedPoseDriver>();

                try
                {
                    var urpCamData = camGO.GetComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    if (urpCamData == null) urpCamData = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                    urpCamData.renderPostProcessing = false;
                }
                catch { }

                _xrOrigin.Camera = _arCamera;
            }
            else
            {
                // Resolve from existing XR Origin
                if (_arPlaneManager == null) _arPlaneManager = _xrOrigin.GetComponentInChildren<ARPlaneManager>();
                if (_arPlaneManager == null) _arPlaneManager = _xrOrigin.gameObject.AddComponent<ARPlaneManager>();

                if (_arRaycastManager == null) _arRaycastManager = _xrOrigin.GetComponentInChildren<ARRaycastManager>();
                if (_arRaycastManager == null) _arRaycastManager = _xrOrigin.gameObject.AddComponent<ARRaycastManager>();

                if (_arPointCloudManager == null) _arPointCloudManager = _xrOrigin.GetComponentInChildren<ARPointCloudManager>();
                if (_arPointCloudManager == null) _arPointCloudManager = _xrOrigin.gameObject.AddComponent<ARPointCloudManager>();

                if (_arCamera == null && _xrOrigin.Camera != null)
                    _arCamera = _xrOrigin.Camera;

                if (_arCamera != null)
                {
                    _arCamera.cullingMask = ~0;
                    _arCameraManager = _arCamera.GetComponent<ARCameraManager>();
                    if (_arCameraManager == null) _arCameraManager = _arCamera.gameObject.AddComponent<ARCameraManager>();
                    _arCameraManager.autoFocusRequested = true;

                    _arCameraBackground = _arCamera.GetComponent<ARCameraBackground>();
                    if (_arCameraBackground == null) _arCameraBackground = _arCamera.gameObject.AddComponent<ARCameraBackground>();

                    _trackedPoseDriver = _arCamera.GetComponent<TrackedPoseDriver>();
                    if (_trackedPoseDriver == null) _trackedPoseDriver = _arCamera.gameObject.AddComponent<TrackedPoseDriver>();
                }
            }

            // ── Verify single active ARPlaneManager ──────────────────────────────────
            var allPlaneManagers = FindObjectsByType<ARPlaneManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Debug.Log($"[ARLab] Active ARPlaneManager count = {allPlaneManagers.Length}");
            if (allPlaneManagers.Length > 1)
            {
                for (int i = 0; i < allPlaneManagers.Length; i++)
                {
                    if (allPlaneManagers[i] != null && allPlaneManagers[i] != _arPlaneManager)
                    {
                        Debug.LogWarning($"[ARLab] Destroying duplicate ARPlaneManager on {allPlaneManagers[i].gameObject.name}");
                        Destroy(allPlaneManagers[i]);
                    }
                }
            }

            // ── Configure ARPlaneManager ──────────────────────────────────────────────
            _arPlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            if (_arPlaneManager.planePrefab == null)
                _arPlaneManager.planePrefab = CreateDefaultPlanePrefab();

            Debug.Log($"[ARLab] ARPlaneManager.requestedDetectionMode = {_arPlaneManager.requestedDetectionMode}");
            Debug.Log($"[ARLab] ARPlaneManager.planePrefab = {(_arPlaneManager.planePrefab != null ? _arPlaneManager.planePrefab.name : "NULL")}");

            // ── Subscribe plane events NOW that _arPlaneManager is resolved ────────────
            SubscribePlaneEvents();
        }

        private GameObject CreateDefaultPlanePrefab()
        {
            // AR Foundation plane prefab MUST have ARPlane + ARPlaneMeshVisualizer.
            // AR Foundation instantiates this for every detected plane.
            var planeGO = new GameObject("ARPlane_Visual");

            // Required by AR Foundation
            planeGO.AddComponent<ARPlane>();
            planeGO.AddComponent<ARPlaneMeshVisualizer>();
            planeGO.AddComponent<MeshFilter>();
            planeGO.AddComponent<MeshCollider>();

            var mr = planeGO.AddComponent<MeshRenderer>();

            // Build a transparent URP material for the plane fill
            Shader urpUnlit = Shader.Find("Universal Render Pipeline/Unlit");
            if (urpUnlit == null) urpUnlit = Shader.Find("Unlit/Color");

            var mat = new Material(urpUnlit);
            // URP transparent setup
            if (mat.HasProperty("_BaseColor"))
                mat.SetColor("_BaseColor", new Color(0.10f, 0.65f, 1.00f, 0.30f));
            else
                mat.color = new Color(0.10f, 0.65f, 1.00f, 0.30f);

            mat.SetFloat("_Surface", 1f);      // 0 = Opaque, 1 = Transparent
            mat.SetFloat("_Blend", 0f);        // 0 = Alpha, 1 = Premultiply, etc.
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            mr.material = mat;

            // Line renderer draws the boundary outline of the plane
            var lineR = planeGO.AddComponent<LineRenderer>();
            lineR.useWorldSpace = false;
            lineR.startWidth = 0.01f;
            lineR.endWidth = 0.01f;
            lineR.loop = true;
            lineR.positionCount = 0;

            var lineMat = new Material(urpUnlit);
            if (lineMat.HasProperty("_BaseColor"))
                lineMat.SetColor("_BaseColor", new Color(0.25f, 0.85f, 1.0f, 0.90f));
            else
                lineMat.color = new Color(0.25f, 0.85f, 1.0f, 0.90f);
            lineMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            lineMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            lineMat.SetInt("_ZWrite", 0);
            lineMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            lineR.material = lineMat;

            planeGO.SetActive(false);
            return planeGO;
        }


        private void CreatePlacementReticle()
        {
            _reticle = new GameObject("ARPlacementReticle");
            _reticle.transform.SetParent(transform, false);

            var mf = _reticle.AddComponent<MeshFilter>();
            var mr = _reticle.AddComponent<MeshRenderer>();

            int segs = 36;
            float outerR = 0.16f, innerR = 0.13f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();

            for (int i = 0; i <= segs; i++)
            {
                float ang = (float)i / segs * Mathf.PI * 2f;
                float cos = Mathf.Cos(ang), sin = Mathf.Sin(ang);
                verts.Add(new Vector3(cos * outerR, 0.002f, sin * outerR));
                verts.Add(new Vector3(cos * innerR, 0.002f, sin * innerR));
                uvs.Add(new Vector2(cos * 0.5f + 0.5f, sin * 0.5f + 0.5f));
                uvs.Add(new Vector2(cos * 0.4f + 0.5f, sin * 0.4f + 0.5f));
            }

            for (int i = 0; i < segs; i++)
            {
                int i0 = i * 2, i1 = i0 + 1, i2 = (i + 1) * 2, i3 = i2 + 1;
                tris.Add(i0); tris.Add(i2); tris.Add(i1);
                tris.Add(i1); tris.Add(i2); tris.Add(i3);
            }

            var mesh = new Mesh { name = "ReticleMesh" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            var rMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
            rMat.color = new Color(0.20f, 0.85f, 1.00f, 0.85f);
            if (rMat.HasProperty("_BaseColor")) rMat.SetColor("_BaseColor", new Color(0.20f, 0.85f, 1.00f, 0.85f));
            rMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            rMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            rMat.SetInt("_ZWrite", 0);
            rMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mr.material = mr.sharedMaterial = rMat;

            _reticle.SetActive(false);
        }

        private void BuildAROverlayUI()
        {
            var canvasGO = new GameObject("AROverlayCanvas");
            canvasGO.transform.SetParent(transform, false);
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 300;

            var cs = canvasGO.AddComponent<CanvasScaler>();
            cs.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            cs.referenceResolution = new Vector2(1080, 1920);
            cs.matchWidthOrHeight = 0.5f;

            canvasGO.AddComponent<GraphicRaycaster>();
            _arOverlayUI = canvasGO;

            // 1. Top Header Bar
            var topPanel = new GameObject("TopARBar", typeof(RectTransform));
            topPanel.transform.SetParent(canvasGO.transform, false);
            var tpRT = topPanel.GetComponent<RectTransform>();
            tpRT.anchorMin = new Vector2(0f, 1f); tpRT.anchorMax = new Vector2(1f, 1f);
            tpRT.pivot = new Vector2(0.5f, 1f);
            tpRT.anchoredPosition = new Vector2(0f, -40f);
            tpRT.sizeDelta = new Vector2(-40f, 100f);

            var badgeGO = new GameObject("ARBadge", typeof(RectTransform));
            badgeGO.transform.SetParent(topPanel.transform, false);
            var bRT = badgeGO.GetComponent<RectTransform>();
            bRT.anchorMin = new Vector2(0f, 0.5f); bRT.anchorMax = new Vector2(0f, 0.5f);
            bRT.pivot = new Vector2(0f, 0.5f);
            bRT.anchoredPosition = new Vector2(20f, 0f);
            bRT.sizeDelta = new Vector2(240f, 60f);

            var bImg = badgeGO.AddComponent<Image>();
            bImg.color = new Color(0.05f, 0.35f, 0.65f, 0.85f);
            var bOutline = badgeGO.AddComponent<Outline>();
            bOutline.effectColor = new Color(0.30f, 0.85f, 1.00f, 0.90f);
            bOutline.effectDistance = new Vector2(2, -2);

            var badgeTxtGO = new GameObject("BadgeText", typeof(RectTransform));
            badgeTxtGO.transform.SetParent(badgeGO.transform, false);
            var btRT = badgeTxtGO.GetComponent<RectTransform>();
            btRT.anchorMin = Vector2.zero; btRT.anchorMax = Vector2.one;
            btRT.sizeDelta = Vector2.zero;
            _statusBadge = badgeTxtGO.AddComponent<TextMeshProUGUI>();
            _statusBadge.text = "✦ AR LAB MODE";
            _statusBadge.fontSize = 24;
            _statusBadge.fontStyle = FontStyles.Bold;
            _statusBadge.alignment = TextAlignmentOptions.Center;
            _statusBadge.color = Color.white;

            // Home Button (Top Right Left)
            var homeGO = new GameObject("HomeARBtn", typeof(RectTransform));
            homeGO.transform.SetParent(topPanel.transform, false);
            var homeRT = homeGO.GetComponent<RectTransform>();
            homeRT.anchorMin = new Vector2(1f, 0.5f); homeRT.anchorMax = new Vector2(1f, 0.5f);
            homeRT.pivot = new Vector2(1f, 0.5f);
            homeRT.anchoredPosition = new Vector2(-400f, 0f);
            homeRT.sizeDelta = new Vector2(160f, 60f);

            var homeImg = homeGO.AddComponent<Image>();
            homeImg.color = new Color(0.12f, 0.22f, 0.38f, 0.90f);
            var homeOut = homeGO.AddComponent<Outline>();
            homeOut.effectColor = new Color(0.30f, 0.70f, 0.95f, 0.90f);
            homeOut.effectDistance = new Vector2(2, -2);

            var homeBtn = homeGO.AddComponent<Button>();
            homeBtn.onClick.AddListener(() => {
                ExitARMode();
                ARVirtualLab.AppShell.AppNavigation.GoToHome();
            });

            var homeTxtGO = new GameObject("Text", typeof(RectTransform));
            homeTxtGO.transform.SetParent(homeGO.transform, false);
            var htRT = homeTxtGO.GetComponent<RectTransform>();
            htRT.anchorMin = Vector2.zero; htRT.anchorMax = Vector2.one;
            htRT.sizeDelta = Vector2.zero;
            var homeTmp = homeTxtGO.AddComponent<TextMeshProUGUI>();
            homeTmp.text = "← Home";
            homeTmp.fontSize = 22;
            homeTmp.fontStyle = FontStyles.Bold;
            homeTmp.alignment = TextAlignmentOptions.Center;
            homeTmp.color = Color.white;

            // Reset Button (Top Right Center)
            var resetGO = new GameObject("ResetARBtn", typeof(RectTransform));
            resetGO.transform.SetParent(topPanel.transform, false);
            var resRT = resetGO.GetComponent<RectTransform>();
            resRT.anchorMin = new Vector2(1f, 0.5f); resRT.anchorMax = new Vector2(1f, 0.5f);
            resRT.pivot = new Vector2(1f, 0.5f);
            resRT.anchoredPosition = new Vector2(-220f, 0f);
            resRT.sizeDelta = new Vector2(160f, 60f);

            var resImg = resetGO.AddComponent<Image>();
            resImg.color = new Color(0.15f, 0.35f, 0.55f, 0.90f);
            var resOut = resetGO.AddComponent<Outline>();
            resOut.effectColor = new Color(0.40f, 0.75f, 1.0f, 0.90f);
            resOut.effectDistance = new Vector2(2, -2);

            _resetARBtn = resetGO.AddComponent<Button>();
            _resetARBtn.onClick.AddListener(ResetARExperiment);

            var resTxtGO = new GameObject("Text", typeof(RectTransform));
            resTxtGO.transform.SetParent(resetGO.transform, false);
            var restRT = resTxtGO.GetComponent<RectTransform>();
            restRT.anchorMin = Vector2.zero; restRT.anchorMax = Vector2.one;
            restRT.sizeDelta = Vector2.zero;
            var resTmp = resTxtGO.AddComponent<TextMeshProUGUI>();
            resTmp.text = "↺ Reset";
            resTmp.fontSize = 22;
            resTmp.fontStyle = FontStyles.Bold;
            resTmp.alignment = TextAlignmentOptions.Center;
            resTmp.color = Color.white;

            // Exit AR Button (Top Right)
            var exitGO = new GameObject("ExitARBtn", typeof(RectTransform));
            exitGO.transform.SetParent(topPanel.transform, false);
            var exitRT = exitGO.GetComponent<RectTransform>();
            exitRT.anchorMin = new Vector2(1f, 0.5f); exitRT.anchorMax = new Vector2(1f, 0.5f);
            exitRT.pivot = new Vector2(1f, 0.5f);
            exitRT.anchoredPosition = new Vector2(-20f, 0f);
            exitRT.sizeDelta = new Vector2(180f, 60f);

            var exitImg = exitGO.AddComponent<Image>();
            exitImg.color = new Color(0.65f, 0.15f, 0.20f, 0.90f);
            var exitOut = exitGO.AddComponent<Outline>();
            exitOut.effectColor = new Color(1.0f, 0.40f, 0.45f, 0.90f);
            exitOut.effectDistance = new Vector2(2, -2);

            _exitARBtn = exitGO.AddComponent<Button>();
            _exitARBtn.onClick.AddListener(ExitARMode);

            var exitTxtGO = new GameObject("Text", typeof(RectTransform));
            exitTxtGO.transform.SetParent(exitGO.transform, false);
            var etRT = exitTxtGO.GetComponent<RectTransform>();
            etRT.anchorMin = Vector2.zero; etRT.anchorMax = Vector2.one;
            etRT.sizeDelta = Vector2.zero;
            var exitTmp = exitTxtGO.AddComponent<TextMeshProUGUI>();
            exitTmp.text = "Exit AR";
            exitTmp.fontSize = 24;
            exitTmp.fontStyle = FontStyles.Bold;
            exitTmp.alignment = TextAlignmentOptions.Center;
            exitTmp.color = Color.white;

            // 2. Central Prompt Banner
            var promptGO = new GameObject("ARPromptPanel", typeof(RectTransform));
            promptGO.transform.SetParent(canvasGO.transform, false);
            var prRT = promptGO.GetComponent<RectTransform>();
            prRT.anchorMin = new Vector2(0.5f, 0.86f); prRT.anchorMax = new Vector2(0.5f, 0.86f);
            prRT.pivot = new Vector2(0.5f, 0.5f);
            prRT.anchoredPosition = Vector2.zero;
            prRT.sizeDelta = new Vector2(960f, 130f);

            var prImg = promptGO.AddComponent<Image>();
            prImg.color = new Color(0.06f, 0.12f, 0.24f, 0.92f);
            var prOut = promptGO.AddComponent<Outline>();
            prOut.effectColor = new Color(0.20f, 0.70f, 1.00f, 0.80f);
            prOut.effectDistance = new Vector2(2, -2);

            var promptTxtGO = new GameObject("PromptText", typeof(RectTransform));
            promptTxtGO.transform.SetParent(promptGO.transform, false);
            var ptRT = promptTxtGO.GetComponent<RectTransform>();
            ptRT.anchorMin = Vector2.zero; ptRT.anchorMax = Vector2.one;
            ptRT.sizeDelta = new Vector2(-40f, -16f);
            _promptText = promptTxtGO.AddComponent<TextMeshProUGUI>();
            _promptText.text = "Move your phone slowly over a flat surface.";
            _promptText.fontSize = 22;
            _promptText.fontStyle = FontStyles.Bold;
            _promptText.alignment = TextAlignmentOptions.Center;
            _promptText.color = new Color(0.90f, 0.96f, 1.00f);

            // 3. Temporary AR Diagnostics Overlay Panel (Hidden in production)
            _diagnosticPanel = new GameObject("ARDiagnosticPanel", typeof(RectTransform));
            _diagnosticPanel.transform.SetParent(canvasGO.transform, false);
            var dRT = _diagnosticPanel.GetComponent<RectTransform>();
            dRT.anchorMin = new Vector2(0f, 1f); dRT.anchorMax = new Vector2(0f, 1f);
            dRT.pivot = new Vector2(0f, 1f);
            dRT.anchoredPosition = new Vector2(25f, -160f);
            dRT.sizeDelta = new Vector2(600f, 320f);

            var dImg = _diagnosticPanel.AddComponent<Image>();
            dImg.color = new Color(0.04f, 0.08f, 0.16f, 0.90f);
            var dOut = _diagnosticPanel.AddComponent<Outline>();
            dOut.effectColor = new Color(0.25f, 0.75f, 1.00f, 0.70f);
            dOut.effectDistance = new Vector2(2, -2);

            var dTxtGO = new GameObject("DiagnosticText", typeof(RectTransform));
            dTxtGO.transform.SetParent(_diagnosticPanel.transform, false);
            var dtRT = dTxtGO.GetComponent<RectTransform>();
            dtRT.anchorMin = Vector2.zero; dtRT.anchorMax = Vector2.one;
            dtRT.sizeDelta = new Vector2(-20f, -20f);
            _diagnosticText = dTxtGO.AddComponent<TextMeshProUGUI>();
            _diagnosticText.fontSize = 15;
            _diagnosticText.alignment = TextAlignmentOptions.TopLeft;
            _diagnosticText.color = new Color(0.85f, 0.95f, 1.00f);
            _diagnosticText.text = "AR Diagnostics Initializing...";
            _diagnosticPanel.SetActive(false); // Clean production UI

            // 4. AR Component Tray (Bottom Drawer - Inspired by Reference UI)
            BuildComponentTray(canvasGO);

            _arOverlayUI.SetActive(false);
        }

        private void BuildComponentTray(GameObject canvasGO)
        {
            _componentTrayRoot = new GameObject("ARComponentTray", typeof(RectTransform));
            _componentTrayRoot.transform.SetParent(canvasGO.transform, false);
            var trayRT = _componentTrayRoot.GetComponent<RectTransform>();
            trayRT.anchorMin = new Vector2(0f, 0f); trayRT.anchorMax = new Vector2(1f, 0f);
            trayRT.pivot = new Vector2(0.5f, 0f);
            trayRT.anchoredPosition = new Vector2(0f, 20f);
            trayRT.sizeDelta = new Vector2(-40f, 320f);

            var trayImg = _componentTrayRoot.AddComponent<Image>();
            trayImg.color = new Color(0.06f, 0.09f, 0.16f, 0.94f);
            var trayOut = _componentTrayRoot.AddComponent<Outline>();
            trayOut.effectColor = new Color(0.25f, 0.55f, 0.85f, 0.75f);
            trayOut.effectDistance = new Vector2(2, 2);

            // Category Tab: [ Chemistry ]
            var catGO = new GameObject("CategoryTab", typeof(RectTransform));
            catGO.transform.SetParent(_componentTrayRoot.transform, false);
            var catRT = catGO.GetComponent<RectTransform>();
            catRT.anchorMin = new Vector2(0f, 1f); catRT.anchorMax = new Vector2(0f, 1f);
            catRT.pivot = new Vector2(0f, 1f);
            catRT.anchoredPosition = new Vector2(25f, -15f);
            catRT.sizeDelta = new Vector2(220f, 45f);

            var catImg = catGO.AddComponent<Image>();
            catImg.color = new Color(0.12f, 0.40f, 0.75f, 0.90f);
            var catTxtGO = new GameObject("Text", typeof(RectTransform));
            catTxtGO.transform.SetParent(catGO.transform, false);
            var ctxtRT = catTxtGO.GetComponent<RectTransform>();
            ctxtRT.anchorMin = Vector2.zero; ctxtRT.anchorMax = Vector2.one;
            ctxtRT.sizeDelta = Vector2.zero;
            var catTmp = catTxtGO.AddComponent<TextMeshProUGUI>();
            catTmp.text = "CHEMISTRY LAB";
            catTmp.fontSize = 20;
            catTmp.fontStyle = FontStyles.Bold;
            catTmp.alignment = TextAlignmentOptions.Center;
            catTmp.color = Color.white;

            // Start Experiment Button (Top Right of Tray)
            var startExpGO = new GameObject("StartExperimentBtn", typeof(RectTransform));
            startExpGO.transform.SetParent(_componentTrayRoot.transform, false);
            var seRT = startExpGO.GetComponent<RectTransform>();
            seRT.anchorMin = new Vector2(1f, 1f); seRT.anchorMax = new Vector2(1f, 1f);
            seRT.pivot = new Vector2(1f, 1f);
            seRT.anchoredPosition = new Vector2(-25f, -12f);
            seRT.sizeDelta = new Vector2(280f, 50f);

            var seImg = startExpGO.AddComponent<Image>();
            seImg.color = new Color(0.10f, 0.65f, 0.35f, 0.95f);
            var seOut = startExpGO.AddComponent<Outline>();
            seOut.effectColor = new Color(0.40f, 1.0f, 0.60f, 0.90f);
            seOut.effectDistance = new Vector2(2, -2);

            _startExperimentBtn = startExpGO.AddComponent<Button>();
            _startExperimentBtn.onClick.AddListener(OnStartExperimentClicked);

            var seTxtGO = new GameObject("Text", typeof(RectTransform));
            seTxtGO.transform.SetParent(startExpGO.transform, false);
            var setRT = seTxtGO.GetComponent<RectTransform>();
            setRT.anchorMin = Vector2.zero; setRT.anchorMax = Vector2.one;
            setRT.sizeDelta = Vector2.zero;
            var seTmp = seTxtGO.AddComponent<TextMeshProUGUI>();
            seTmp.text = "▶ Start Experiment";
            seTmp.fontSize = 22;
            seTmp.fontStyle = FontStyles.Bold;
            seTmp.alignment = TextAlignmentOptions.Center;
            seTmp.color = Color.white;

            // Quick Place All Button
            var placeAllGO = new GameObject("PlaceAllBtn", typeof(RectTransform));
            placeAllGO.transform.SetParent(_componentTrayRoot.transform, false);
            var paRT = placeAllGO.GetComponent<RectTransform>();
            paRT.anchorMin = new Vector2(1f, 1f); paRT.anchorMax = new Vector2(1f, 1f);
            paRT.pivot = new Vector2(1f, 1f);
            paRT.anchoredPosition = new Vector2(-320f, -12f);
            paRT.sizeDelta = new Vector2(240f, 50f);

            var paImg = placeAllGO.AddComponent<Image>();
            paImg.color = new Color(0.20f, 0.40f, 0.65f, 0.90f);
            var paOut = placeAllGO.AddComponent<Outline>();
            paOut.effectColor = new Color(0.45f, 0.80f, 1.0f, 0.85f);
            paOut.effectDistance = new Vector2(2, -2);

            _placeAllBtn = placeAllGO.AddComponent<Button>();
            _placeAllBtn.onClick.AddListener(OnPlaceAllEquipmentClicked);

            var paTxtGO = new GameObject("Text", typeof(RectTransform));
            paTxtGO.transform.SetParent(placeAllGO.transform, false);
            var patRT = paTxtGO.GetComponent<RectTransform>();
            patRT.anchorMin = Vector2.zero; patRT.anchorMax = Vector2.one;
            patRT.sizeDelta = Vector2.zero;
            var paTmp = paTxtGO.AddComponent<TextMeshProUGUI>();
            paTmp.text = "✦ Place All Lab";
            paTmp.fontSize = 20;
            paTmp.fontStyle = FontStyles.Bold;
            paTmp.alignment = TextAlignmentOptions.Center;
            paTmp.color = Color.white;

            // Horizontal Scroll / Flex Container for Cards
            var scrollRoot = new GameObject("CardScrollView", typeof(RectTransform));
            scrollRoot.transform.SetParent(_componentTrayRoot.transform, false);
            var sRT = scrollRoot.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0f); sRT.anchorMax = new Vector2(1f, 0f);
            sRT.pivot = new Vector2(0.5f, 0f);
            sRT.anchoredPosition = new Vector2(0f, 15f);
            sRT.sizeDelta = new Vector2(-40f, 220f);

            var hlg = scrollRoot.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Build 5 Equipment Cards
            CreateEquipmentCard(scrollRoot.transform, ARComponentType.Burner, "Bunsen Burner", "🔥");
            CreateEquipmentCard(scrollRoot.transform, ARComponentType.Ribbon, "Magnesium Ribbon", "⚡");
            CreateEquipmentCard(scrollRoot.transform, ARComponentType.Tongs, "Laboratory Tongs", "✂");
            CreateEquipmentCard(scrollRoot.transform, ARComponentType.Sandpaper, "Sandpaper", "⬛");
            CreateEquipmentCard(scrollRoot.transform, ARComponentType.WatchGlass, "Watch Glass", "⚪");

            _componentTrayRoot.SetActive(false);
        }

        private void CreateEquipmentCard(Transform parent, ARComponentType type, string title, string iconSymbol)
        {
            var cardGO = new GameObject($"Card_{type}", typeof(RectTransform));
            cardGO.transform.SetParent(parent, false);
            var cRT = cardGO.GetComponent<RectTransform>();
            cRT.sizeDelta = new Vector2(180f, 200f);

            var cImg = cardGO.AddComponent<Image>();
            cImg.color = new Color(0.12f, 0.18f, 0.28f, 0.90f);
            var cOut = cardGO.AddComponent<Outline>();
            cOut.effectColor = new Color(0.30f, 0.60f, 0.90f, 0.60f);
            cOut.effectDistance = new Vector2(2, -2);
            _cardBackgrounds[type] = cImg;

            // Icon Symbol Badge
            var iconGO = new GameObject("Icon", typeof(RectTransform));
            iconGO.transform.SetParent(cardGO.transform, false);
            var iRT = iconGO.GetComponent<RectTransform>();
            iRT.anchorMin = new Vector2(0.5f, 0.70f); iRT.anchorMax = new Vector2(0.5f, 0.70f);
            iRT.sizeDelta = new Vector2(70f, 70f);
            var iconTmp = iconGO.AddComponent<TextMeshProUGUI>();
            iconTmp.text = iconSymbol;
            iconTmp.fontSize = 38;
            iconTmp.alignment = TextAlignmentOptions.Center;
            iconTmp.color = new Color(1.0f, 0.85f, 0.40f);

            // Title
            var titleGO = new GameObject("Title", typeof(RectTransform));
            titleGO.transform.SetParent(cardGO.transform, false);
            var tRT = titleGO.GetComponent<RectTransform>();
            tRT.anchorMin = new Vector2(0f, 0.25f); tRT.anchorMax = new Vector2(1f, 0.45f);
            tRT.sizeDelta = Vector2.zero;
            var tTmp = titleGO.AddComponent<TextMeshProUGUI>();
            tTmp.text = title;
            tTmp.fontSize = 18;
            tTmp.fontStyle = FontStyles.Bold;
            tTmp.alignment = TextAlignmentOptions.Center;
            tTmp.color = Color.white;

            // Drag / Status Label
            var statusGO = new GameObject("Status", typeof(RectTransform));
            statusGO.transform.SetParent(cardGO.transform, false);
            var stRT = statusGO.GetComponent<RectTransform>();
            stRT.anchorMin = new Vector2(0f, 0.05f); stRT.anchorMax = new Vector2(1f, 0.25f);
            stRT.sizeDelta = Vector2.zero;
            var stTmp = statusGO.AddComponent<TextMeshProUGUI>();
            stTmp.text = "Drag to Place";
            stTmp.fontSize = 14;
            stTmp.alignment = TextAlignmentOptions.Center;
            stTmp.color = new Color(0.45f, 0.80f, 1.0f);
            _cardStatusTexts[type] = stTmp;

            // Card Event Trigger for Drag / Tap
            var trigger = cardGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var entryDown = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            entryDown.callback.AddListener((data) => { OnCardPointerDown(type, (UnityEngine.EventSystems.PointerEventData)data); });
            trigger.triggers.Add(entryDown);

            var entryDrag = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.Drag };
            entryDrag.callback.AddListener((data) => { OnCardPointerDrag(type, (UnityEngine.EventSystems.PointerEventData)data); });
            trigger.triggers.Add(entryDrag);

            var entryUp = new UnityEngine.EventSystems.EventTrigger.Entry { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            entryUp.callback.AddListener((data) => { OnCardPointerUp(type, (UnityEngine.EventSystems.PointerEventData)data); });
            trigger.triggers.Add(entryUp);
        }

        private void SetARHierarchyActive(bool active)
        {
            if (_arSession != null) _arSession.gameObject.SetActive(active);
            if (_xrOrigin != null) _xrOrigin.gameObject.SetActive(active);
            if (_arOverlayUI != null) _arOverlayUI.SetActive(active);
            if (_reticle != null) _reticle.SetActive(false);
            if (_componentTrayRoot != null) _componentTrayRoot.SetActive(false);
        }

        public void EnterARMode()
        {
            Debug.Log("[ARLab] AR Mode started");
            Debug.Log("[ARLab] AR MODE pressed");

            if (_labScene == null) _labScene = FindFirstObjectByType<MagnesiumLabScene>();
            _hasLoggedPlaneFound = false;
            _hasLoggedARCoreInit = false;
            _scanningTimer = 0f;
            _placedComponents.Clear();

            if (_permissionCoroutine != null) StopCoroutine(_permissionCoroutine);
            _permissionCoroutine = StartCoroutine(RequestCameraPermissionAndStartAR());
        }

        private IEnumerator RequestCameraPermissionAndStartAR()
        {
            _placementState = ARPlacementState.WaitingForPermission;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
                Debug.Log("[ARLab] Camera permission status: Requesting");
                bool permissionGranted = false;
                bool permissionDenied = false;

                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += (perm) => { permissionGranted = true; };
                callbacks.PermissionDenied += (perm) => { permissionDenied = true; };
                callbacks.PermissionDeniedAndDontAskAgain += (perm) => { permissionDenied = true; };

                Permission.RequestUserPermission(Permission.Camera, callbacks);

                float timeout = 15f;
                float timer = 0f;
                while (!permissionGranted && !permissionDenied && timer < timeout)
                {
                    timer += Time.unscaledDeltaTime;
                    if (Permission.HasUserAuthorizedPermission(Permission.Camera))
                    {
                        permissionGranted = true;
                        break;
                    }
                    yield return null;
                }

                if (!permissionGranted)
                {
                    Debug.LogWarning("[ARLab] Camera permission status: Denied");
                    SetPrompt("Camera permission is required for AR Mode.");
                    if (_arOverlayUI != null) _arOverlayUI.SetActive(true);
                    yield break;
                }
            }
#endif

            Debug.Log("[ARLab] Camera permission status: Granted");

            // ── Check ARCore Device Availability & Installation ──────────────────────
            Debug.Log("[ARLab] Checking ARCore availability on device...");
            yield return ARSession.CheckAvailability();
            Debug.Log($"[ARLab] ARSession.state after CheckAvailability: {ARSession.state}");

            if (ARSession.state == ARSessionState.NeedsInstall)
            {
                Debug.Log("[ARLab] ARCore requires installation. Starting install...");
                yield return ARSession.Install();
                Debug.Log($"[ARLab] ARSession.state after Install: {ARSession.state}");
            }

            if (ARSession.state == ARSessionState.Unsupported)
            {
                Debug.LogError("[ARLab] ARCore is unavailable on this device.");
                SetPrompt("ARCore is unavailable on this device.");
                if (_arOverlayUI != null) _arOverlayUI.SetActive(true);
                yield break;
            }

            // ── Resolve AR components (also subscribes plane events) ────────────────
            EnsureARComponents();

            // ── XR Loader: ensure active on Android ─────────────────────────────────
            try
            {
                var xrSettings = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                if (xrSettings != null && xrSettings.Manager != null)
                {
                    var mgr = xrSettings.Manager;
                    Debug.Log($"[ARLab] XR Loader init complete: {mgr.isInitializationComplete}");
                    Debug.Log($"[ARLab] XR Active Loader: {(mgr.activeLoader != null ? mgr.activeLoader.name : "none")}");

                    if (!mgr.isInitializationComplete)
                    {
                        Debug.Log("[ARLab] Initializing XR loader sync...");
                        mgr.InitializeLoaderSync();
                        Debug.Log($"[ARLab] XR Loader init after sync: {mgr.isInitializationComplete}, activeLoader: {(mgr.activeLoader != null ? mgr.activeLoader.name : "none")}");
                    }

                    if (mgr.isInitializationComplete && mgr.activeLoader != null)
                    {
                        Debug.Log("[ARLab] ARCore provider initialized");
                        mgr.StartSubsystems();
                    }
                }
                else
                {
                    Debug.LogWarning("[ARLab] XRGeneralSettings.Instance is null — check XR Plugin Management settings!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[ARLab] XR Manager note: {ex.Message}");
            }

            // ── Disable conflicting cameras ──────────────────────────────────────────
            DisableConflictingCameras();

            // ── Activate AR hierarchy ────────────────────────────────────────────────
            SetARHierarchyActive(true);

            // Wait one frame so all components fully wake up
            yield return null;

            // ── Re-apply detection mode AFTER subsystem is live ──────────────────────
            if (_arPlaneManager != null)
            {
                _arPlaneManager.enabled = true;
                _arPlaneManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
                Debug.Log($"[ARLab] PlaneManager.enabled = {_arPlaneManager.enabled}");
                Debug.Log($"[ARLab] Runtime plane detection mode = {_arPlaneManager.requestedDetectionMode}");

                if (_arPlaneManager.subsystem != null)
                {
                    Debug.Log($"[ARLab] XRPlaneSubsystem id = {_arPlaneManager.subsystem.subsystemDescriptor.id}");
                    Debug.Log($"[ARLab] XRPlaneSubsystem running = {_arPlaneManager.subsystem.running}");
                    Debug.Log($"[ARLab] supportsHorizontal = {_arPlaneManager.subsystem.subsystemDescriptor.supportsHorizontalPlaneDetection}");
                    Debug.Log($"[ARLab] supportsVertical = {_arPlaneManager.subsystem.subsystemDescriptor.supportsVerticalPlaneDetection}");
                }
            }
            else
            {
                Debug.LogError("[ARLab] _arPlaneManager is NULL after EnsureARComponents!");
            }

            if (_arPointCloudManager != null)
            {
                _arPointCloudManager.enabled = true;
                Debug.Log($"[ARLab] ARPointCloudManager.enabled = {_arPointCloudManager.enabled}");
            }

            if (_arRaycastManager != null)
            {
                _arRaycastManager.enabled = true;
                Debug.Log($"[ARLab] ARRaycastManager.enabled = {_arRaycastManager.enabled}");
            }

            // Status log block
            Debug.Log($"[ARLab] ARSession.state = {ARSession.state}");
            Debug.Log($"[ARLab] ARSession GameObject active: {(_arSession != null ? _arSession.gameObject.activeSelf.ToString() : "null")}");
            Debug.Log($"[ARLab] XROrigin GameObject active: {(_xrOrigin != null ? _xrOrigin.gameObject.activeSelf.ToString() : "null")}");
            Debug.Log($"[ARLab] AR Camera active: {(_arCamera != null ? _arCamera.gameObject.activeSelf.ToString() : "null")}");
            Debug.Log($"[ARLab] ARCameraManager enabled: {(_arCameraManager != null ? _arCameraManager.enabled.ToString() : "null")}");
            Debug.Log($"[ARLab] ARCameraBackground enabled: {(_arCameraBackground != null ? _arCameraBackground.enabled.ToString() : "null")}");
            Debug.Log($"[ARLab] Searching for horizontal planes");

            _placementState = ARPlacementState.Scanning;

            if (_labScene != null)
                _labScene.OnEnterARMode(_arCamera);

            SetPrompt("<b>Scan your workspace</b>\n<size=18><color=#93c5fd>Move your phone slowly over a flat surface.</color></size>");

            // ── Start periodic diagnostic coroutine ──────────────────────────────────
            StartCoroutine(PeriodicARDiagnostics());
        }

        private void DisableConflictingCameras()
        {
            _disabledCameras.Clear();
            var allCams = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var cam in allCams)
            {
                if (cam != _arCamera && cam.gameObject.activeInHierarchy)
                {
                    cam.gameObject.SetActive(false);
                    _disabledCameras.Add(cam);
                }
            }
        }

        private void RestoreDisabledCameras()
        {
            foreach (var cam in _disabledCameras)
            {
                if (cam != null)
                {
                    cam.gameObject.SetActive(true);
                }
            }
            _disabledCameras.Clear();
        }

        public void ExitARMode()
        {
            Debug.Log("[ARLab] AR mode exited");
            _placementState = ARPlacementState.Inactive;

            if (_permissionCoroutine != null)
            {
                StopCoroutine(_permissionCoroutine);
                _permissionCoroutine = null;
            }

            if (_ghostPreviewGO != null) Destroy(_ghostPreviewGO);

            SetARHierarchyActive(false);
            RestoreDisabledCameras();

            if (_labScene != null)
            {
                _labScene.OnExitARMode();
            }
        }

        public void ResetARExperiment()
        {
            Debug.Log("[ARLab] AR experiment reset");
            if (_labScene != null)
            {
                _labScene.ResetExperiment();
            }

            if (_componentTrayRoot != null) _componentTrayRoot.SetActive(true);
            SetPlaneVisualsActive(true);
            SetPrompt("Experiment reset. Surface preserved. Tap 'Start Experiment' or adjust equipment.");
        }

        private IEnumerator PeriodicARDiagnostics()
        {
            var waitSec = new WaitForSeconds(3.0f);
            while (IsARModeActive)
            {
                if (_placementState == ARPlacementState.Scanning)
                {
                    int planeCount = _arPlaneManager != null ? _arPlaneManager.trackables.count : -1;
                    int pointCount = _arPointCloudManager != null ? _arPointCloudManager.trackables.count : -1;
                    bool planeEnabled = _arPlaneManager != null && _arPlaneManager.enabled;
                    var reqMode = _arPlaneManager != null ? _arPlaneManager.requestedDetectionMode.ToString() : "null";
                    var curMode = _arPlaneManager != null ? _arPlaneManager.currentDetectionMode.ToString() : "null";
                    string trackingState = _arSession != null && _arSession.subsystem != null ? _arSession.subsystem.trackingState.ToString() : _lastCameraTrackingState;
                    bool xrRunning = _arSession != null && _arSession.subsystem != null && _arSession.subsystem.running;
                    bool planeSubRunning = _arPlaneManager != null && _arPlaneManager.subsystem != null && _arPlaneManager.subsystem.running;
                    string capability = "Unknown";
                    if (_arPlaneManager != null && _arPlaneManager.subsystem != null && _arPlaneManager.subsystem.subsystemDescriptor != null)
                    {
                        var d = _arPlaneManager.subsystem.subsystemDescriptor;
                        capability = $"H:{(d.supportsHorizontalPlaneDetection ? "Y" : "N")} V:{(d.supportsVerticalPlaneDetection ? "Y" : "N")}";
                    }
                    
                    Debug.Log($"[ARLab] --- AR RUNTIME DIAGNOSTICS ---");
                    Debug.Log($"[ARLab] ARSession.state = {ARSession.state}");
                    Debug.Log($"[ARLab] XR Session Running = {xrRunning}, Tracking State = {trackingState}");
                    Debug.Log($"[ARLab] PlaneManager.enabled = {planeEnabled}, RequestedMode = {reqMode}, CurrentMode = {curMode}");
                    Debug.Log($"[ARLab] Plane Subsystem Running = {planeSubRunning}, Capabilities = {capability}");
                    Debug.Log($"[ARLab] Plane count = {planeCount}, Point Cloud count = {pointCount}");
                    Debug.Log($"[ARLab] ARRaycastManager.enabled = {(_arRaycastManager != null ? _arRaycastManager.enabled.ToString() : "null")}");
                    Debug.Log($"[ARLab] Camera Pose = {(_arCamera != null ? _arCamera.transform.position.ToString() : "null")}");
                    Debug.Log($"[ARLab] ---------------------------------");
                }
                yield return waitSec;
            }
        }

        private void OnTrackablesChanged(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var plane in args.added)
            {
                if (!_loggedPlaneIds.Contains(plane.trackableId))
                {
                    _loggedPlaneIds.Add(plane.trackableId);
                    Debug.Log("[ARLab] REAL ARPLANE ADDED");
                    Debug.Log($"[ARLab] Plane ID = {plane.trackableId}");
                    Debug.Log($"[ARLab] Plane alignment = {plane.alignment}");
                    Debug.Log($"[ARLab] Plane center = {plane.center}");
                    Debug.Log($"[ARLab] Plane size = {plane.size}");

                    OnPlaneDiscovered();
                }
            }
            foreach (var plane in args.updated)
            {
                // Real plane updated
            }
            foreach (var removedPair in args.removed)
            {
                _loggedPlaneIds.Remove(removedPair.Key);
                Debug.Log($"[ARLab] Real plane removed: {removedPair.Key}");
            }
        }

        private void OnPlaneDiscovered()
        {
            if (!_hasLoggedPlaneFound)
            {
                _hasLoggedPlaneFound = true;
                if (_placementState == ARPlacementState.Scanning)
                {
                    _placementState = ARPlacementState.PlaneDetected;
                    if (_componentTrayRoot != null) _componentTrayRoot.SetActive(true);
                    SetPrompt("<b>Surface detected</b>\n<size=18><color=#86efac>Drag equipment onto the surface.</color></size>");
                    Debug.Log("[ARLab] Surface detected — drag a component onto the surface.");
                }
            }
        }

        private void Update()
        {
            if (!IsARModeActive) return;

            UpdateDiagnosticOverlay();

            UpdateCenterReticle();

            if (_placementState == ARPlacementState.PlaneDetected)
            {
                // While a card drag is active, poll raw touch for AR raycasting
                // regardless of whether the finger is still over the UI card.
                if (_currentlyDraggingType != ARComponentType.None)
                {
                    UpdateActiveDrag();
                }
                else
                {
                    // Allow repositioning of already-placed objects
                    HandleEquipmentDragAndDrop();
                }
            }
            else if (_placementState == ARPlacementState.ExperimentActive)
            {
                HandlePlacedExperimentGestures();
            }
        }

        private void UpdateDiagnosticOverlay()
        {
            string sessionState = ARSession.state.ToString();
            string loaderName = "None";
            bool xrSessionRunning = false;
            string trackingState = _lastCameraTrackingState;
            string planeSubsystemRunning = "False";
            string planeCapability = "None";
            string deviceSupport = ARSession.state == ARSessionState.Unsupported ? "Unsupported" : "Supported";

            try
            {
                if (UnityEngine.XR.Management.XRGeneralSettings.Instance != null &&
                    UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager != null &&
                    UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader != null)
                {
                    loaderName = UnityEngine.XR.Management.XRGeneralSettings.Instance.Manager.activeLoader.name;
                }

                if (_arSession != null && _arSession.subsystem != null)
                {
                    xrSessionRunning = _arSession.subsystem.running;
                    trackingState = _arSession.subsystem.trackingState.ToString();
                }
            }
            catch { }

            bool planeEnabled = _arPlaneManager != null && _arPlaneManager.enabled;
            string reqMode = _arPlaneManager != null ? _arPlaneManager.requestedDetectionMode.ToString() : "None";
            string curMode = _arPlaneManager != null ? _arPlaneManager.currentDetectionMode.ToString() : "None";
            int planeCount = _arPlaneManager != null ? _arPlaneManager.trackables.count : 0;
            int pointCloudCount = _arPointCloudManager != null ? _arPointCloudManager.trackables.count : 0;
            bool raycastEnabled = _arRaycastManager != null && _arRaycastManager.enabled;

            if (_arPlaneManager != null && _arPlaneManager.subsystem != null)
            {
                planeSubsystemRunning = _arPlaneManager.subsystem.running.ToString();
                var desc = _arPlaneManager.subsystem.subsystemDescriptor;
                if (desc != null)
                {
                    planeCapability = $"H:{(desc.supportsHorizontalPlaneDetection ? "Y" : "N")} V:{(desc.supportsVerticalPlaneDetection ? "Y" : "N")}";
                }
            }

            if (_diagnosticText != null)
            {
                _diagnosticText.text = 
                    $"<b>AR Session:</b> {sessionState}\n" +
                    $"<b>ARCore:</b> {loaderName}\n" +
                    $"<b>Camera:</b> {trackingState}\n" +
                    $"<b>Plane Manager:</b> {(planeEnabled ? "Enabled" : "Disabled")}\n" +
                    $"<b>Detection:</b> {reqMode}\n" +
                    $"<b>Plane Count:</b> {planeCount}\n" +
                    $"<b>Point Cloud Count:</b> {pointCloudCount}\n" +
                    $"<b>Plane Subsystem Running:</b> {planeSubsystemRunning}\n" +
                    $"<b>ARCore Plane Capability:</b> {planeCapability}\n" +
                    $"<b>Device ARCore Support:</b> {deviceSupport}";
            }

            if (_placementState == ARPlacementState.Scanning)
            {
                if (planeCount > 0)
                {
                    OnPlaneDiscovered();
                }
                else
                {
                    SetPrompt("<b>Scan your workspace</b>\n<size=18><color=#93c5fd>Move your phone slowly over a flat surface.</color></size>");
                }
            }
        }

        private void UpdateCenterReticle()
        {
            var screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            if (_arRaycastManager != null && _arRaycastManager.Raycast(screenCenter, _raycastHits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated))
            {
                _currentReticlePose = _raycastHits[0].pose;
                _reticlePoseValid = true;

                if (_reticle != null && _placementState != ARPlacementState.ExperimentActive)
                {
                    _reticle.SetActive(true);
                    _reticle.transform.position = _currentReticlePose.position;
                    _reticle.transform.rotation = _currentReticlePose.rotation;
                }
            }
            else
            {
                _reticlePoseValid = false;
                if (_reticle != null) _reticle.SetActive(false);
            }
        }

        // ═════════════════════════════════════════════════════
        //  DRAG & DROP FROM TRAY ONTO DETECTED REAL PLANE
        //  Touch is tracked in Update() independently of card
        //  bounds, so dragging off-card still works on Android.
        // ═════════════════════════════════════════════════════
        private void OnCardPointerDown(ARComponentType type, UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Skip if already placed (don't re-drag placed components)
            if (_placedComponents.Contains(type)) return;
            // Skip if another drag is already active
            if (_currentlyDraggingType != ARComponentType.None) return;

            _currentlyDraggingType = type;
            _activeDragFingerId = eventData.pointerId;  // fingerId on touch, -1 on mouse
            _hasValidHitPose = false;

            CreateGhostPreview(type);
            SetPrompt("<b>Move over the detected surface</b>");
            Debug.Log($"[ARLab] Placement started: {type}");
        }

        private void OnCardPointerDrag(ARComponentType type, UnityEngine.EventSystems.PointerEventData eventData)
        {
            // UpdateActiveDrag() in Update() handles all AR raycasting.
            // This callback fires only while finger is over the card, so we
            // deliberately leave it empty — Update() covers the full drag.
        }

        private void OnCardPointerUp(ARComponentType type, UnityEngine.EventSystems.PointerEventData eventData)
        {
            // Only act if this is the drag we started
            if (_currentlyDraggingType != type) return;
            FinishDrag(type);
        }

        // Called every frame while a card drag is active.
        // Polls raw touch input so the AR raycast continues even when
        // the finger has moved outside the UI card's bounds.
        private void UpdateActiveDrag()
        {
            Vector2 screenPos = Vector2.zero;
            bool foundInput = false;
            bool inputEnded = false;

#if UNITY_EDITOR || UNITY_STANDALONE
            // Editor / standalone: mouse emulation
            screenPos = Input.mousePosition;
            if (Input.GetMouseButton(0))
            {
                foundInput = true;
            }
            else
            {
                inputEnded = true;
            }
#else
            // Device: real touch tracking by fingerId
            for (int i = 0; i < Input.touchCount; i++)
            {
                var t = Input.GetTouch(i);
                if (t.fingerId == _activeDragFingerId || _activeDragFingerId == -1)
                {
                    screenPos = t.position;
                    if (t.phase == TouchPhase.Ended || t.phase == TouchPhase.Canceled)
                    {
                        inputEnded = true;
                    }
                    else
                    {
                        foundInput = true;
                    }
                    break;
                }
            }
            // If the tracked finger is gone from Input.touches, treat as ended
            if (!foundInput && !inputEnded)
            {
                inputEnded = true;
            }
#endif

            if (inputEnded)
            {
                FinishDrag(_currentlyDraggingType);
                return;
            }

            if (!foundInput) return;

            // AR raycast at current touch/mouse screen position
            bool hitValid = _arRaycastManager != null &&
                            _arRaycastManager.Raycast(screenPos, _raycastHits,
                                TrackableType.PlaneWithinPolygon | TrackableType.PlaneEstimated);

            if (hitValid)
            {
                _lastValidHitPose = _raycastHits[0].pose;
                _hasValidHitPose = true;
                SetPrompt("<b>Release to place</b>");

                if (_ghostPreviewGO != null)
                {
                    _ghostPreviewGO.SetActive(true);
                    _ghostPreviewGO.transform.position = _lastValidHitPose.position;

                    Vector3 camFwd = _arCamera != null
                        ? (_arCamera.transform.position - _lastValidHitPose.position)
                        : Vector3.forward;
                    camFwd.y = 0f;
                    if (camFwd.sqrMagnitude > 0.001f)
                        _ghostPreviewGO.transform.rotation = Quaternion.LookRotation(-camFwd.normalized, Vector3.up);
                    else
                        _ghostPreviewGO.transform.rotation = _lastValidHitPose.rotation;
                }
            }
            else
            {
                _hasValidHitPose = false;
                if (_ghostPreviewGO != null) _ghostPreviewGO.SetActive(false);
                SetPrompt("<b>Move over the detected surface</b>");
            }
        }

        // Finalises a card drag: places the object if a valid plane hit exists,
        // or cancels gracefully. Resets all drag state.
        private void FinishDrag(ARComponentType type)
        {
            // Destroy ghost preview
            if (_ghostPreviewGO != null)
            {
                Destroy(_ghostPreviewGO);
                _ghostPreviewGO = null;
            }

            if (_hasValidHitPose)
            {
                PlaceIndividualEquipment(type, _lastValidHitPose);
            }
            else
            {
                // No valid AR plane hit — do NOT place, do NOT mark as "Placed"
                SetPrompt("<b>Surface detected</b>\n<size=18><color=#86efac>Drag equipment onto the surface.</color></size>");
                Debug.Log($"[ARLab] Drag cancelled for {type} — no valid AR plane hit recorded.");
            }

            _currentlyDraggingType = ARComponentType.None;
            _activeDragFingerId = -1;
            _hasValidHitPose = false;
        }

        private void CreateGhostPreview(ARComponentType type)
        {
            if (_ghostPreviewGO != null) Destroy(_ghostPreviewGO);

            _ghostPreviewGO = new GameObject($"GhostPreview_{type}");
            var mf = _ghostPreviewGO.AddComponent<MeshFilter>();
            var mr = _ghostPreviewGO.AddComponent<MeshRenderer>();

            Mesh targetMesh = null;
            switch (type)
            {
                case ARComponentType.Burner:
                    targetMesh = Resources.Load<Mesh>("Models/BunsenBurner") ?? CreateCylinderMesh(0.04f, 0.22f);
                    break;
                case ARComponentType.Ribbon:
                    targetMesh = CreateBoxMesh(new Vector3(0.015f, 0.002f, 0.060f));
                    break;
                case ARComponentType.Tongs:
                    targetMesh = Resources.Load<Mesh>("Models/CrucibleTongs") ?? CreateBoxMesh(new Vector3(0.04f, 0.02f, 0.18f));
                    break;
                case ARComponentType.Sandpaper:
                    targetMesh = CreateBoxMesh(new Vector3(0.075f, 0.010f, 0.075f));
                    break;
                case ARComponentType.WatchGlass:
                    targetMesh = CreateCylinderMesh(0.06f, 0.012f);
                    break;
                default:
                    targetMesh = CreateBoxMesh(new Vector3(0.1f, 0.05f, 0.1f));
                    break;
            }

            mf.sharedMesh = targetMesh;
            var gMat = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Sprites/Default"));
            gMat.color = new Color(0.20f, 0.85f, 1.0f, 0.55f);
            if (gMat.HasProperty("_BaseColor")) gMat.SetColor("_BaseColor", new Color(0.20f, 0.85f, 1.0f, 0.55f));
            gMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            gMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            gMat.SetInt("_ZWrite", 0);
            gMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mr.material = mr.sharedMaterial = gMat;

            _ghostPreviewGO.SetActive(false);
        }

        private Mesh CreateBoxMesh(Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var m = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            Destroy(go);
            var verts = m.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = Vector3.Scale(verts[i], size);
            }
            m.vertices = verts;
            m.RecalculateBounds();
            return m;
        }

        private Mesh CreateCylinderMesh(float radius, float height)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            var m = Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            Destroy(go);
            var verts = m.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                verts[i] = new Vector3(verts[i].x * radius * 2f, verts[i].y * height * 0.5f, verts[i].z * radius * 2f);
            }
            m.vertices = verts;
            m.RecalculateBounds();
            return m;
        }

        private void PlaceIndividualEquipment(ARComponentType type, Pose hitPose)
        {
            Debug.Log($"[ARLab] Placement started: {type}");
            Debug.Log($"[ARLab] Raycast hit: Position = {hitPose.position} Rotation = {hitPose.rotation.eulerAngles} " +
                      $"Distance = {(_arCamera != null ? Vector3.Distance(_arCamera.transform.position, hitPose.position) : -1f):F2}m");

            if (_labScene == null) _labScene = FindFirstObjectByType<MagnesiumLabScene>();
            if (_labScene == null)
            {
                Debug.LogError("[ARLab] Placement FAILED: MagnesiumLabScene not found.");
                return;
            }

            // Anchor the lab root to the detected plane on first placement
            if (_placedComponents.Count == 0)
            {
                _labScene.transform.position = hitPose.position;
                Vector3 camFwd = _arCamera != null
                    ? (_arCamera.transform.position - hitPose.position)
                    : Vector3.forward;
                camFwd.y = 0f;
                _labScene.transform.rotation = camFwd.sqrMagnitude > 0.001f
                    ? Quaternion.LookRotation(-camFwd.normalized, Vector3.up)
                    : hitPose.rotation;
                _labScene.transform.localScale = Vector3.one * 0.85f;
            }

            LabTag tag = type switch
            {
                ARComponentType.Burner     => LabTag.Burner,
                ARComponentType.Ribbon     => LabTag.Ribbon,
                ARComponentType.Tongs      => LabTag.Tongs,
                ARComponentType.Sandpaper  => LabTag.Sandpaper,
                ARComponentType.WatchGlass => LabTag.WatchGlass,
                _ => LabTag.None
            };

            Debug.Log($"[ARLab] Instantiating {type}");

            // Orient the dropped object to face the camera
            Vector3 fwd = _arCamera != null
                ? (_arCamera.transform.position - hitPose.position)
                : Vector3.forward;
            fwd.y = 0f;
            Quaternion faceCamera = fwd.sqrMagnitude > 0.001f
                ? Quaternion.LookRotation(-fwd.normalized, Vector3.up)
                : hitPose.rotation;

            _labScene.PositionEquipmentInAR(tag, hitPose.position, faceCamera);

            // ── Verify placement ──────────────────────────────────────────────
            GameObject placedObject = tag switch
            {
                LabTag.Burner     => _labScene.BurnerObject,
                LabTag.Ribbon     => _labScene.RibbonObject,
                LabTag.Tongs      => _labScene.TongsObject,
                LabTag.Sandpaper  => _labScene.SandpaperObject,
                LabTag.WatchGlass => _labScene.WatchGlassObject,
                _ => null
            };

            bool placementVerified = false;
            if (placedObject != null)
            {
                bool isActive          = placedObject.activeSelf;
                bool isActiveHierarchy = placedObject.activeInHierarchy;
                var renderers          = placedObject.GetComponentsInChildren<Renderer>(true);
                bool anyRendererOn     = false;
                Bounds combined        = new Bounds(placedObject.transform.position, Vector3.zero);
                bool hasBounds         = false;

                foreach (var r in renderers)
                {
                    if (r.enabled && r.gameObject.activeInHierarchy)
                    {
                        anyRendererOn = true;
                        if (!hasBounds) { combined = r.bounds; hasBounds = true; }
                        else combined.Encapsulate(r.bounds);
                    }
                }

                Vector3 pos   = placedObject.transform.position;
                Vector3 scale = placedObject.transform.localScale;
                bool hasValidBounds = hasBounds && combined.size.magnitude > 0.001f;

                Debug.Log($"[ARLab] Object active = {isActive}");
                Debug.Log($"[ARLab] Renderer enabled = {anyRendererOn}");
                Debug.Log($"[ARLab] Object position = {pos}");
                Debug.Log($"[ARLab] Object scale = {scale}");
                Debug.Log($"[ARLab] Object bounds = {(hasBounds ? combined.ToString() : "none")}");

                placementVerified = isActive && isActiveHierarchy && anyRendererOn && hasValidBounds;
            }
            else
            {
                Debug.LogWarning($"[ARLab] Could not retrieve placed object reference for {type}.");
            }

            if (placementVerified)
            {
                _placedComponents.Add(type);

                if (_cardStatusTexts.TryGetValue(type, out var stTxt))
                {
                    stTxt.text  = "✓ Placed";
                    stTxt.color = new Color(0.40f, 1.0f, 0.50f);
                }
                if (_cardBackgrounds.TryGetValue(type, out var bgImg))
                {
                    bgImg.color = new Color(0.08f, 0.28f, 0.18f, 0.92f);
                }

                Debug.Log($"[ARLab] Placement SUCCESS: {type}");
                SetPrompt($"{type} placed! Drag next component or tap 'Start Experiment'.");
            }
            else
            {
                Debug.LogError($"[ARLab] Placement FAILED: {type} — object is not visible after placement.");
                SetPrompt($"Placement failed for {type}. Try again over the detected surface.");
            }
        }

        private void OnPlaceAllEquipmentClicked()
        {
            if (_labScene == null) _labScene = FindFirstObjectByType<MagnesiumLabScene>();
            if (_labScene == null) return;

            // Require a real detected plane — do not use a fallback guess
            if (!_reticlePoseValid)
            {
                SetPrompt("<b>No surface detected yet</b>\n<size=18><color=#fca5a5>Move your phone slowly over a flat surface.</color></size>");
                Debug.Log("[ARLab] PlaceAll skipped — no valid AR plane detected yet.");
                return;
            }

            PlaceLaboratory(_currentReticlePose);

            // Verify each object and update tray only for those actually visible
            var types   = new[] { ARComponentType.Burner, ARComponentType.Ribbon, ARComponentType.Tongs, ARComponentType.Sandpaper, ARComponentType.WatchGlass };
            var objects = new GameObject[]
            {
                _labScene.BurnerObject, _labScene.RibbonObject, _labScene.TongsObject,
                _labScene.SandpaperObject, _labScene.WatchGlassObject
            };

            for (int i = 0; i < types.Length; i++)
            {
                var obj = objects[i];
                bool verified = false;
                if (obj != null && obj.activeSelf && obj.activeInHierarchy)
                {
                    var rends = obj.GetComponentsInChildren<Renderer>(true);
                    foreach (var r in rends)
                    {
                        if (r.enabled && r.gameObject.activeInHierarchy) { verified = true; break; }
                    }
                }

                if (verified)
                {
                    _placedComponents.Add(types[i]);
                    if (_cardStatusTexts.TryGetValue(types[i], out var stTxt))
                    {
                        stTxt.text  = "✓ Placed";
                        stTxt.color = new Color(0.40f, 1.0f, 0.50f);
                    }
                    if (_cardBackgrounds.TryGetValue(types[i], out var bgImg))
                    {
                        bgImg.color = new Color(0.08f, 0.28f, 0.18f, 0.92f);
                    }
                    Debug.Log($"[ARLab] PlaceAll — {types[i]} verified placed.");
                }
                else
                {
                    Debug.LogWarning($"[ARLab] PlaceAll — {types[i]} NOT verified after placement.");
                }
            }

            SetPrompt("All equipment placed on surface! Tap 'Start Experiment' to begin.");
        }

        public void PlaceLaboratory(Pose pose)
        {
            if (_labScene == null) _labScene = FindFirstObjectByType<MagnesiumLabScene>();
            if (_labScene == null) return;

            _labScene.PlaceAllEquipmentInAR(pose);

            Debug.Log("[ARLab] Laboratory placed on detected real plane");
            if (_reticle != null) _reticle.SetActive(false);
        }

        private void OnStartExperimentClicked()
        {
            // If user taps Start Experiment before placing anything, auto-place at reticle
            if (_placedComponents.Count == 0)
            {
                OnPlaceAllEquipmentClicked();
            }

            _placementState = ARPlacementState.ExperimentActive;
            Debug.Log("[ARLab] Experiment started in AR");

            if (_reticle != null) _reticle.SetActive(false);
            if (_componentTrayRoot != null) _componentTrayRoot.SetActive(false);
            SetPlaneVisualsActive(false);

            if (_labScene != null)
            {
                _labScene.OnEnterARMode(_arCamera);
                _labScene.SetState(LabState.Step1_SelectRibbon);
            }

            SetPrompt("Step 1: Tap the Magnesium Ribbon to begin.");
        }

        private void HandleEquipmentDragAndDrop()
        {
            // Allow moving placed 3D equipment on the table surface before starting experiment
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    if (UnityEngine.EventSystems.EventSystem.current != null &&
                        UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject(touch.fingerId))
                        return;

                    Ray ray = _arCamera.ScreenPointToRay(touch.position);
                    if (Physics.Raycast(ray, out RaycastHit hit, 10f))
                    {
                        var interactable = hit.collider.GetComponentInParent<LabInteractable>();
                        if (interactable != null)
                        {
                            _selectedEquipmentGO = interactable.gameObject;
                            _equipmentDragPlane = new Plane(Vector3.up, _selectedEquipmentGO.transform.position);
                            if (_equipmentDragPlane.Raycast(ray, out float enter))
                            {
                                _equipmentDragOffset = _selectedEquipmentGO.transform.position - ray.GetPoint(enter);
                            }
                        }
                    }
                }
                else if (touch.phase == TouchPhase.Moved && _selectedEquipmentGO != null)
                {
                    Ray ray = _arCamera.ScreenPointToRay(touch.position);
                    if (_equipmentDragPlane.Raycast(ray, out float enter))
                    {
                        Vector3 targetPos = ray.GetPoint(enter) + _equipmentDragOffset;
                        _selectedEquipmentGO.transform.position = targetPos;
                    }
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    _selectedEquipmentGO = null;
                }
            }
        }

        private void HandlePlacedExperimentGestures()
        {
            if (_labScene == null) return;

            // Two-finger pinch scale and rotation during experiment
            if (Input.touchCount == 2)
            {
                var t0 = Input.GetTouch(0);
                var t1 = Input.GetTouch(1);

                if (t0.phase == TouchPhase.Began || t1.phase == TouchPhase.Began)
                {
                    _initialPinchDistance = Vector2.Distance(t0.position, t1.position);
                    _initialLabScale = _labScene.transform.localScale;

                    Vector2 dir = t1.position - t0.position;
                    _initialTwistAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    _initialLabRotation = _labScene.transform.rotation;
                }
                else if (t0.phase == TouchPhase.Moved || t1.phase == TouchPhase.Moved)
                {
                    float currentDist = Vector2.Distance(t0.position, t1.position);
                    if (_initialPinchDistance > 10f)
                    {
                        float scaleFactor = currentDist / _initialPinchDistance;
                        float newScale = Mathf.Clamp(_initialLabScale.x * scaleFactor, 0.45f, 1.40f);
                        _labScene.transform.localScale = Vector3.one * newScale;
                    }

                    Vector2 curDir = t1.position - t0.position;
                    float curAngle = Mathf.Atan2(curDir.y, curDir.x) * Mathf.Rad2Deg;
                    float deltaAngle = Mathf.DeltaAngle(_initialTwistAngle, curAngle);
                    _labScene.transform.rotation = _initialLabRotation * Quaternion.Euler(0f, -deltaAngle, 0f);
                }
            }
        }

        private void SetPlaneVisualsActive(bool active)
        {
            if (_arPlaneManager == null) return;
            foreach (var plane in _arPlaneManager.trackables)
            {
                var r = plane.GetComponentInChildren<Renderer>();
                if (r != null) r.enabled = active;
                var lr = plane.GetComponentInChildren<LineRenderer>();
                if (lr != null) lr.enabled = active;
            }
        }

        private void SetPrompt(string msg)
        {
            if (_promptText != null) _promptText.text = msg;
        }
    }
}
