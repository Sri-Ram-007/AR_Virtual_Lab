// ============================================================
// ARExperimentMode.cs  —  AR Virtual Lab
// One AR mode for every experiment: the student finds a flat surface with the phone camera, taps it, and the whole
// lab appears on that surface at real size. The experiment is then performed exactly as on screen (drag, tap, pour...).
//
// How placement works: the experiment always lives at the world origin (all its code assumes that). Instead of moving
// the experiment, we move the AR camera's XR Origin so that the tapped point on the real surface maps onto the lab's
// bench. Every existing drag, tap, animation and physics ray therefore keeps working unchanged.
//
// Flow:   Starting -> Scanning (plane detection) -> Ready (a plane is found, aim + tap) -> Placed
//         "Move lab" goes to Repositioning, "Exit AR" returns to the normal on-screen experiment.
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ARVirtualLab.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARVirtualLab.AR
{
    /// <summary>What an experiment tells the AR mode about itself.</summary>
    public class ARExperimentSetup
    {
        public Transform SceneRoot;                 // holds the experiment's 3D objects (and its UI canvas)
        public Camera StandaloneCamera;             // the normal on-screen camera, switched off while in AR
        public GameObject Bench;                    // the virtual table, hidden in AR (the real table is used)
        public float BenchSurfaceY;                 // world height of the bench top
        public float BenchCenterZ;                  // world z of the middle of the apparatus
        public float DefaultScale = 1.6f;           // how much larger than life the lab is shown
        public Action<Camera> OnStarted;            // hand the experiment the AR camera
        public Action<Camera> OnStopped;            // hand it back the normal camera
    }

    public class ARExperimentMode : MonoBehaviour
    {
        public enum Phase { Starting, Scanning, Ready, Placed, Repositioning, Failed }

        /// <summary>True from the moment AR starts until the lab is first placed: experiments must ignore touches.</summary>
        public static bool BlockSceneInput { get { return _blockPlacing || _blockPinch; } }
        private static bool _blockPlacing, _blockPinch;
        public static bool Active { get { return _instance != null; } }

        private static ARExperimentMode _instance;

        private ARExperimentSetup _setup;
        private Phase _phase = Phase.Starting;

        // rig
        private GameObject _sessionGO, _originGO, _templatesGO;
        private XROrigin _origin;
        private Camera _arCamera;
        private ARCameraPoseDriver _poseDriver;
        private ARPlaneManager _planeManager;
        private ARRaycastManager _raycastManager;
        private readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

        // scene state
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private bool _benchWasActive;

        // reticle
        private GameObject _reticle;
        private Material _reticleMat;
        private bool _reticleOnPlane;
        private Pose _reticlePose;

        // UI
        private GameObject _canvasGO;
        private Image _statusBg;
        private TextMeshProUGUI _statusText;
        private Button _placeBtn, _moveBtn, _cancelBtn, _exitBtn;
        private float _statusHideAt = -1f;
        private float _nextLog;
        private static Texture2D _ringTex;

        // The lab is shown larger than life so it reads well on a phone: world = tracking x (1 / _arScale).
        private float _arScale = 1.6f;
        private const float MinScale = 0.8f, MaxScale = 3.5f;
        private Vector3 _anchorTracking;       // the tapped surface point, in tracking space
        private float _anchorYaw;
        private bool _pinching;
        private float _lastPinchDistance;

        private const float MinPlaneArea = 0.12f;      // m² — big enough for the lab

        // ═════════════════════════════════════════════════════
        //  PUBLIC API
        // ═════════════════════════════════════════════════════
        public static void Begin(ARExperimentSetup setup)
        {
            if (_instance != null) return;
            var go = new GameObject("ARExperimentMode");
            _instance = go.AddComponent<ARExperimentMode>();
            _instance._setup = setup;
            _instance._arScale = setup.DefaultScale > 0f ? setup.DefaultScale : 1.6f;
            _instance.StartCoroutine(_instance.Run());
        }

        public static void End()
        {
            if (_instance != null) _instance.Stop();
        }

        public static void Toggle(ARExperimentSetup setup)
        {
            if (Active) End(); else Begin(setup);
        }

        // ═════════════════════════════════════════════════════
        //  START-UP
        // ═════════════════════════════════════════════════════
        private IEnumerator Run()
        {
            BuildUI();
            SetStatus("Starting the camera...", false);

            if (Application.isEditor)
            {
                SetStatus("AR needs a phone with ARCore. Use the on-screen experiment here.", true);
                yield return new WaitForSecondsRealtime(2.5f);
                Stop();
                yield break;
            }

            yield return RequestCameraPermission();
            if (_phase == Phase.Failed) yield break;

            Debug.Log("[ARMode] camera permission ok, checking ARCore");
            yield return ARSession.CheckAvailability();
            Debug.Log("[ARMode] ARSession.state = " + ARSession.state);
            if (ARSession.state == ARSessionState.NeedsInstall) yield return ARSession.Install();
            if (ARSession.state == ARSessionState.Unsupported)
            {
                Fail("This phone does not support AR (ARCore). The experiment stays on screen.");
                yield break;
            }

            try
            {
                var xr = UnityEngine.XR.Management.XRGeneralSettings.Instance;
                if (xr != null && xr.Manager != null)
                {
                    if (!xr.Manager.isInitializationComplete) xr.Manager.InitializeLoaderSync();
                    if (xr.Manager.isInitializationComplete && xr.Manager.activeLoader != null) xr.Manager.StartSubsystems();
                }
            }
            catch (Exception e) { Debug.LogWarning("[ARMode] XR loader: " + e.Message); }

            BuildRig();
            Debug.Log("[ARMode] AR rig built (session, origin, camera, plane + raycast managers)");
            yield return null;
            yield return null;

            // from here the AR camera is the camera; the lab is invisible until it has been placed on a surface
            if (_setup.StandaloneCamera != null) _setup.StandaloneCamera.gameObject.SetActive(false);
            if (_setup.Bench != null) { _benchWasActive = _setup.Bench.activeSelf; _setup.Bench.SetActive(false); }
            HideExperiment();
            _blockPlacing = true;
            if (_setup.OnStarted != null) _setup.OnStarted(_arCamera);

            _phase = Phase.Scanning;
            Debug.Log("[ARMode] scanning for a flat surface");
            SetStatus("Move your phone slowly over a table or the floor to find a flat surface", false);
        }

        private IEnumerator RequestCameraPermission()
        {
#if UNITY_ANDROID
            if (Application.isEditor || Permission.HasUserAuthorizedPermission(Permission.Camera)) yield break;
            bool granted = false, denied = false;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => granted = true;
            cb.PermissionDenied += _ => denied = true;
            cb.PermissionDeniedAndDontAskAgain += _ => denied = true;
            Permission.RequestUserPermission(Permission.Camera, cb);
            float t = 0f;
            while (!granted && !denied && t < 20f)
            {
                t += Time.unscaledDeltaTime;
                if (Permission.HasUserAuthorizedPermission(Permission.Camera)) granted = true;
                yield return null;
            }
            if (!granted) Fail("Camera permission is needed for AR. Allow it in the phone settings.");
#else
            yield break;
#endif
        }

        private void Fail(string message)
        {
            _phase = Phase.Failed;
            SetStatus(message, true);
            StartCoroutine(StopAfter(3.5f));
        }

        private IEnumerator StopAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            Stop();
        }

        // ═════════════════════════════════════════════════════
        //  AR RIG
        // ═════════════════════════════════════════════════════
        private void BuildRig()
        {
            _sessionGO = new GameObject("AR Session (Experiment)");
            var session = _sessionGO.AddComponent<ARSession>();
            session.attemptUpdate = true;
            _sessionGO.AddComponent<ARInputManager>();

            _originGO = new GameObject("XR Origin (Experiment)");
            _origin = _originGO.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(_originGO.transform, false);
            _origin.CameraFloorOffsetObject = offset;

            var camGO = new GameObject("AR Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(offset.transform, false);
            _arCamera = camGO.AddComponent<Camera>();
            _arCamera.clearFlags = CameraClearFlags.SolidColor;
            _arCamera.backgroundColor = Color.black;
            _arCamera.nearClipPlane = 0.03f;
            _arCamera.farClipPlane = 30f;
            camGO.AddComponent<AudioListener>();
            var camMgr = camGO.AddComponent<ARCameraManager>();
            camMgr.autoFocusRequested = true;
            camGO.AddComponent<ARCameraBackground>();

            _poseDriver = camGO.AddComponent<ARCameraPoseDriver>();     // the camera must actually follow the phone

            try
            {
                var urp = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                urp.renderPostProcessing = false;
            }
            catch { }
            _origin.Camera = _arCamera;

            // plane detection + raycasts against the detected planes
            _raycastManager = _originGO.AddComponent<ARRaycastManager>();
            _planeManager = _originGO.AddComponent<ARPlaneManager>();
            _planeManager.requestedDetectionMode = PlaneDetectionMode.Horizontal;
            _planeManager.planePrefab = BuildPlaneTemplate();

            BuildReticle();
        }

        /// <summary>The look of a detected surface: a soft teal sheet with an outline.</summary>
        private GameObject BuildPlaneTemplate()
        {
            // Lives under an inactive parent so it never appears itself; instances made from it are active.
            _templatesGO = new GameObject("AR Templates");
            _templatesGO.SetActive(false);
            var t = new GameObject("PlaneVisual");
            t.transform.SetParent(_templatesGO.transform, false);

            t.AddComponent<MeshFilter>();
            var mr = t.AddComponent<MeshRenderer>();
            mr.sharedMaterial = MakeUnlit(new Color(EduTheme.Teal.r, EduTheme.Teal.g, EduTheme.Teal.b, 0.16f), true);
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            t.AddComponent<ARPlaneMeshVisualizer>();
            t.AddComponent<ARPlane>();

            var lr = t.AddComponent<LineRenderer>();
            lr.sharedMaterial = MakeUnlit(new Color(EduTheme.TealDark.r, EduTheme.TealDark.g, EduTheme.TealDark.b, 0.95f), false);
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.widthMultiplier = 0.004f;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return t;
        }

        private static Material MakeUnlit(Color c, bool transparent)
        {
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (transparent && m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f);
                m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);      // visible from both sides
            return m;
        }

        // ═════════════════════════════════════════════════════
        //  RETICLE
        // ═════════════════════════════════════════════════════
        private static float SS(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D RingTexture()
        {
            if (_ringTex != null) return _ringTex;
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = SS(0.68f, 0.78f, r) * (1f - SS(0.86f, 0.94f, r));
                float dot = 1f - SS(0.05f, 0.12f, r);
                t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(ring + dot)));
            }
            t.Apply();
            return _ringTex = t;
        }

        private void BuildReticle()
        {
            _reticle = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _reticle.name = "PlacementReticle";
            Destroy(_reticle.GetComponent<Collider>());
            var r = _reticle.GetComponent<Renderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _reticleMat = MakeUnlit(Color.white, true);
            if (_reticleMat.HasProperty("_BaseMap")) _reticleMat.SetTexture("_BaseMap", RingTexture());
            _reticleMat.mainTexture = RingTexture();
            r.sharedMaterial = _reticleMat;
            _reticle.transform.localScale = Vector3.one * 0.22f;
            _reticle.SetActive(false);
        }

        // ═════════════════════════════════════════════════════
        //  PER-FRAME
        // ═════════════════════════════════════════════════════
        private void Update()
        {
            if (_statusHideAt > 0f && Time.unscaledTime >= _statusHideAt) { _statusBg.gameObject.SetActive(false); _statusHideAt = -1f; }
            if (_arCamera == null || _raycastManager == null) return;
            if (_phase == Phase.Failed || _phase == Phase.Starting) return;

            if (Time.unscaledTime >= _nextLog)      // heartbeat so a phone log shows what AR is doing
            {
                _nextLog = Time.unscaledTime + 2.5f;
                Debug.Log("[ARMode] phase=" + _phase + " session=" + ARSession.state + " planes=" + _planeManager.trackables.count +
                          " camPos=" + _arCamera.transform.position.ToString("F2") + " pose=" + (_poseDriver != null ? _poseDriver.Source : "-"));
            }

            bool placing = _phase == Phase.Scanning || _phase == Phase.Ready || _phase == Phase.Repositioning;
            if (placing)
            {
                // reticle follows the middle of the screen
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                _reticleOnPlane = _raycastManager.Raycast(center, _hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds);
                if (_reticleOnPlane)
                {
                    _reticlePose = _hits[0].pose;
                    _reticle.SetActive(true);
                    _reticle.transform.SetPositionAndRotation(_reticlePose.position + Vector3.up * 0.002f * _origin.transform.localScale.x,
                        _reticlePose.rotation * Quaternion.Euler(90f, 0f, 0f));
                    float pulse = 1f + 0.06f * Mathf.Sin(Time.unscaledTime * 4f);
                    _reticle.transform.localScale = Vector3.one * 0.20f * pulse * _origin.transform.localScale.x;
                }
                else _reticle.SetActive(false);

                if (_phase == Phase.Scanning && HasUsablePlane())
                {
                    _phase = Phase.Ready;
                    SetStatus("Surface found. Aim at the spot you want and tap to place the lab", false);
                }
                if (_placeBtn != null) _placeBtn.gameObject.SetActive(_reticleOnPlane);

                if (TapPosition(out Vector2 tap) && _raycastManager.Raycast(tap, _hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds))
                    PlaceLab(_hits[0].pose);
            }
            else
            {
                if (_reticle != null && _reticle.activeSelf) _reticle.SetActive(false);
                if (_phase == Phase.Placed) HandlePinch();
            }
        }

        private void LateUpdate()
        {
            // once placed, the detected-surface sheets are hidden (the lab is what matters)
            if (_planeManager == null) return;
            bool show = _phase != Phase.Placed;
            foreach (var plane in _planeManager.trackables)
            {
                if (plane == null) continue;
                foreach (var r in plane.GetComponents<Renderer>()) if (r.enabled != show) r.enabled = show;
                var lr = plane.GetComponent<LineRenderer>();
                if (lr != null && lr.enabled != show) lr.enabled = show;
            }
        }

        private bool HasUsablePlane()
        {
            foreach (var plane in _planeManager.trackables)
            {
                if (plane == null) continue;
                if (plane.size.x * plane.size.y >= MinPlaneArea) return true;
            }
            return false;
        }

        private static bool TapPosition(out Vector2 pos)
        {
            pos = Vector2.zero;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (Input.touchCount > 0)
            {
                var t = Input.GetTouch(0);
                if (t.phase != UnityEngine.TouchPhase.Began) return false;
                if (es != null && es.IsPointerOverGameObject(t.fingerId)) return false;
                pos = t.position;
                return true;
            }
            if (Input.GetMouseButtonDown(0))
            {
                if (es != null && es.IsPointerOverGameObject()) return false;
                pos = Input.mousePosition;
                return true;
            }
            return false;
        }

        // ═════════════════════════════════════════════════════
        //  PLACE THE LAB
        // ═════════════════════════════════════════════════════
        private void PlaceLab(Pose surface)
        {
            var o = _origin.transform;
            float k = o.localScale.x;
            Debug.Log("[ARMode] placing the lab at " + surface.position.ToString("F2") + " (size x" + _arScale.ToString("F1") + ")");

            // where the tapped point sits in tracking space (before we move anything)
            _anchorTracking = Quaternion.Inverse(o.rotation) * (surface.position - o.position) / Mathf.Max(0.0001f, k);

            // turn the lab to face the way the student is looking
            Vector3 look = _arCamera.transform.forward; look.y = 0f;
            if (look.sqrMagnitude < 0.01f) { look = _arCamera.transform.up; look.y = 0f; }
            look.Normalize();
            Vector3 lookInTracking = Quaternion.Inverse(o.rotation) * look;
            _anchorYaw = Mathf.Atan2(lookInTracking.x, lookInTracking.z) * Mathf.Rad2Deg;

            ApplyPlacement();

            _phase = Phase.Placed;
            _reticle.SetActive(false);
            ShowExperiment();
            _blockPlacing = false;
            RefreshButtons();
            SetStatus("Lab placed. Pinch with two fingers to resize it. Follow the steps to do the experiment", false, 5f);
        }

        /// <summary>Puts the tapped surface point exactly on the middle of the lab bench, at the current size.</summary>
        private void ApplyPlacement()
        {
            var o = _origin.transform;
            float k = 1f / _arScale;
            Quaternion rot = Quaternion.Euler(0f, -_anchorYaw, 0f);
            Vector3 benchCenter = new Vector3(0f, _setup.BenchSurfaceY, _setup.BenchCenterZ);
            o.localScale = Vector3.one * k;
            o.SetPositionAndRotation(benchCenter - rot * (k * _anchorTracking), rot);
        }

        /// <summary>Two-finger pinch resizes the lab around the spot it was placed.</summary>
        private void HandlePinch()
        {
            if (Input.touchCount == 2)
            {
                var a = Input.GetTouch(0); var b = Input.GetTouch(1);
                float d = Vector2.Distance(a.position, b.position);
                if (!_pinching || a.phase == UnityEngine.TouchPhase.Began || b.phase == UnityEngine.TouchPhase.Began)
                {
                    _pinching = true; _lastPinchDistance = d; _blockPinch = true;
                    return;
                }
                if (_lastPinchDistance > 1f && d > 1f)
                {
                    _arScale = Mathf.Clamp(_arScale * (d / _lastPinchDistance), MinScale, MaxScale);
                    ApplyPlacement();
                }
                _lastPinchDistance = d;
            }
            else if (_pinching)
            {
                _pinching = false;
                _blockPinch = false;
            }
        }

        private void HideExperiment()
        {
            _hiddenRenderers.Clear();
            if (_setup.SceneRoot == null) return;
            foreach (var r in _setup.SceneRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || !r.enabled) continue;
                if (r.GetComponentInParent<Canvas>() != null) continue;       // never the app UI
                r.enabled = false;
                _hiddenRenderers.Add(r);
            }
        }

        private void ShowExperiment()
        {
            foreach (var r in _hiddenRenderers) if (r != null) r.enabled = true;
            _hiddenRenderers.Clear();
        }

        // ═════════════════════════════════════════════════════
        //  LEAVE AR
        // ═════════════════════════════════════════════════════
        private void Stop()
        {
            ShowExperiment();
            _blockPlacing = false; _blockPinch = false;

            if (_setup != null)
            {
                if (_setup.Bench != null && _benchWasActive) _setup.Bench.SetActive(true);
                if (_setup.StandaloneCamera != null) _setup.StandaloneCamera.gameObject.SetActive(true);
                if (_setup.OnStopped != null) _setup.OnStopped(_setup.StandaloneCamera);
            }

            if (_reticle != null) Destroy(_reticle);
            if (_sessionGO != null) Destroy(_sessionGO);
            if (_originGO != null) Destroy(_originGO);
            if (_templatesGO != null) Destroy(_templatesGO);
            if (_canvasGO != null) Destroy(_canvasGO);
            _instance = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_instance == this) { _blockPlacing = false; _blockPinch = false; _instance = null; }
        }

        // ═════════════════════════════════════════════════════
        //  UI (kept out of the way of each experiment's own top and bottom bars)
        // ═════════════════════════════════════════════════════
        private void BuildUI()
        {
            _canvasGO = new GameObject("ARModeCanvas", typeof(RectTransform));
            var canvas = _canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30;
            var scaler = _canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            _canvasGO.AddComponent<GraphicRaycaster>();
            var safe = UiKit.Node(_canvasGO.transform, "Safe");
            UiKit.Fill(safe);

            _statusBg = UiKit.Box(safe, "Status", EduTheme.Paper, 28);
            UiKit.Anchor(_statusBg.rectTransform, 0.06f, 0.715f, 0.94f, 0.795f);
            _statusBg.raycastTarget = false;
            _statusText = UiKit.Text(_statusBg.transform, "Text", "", 30, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(_statusText.rectTransform);
            _statusText.rectTransform.offsetMin = new Vector2(24f, 6f);
            _statusText.rectTransform.offsetMax = new Vector2(-24f, -6f);
            _statusText.enableAutoSizing = true; _statusText.fontSizeMin = 24f; _statusText.fontSizeMax = 32f;

            _placeBtn = UiKit.Button(safe, "PlaceHere", "Place lab here", EduTheme.Teal, Color.white, 38, 22);
            UiKit.Anchor(_placeBtn.GetComponent<RectTransform>(), 0.20f, 0.20f, 0.80f, 0.265f);
            _placeBtn.onClick.AddListener(() => { if (_reticleOnPlane) PlaceLab(_reticlePose); });
            _placeBtn.gameObject.SetActive(false);

            _moveBtn = UiKit.Button(safe, "MoveLab", "Move lab", EduTheme.Neutral, EduTheme.Ink, 32, 18);
            UiKit.Anchor(_moveBtn.GetComponent<RectTransform>(), 0.05f, 0.105f, 0.47f, 0.165f);
            _moveBtn.onClick.AddListener(() => { _phase = Phase.Repositioning; _blockPlacing = true; RefreshButtons(); SetStatus("Aim at a new spot and tap to move the lab", false); });

            _cancelBtn = UiKit.Button(safe, "CancelMove", "Keep here", EduTheme.Neutral, EduTheme.Ink, 32, 18);
            UiKit.Anchor(_cancelBtn.GetComponent<RectTransform>(), 0.05f, 0.105f, 0.47f, 0.165f);
            _cancelBtn.onClick.AddListener(() => { _phase = Phase.Placed; _blockPlacing = false; _blockPinch = false; _reticle.SetActive(false); RefreshButtons(); _statusBg.gameObject.SetActive(false); });

            _exitBtn = UiKit.Button(safe, "ExitAR", "Exit AR", EduTheme.Red, Color.white, 32, 18);
            UiKit.Anchor(_exitBtn.GetComponent<RectTransform>(), 0.53f, 0.105f, 0.95f, 0.165f);
            _exitBtn.onClick.AddListener(Stop);

            RefreshButtons();
        }

        private void RefreshButtons()
        {
            if (_moveBtn != null) _moveBtn.gameObject.SetActive(_phase == Phase.Placed);
            if (_cancelBtn != null) _cancelBtn.gameObject.SetActive(_phase == Phase.Repositioning);
            if (_placeBtn != null && _phase == Phase.Placed) _placeBtn.gameObject.SetActive(false);
        }

        private void SetStatus(string text, bool problem, float autoHideSeconds = -1f)
        {
            if (_statusText == null) return;
            _statusBg.gameObject.SetActive(true);
            _statusText.text = MobileText.Clean(text);
            _statusBg.color = problem ? EduTheme.Orange : EduTheme.Paper;
            _statusText.color = problem ? Color.white : EduTheme.Ink;
            _statusHideAt = autoHideSeconds > 0f ? Time.unscaledTime + autoHideSeconds : -1f;
        }
    }

    /// <summary>
    /// Moves the AR camera with the phone. Reads the pose straight from the device instead of relying on the
    /// TrackedPoseDriver's input-action bindings (which left the camera frozen, so the lab seemed glued to the screen).
    /// </summary>
    public class ARCameraPoseDriver : MonoBehaviour
    {
        public string Source { get; private set; } = "none";
        private static readonly List<UnityEngine.XR.InputDevice> _xrDevices = new List<UnityEngine.XR.InputDevice>();

        private void OnEnable() { Application.onBeforeRender += Apply; }
        private void OnDisable() { Application.onBeforeRender -= Apply; }
        private void Update() { Apply(); }

        private void Apply()
        {
            // 1) the AR handheld device exposed by ARFoundation through the Input System
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
            {
                if (d.layout != "HandheldARInputDevice") continue;
                var pc = d.TryGetChildControl<Vector3Control>("devicePosition");
                var rc = d.TryGetChildControl<QuaternionControl>("deviceRotation");
                if (pc == null || rc == null) continue;
                Quaternion r = rc.ReadValue();
                if (r.x == 0f && r.y == 0f && r.z == 0f && r.w == 0f) continue;      // no data yet
                transform.SetLocalPositionAndRotation(pc.ReadValue(), r);
                Source = "HandheldARInputDevice";
                return;
            }

            // 2) the classic XR input device (ARCore's tracked device)
            UnityEngine.XR.InputDevices.GetDevicesWithCharacteristics(UnityEngine.XR.InputDeviceCharacteristics.TrackedDevice, _xrDevices);
            foreach (var dev in _xrDevices)
            {
                Vector3 pos; Quaternion rot;
                if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos) &&
                    dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot))
                {
                    transform.SetLocalPositionAndRotation(pos, rot);
                    Source = "XR InputDevice";
                    return;
                }
            }
            Source = "none";
        }
    }
}
