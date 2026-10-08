// ============================================================
// HomeScreenController.cs  —  AR Virtual Lab
// Home screen, styled like the textbook it supports: white page, teal and blue accents, plain dark text.
//   • the main action is always visible at the bottom: Scan Textbook Page
//   • the three experiments are listed with the textbook page and activity they belong to
// ============================================================
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ARVirtualLab.Scan;
using ARVirtualLab.UI;

namespace ARVirtualLab.AppShell
{
    public class HomeScreenController : MonoBehaviour
    {
        private CanvasGroup _splash;
        private GameObject _loadingOverlay;
        private TextMeshProUGUI _loadingText;
        private Image _loadingFill;

        private void Awake()
        {
            EnsureCamera();
            EnsureEventSystem();
            BuildUI();
        }

        private void Start()
        {
            if (_reopenSettings)
            {
                _reopenSettings = false;
                _splash.gameObject.SetActive(false);
                _settingsPanel.SetActive(true);
                return;
            }
            StartCoroutine(SplashRoutine());
        }

        // ── scene plumbing ─────────────────────────────────────
        private void EnsureEventSystem()
        {
            if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        private void EnsureCamera()
        {
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                cam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
                camGO.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = EduTheme.Page;
        }

        // ── UI ─────────────────────────────────────────────────
        private void BuildUI()
        {
            var canvasGO = new GameObject("HomeCanvas", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 2400f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            var safe = UiKit.Node(canvasGO.transform, "Safe");
            UiKit.Fill(safe);
            safe.gameObject.AddComponent<SafeAreaHelper>();

            var bg = UiKit.Box(safe, "Background", EduTheme.Page);
            UiKit.Fill(bg.rectTransform);

            BuildHeader(safe);
            BuildBody(safe);
            BuildScanBar(safe);

            BuildLoadingOverlay(canvasGO.transform);
            BuildSettingsPanel(safe);
            BuildSplash(canvasGO.transform);
        }

        private void BuildHeader(Transform parent)
        {
            var header = UiKit.Box(parent, "Header", EduTheme.Paper);
            var rt = header.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, 190f); rt.anchoredPosition = Vector2.zero;

            var line = UiKit.Box(header.transform, "TealLine", EduTheme.Teal);
            line.rectTransform.anchorMin = new Vector2(0f, 0f); line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f); line.rectTransform.sizeDelta = new Vector2(0f, 8f);
            line.rectTransform.anchoredPosition = Vector2.zero;

            var title = UiKit.Text(header.transform, "Title", "Virtual Science Lab", 54, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            UiKit.Anchor(title.rectTransform, 0.05f, 0.42f, 0.84f, 0.92f);
            var sub = UiKit.Text(header.transform, "Subtitle", "Class 10  ·  Physical Science  ·  Andhra Pradesh", 29, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(sub.rectTransform, 0.05f, 0.10f, 0.84f, 0.42f);

            var gear = UiKit.Button(header.transform, "SettingsButton", "", EduTheme.Neutral, EduTheme.Ink, 30, 52);
            UiKit.Pin(gear.GetComponent<RectTransform>(), 1f, 0.5f, -40f, 4f, 104f, 104f);
            var icon = UiKit.Box(gear.transform, "Icon", EduTheme.InkSoft);
            icon.sprite = UiKit.GearSprite();
            UiKit.Fill(icon.rectTransform);
            icon.rectTransform.offsetMin = new Vector2(22f, 22f);
            icon.rectTransform.offsetMax = new Vector2(-22f, -22f);
            gear.onClick.AddListener(() => _settingsPanel.SetActive(true));
        }

        // ── settings ──────────────────────────────────────────
        private GameObject _settingsPanel;
        private static bool _reopenSettings;      // a change rebuilds the screen; keep the panel open across it

        private void BuildSettingsPanel(Transform parent)
        {
            var scrim = UiKit.Box(parent, "SettingsScrim", new Color(0.05f, 0.08f, 0.10f, 0.60f));
            UiKit.Fill(scrim.rectTransform);
            scrim.raycastTarget = true;
            var closeOutside = scrim.gameObject.AddComponent<Button>();
            closeOutside.transition = Selectable.Transition.None;
            closeOutside.onClick.AddListener(() => _settingsPanel.SetActive(false));
            _settingsPanel = scrim.gameObject;

            var fill = UiKit.Card(scrim.transform, "SettingsCard", 32);
            var border = fill.transform.parent.GetComponent<Image>();
            border.raycastTarget = true;              // taps on the card must not bubble up to the scrim and close it
            border.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;
            UiKit.Pin(border.rectTransform, 0.5f, 0.5f, 0f, 0f, 920f, 520f);
            var card = fill.rectTransform;

            var title = UiKit.Text(card, "Title", "Settings", 48, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            UiKit.Pin(title.rectTransform, 0f, 1f, 50f, -36f, 600f, 90f);

            AddChoiceRow(card, -170f, "Theme", "Light", "Dark", AppSettings.DarkMode, dark =>
            {
                if (dark == AppSettings.DarkMode) return;
                AppSettings.DarkMode = dark;
                Rebuild();
            });

            var close = UiKit.Button(card, "Close", "Close", EduTheme.Neutral, EduTheme.Ink, 32, 18);
            UiKit.Pin(close.GetComponent<RectTransform>(), 0.5f, 0f, 0f, 40f, 300f, 96f);
            close.onClick.AddListener(() => _settingsPanel.SetActive(false));

            _settingsPanel.SetActive(false);
        }

        // Label on the left, two options on the right; the selected one is filled teal
        private static void AddChoiceRow(RectTransform card, float y, string label, string first, string second,
            bool secondSelected, System.Action<bool> onPick)
        {
            var lab = UiKit.Text(card, label + "Label", label, 36, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            UiKit.Pin(lab.rectTransform, 0f, 1f, 50f, y, 330f, 96f);
            for (int i = 0; i < 2; i++)
            {
                bool isSecond = i == 1;
                bool selected = isSecond == secondSelected;
                var btn = UiKit.Button(card, label + "Option" + i, isSecond ? second : first,
                    selected ? EduTheme.Teal : EduTheme.Neutral, selected ? Color.white : EduTheme.Ink, 32, 18);
                UiKit.Pin(btn.GetComponent<RectTransform>(), 1f, 1f, isSecond ? -50f : -290f, y, 220f, 96f);
                btn.onClick.AddListener(() => onPick(isSecond));
            }
        }

        private static void Rebuild()
        {
            _reopenSettings = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
        }

        private void BuildBody(Transform parent)
        {
            var scrollRT = UiKit.Node(parent, "Scroll");
            scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMax = new Vector2(0f, -190f);
            scrollRT.offsetMin = new Vector2(0f, 210f);
            var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false; scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 40f;

            var viewport = UiKit.Node(scrollRT, "Viewport");
            UiKit.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            // A scroll view only sees a drag if something under the finger can receive it. Cards and gaps have no
            // raycast target, so give the viewport an invisible one; then dragging anywhere scrolls the list.
            var touch = viewport.gameObject.AddComponent<Image>();
            touch.color = new Color(1f, 1f, 1f, 0f);
            touch.raycastTarget = true;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.viewport = viewport;

            var content = UiKit.Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = Vector2.zero; content.anchoredPosition = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 36, 40);
            vlg.spacing = 30f;
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;

            BuildHowItWorks(content);

            var sec = UiKit.Text(content, "SectionTitle", "Experiments in your textbook", 40, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.BottomLeft);
            UiKit.Size(sec, -1f, 70f);

            foreach (var page in TextbookCatalog.Pages) BuildExperimentCard(content, page);

            BuildSafetyCard(content);

            var foot = UiKit.Text(content, "Footer", "Your teacher can help with any activity marked with a caution in the book.", 26, EduTheme.InkFaint, FontStyles.Italic, TextAlignmentOptions.Center);
            UiKit.Size(foot, -1f, 90f);
        }

        private void BuildHowItWorks(Transform parent)
        {
            var card = UiKit.Box(parent, "ScanIntro", EduTheme.Teal, 28);
            UiKit.Size(card, -1f, 400f);

            var title = UiKit.Text(card.transform, "Title", "Scan your textbook", 50, Color.white, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(title.rectTransform, 0.06f, 0.70f, 0.94f, 0.93f);
            var body = UiKit.Text(card.transform, "Body", "Point the camera at an experiment page and the matching experiment opens.", 31, new Color(1f, 1f, 1f, 0.95f), FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(body.rectTransform, 0.06f, 0.46f, 0.94f, 0.70f);

            string[] steps = { "Open the page", "Point the camera", "Experiment opens" };
            for (int i = 0; i < 3; i++)
            {
                float x0 = 0.06f + i * 0.3167f;
                var chip = UiKit.Box(card.transform, "Step" + (i + 1), new Color(1f, 1f, 1f, 0.16f), 18);
                UiKit.Anchor(chip.rectTransform, x0, 0.08f, x0 + 0.295f, 0.40f);
                var num = UiKit.Text(chip.transform, "Num", (i + 1).ToString(), 40, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
                UiKit.Anchor(num.rectTransform, 0f, 0.48f, 1f, 0.96f);
                var lab = UiKit.Text(chip.transform, "Label", steps[i], 25, Color.white, FontStyles.Normal, TextAlignmentOptions.Top);
                UiKit.Anchor(lab.rectTransform, 0.04f, 0.05f, 0.96f, 0.50f);
            }
        }

        private void BuildExperimentCard(Transform parent, TextbookPage page)
        {
            var fill = UiKit.Card(parent, "Card_" + page.Id);
            UiKit.Size(fill.transform.parent.GetComponent<Image>(), -1f, 580f);

            var accent = UiKit.Box(fill.transform, "Accent", EduTheme.Blue, 6);
            UiKit.Anchor(accent.rectTransform, 0f, 0.06f, 0f, 0.94f);
            accent.rectTransform.offsetMin = new Vector2(0f, 0f);
            accent.rectTransform.offsetMax = new Vector2(12f, 0f);

            var chip = UiKit.Box(fill.transform, "Chip", EduTheme.BlueTint, 16);
            UiKit.Anchor(chip.rectTransform, 0.05f, 0.845f, 0.66f, 0.955f);
            var chipText = UiKit.Text(chip.transform, "Text", "Page " + page.PageNumber + "  ·  " + page.Activity, 27, EduTheme.BlueDark, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(chipText.rectTransform);

            var title = UiKit.Text(fill.transform, "Title", page.Title, 42, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(title.rectTransform, 0.05f, 0.615f, 0.95f, 0.83f);
            var chapter = UiKit.Text(fill.transform, "Chapter", page.Chapter, 27, EduTheme.InkFaint, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(chapter.rectTransform, 0.05f, 0.535f, 0.95f, 0.615f);
            var summary = UiKit.Text(fill.transform, "Summary", page.Summary, 30, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(summary.rectTransform, 0.05f, 0.27f, 0.95f, 0.53f);

            var start = UiKit.Button(fill.transform, "Start", "Start experiment", EduTheme.Blue, Color.white, 33, 18);
            UiKit.Anchor(start.GetComponent<RectTransform>(), 0.05f, 0.05f, 0.62f, 0.22f);
            var captured = page;
            start.onClick.AddListener(() => OpenExperiment(captured, false));

            var ar = UiKit.Button(fill.transform, "AR", "AR mode", EduTheme.Neutral, EduTheme.Ink, 31, 18);
            UiKit.Anchor(ar.GetComponent<RectTransform>(), 0.66f, 0.05f, 0.95f, 0.22f);
            ar.onClick.AddListener(() => OpenExperiment(captured, true));
        }

        private void BuildSafetyCard(Transform parent)
        {
            var fill = UiKit.Card(parent, "SafetyCard");
            UiKit.Size(fill.transform.parent.GetComponent<Image>(), -1f, 400f);

            var accent = UiKit.Box(fill.transform, "Accent", EduTheme.Orange, 6);
            UiKit.Anchor(accent.rectTransform, 0f, 0.06f, 0f, 0.94f);
            accent.rectTransform.offsetMax = new Vector2(12f, 0f);

            var head = UiKit.Text(fill.transform, "Head", "Lab safety", 40, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(head.rectTransform, 0.05f, 0.80f, 0.95f, 0.95f);
            var body = UiKit.Text(fill.transform, "Body",
                "•  Wear safety goggles and keep your face away from the flame.\n" +
                "•  Hold the burning ribbon with tongs, never with your fingers.\n" +
                "•  Handle acids carefully and rinse spills with plenty of water.\n" +
                "•  Do these activities with your teacher's help.",
                30, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(body.rectTransform, 0.05f, 0.06f, 0.95f, 0.79f);
        }

        private void BuildScanBar(Transform parent)
        {
            var bar = UiKit.Box(parent, "ScanBar", EduTheme.Paper);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f); rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(0.5f, 0f); rt.sizeDelta = new Vector2(0f, 210f); rt.anchoredPosition = Vector2.zero;
            bar.raycastTarget = true;

            var line = UiKit.Box(bar.transform, "Line", EduTheme.Border);
            line.rectTransform.anchorMin = new Vector2(0f, 1f); line.rectTransform.anchorMax = new Vector2(1f, 1f);
            line.rectTransform.pivot = new Vector2(0.5f, 1f); line.rectTransform.sizeDelta = new Vector2(0f, 3f);
            line.rectTransform.anchoredPosition = Vector2.zero;

            var scan = UiKit.Button(bar.transform, "ScanButton", "Scan textbook page", EduTheme.Teal, Color.white, 44, 24);
            UiKit.Anchor(scan.GetComponent<RectTransform>(), 0.05f, 0.20f, 0.95f, 0.80f);
            scan.onClick.AddListener(() => AppNavigation.GoToScan());
        }

        // ── loading + splash ──────────────────────────────────
        private void BuildLoadingOverlay(Transform parent)
        {
            var paper = EduTheme.Paper;
            var overlay = UiKit.Box(parent, "LoadingOverlay", new Color(paper.r, paper.g, paper.b, 0.97f));
            UiKit.Fill(overlay.rectTransform);
            overlay.raycastTarget = true;
            _loadingOverlay = overlay.gameObject;

            _loadingText = UiKit.Text(overlay.transform, "Text", "Opening experiment...", 44, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Anchor(_loadingText.rectTransform, 0.1f, 0.50f, 0.9f, 0.56f);

            var track = UiKit.Box(overlay.transform, "Track", EduTheme.PillIdle, 12);
            UiKit.Anchor(track.rectTransform, 0.2f, 0.47f, 0.8f, 0.485f);
            _loadingFill = UiKit.Box(track.transform, "Fill", EduTheme.Teal, 12);
            UiKit.Fill(_loadingFill.rectTransform);
            _loadingFill.type = Image.Type.Filled;
            _loadingFill.fillMethod = Image.FillMethod.Horizontal;
            _loadingFill.fillAmount = 0f;
            overlay.gameObject.SetActive(false);
        }

        private void BuildSplash(Transform parent)
        {
            var splash = UiKit.Box(parent, "Splash", EduTheme.Paper);
            UiKit.Fill(splash.rectTransform);
            splash.raycastTarget = true;
            _splash = splash.gameObject.AddComponent<CanvasGroup>();

            var title = UiKit.Text(splash.transform, "Title", "Virtual Science Lab", 72, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Anchor(title.rectTransform, 0.05f, 0.50f, 0.95f, 0.58f);
            var line = UiKit.Box(splash.transform, "Line", EduTheme.Teal);
            UiKit.Anchor(line.rectTransform, 0.36f, 0.495f, 0.64f, 0.499f);
            var sub = UiKit.Text(splash.transform, "Sub", "Class 10  ·  Physical Science", 34, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.Center);
            UiKit.Anchor(sub.rectTransform, 0.05f, 0.44f, 0.95f, 0.49f);
        }

        private IEnumerator SplashRoutine()
        {
            if (_splash == null) yield break;
            yield return new WaitForSecondsRealtime(0.9f);
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.unscaledDeltaTime;
                _splash.alpha = 1f - Mathf.Clamp01(t / 0.4f);
                yield return null;
            }
            _splash.gameObject.SetActive(false);
        }

        // ── actions ───────────────────────────────────────────
        private void OpenExperiment(TextbookPage page, bool arMode)
        {
            _loadingOverlay.SetActive(true);
            _loadingText.text = MobileText.Clean(arMode ? "Opening AR mode..." : "Opening " + page.Activity + "...");
            // The experiment reads these in its Start(); they must be set BEFORE the scene loads, not after.
            AppNavigation.LaunchDirectlyToAR = arMode;
            AppNavigation.LaunchDirectlyToExperiment = true;
            StartCoroutine(AppNavigation.LoadSceneAsyncRoutine(
                page.SceneName,
                p => { if (_loadingFill != null) _loadingFill.fillAmount = p; },
                () =>
                {
                    AppNavigation.LaunchDirectlyToAR = arMode;
                    AppNavigation.LaunchDirectlyToExperiment = true;
                }));
        }
    }
}
