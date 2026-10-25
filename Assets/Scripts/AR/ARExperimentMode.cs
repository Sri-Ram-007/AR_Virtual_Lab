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
        public float DefaultScale = 0f;             // 0 = size the lab to the table automatically (FitScale)
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
        private ARAnchorManager _anchorManager;
        private GameObject _offsetGO;
        private ARAnchor _anchor;            // pins the lab to the real spot: ARCore keeps correcting it as its map of the room improves
        private int _anchorTicket;
        private readonly List<ARRaycastHit> _hits = new List<ARRaycastHit>();

        // scene state
        private readonly List<Renderer> _hiddenRenderers = new List<Renderer>();
        private bool _benchWasActive;

        // reticle
        private GameObject _reticle;
        private Material _reticleMat;
        private bool _reticleOnPlane;
        private Pose _reticlePose;
        private ARPlane _reticlePlane;        // the detected surface under the circle (null for a loose feature point)

        // UI
        private GameObject _canvasGO;
        private CanvasScaler _scaler;
        private bool _layoutLandscape;
        private Image _statusBg;
        private TextMeshProUGUI _statusText, _diagText;
        private Image _diagBg;
        private float _scanStart, _nextTip;
        private Button _placeBtn, _frontBtn, _moveBtn, _cancelBtn, _exitBtn, _steadyBtn;
        private float _statusHideAt = -1f;
        private float _nextLog, _nextDiag, _nextReset, _nextReason;
        private static Texture2D _ringTex;

        // The lab is shown larger than life so it reads well on a phone: world = tracking x (1 / _arScale).
        private float _arScale = 1.6f;
        private const float MinScale = 0.25f, MaxScale = 12f;
        private float _baseScale = 1.6f;      // the size that looks right about a metre away; placing further off enlarges it to match
        private const float TargetFootprint = 0.40f;   // m of real table the apparatus first covers; pinch to change
        private Vector3 _anchorTracking;       // the tapped surface point, in tracking space
        private float _anchorYaw;
        private bool _pinching;
        private float _lastPinchDistance;

        private const float FrontButtonAfter = 4f;       // seconds of searching before "Place in front of me" is offered
        private const float MinPlaneArea = 0.12f;      // m² — big enough for the lab
        private const float MaxPlaceDistance = 4f, MinPlaceDistance = 0.15f;   // metres from the phone to the spot the lab may be put on
        private float _reticleDistance;

        // ═════════════════════════════════════════════════════
        //  PUBLIC API
        // ═════════════════════════════════════════════════════
        public static void Begin(ARExperimentSetup setup)
        {
            if (_instance != null) return;
            var go = new GameObject("ARExperimentMode");
            _instance = go.AddComponent<ARExperimentMode>();
            _instance._setup = setup;
            _instance._arScale = _instance._baseScale = setup.DefaultScale > 0f ? setup.DefaultScale : FitScale(setup);
            _instance.StartCoroutine(_instance.Run());
        }

        /// <summary>
        /// The magnification that makes the apparatus (not the virtual bench) cover TargetFootprint of the real table,
        /// so every experiment starts at a sensible size whatever the size of its models.
        /// </summary>
        private static float FitScale(ARExperimentSetup setup)
        {
            Bounds b;
            float footprint = ApparatusBounds.TryGet(setup.SceneRoot, setup.Bench, out b) ? Mathf.Max(b.size.x, b.size.z) : 0f;
            float scale = footprint > 0.01f ? TargetFootprint / footprint : 1f;
            Debug.Log("[ARMode] apparatus footprint " + footprint.ToString("F2") + " m -> starting size x" + scale.ToString("F2"));
            return Mathf.Clamp(scale, MinScale, 3.5f);
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

            // An AR system left running by an earlier screen (the textbook scanner is portrait, the experiments are landscape)
            // keeps the screen orientation it started with, which rolled the whole lab 90 degrees. Start it afresh here.
            var xr0 = UnityEngine.XR.Management.XRGeneralSettings.Instance;
            if (xr0 != null && xr0.Manager != null && xr0.Manager.isInitializationComplete)
            {
                Debug.Log("[ARMode] restarting the AR system left over from the previous screen (screen " + Screen.width + "x" + Screen.height + ", " + Screen.orientation + ")");
                try { xr0.Manager.StopSubsystems(); xr0.Manager.DeinitializeLoader(); }
                catch (Exception e) { Debug.LogWarning("[ARMode] XR restart: " + e.Message); }
                yield return null;
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
            _scanStart = Time.unscaledTime;
            Debug.Log("[ARMode] scanning for a flat surface");
            SetStatus("Hold the phone at arm's length above a table, bed or the floor (not close to it) and move it slowly to find a flat surface", false);
        }

        private IEnumerator RequestCameraPermission()
        {
            bool granted = false;
            yield return ARVirtualLab.AppShell.CameraPermission.Request(
                "EduAR uses the camera to place the lab on a real table in front of you.", g => granted = g);
            if (!granted) Fail("Camera permission is needed for AR. Allow it in the phone settings.");
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
            // XR Origin lifts the camera 1.12 m ("camera height") when the device reports a Device tracking origin. A phone's AR pose
            // already includes its real height, so the lift put the camera 1.12 m above where ARCore thinks it is: every raycast
            // started too high (surfaces read 3-8 m away) and the lab slid against the real table whenever the phone moved.
            _origin.CameraYOffset = 0f;
            _offsetGO = offset;

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
            camMgr.autoFocusRequested = false;      // ARCore tracks best with fixed focus: refocusing changes the image scale and can make the tracked position wander
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
            _anchorManager = _originGO.AddComponent<ARAnchorManager>();
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
            // only needed while setting up, and only for us (editor / Development Build), never for students
            if (_diagBg != null) _diagBg.gameObject.SetActive(Debug.isDebugBuild && _phase != Phase.Placed);
            if (_diagText != null && Debug.isDebugBuild && Time.unscaledTime >= _nextDiag)
            {
                _nextDiag = Time.unscaledTime + 0.5f;
                string loader = "none";
                try { var m = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager; if (m != null && m.activeLoader != null) loader = m.activeLoader.name; } catch { }
                _diagText.text = "session " + ARSession.state + "  |  pose " + (_poseDriver != null ? _poseDriver.Source : "-") +
                                 "  |  planes " + (_planeManager != null ? _planeManager.trackables.count : 0) + "  |  xr " + loader + "  |  " + _phase + (_reticleDistance > 0f ? "  |  spot " + _reticleDistance.ToString("F1") + " m" : "");
            }
            if (_scaler != null && IsLandscape() != _layoutLandscape) ApplyLayout(IsLandscape());   // the phone was turned
            if (_arCamera == null || _raycastManager == null) return;
            if (_phase == Phase.Failed || _phase == Phase.Starting) return;

            if (Time.unscaledTime >= _nextLog)      // heartbeat so a phone log shows what AR is doing
            {
                _nextLog = Time.unscaledTime + 2.5f;
                Debug.Log("[ARMode] phase=" + _phase + " session=" + ARSession.state + " planes=" + _planeManager.trackables.count +
                          " anchor=" + (_anchor != null ? _anchor.trackingState + " " + _origin.TrackablesParent.InverseTransformPoint(_anchor.transform.position).ToString("F2") : "none") +
                          " scale=" + _arScale.ToString("F1") + " why=" + ARSession.notTrackingReason + " fps=" + (1f / Mathf.Max(0.0001f, Time.smoothDeltaTime)).ToString("F0") +
                          " camPos=" + _arCamera.transform.position.ToString("F2") + " pose=" + (_poseDriver != null ? _poseDriver.Source : "-"));
            }

            if (_offsetGO != null && _offsetGO.transform.localPosition != Vector3.zero) _offsetGO.transform.localPosition = Vector3.zero;   // see BuildRig
            if (_phase == Phase.Placed || _phase == Phase.Repositioning) FollowAnchor();

            // A hand-held phone does not travel metres while we are still searching: the tracking has lost its scale (usually the
            // camera was too close to the surface or moved too fast). Start the AR session again so it re-learns the room.
            if ((_phase == Phase.Scanning || _phase == Phase.Ready) && Time.unscaledTime >= _nextReset &&
                _arCamera.transform.localPosition.magnitude > 6f)
            {
                _nextReset = Time.unscaledTime + 6f;
                Debug.Log("[ARMode] tracking lost its scale (camera " + _arCamera.transform.localPosition.ToString("F1") + " from the start), restarting AR");
                if (_sessionGO != null) _sessionGO.GetComponent<ARSession>().Reset();
                _phase = Phase.Scanning; _scanStart = Time.unscaledTime;
                SetStatus("AR lost track. Hold the phone at arm's length, about half a metre or more above the surface, and move it slowly", false, 6f);
            }

            if ((_phase == Phase.Scanning || _phase == Phase.Ready) && Time.unscaledTime >= _nextReason)
            {
                _nextReason = Time.unscaledTime + 4f;
                string tip = null;
                switch (ARSession.notTrackingReason)
                {
                    case NotTrackingReason.ExcessiveMotion: tip = "You are moving too fast. Move the phone slowly and smoothly"; break;
                    case NotTrackingReason.InsufficientFeatures: tip = "Not enough detail to see here. Aim at a surface with some texture (a book, a printed cloth) about 1 metre away"; break;
                    case NotTrackingReason.InsufficientLight: tip = "It is too dark. Turn on more light"; break;
                    case NotTrackingReason.Relocalizing: tip = "AR is finding its place again. Hold the phone still for a moment"; break;
                }
                if (tip != null) SetStatus(tip, false, 3.5f);
            }

            bool placing = _phase == Phase.Scanning || _phase == Phase.Ready || _phase == Phase.Repositioning;
            if (placing)
            {
                // reticle follows the middle of the screen
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                _reticleOnPlane = _raycastManager.Raycast(center, _hits, TrackableType.PlaneWithinPolygon | TrackableType.PlaneWithinBounds);
                _reticleDistance = 0f;
                if (_reticleOnPlane)
                {
                    // real-world distance from the phone to the spot (the tracking origin may already be scaled once a lab exists)
                    _reticleDistance = Vector3.Distance(_hits[0].pose.position, _arCamera.transform.position) / Mathf.Max(0.0001f, _origin.transform.localScale.x);
                    if (_reticleDistance > MaxPlaceDistance || _reticleDistance < MinPlaceDistance)
                    {
                        // a surface metres away would put the lab far off as a tiny speck, so it is not offered
                        _reticleOnPlane = false;
                        if (Time.unscaledTime >= _nextTip && _phase != Phase.Repositioning)
                        {
                            _nextTip = Time.unscaledTime + 3f;
                            SetStatus(_reticleDistance > MaxPlaceDistance
                                ? "That surface is " + _reticleDistance.ToString("F1") + " m away. Aim the circle at a nearer spot, about 1 to 2 metres away"
                                : "Too close. Move the phone back a little", false, 3f);
                        }
                    }
                }
                if (_reticleOnPlane)
                {
                    _reticlePose = _hits[0].pose;
                    _reticlePlane = _hits[0].trackable as ARPlane;
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
                else if (_phase == Phase.Scanning && Time.unscaledTime - _scanStart > 8f && Time.unscaledTime >= _nextTip)
                {
                    _nextTip = Time.unscaledTime + 8f;
                    SetStatus("Still searching. Stand about 1 metre from a table or floor, tilt the phone down a little and sweep it slowly side to side", false);
                }
                if (_placeBtn != null) _placeBtn.gameObject.SetActive(_reticleOnPlane);
                // ARCore can take a long while to turn a soft or patterned surface into a plane, so after a few seconds the lab can
                // also be put straight in front of the phone, on the table level a phone is usually held above
                if (_frontBtn != null) _frontBtn.gameObject.SetActive(!_reticleOnPlane && _phase != Phase.Repositioning && Time.unscaledTime - _scanStart > FrontButtonAfter);

                // a tap anywhere places the lab where the CIRCLE is (the instruction says "aim and tap"), never at the
                // point that was touched — that used to drop the lab on a far wall or floor
                if (TapPosition(out Vector2 tap) && _reticleOnPlane)
                    PlaceLab(_reticlePose, _reticlePlane);
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
        private void PlaceLab(Pose surface, ARPlane plane)
        {
            var o = _origin.transform;
            float k = o.localScale.x;
            // ARCore's distances are unreliable on soft or patterned cloth (it can read 3 m for a bed half a metre away), so a far
            // spot is not refused: the lab is simply made bigger by the same factor, so it looks the same size as at a metre
            _arScale = Mathf.Clamp(_baseScale * Mathf.Clamp(_reticleDistance, 0.4f, 2.5f), MinScale, MaxScale);
            Debug.Log("[ARMode] placing the lab at " + surface.position.ToString("F2") + " spot " + _reticleDistance.ToString("F1") + " m away (size x" + _arScale.ToString("F1") + ")");

            // where the tapped point sits in tracking space (before we move anything)
            _anchorTracking = Quaternion.Inverse(o.rotation) * (surface.position - o.position) / Mathf.Max(0.0001f, k);

            // turn the lab to face the way the student is looking
            Vector3 look = _arCamera.transform.forward; look.y = 0f;
            if (look.sqrMagnitude < 0.01f) { look = _arCamera.transform.up; look.y = 0f; }
            look.Normalize();
            Vector3 lookInTracking = Quaternion.Inverse(o.rotation) * look;
            _anchorYaw = Mathf.Atan2(lookInTracking.x, lookInTracking.z) * Mathf.Rad2Deg;

            CreateAnchor(surface, plane);       // before the origin moves: the anchor takes the surface point as it is in the world right now
            ApplyPlacement();

            _phase = Phase.Placed;
            _reticle.SetActive(false);
            ShowExperiment();
            _blockPlacing = false;
            RefreshButtons();
            SetStatus("Lab placed. Pinch with two fingers to resize it. Follow the steps to do the experiment", false, 5f);
        }

        /// <summary>
        /// No surface needed: the lab goes in front of the phone on the level of a table below it (about 55 cm under the phone),
        /// along where the camera is aiming, 0.4 to 1.4 m away. "Move lab" and the pinch fix any difference afterwards.
        /// </summary>
        private void PlaceInFront()
        {
            if (_phase != Phase.Scanning && _phase != Phase.Ready) return;
            Transform c = _arCamera.transform;
            float k = Mathf.Max(0.0001f, _origin.transform.localScale.x);
            float drop = 0.55f * k;
            Vector3 fwd = c.forward, p;
            if (fwd.y < -0.25f)
            {
                float t = Mathf.Clamp(drop / -fwd.y, 0.4f * k, 1.4f * k);
                p = c.position + fwd * t;
            }
            else
            {
                Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
                if (flat.sqrMagnitude < 0.01f) flat = new Vector3(c.up.x, 0f, c.up.z);
                p = c.position + flat.normalized * 0.7f * k;
            }
            p.y = c.position.y - drop;
            _reticleDistance = Vector3.Distance(p, c.position) / k;
            Debug.Log("[ARMode] placing in front of the phone, " + _reticleDistance.ToString("F2") + " m away");
            PlaceLab(new Pose(p, Quaternion.identity), null);
        }

        private void ToggleSteady()
        {
            if (_poseDriver == null) return;
            _poseDriver.SetHold(!_poseDriver.Holding);
            var label = _steadyBtn.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = _poseDriver.Holding ? "Steady view: on" : "Steady view: off";
            SetStatus(_poseDriver.Holding
                ? "Steady view on. The lab stays still; turn the phone to look around it"
                : "Steady view off. The lab follows the phone's movement again", false, 4f);
        }

        private async void CreateAnchor(Pose world, ARPlane plane)
        {
            if (_anchorManager == null) return;
            int ticket = ++_anchorTicket;
            if (_anchor != null) { _anchorManager.TryRemoveAnchor(_anchor); _anchor = null; }
            if (plane != null)
            {
                // an anchor attached to the surface moves with it when ARCore refines the surface's position and height
                try { _anchor = _anchorManager.AttachAnchor(plane, world); } catch (Exception e) { Debug.LogWarning("[ARMode] attach failed: " + e.Message); }
                if (_anchor != null) { Debug.Log("[ARMode] lab anchored to the surface"); return; }
            }
            try
            {
                var result = await _anchorManager.TryAddAnchorAsync(world);
                if (this == null) return;
                if (!result.status.IsSuccess() || result.value == null)
                {
                    Debug.LogWarning("[ARMode] could not anchor the lab (" + result.status + "), it will not follow ARCore's corrections");
                    return;
                }
                if (ticket != _anchorTicket) { _anchorManager.TryRemoveAnchor(result.value); return; }     // placed again meanwhile
                _anchor = result.value;
                Debug.Log("[ARMode] lab anchored");
            }
            catch (Exception e) { Debug.LogWarning("[ARMode] anchor failed: " + e.Message); }
        }

        /// <summary>The anchor's position is corrected by ARCore as it learns the room; the lab follows it so it stays on the spot.</summary>
        private void FollowAnchor()
        {
            if (_anchor == null || _origin == null || _anchor.trackingState != TrackingState.Tracking) return;
            if (_poseDriver != null && _poseDriver.Holding) return;
            Vector3 p = _origin.TrackablesParent.InverseTransformPoint(_anchor.transform.position);
            if ((p - _anchorTracking).sqrMagnitude < 1e-8f) return;
            _anchorTracking = p;
            ApplyPlacement();
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
                // thumbs gripping the phone's edges are not a pinch
                float edge = Screen.width * 0.08f;
                if (a.position.x < edge || b.position.x < edge || a.position.x > Screen.width - edge || b.position.x > Screen.width - edge)
                { _pinching = false; _blockPinch = false; return; }
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
            _scaler = _canvasGO.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.matchWidthOrHeight = 0.5f;
            _canvasGO.AddComponent<GraphicRaycaster>();
            var safe = UiKit.Node(_canvasGO.transform, "Safe");
            UiKit.Fill(safe);
            safe.gameObject.AddComponent<ARVirtualLab.AppShell.SafeAreaHelper>();

            _statusBg = UiKit.Box(safe, "Status", EduTheme.Paper, 28);
            _statusBg.raycastTarget = false;
            _statusText = UiKit.Text(_statusBg.transform, "Text", "", 30, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(_statusText.rectTransform);
            _statusText.rectTransform.offsetMin = new Vector2(24f, 6f);
            _statusText.rectTransform.offsetMax = new Vector2(-24f, -6f);
            _statusText.enableAutoSizing = true; _statusText.fontSizeMin = 24f; _statusText.fontSizeMax = 32f;

            // small read-out of what AR is doing, so a screenshot of a misbehaving phone says why (debug builds only)
            var diagBg = UiKit.Box(safe, "AR Diagnostics", new Color(0f, 0f, 0f, 0.55f), 10);
            UiKit.Anchor(diagBg.rectTransform, 0.02f, 0.30f, 0.98f, 0.345f);   // above the buttons so it never covers them
            _diagBg = diagBg;
            diagBg.raycastTarget = false;
            _diagText = UiKit.Text(diagBg.transform, "Text", "AR starting...", 22, Color.white, FontStyles.Normal, TextAlignmentOptions.Center);
            UiKit.Fill(_diagText.rectTransform);
            _diagText.enableAutoSizing = true; _diagText.fontSizeMin = 14f; _diagText.fontSizeMax = 22f;
            _diagText.raycastTarget = false;
            var diagNative = _diagText.GetComponent<ARVirtualLab.Localization.NativeText>();
            if (diagNative != null) diagNative.enabled = false;    // technical read-out: stays English, never sent for translation

            _placeBtn = UiKit.Button(safe, "PlaceHere", "Place lab here", EduTheme.Teal, Color.white, 38, 22);
            _placeBtn.onClick.AddListener(() => { if (_reticleOnPlane) PlaceLab(_reticlePose, _reticlePlane); });
            _placeBtn.gameObject.SetActive(false);

            _frontBtn = UiKit.Button(safe, "PlaceInFront", "Place in front of me", EduTheme.Neutral, EduTheme.Ink, 34, 22);
            _frontBtn.onClick.AddListener(PlaceInFront);
            _frontBtn.gameObject.SetActive(false);

            _steadyBtn = UiKit.Button(safe, "SteadyView", "Steady view: off", EduTheme.Neutral, EduTheme.Ink, 28, 18);
            _steadyBtn.onClick.AddListener(ToggleSteady);
            _steadyBtn.gameObject.SetActive(false);

            _moveBtn = UiKit.Button(safe, "MoveLab", "Move lab", EduTheme.Neutral, EduTheme.Ink, 32, 18);
            _moveBtn.onClick.AddListener(() => { if (_poseDriver != null && _poseDriver.Holding) ToggleSteady(); _phase = Phase.Repositioning; _blockPlacing = true; RefreshButtons(); SetStatus("Aim at a new spot and tap to move the lab", false); });

            _cancelBtn = UiKit.Button(safe, "CancelMove", "Keep here", EduTheme.Neutral, EduTheme.Ink, 32, 18);
            _cancelBtn.onClick.AddListener(() => { _phase = Phase.Placed; _blockPlacing = false; _blockPinch = false; _reticle.SetActive(false); RefreshButtons(); _statusBg.gameObject.SetActive(false); });

            _exitBtn = UiKit.Button(safe, "ExitAR", "Exit AR", EduTheme.Red, Color.white, 32, 18);
            _exitBtn.onClick.AddListener(Stop);

            ApplyLayout(IsLandscape());
            RefreshButtons();
        }

        private static bool IsLandscape() { return Screen.width > Screen.height; }

        /// <summary>
        /// Landscape matches the experiments' landscape HUD: top-centre and bottom-centre are free, and the
        /// bottom-right corner is reserved for the AR buttons (the experiment lifts its own buttons above them).
        /// </summary>
        private void ApplyLayout(bool landscape)
        {
            _layoutLandscape = landscape;
            _scaler.referenceResolution = landscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
            var place = _placeBtn.GetComponent<RectTransform>();
            var front = _frontBtn.GetComponent<RectTransform>();
            var steady = _steadyBtn.GetComponent<RectTransform>();
            var move = _moveBtn.GetComponent<RectTransform>();
            var cancel = _cancelBtn.GetComponent<RectTransform>();
            var exit = _exitBtn.GetComponent<RectTransform>();
            if (landscape)
            {
                UiKit.Pin(_statusBg.rectTransform, 0.5f, 1f, 0f, -40f, 600f, 110f);
                UiKit.Pin(place, 0.5f, 0f, 0f, 40f, 440f, 96f);
                UiKit.Pin(front, 0.5f, 0f, 0f, 40f, 520f, 96f);
                UiKit.Pin(steady, 1f, 0f, -256f, 124f, 300f, 80f);
                UiKit.Pin(move, 1f, 0f, -256f, 32f, 200f, 80f);
                UiKit.Pin(cancel, 1f, 0f, -256f, 32f, 200f, 80f);
                UiKit.Pin(exit, 1f, 0f, -40f, 32f, 200f, 80f);
                if (_diagBg != null) UiKit.Pin(_diagBg.rectTransform, 0f, 0f, 24f, 24f, 900f, 44f);
            }
            else
            {
                UiKit.Anchor(_statusBg.rectTransform, 0.06f, 0.715f, 0.94f, 0.795f);
                UiKit.Anchor(place, 0.20f, 0.265f, 0.80f, 0.33f);
                UiKit.Anchor(front, 0.15f, 0.265f, 0.85f, 0.33f);
                UiKit.Anchor(steady, 0.05f, 0.175f, 0.55f, 0.235f);
                UiKit.Anchor(move, 0.05f, 0.105f, 0.47f, 0.165f);
                UiKit.Anchor(cancel, 0.05f, 0.105f, 0.47f, 0.165f);
                UiKit.Anchor(exit, 0.53f, 0.105f, 0.95f, 0.165f);
                if (_diagBg != null) UiKit.Anchor(_diagBg.rectTransform, 0.02f, 0.30f, 0.98f, 0.345f);
            }
        }

        private void RefreshButtons()
        {
            if (_moveBtn != null) _moveBtn.gameObject.SetActive(_phase == Phase.Placed);
            if (_cancelBtn != null) _cancelBtn.gameObject.SetActive(_phase == Phase.Repositioning);
            if (_placeBtn != null && _phase == Phase.Placed) _placeBtn.gameObject.SetActive(false);
            if (_frontBtn != null && _phase == Phase.Placed) _frontBtn.gameObject.SetActive(false);
            if (_steadyBtn != null) _steadyBtn.gameObject.SetActive(_phase == Phase.Placed);
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
        private bool _hold;
        private Vector3 _heldPos;
        /// <summary>Freeze the camera where it is: the phone can still turn, but ARCore's (sometimes wandering) movement is ignored.</summary>
        public void SetHold(bool on) { _hold = on; _heldPos = transform.localPosition; }
        public bool Holding { get { return _hold; } }
        private void SetPose(Vector3 pos, Quaternion rot) { transform.SetLocalPositionAndRotation(_hold ? _heldPos : pos, rot); }
        private static readonly List<UnityEngine.XR.InputDevice> _xrDevices = new List<UnityEngine.XR.InputDevice>();
        private float _enabledAt;
        private bool _loggedDevices;

        private void OnEnable() { _enabledAt = Time.unscaledTime; Application.onBeforeRender += Apply; }
        private void OnDisable() { Application.onBeforeRender -= Apply; }
        private void Update()
        {
            Apply();
            if (!_loggedDevices && Source == "none" && Time.unscaledTime - _enabledAt > 3f) LogDevices();
        }

        private void Apply()
        {
            // 1) Input System: any device with a device pose. The layout name differs between setups
            //    (e.g. HandheldARInputDevice, or a generated XRInputV1 layout when the AR layout isn't matched).
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
            {
                var pc = d.TryGetChildControl<Vector3Control>("devicePosition");
                var rc = d.TryGetChildControl<QuaternionControl>("deviceRotation");
                if (pc == null || rc == null) continue;
                Quaternion r = rc.ReadValue();
                if (r.x == 0f && r.y == 0f && r.z == 0f && r.w == 0f) continue;      // no data yet
                SetPose(pc.ReadValue(), r);
                Source = d.layout;
                return;
            }

            // 2) Legacy XR input: any device reporting a pose, whatever its characteristics
            UnityEngine.XR.InputDevices.GetDevices(_xrDevices);
            foreach (var dev in _xrDevices)
            {
                Vector3 pos; Quaternion rot;
                if (dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out pos) &&
                    dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out rot) &&
                    !(rot.x == 0f && rot.y == 0f && rot.z == 0f && rot.w == 0f))
                {
                    SetPose(pos, rot);
                    Source = "XR:" + dev.name;
                    return;
                }
            }
            Source = "none";
        }

        private void LogDevices()
        {
            _loggedDevices = true;
            var sb = new System.Text.StringBuilder("[ARMode] no camera pose after 3s. Input System devices:");
            foreach (var d in UnityEngine.InputSystem.InputSystem.devices)
                sb.Append("\n  layout=").Append(d.layout).Append(" name=").Append(d.name)
                  .Append(" hasPose=").Append(d.TryGetChildControl("devicePosition") != null);
            UnityEngine.XR.InputDevices.GetDevices(_xrDevices);
            sb.Append("\nXR input devices: ").Append(_xrDevices.Count);
            foreach (var dev in _xrDevices)
                sb.Append("\n  name=").Append(dev.name).Append(" characteristics=").Append(dev.characteristics);
            var loader = UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager?.activeLoader;
            sb.Append("\nactive XR loader: ").Append(loader != null ? loader.name : "none");
            Debug.Log(sb.ToString());
        }
    }
}
