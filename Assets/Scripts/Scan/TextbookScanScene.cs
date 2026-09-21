// ============================================================
// TextbookScanScene.cs  —  AR Virtual Lab
// "Scan textbook page": the student points the phone at an experiment page in the Class 10 Physical Science book
// and the matching experiment opens.
//
//   • ARFoundation image tracking with a runtime reference library built from the page pictures in
//     Assets/Resources/Textbook (see TextbookCatalog).
//   • A page must be seen steadily for half a second before it counts, so a passing glance does not launch anything.
//   • If the camera / ARCore is unavailable (or in the Editor) the list at the bottom still opens every experiment.
// ============================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem.XR;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using ARVirtualLab.AppShell;
using ARVirtualLab.UI;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARVirtualLab.Scan
{
    public class TextbookScanScene : MonoBehaviour
    {
        // ── UI ─────────────────────────────────────────────────
        private Canvas _canvas;
        private GameObject _placeholder;
        private Image _statusBg;
        private TextMeshProUGUI _statusText;
        private RectTransform _scanLine;
        private RectTransform _frame;
        private readonly List<Image> _bracketParts = new List<Image>();
        private Camera _uiCamera;

        // ── AR ─────────────────────────────────────────────────
        private ARSession _session;
        private XROrigin _origin;
        private Camera _arCamera;
        private ARTrackedImageManager _imageManager;
        private readonly List<Texture2D> _textures = new List<Texture2D>();

        // ── Recognition state ──────────────────────────────────
        private readonly Dictionary<string, float> _firstSeen = new Dictionary<string, float>();
        private readonly Dictionary<string, float> _lastSeen = new Dictionary<string, float>();
        private bool _scanning;
        private bool _launching;

        private const float SteadyTime = 0.5f;

        // ═════════════════════════════════════════════════════
        private void Awake()
        {
            BuildCamera();
            EnsureEventSystem();
            BuildUI();
        }

        private void Start()
        {
            SetStatus("Starting the camera...", StatusKind.Info);
#if UNITY_EDITOR
            SetStatus("Scanning works on a phone. Choose a page below to try the app.", StatusKind.Info);
#else
            StartCoroutine(StartScanning());
#endif
        }

        private void OnDestroy()
        {
            if (_imageManager != null) _imageManager.trackablesChanged.RemoveListener(OnImagesChanged);
            foreach (var t in _textures) if (t != null) Destroy(t);
        }

        private void Update()
        {
            if (_scanLine != null && _scanning && !_launching)
            {
                float k = Mathf.PingPong(Time.unscaledTime * 0.55f, 1f);
                _scanLine.anchorMin = new Vector2(0.03f, Mathf.Lerp(0.04f, 0.96f, k));
                _scanLine.anchorMax = new Vector2(0.97f, Mathf.Lerp(0.04f, 0.96f, k));
            }
            else if (_scanLine != null && _scanLine.gameObject.activeSelf && !_scanning)
            {
                _scanLine.gameObject.SetActive(false);
            }
        }

        private void BuildCamera()
        {
            var camGO = new GameObject("Main Camera");
            camGO.tag = "MainCamera";
            _uiCamera = camGO.AddComponent<Camera>();
            _uiCamera.clearFlags = CameraClearFlags.SolidColor;
            _uiCamera.backgroundColor = EduTheme.Page;
            camGO.AddComponent<AudioListener>();
        }

        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        // ═════════════════════════════════════════════════════
        //  UI
        // ═════════════════════════════════════════════════════
        private void BuildUI()
        {
            var canvasGO = new GameObject("ScanCanvas", typeof(RectTransform));
            _canvas = canvasGO.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var safe = UiKit.Node(canvasGO.transform, "Safe");
            UiKit.Fill(safe);
            safe.gameObject.AddComponent<SafeAreaHelper>();

            // Shown until the phone camera is running (and always in the Editor)
            var ph = UiKit.Box(safe, "CameraPlaceholder", EduTheme.Page);
            UiKit.Fill(ph.rectTransform);
            _placeholder = ph.gameObject;
            var phIcon = UiKit.Text(ph.transform, "Hint", "Camera preview", 40, EduTheme.InkFaint, FontStyles.Normal, TextAlignmentOptions.Center);
            UiKit.Anchor(phIcon.rectTransform, 0.1f, 0.48f, 0.9f, 0.56f);

            BuildTopBar(safe);
            BuildFrame(safe);
            BuildStatus(safe);
            BuildPageList(safe);
        }

        private void BuildTopBar(Transform parent)
        {
            var bar = UiKit.Box(parent, "TopBar", new Color(1f, 1f, 1f, 0.96f));
            UiKit.Anchor(bar.rectTransform, 0f, 0.925f, 1f, 1f);
            bar.raycastTarget = true;
            var line = UiKit.Box(bar.transform, "Line", EduTheme.Teal);
            line.rectTransform.anchorMin = new Vector2(0f, 0f); line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.sizeDelta = new Vector2(0f, 6f); line.rectTransform.pivot = new Vector2(0.5f, 0f);
            line.rectTransform.anchoredPosition = Vector2.zero;

            var back = UiKit.Button(bar.transform, "Back", "Back", EduTheme.Neutral, EduTheme.Ink, 32, 16);
            UiKit.Anchor(back.GetComponent<RectTransform>(), 0.035f, 0.22f, 0.24f, 0.78f);
            back.onClick.AddListener(() => AppNavigation.GoToHome());

            var title = UiKit.Text(bar.transform, "Title", "Scan textbook page", 42, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Left);
            UiKit.Anchor(title.rectTransform, 0.28f, 0.15f, 0.97f, 0.85f);
            title.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private void BuildFrame(Transform parent)
        {
            _frame = UiKit.Node(parent, "ScanFrame");
            UiKit.Anchor(_frame, 0.11f, 0.30f, 0.89f, 0.875f);

            // four corner brackets (dark shadow underneath so they read on any camera image)
            for (int corner = 0; corner < 4; corner++)
            {
                bool right = (corner & 1) == 1, top = (corner & 2) == 2;
                AddBracket(_frame, right, top, new Color(0f, 0f, 0f, 0.35f), 4f);
                AddBracket(_frame, right, top, Color.white, 0f);
            }

            var line = UiKit.Box(_frame, "ScanLine", new Color(EduTheme.Teal.r, EduTheme.Teal.g, EduTheme.Teal.b, 0.9f));
            _scanLine = line.rectTransform;
            _scanLine.sizeDelta = new Vector2(0f, 5f);
            _scanLine.anchorMin = new Vector2(0.03f, 0.5f); _scanLine.anchorMax = new Vector2(0.97f, 0.5f);
            _scanLine.offsetMin = new Vector2(0f, -2.5f); _scanLine.offsetMax = new Vector2(0f, 2.5f);
            _scanLine.gameObject.SetActive(false);
        }

        private void AddBracket(RectTransform frame, bool right, bool top, Color color, float shadowShift)
        {
            const float len = 110f, thick = 12f;
            float sx = right ? -1f : 1f, sy = top ? -1f : 1f;
            Vector2 anchor = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            for (int arm = 0; arm < 2; arm++)
            {
                var img = UiKit.Box(frame, "Bracket", color, 4);
                var rt = img.rectTransform;
                rt.anchorMin = rt.anchorMax = anchor;
                rt.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
                rt.sizeDelta = arm == 0 ? new Vector2(len, thick) : new Vector2(thick, len);
                rt.anchoredPosition = new Vector2(sx * (-shadowShift), sy * (-shadowShift)) * -1f;
                if (shadowShift == 0f) _bracketParts.Add(img);
            }
        }

        private void BuildStatus(Transform parent)
        {
            _statusBg = UiKit.Box(parent, "StatusPill", EduTheme.Paper, 30);
            UiKit.Anchor(_statusBg.rectTransform, 0.08f, 0.232f, 0.92f, 0.292f);
            _statusText = UiKit.Text(_statusBg.transform, "Text", "", 30, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(_statusText.rectTransform);
            _statusText.rectTransform.offsetMin = new Vector2(20f, 4f);
            _statusText.rectTransform.offsetMax = new Vector2(-20f, -4f);
            _statusText.enableAutoSizing = true;
            _statusText.fontSizeMin = 24f; _statusText.fontSizeMax = 32f;
        }

        private void BuildPageList(Transform parent)
        {
            var sheetBorder = UiKit.Box(parent, "SheetBorder", EduTheme.Border, 34);
            UiKit.Anchor(sheetBorder.rectTransform, 0.0f, 0.0f, 1f, 0.215f);
            var sheet = UiKit.Box(sheetBorder.transform, "Sheet", EduTheme.Paper, 32);
            UiKit.Fill(sheet.rectTransform);
            sheet.rectTransform.offsetMin = new Vector2(0f, 0f);
            sheet.rectTransform.offsetMax = new Vector2(0f, -3f);
            sheet.raycastTarget = true;

            var head = UiKit.Text(sheet.transform, "Head", "Can't scan? Choose your page", 32, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            UiKit.Anchor(head.rectTransform, 0.05f, 0.80f, 0.95f, 0.97f);

            var pages = TextbookCatalog.Pages;
            float rowH = 0.235f, gap = 0.02f, top = 0.77f;
            for (int i = 0; i < pages.Length; i++)
            {
                var page = pages[i];
                float y1 = top - i * (rowH + gap), y0 = y1 - rowH;
                var btn = UiKit.Button(sheet.transform, "Row_" + page.Id,
                    "", EduTheme.PaperTint, EduTheme.Ink, 28, 16);
                var rt = btn.GetComponent<RectTransform>();
                UiKit.Anchor(rt, 0.04f, y0, 0.96f, y1);

                var label = btn.GetComponentInChildren<TextMeshProUGUI>();
                label.text = MobileText.Clean("Page " + page.PageNumber + "  ·  " + page.Activity + "   " + page.Title);
                label.alignment = TextAlignmentOptions.MidlineLeft;
                label.fontStyle = FontStyles.Normal;
                label.enableAutoSizing = true; label.fontSizeMin = 22f; label.fontSizeMax = 30f;
                label.rectTransform.offsetMin = new Vector2(26f, 2f);
                label.rectTransform.offsetMax = new Vector2(-26f, -2f);

                var captured = page;
                btn.onClick.AddListener(() => OnPageChosen(captured, false));
            }
        }

        private enum StatusKind { Info, Searching, Found, Problem }

        private void SetStatus(string text, StatusKind kind)
        {
            if (_statusText == null) return;
            _statusText.text = MobileText.Clean(text);
            switch (kind)
            {
                case StatusKind.Found:   _statusBg.color = EduTheme.Green;  _statusText.color = Color.white; break;
                case StatusKind.Problem: _statusBg.color = EduTheme.Orange; _statusText.color = Color.white; break;
                default:                 _statusBg.color = EduTheme.Paper;  _statusText.color = EduTheme.Ink; break;
            }
            Color b = kind == StatusKind.Found ? new Color(0.55f, 0.95f, 0.65f) : Color.white;
            foreach (var p in _bracketParts) if (p != null) p.color = b;
        }

        // ═════════════════════════════════════════════════════
        //  START THE CAMERA + IMAGE TRACKING
        // ═════════════════════════════════════════════════════
        private IEnumerator StartScanning()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
            {
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
                if (!granted)
                {
                    SetStatus("Camera permission is needed to scan. Choose your page below instead.", StatusKind.Problem);
                    yield break;
                }
            }
#endif
            yield return ARSession.CheckAvailability();
            if (ARSession.state == ARSessionState.NeedsInstall) yield return ARSession.Install();
            if (ARSession.state == ARSessionState.Unsupported)
            {
                SetStatus("This phone cannot scan pages. Choose your page below instead.", StatusKind.Problem);
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
            catch (System.Exception e) { Debug.LogWarning("[TextbookScan] XR loader: " + e.Message); }

            BuildARRig();
            yield return null;
            yield return null;

            yield return BuildReferenceLibrary();
        }

        private void BuildARRig()
        {
            var sessionGO = new GameObject("AR Session");
            _session = sessionGO.AddComponent<ARSession>();
            _session.attemptUpdate = true;
            sessionGO.AddComponent<ARInputManager>();

            var originGO = new GameObject("XR Origin");
            _origin = originGO.AddComponent<XROrigin>();
            var offset = new GameObject("Camera Offset");
            offset.transform.SetParent(originGO.transform, false);
            _origin.CameraFloorOffsetObject = offset;

            var camGO = new GameObject("AR Camera");
            camGO.tag = "MainCamera";
            camGO.transform.SetParent(offset.transform, false);
            _arCamera = camGO.AddComponent<Camera>();
            _arCamera.clearFlags = CameraClearFlags.SolidColor;
            _arCamera.backgroundColor = Color.black;
            _arCamera.nearClipPlane = 0.05f;
            _arCamera.farClipPlane = 20f;
            var camMgr = camGO.AddComponent<ARCameraManager>();
            camMgr.autoFocusRequested = true;                 // a sharp page is read far more reliably
            camGO.AddComponent<ARCameraBackground>();
            camGO.AddComponent<TrackedPoseDriver>();
            try
            {
                var urp = camGO.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
                urp.renderPostProcessing = false;
            }
            catch { }
            _origin.Camera = _arCamera;

            _imageManager = originGO.AddComponent<ARTrackedImageManager>();

            if (_uiCamera != null) _uiCamera.gameObject.SetActive(false);   // the AR camera draws the live picture
            if (_placeholder != null) _placeholder.SetActive(false);
        }

        private IEnumerator BuildReferenceLibrary()
        {
            SetStatus("Getting the pages ready...", StatusKind.Info);
            _imageManager.referenceLibrary = _imageManager.CreateRuntimeLibrary();
            var library = _imageManager.referenceLibrary as MutableRuntimeReferenceImageLibrary;
            if (library == null)
            {
                SetStatus("This phone cannot scan pages. Choose your page below instead.", StatusKind.Problem);
                yield break;
            }

            int added = 0;
            foreach (var page in TextbookCatalog.Pages)
            {
                for (int variant = 0; variant < 2; variant++)
                {
                    string name = variant == 0 ? page.Id : page.Id + "_fig";
                    var asset = Resources.Load<TextAsset>("Textbook/" + name);
                    if (asset == null) continue;

                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (!tex.LoadImage(asset.bytes, false)) { Destroy(tex); continue; }
                    _textures.Add(tex);

                    float width = page.PageWidthMeters * (variant == 0 ? 1f : page.FigureWidthFraction);
                    var job = library.ScheduleAddImageWithValidationJob(tex, name, width);
                    while (!job.jobHandle.IsCompleted) yield return null;
                    job.jobHandle.Complete();
                    if (job.status == AddReferenceImageJobStatus.Success) added++;
                    else Debug.LogWarning("[TextbookScan] could not add " + name + ": " + job.status);
                }
            }

            if (added == 0)
            {
                SetStatus("Could not load the textbook pages. Choose your page below instead.", StatusKind.Problem);
                yield break;
            }

            _imageManager.requestedMaxNumberOfMovingImages = 1;
            _imageManager.trackablesChanged.AddListener(OnImagesChanged);
            _scanning = true;
            if (_scanLine != null) _scanLine.gameObject.SetActive(true);
            SetStatus("Point the camera at an experiment page", StatusKind.Searching);
        }

        // ═════════════════════════════════════════════════════
        //  RECOGNITION
        // ═════════════════════════════════════════════════════
        private void OnImagesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> args)
        {
            foreach (var img in args.added) Consider(img);
            foreach (var img in args.updated) Consider(img);
        }

        private void Consider(ARTrackedImage img)
        {
            if (_launching || img == null || img.trackingState == TrackingState.None) return;
            var page = TextbookCatalog.FindByReferenceName(img.referenceImage.name);
            if (page == null) return;

            float now = Time.unscaledTime;
            if (!_lastSeen.ContainsKey(page.Id) || now - _lastSeen[page.Id] > 0.8f) _firstSeen[page.Id] = now;   // lost it for a moment: start again
            _lastSeen[page.Id] = now;

            if (img.trackingState == TrackingState.Tracking && now - _firstSeen[page.Id] >= SteadyTime)
                OnPageChosen(page, true);
            else
                SetStatus("Hold steady... " + page.Activity, StatusKind.Searching);
        }

        private void OnPageChosen(TextbookPage page, bool scanned)
        {
            if (_launching) return;
            _launching = true;
            _scanning = false;
            SetStatus((scanned ? "Found  " : "Opening  ") + page.Activity + "  -  " + page.Title, StatusKind.Found);
            StartCoroutine(LaunchAfterDelay(page, scanned ? 1.1f : 0.35f));
        }

        private IEnumerator LaunchAfterDelay(TextbookPage page, float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            page.Launch(false);
        }
    }
}
