// ============================================================
// ExperimentHud.cs  —  EduAR
// The screen every experiment shares, in landscape. Floating cards sit at the edges so the middle of the screen stays
// clear for the lab (and for the real table in AR). An experiment passes its text and callbacks in HudConfig and keeps
// its own logic; it never builds UI chrome itself.
//
//   top-left      Back · experiment name · step card (badge, Hide/Show, title, progress pills, instruction)
//   top-right     AR LAB · equation card (ShowEquation)
//   top-centre    TopCentreSlot — for a progress bar         bottom-left   StatusSlot — for a status panel
//   bottom-centre feedback toast · Previous / Next · Observation / Equation / Result
//   bottom-right  Reset · Safety (lifted above Move lab / Exit AR while in AR)
//
// Canvas units are a 1920 x 1080 reference (CreateCanvas); sizes below are in those units.
// ============================================================
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARVirtualLab.UI
{
    public class HudConfig
    {
        public string AppLabel;                 // top-left name, e.g. "ELECTROLYSIS OF WATER"
        public string IntroTitle;
        public string IntroSubtitle;
        public string Equation;                 // intro card and the equation card
        public string EquationCaption;          // optional second line of the equation card, e.g. in words
        public string[] OverviewSteps;          // numbered list on the intro card
        public int StepCount;                   // number of progress pills
        public string SafetyBody;
        public string AboutBody;
        public string ObservationBody;
        public string EquationBody;
        public string ResultBody;
        public System.Action OnStart;
        public System.Action OnArToggle;
        public System.Action OnPrev;
        public System.Action OnNext;
        public System.Action OnReset;
    }

    public class ExperimentHud
    {
        public GameObject IntroPanel, HudPanel;
        public TMP_Text StepNumberBadge, StepTitle, StepDesc;
        public readonly List<Image> Pills = new List<Image>();
        public Button PrevBtn, NextBtn, ObsBtn, EqBtn, ResultBtn, ResetBtn, SafetyBtn;
        public GameObject SafetyPanel, ObsPanel, EqPanel, ResultPanel, AboutPanel, ResetConfirmDialog;
        /// <summary>Bottom-left, 640 x 290: an experiment's own status panel (e.g. heating progress) goes here.</summary>
        public RectTransform StatusSlot;
        /// <summary>Top-centre, 600 x 56: an experiment's own progress bar goes here.</summary>
        public RectTransform TopCentreSlot;

        private const float M = 40f;                              // edge margin
        private const float CardW = 640f, TextW = 592f, CardCollapsedH = 152f, TextMaxH = 220f;

        private MonoBehaviour _owner;
        private HudConfig _cfg;
        private GameObject _toast, _equation;
        private TMP_Text _toastText, _toggleLabel;
        private Coroutine _toastCo;
        private RectTransform _card, _rightColumn;
        private GameObject _pillRow;
        private bool _collapsed, _inAR;
        private readonly List<Image> _seeThrough = new List<Image>();

        // ── canvas ─────────────────────────────────────────────
        /// <summary>The landscape canvas experiments build their UI on (labels, wires and the HUD).</summary>
        public static RectTransform CreateCanvas(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            if (Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
                new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            return go.GetComponent<RectTransform>();
        }

        public static ExperimentHud Build(MonoBehaviour owner, RectTransform canvasRoot, HudConfig cfg)
        {
            var hud = new ExperimentHud { _owner = owner, _cfg = cfg };
            hud.BuildHud(canvasRoot);
            hud.BuildIntro(canvasRoot);
            hud.BuildModals(canvasRoot);
            canvasRoot.gameObject.AddComponent<ExperimentHudDriver>().Hud = hud;
            return hud;
        }

        // ── intro card ─────────────────────────────────────────
        private void BuildIntro(RectTransform root)
        {
            var overlay = UiKit.Box(root, "IntroPanel", EduTheme.Page);
            UiKit.Fill(overlay.rectTransform);
            overlay.raycastTarget = true;
            IntroPanel = overlay.gameObject;

            var card = UiKit.Card(overlay.transform, "Card", 30);
            UiKit.Anchor(card.transform.parent.GetComponent<RectTransform>(), 0.22f, 0.04f, 0.78f, 0.96f);

            var badge = UiKit.Box(card.transform, "Badge", EduTheme.TealTint, 16);
            UiKit.Anchor(badge.rectTransform, 0.12f, 0.89f, 0.88f, 0.96f);
            UiKit.Fill(Fixed(UiKit.Text(badge.transform, "T", "VIRTUAL CHEMISTRY LABORATORY", 22, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.Center), 22f).rectTransform);

            var title = Fit(UiKit.Text(card.transform, "Title", _cfg.IntroTitle, 40, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center), 28f, 40f);
            UiKit.Anchor(title.rectTransform, 0.06f, 0.77f, 0.94f, 0.88f);
            var sub = Fixed(UiKit.Text(card.transform, "Sub", _cfg.IntroSubtitle, 26, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.Center), 26f);
            UiKit.Anchor(sub.rectTransform, 0.06f, 0.71f, 0.94f, 0.77f);

            var eqPill = UiKit.Box(card.transform, "EqPill", EduTheme.TealTint, 14);
            UiKit.Anchor(eqPill.rectTransform, 0.08f, 0.61f, 0.92f, 0.70f);
            UiKit.Fill(Fit(UiKit.Text(eqPill.transform, "Eq", _cfg.Equation, 28, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.Center), 20f, 28f).rectTransform);

            var info = UiKit.Box(card.transform, "InfoBox", EduTheme.PaperTint, 16);
            UiKit.Anchor(info.rectTransform, 0.06f, 0.26f, 0.94f, 0.59f);
            var infoTitle = Fixed(UiKit.Text(info.transform, "InfoTitle", "EXPERIMENT OVERVIEW", 22, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.TopLeft), 22f);
            UiKit.Anchor(infoTitle.rectTransform, 0.05f, 0.84f, 0.95f, 0.96f);
            var body = new System.Text.StringBuilder();
            if (_cfg.OverviewSteps != null)
                for (int i = 0; i < _cfg.OverviewSteps.Length; i++) body.Append(i + 1).Append(". ").Append(_cfg.OverviewSteps[i]).Append('\n');
            var infoBody = Fit(UiKit.Text(info.transform, "InfoBody", body.ToString().TrimEnd(), 24, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft), 16f, 24f);
            UiKit.Anchor(infoBody.rectTransform, 0.05f, 0.04f, 0.95f, 0.82f);

            var start = UiKit.Button(card.transform, "StartBtn", "Start Experiment", EduTheme.Teal, Color.white, 30, 18);
            UiKit.Anchor(start.GetComponent<RectTransform>(), 0.08f, 0.15f, 0.92f, 0.235f);
            start.onClick.AddListener(() => { if (_cfg.OnStart != null) _cfg.OnStart(); });

            var ar = UiKit.Button(card.transform, "ARIntroBtn", "AR Lab", EduTheme.PurpleAR, Color.white, 28, 18);
            UiKit.Anchor(ar.GetComponent<RectTransform>(), 0.08f, 0.07f, 0.92f, 0.14f);
            ar.onClick.AddListener(() => { if (_cfg.OnArToggle != null) _cfg.OnArToggle(); });

            var home = UiKit.Button(card.transform, "HomeLink", "Home", EduTheme.Neutral, EduTheme.Ink, 22, 14);
            UiKit.Anchor(home.GetComponent<RectTransform>(), 0.08f, 0.012f, 0.34f, 0.06f);
            home.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());
            var safety = UiKit.Button(card.transform, "SafetyLink", "Safety", EduTheme.Neutral, EduTheme.Ink, 22, 14);
            UiKit.Anchor(safety.GetComponent<RectTransform>(), 0.37f, 0.012f, 0.63f, 0.06f);
            safety.onClick.AddListener(() => TogglePanel(SafetyPanel));
            var about = UiKit.Button(card.transform, "AboutLink", "About", EduTheme.Neutral, EduTheme.Ink, 22, 14);
            UiKit.Anchor(about.GetComponent<RectTransform>(), 0.66f, 0.012f, 0.92f, 0.06f);
            about.onClick.AddListener(() => TogglePanel(AboutPanel));
        }

        // ── the landscape HUD ──────────────────────────────────
        private void BuildHud(RectTransform root)
        {
            var hud = UiKit.Node(root, "HUD");
            UiKit.Fill(hud);
            hud.gameObject.AddComponent<ARVirtualLab.AppShell.SafeAreaHelper>();
            HudPanel = hud.gameObject;

            // top-left: Back + name chip
            var back = UiKit.Button(hud, "BackButton", "Back", EduTheme.Neutral, EduTheme.Ink, 30, 16);
            UiKit.Pin(Rt(back), 0f, 1f, M, -32f, 170f, 80f);
            back.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());

            var chip = SeeThrough(UiKit.Box(hud, "AppLabelChip", EduTheme.Paper, 14));
            UiKit.Pin(chip.rectTransform, 0f, 1f, M + 186f, -44f, 300f, 56f);
            var chipLayout = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
            chipLayout.padding = new RectOffset(20, 20, 0, 0);
            chipLayout.childAlignment = TextAnchor.MiddleCenter;
            chipLayout.childControlWidth = chipLayout.childControlHeight = true;
            chipLayout.childForceExpandWidth = chipLayout.childForceExpandHeight = false;
            chip.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            var appLabel = Fixed(UiKit.Text(chip.transform, "AppLabel", _cfg.AppLabel, 24, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.Center), 24f);
            appLabel.textWrappingMode = TextWrappingModes.NoWrap;

            // top-left: step card
            var card = SeeThrough(UiKit.Box(hud, "StepCard", EduTheme.Paper, 18));
            _card = UiKit.Pin(card.rectTransform, 0f, 1f, M, -128f, CardW, 340f);
            var outline = card.gameObject.AddComponent<Outline>();
            outline.effectColor = EduTheme.Border; outline.effectDistance = new Vector2(1.5f, -1.5f);

            var badge = UiKit.Box(_card, "StepBadge", EduTheme.Blue, 12);
            UiKit.Pin(badge.rectTransform, 0f, 1f, 20f, -18f, 220f, 52f);
            StepNumberBadge = Fixed(UiKit.Text(badge.transform, "T", "STEP 1", 22, Color.white, FontStyles.Bold, TextAlignmentOptions.Center), 22f);
            UiKit.Fill(StepNumberBadge.rectTransform);

            var toggle = UiKit.Button(_card, "StepToggle", "Hide", EduTheme.Neutral, EduTheme.Ink, 24, 12);
            UiKit.Pin(Rt(toggle), 1f, 1f, -20f, -18f, 120f, 52f);
            _toggleLabel = toggle.GetComponentInChildren<TMP_Text>();
            Fixed(_toggleLabel, 24f);
            toggle.onClick.AddListener(() => SetCollapsed(!_collapsed));

            StepTitle = Fit(UiKit.Text(_card, "StepTitle", "", 32, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft), 24f, 32f);
            UiKit.Pin(StepTitle.rectTransform, 0f, 1f, 24f, -80f, TextW, 60f);

            _pillRow = UiKit.Node(_card, "Pills").gameObject;
            var pills = UiKit.Pin(_pillRow.GetComponent<RectTransform>(), 0f, 1f, 24f, -152f, TextW, 12f);
            Pills.Clear();
            int n = Mathf.Max(1, _cfg.StepCount);
            for (int i = 0; i < n; i++)
            {
                var pill = UiKit.Box(pills, "Pill_" + (i + 1), EduTheme.PillIdle, 4);
                UiKit.Anchor(pill.rectTransform, (float)i / n + 0.008f, 0f, (float)(i + 1) / n - 0.008f, 1f);
                Pills.Add(pill);
            }

            StepDesc = Fixed(UiKit.Text(_card, "StepDesc", "", 26, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft), 26f);
            UiKit.Pin(StepDesc.rectTransform, 0f, 1f, 24f, -180f, TextW, 144f);

            // top-right: AR toggle + equation card
            var arBtn = UiKit.Button(hud, "ARButton", "AR LAB", EduTheme.PurpleAR, Color.white, 30, 16);
            UiKit.Pin(Rt(arBtn), 1f, 1f, -M, -32f, 220f, 80f);
            arBtn.onClick.AddListener(() => { if (_cfg.OnArToggle != null) _cfg.OnArToggle(); });

            var eq = SeeThrough(UiKit.Box(hud, "EquationCard", EduTheme.Paper, 18));
            var eqRT = UiKit.Pin(eq.rectTransform, 1f, 1f, -M, -128f, 560f, string.IsNullOrEmpty(_cfg.EquationCaption) ? 116f : 150f);
            _equation = eq.gameObject;
            var eqHead = Fixed(UiKit.Text(eqRT, "Label", "Reaction:", 22, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.TopLeft), 22f);
            UiKit.Pin(eqHead.rectTransform, 0f, 1f, 24f, -14f, 512f, 30f);
            var eqText = Fit(UiKit.Text(eqRT, "Equation", _cfg.Equation, 30, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft), 20f, 30f);
            UiKit.Pin(eqText.rectTransform, 0f, 1f, 24f, -46f, 512f, 52f);
            if (!string.IsNullOrEmpty(_cfg.EquationCaption))
            {
                var cap = Fit(UiKit.Text(eqRT, "Caption", _cfg.EquationCaption, 20, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft), 16f, 20f);
                UiKit.Pin(cap.rectTransform, 0f, 1f, 24f, -100f, 512f, 36f);
            }
            _equation.SetActive(false);

            // experiment slots
            TopCentreSlot = UiKit.Pin(UiKit.Node(hud, "TopCentreSlot"), 0.5f, 1f, 0f, -M, 600f, 56f);
            StatusSlot = UiKit.Pin(UiKit.Node(hud, "StatusSlot"), 0f, 0f, M, 32f, CardW, 290f);

            // bottom-centre: toast + step / results buttons (only the active ones show, centred)
            var toast = SeeThrough(UiKit.Box(hud, "Toast", EduTheme.Paper, 20));
            UiKit.Pin(toast.rectTransform, 0.5f, 0f, 60f, 130f, 700f, 100f);
            var toastOutline = toast.gameObject.AddComponent<Outline>();
            toastOutline.effectColor = EduTheme.Border; toastOutline.effectDistance = new Vector2(1.5f, -1.5f);
            _toast = toast.gameObject;
            _toastText = Fit(UiKit.Text(toast.transform, "Text", "", 26, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center), 20f, 26f);
            UiKit.Fill(_toastText.rectTransform);
            _toastText.rectTransform.offsetMin = new Vector2(24f, 6f);
            _toastText.rectTransform.offsetMax = new Vector2(-24f, -6f);
            _toast.SetActive(false);

            var row = UiKit.Pin(UiKit.Node(hud, "BottomRow"), 0.5f, 0f, 60f, 32f, 960f, 80f);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 16f; hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = hlg.childControlHeight = false;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            PrevBtn = RowButton(row, "Prev", "Previous", EduTheme.Neutral, EduTheme.Ink, () => _cfg.OnPrev);
            NextBtn = RowButton(row, "Next", "Next", EduTheme.Blue, Color.white, () => _cfg.OnNext);
            ObsBtn = RowButton(row, "Obs", "Observation", EduTheme.Green, Color.white, null);
            ObsBtn.onClick.AddListener(() => TogglePanel(ObsPanel));
            EqBtn = RowButton(row, "Eq", "Equation", EduTheme.PurpleAR, Color.white, null);
            EqBtn.onClick.AddListener(() => TogglePanel(EqPanel));
            ResultBtn = RowButton(row, "Result", "Result", EduTheme.TealDark, Color.white, null);
            ResultBtn.onClick.AddListener(() => TogglePanel(ResultPanel));
            SetNav(false, false);
            ShowResultButtons(false);

            // bottom-right: Reset / Safety
            _rightColumn = UiKit.Pin(UiKit.Node(hud, "RightColumn"), 1f, 0f, -M, 32f, 200f, 172f);
            var vlg = _rightColumn.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 12f; vlg.childAlignment = TextAnchor.LowerCenter;
            vlg.childControlWidth = vlg.childControlHeight = false;
            vlg.childForceExpandWidth = vlg.childForceExpandHeight = false;
            ResetBtn = RowButton(_rightColumn, "Reset", "Reset", EduTheme.Red, Color.white, null, 200f);
            ResetBtn.onClick.AddListener(() =>
            {
                if (ResetConfirmDialog != null) ResetConfirmDialog.SetActive(true);
                else if (_cfg.OnReset != null) _cfg.OnReset();
            });
            SafetyBtn = RowButton(_rightColumn, "Safety", "Safety", EduTheme.Orange, Color.white, null, 200f);
            SafetyBtn.onClick.AddListener(() => TogglePanel(SafetyPanel));

            HudPanel.SetActive(false);
        }

        // ── info cards and the reset question ──────────────────
        private void BuildModals(RectTransform root)
        {
            ObsPanel = MakeInfoPanel(root, "ObsPanel", "OBSERVATION", _cfg.ObservationBody);
            EqPanel = MakeInfoPanel(root, "EqPanel", "CHEMICAL EQUATION", _cfg.EquationBody);
            ResultPanel = MakeInfoPanel(root, "ResultPanel", "EXPERIMENT RESULT", _cfg.ResultBody);
            SafetyPanel = MakeInfoPanel(root, "SafetyPanel", "SAFETY PRECAUTIONS", _cfg.SafetyBody);
            AboutPanel = MakeInfoPanel(root, "AboutPanel", "ABOUT EXPERIMENT", _cfg.AboutBody);

            var overlay = Scrim(root, "ResetConfirmOverlay");
            ResetConfirmDialog = overlay.gameObject;
            var dlg = UiKit.Card(overlay.transform, "Bg", 24);
            UiKit.Anchor(dlg.transform.parent.GetComponent<RectTransform>(), 0.30f, 0.30f, 0.70f, 0.70f);
            var t = Fixed(UiKit.Text(dlg.transform, "Title", "Reset Experiment?", 32, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center), 32f);
            UiKit.Anchor(t.rectTransform, 0.05f, 0.62f, 0.95f, 0.90f);
            var m = Fit(UiKit.Text(dlg.transform, "Msg", "All progress will be reset to Step 1.", 26, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.Center), 20f, 26f);
            UiKit.Anchor(m.rectTransform, 0.05f, 0.36f, 0.95f, 0.60f);
            var cancel = UiKit.Button(dlg.transform, "Cancel", "Cancel", EduTheme.Neutral, EduTheme.Ink, 26, 14);
            UiKit.Anchor(cancel.GetComponent<RectTransform>(), 0.08f, 0.09f, 0.46f, 0.30f);
            cancel.onClick.AddListener(() => ResetConfirmDialog.SetActive(false));
            var confirm = UiKit.Button(dlg.transform, "Confirm", "Reset", EduTheme.Red, Color.white, 26, 14);
            UiKit.Anchor(confirm.GetComponent<RectTransform>(), 0.54f, 0.09f, 0.92f, 0.30f);
            confirm.onClick.AddListener(() => { ResetConfirmDialog.SetActive(false); if (_cfg.OnReset != null) _cfg.OnReset(); });
            ResetConfirmDialog.SetActive(false);

            // Android back closes an open card before it leaves the experiment
            foreach (var p in new[] { ObsPanel, EqPanel, ResultPanel, SafetyPanel, AboutPanel, ResetConfirmDialog })
            {
                var panel = p;
                ARVirtualLab.AppShell.BackButton.Register(panel, () => { panel.SetActive(false); return true; });
            }
        }

        private GameObject MakeInfoPanel(RectTransform root, string name, string heading, string body)
        {
            var overlay = Scrim(root, name + "_Overlay");
            var bg = UiKit.Card(overlay.transform, "Bg", 26);
            UiKit.Anchor(bg.transform.parent.GetComponent<RectTransform>(), 0.20f, 0.06f, 0.80f, 0.94f);
            var head = Fixed(UiKit.Text(bg.transform, "Heading", heading, 34, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.Center), 34f);
            UiKit.Anchor(head.rectTransform, 0.06f, 0.87f, 0.94f, 0.97f);
            var text = Fit(UiKit.Text(bg.transform, "Body", body ?? "", 26, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft), 16f, 26f);
            UiKit.Anchor(text.rectTransform, 0.06f, 0.13f, 0.94f, 0.85f);
            var close = UiKit.Button(bg.transform, "CloseBtn", "Close", EduTheme.Neutral, EduTheme.Ink, 26, 14);
            UiKit.Anchor(close.GetComponent<RectTransform>(), 0.35f, 0.025f, 0.65f, 0.105f);
            close.onClick.AddListener(() => overlay.gameObject.SetActive(false));
            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        private static Image Scrim(RectTransform root, string name)
        {
            var overlay = UiKit.Box(root, name, new Color(0.05f, 0.08f, 0.10f, 0.60f));
            UiKit.Fill(overlay.rectTransform);
            overlay.raycastTarget = true;
            return overlay;
        }

        // ── used by the experiments ────────────────────────────
        public void TogglePanel(GameObject panel)
        {
            if (panel != null) panel.SetActive(!panel.activeSelf);
        }

        public void ShowFeedback(string message)
        {
            if (_toastText == null) return;
            _toastText.text = MobileText.Clean(message);
            _toast.SetActive(true);
            if (_toastCo != null) _owner.StopCoroutine(_toastCo);
            _toastCo = _owner.StartCoroutine(HideAfter(3.5f));
        }

        private IEnumerator HideAfter(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_toast != null) _toast.SetActive(false);
        }

        /// <summary>Pills, badge, title and instruction. badge null = "STEP n OF m".</summary>
        public void SetStep(int stepOneBased, string title, string desc, string badge = null)
        {
            for (int i = 0; i < Pills.Count; i++)
                if (Pills[i] != null)
                    Pills[i].color = i < stepOneBased - 1 ? EduTheme.PillDone : i == stepOneBased - 1 ? EduTheme.PillActive : EduTheme.PillIdle;
            if (StepNumberBadge != null) StepNumberBadge.text = MobileText.Clean(badge ?? $"STEP {stepOneBased} OF {Pills.Count}");
            if (StepTitle != null) StepTitle.text = MobileText.Clean(title);
            if (StepDesc != null) StepDesc.text = MobileText.Clean(desc);
            RefreshCardHeight();
        }

        public void SetNav(bool showPrev, bool showNext)
        {
            if (PrevBtn != null) PrevBtn.gameObject.SetActive(showPrev);
            if (NextBtn != null) NextBtn.gameObject.SetActive(showNext);
        }

        public void ShowResultButtons(bool show)
        {
            if (ObsBtn != null) ObsBtn.gameObject.SetActive(show);
            if (EqBtn != null) EqBtn.gameObject.SetActive(show);
            if (ResultBtn != null) ResultBtn.gameObject.SetActive(show);
        }

        /// <summary>Step text plus the usual buttons: Previous/Next during the steps, the result cards at the end.</summary>
        public void SetProgress(int stepIndexOneBased, string title, string desc, bool isResults)
        {
            SetStep(stepIndexOneBased, title, desc, isResults ? "DONE" : null);
            SetNav(!isResults && stepIndexOneBased > 1, !isResults);
            ShowResultButtons(isResults);
        }

        public void ShowEquation(bool show)
        {
            if (_equation != null) _equation.SetActive(show);
        }

        public void ShowIntro(bool show)
        {
            if (IntroPanel != null) IntroPanel.SetActive(show);
            if (HudPanel != null) HudPanel.SetActive(!show);
        }

        public void CloseAllModals()
        {
            foreach (var p in new[] { SafetyPanel, ObsPanel, EqPanel, ResultPanel, AboutPanel, ResetConfirmDialog })
                if (p != null) p.SetActive(false);
        }

        // ── step card size, AR look ────────────────────────────
        private void SetCollapsed(bool collapsed)
        {
            _collapsed = collapsed;
            if (_pillRow != null) _pillRow.SetActive(!collapsed);
            if (StepDesc != null) StepDesc.gameObject.SetActive(!collapsed);
            if (_toggleLabel != null) _toggleLabel.text = collapsed ? "Show" : "Hide";
            RefreshCardHeight();
        }

        // The card grows with the instruction instead of reserving room for the longest one
        internal void RefreshCardHeight()
        {
            if (_card == null || StepDesc == null) return;
            float h = CardCollapsedH;
            if (!_collapsed)
            {
                var native = StepDesc.GetComponent<ARVirtualLab.Localization.NativeText>();
                float measured = native != null && native.ShowingNative
                    ? native.RenderedSize.y
                    : StepDesc.GetPreferredValues(StepDesc.text, TextW, 0f).y;
                float textH = Mathf.Clamp(measured, 34f, TextMaxH);
                StepDesc.rectTransform.sizeDelta = new Vector2(TextW, textH);
                h = 180f + textH + 22f;
            }
            _card.sizeDelta = new Vector2(CardW, h);
        }

        internal bool IsStepText(TMP_Text t) { return t == StepDesc; }

        // In AR the cards become slightly see-through and Reset/Safety step up above Move lab / Exit AR
        internal void Tick()
        {
            bool inAR = ARVirtualLab.AR.ARExperimentMode.Active;
            if (inAR == _inAR) return;
            _inAR = inAR;
            foreach (var img in _seeThrough)
            {
                if (img == null) continue;
                var c = img.color; c.a = inAR ? 0.86f : 1f; img.color = c;
            }
            if (_rightColumn != null) _rightColumn.anchoredPosition = new Vector2(-M, inAR ? 32f + 96f : 32f);
        }

        // ── small helpers ──────────────────────────────────────
        private Image SeeThrough(Image img) { _seeThrough.Add(img); return img; }

        private static RectTransform Rt(Component c) { return c.GetComponent<RectTransform>(); }

        private static TMP_Text Fixed(TMP_Text t, float size)
        {
            t.enableAutoSizing = false;
            t.fontSize = size;
            return t;
        }

        private static TMP_Text Fit(TMP_Text t, float min, float max)
        {
            t.enableAutoSizing = true;
            t.fontSizeMin = min;
            t.fontSizeMax = max;
            return t;
        }

        private Button RowButton(RectTransform parent, string name, string label, Color bg, Color fg,
            System.Func<System.Action> action, float width = 220f)
        {
            var b = UiKit.Button(parent, name, label, bg, fg, 28, 16);
            Fixed(b.GetComponentInChildren<TMP_Text>(), 28f);
            Rt(b).sizeDelta = new Vector2(width, 80f);
            if (action != null) b.onClick.AddListener(() => { var a = action(); if (a != null) a(); });
            return b;
        }
    }

    /// <summary>Per-frame upkeep for ExperimentHud, which is a plain object.</summary>
    public class ExperimentHudDriver : MonoBehaviour
    {
        public ExperimentHud Hud;

        private void OnEnable() { ARVirtualLab.Localization.NativeText.Rendered += OnRendered; }
        private void OnDisable() { ARVirtualLab.Localization.NativeText.Rendered -= OnRendered; }
        private void Update() { if (Hud != null) Hud.Tick(); }

        private void OnRendered(TMP_Text text, Vector2 size)
        {
            if (Hud != null && Hud.IsStepText(text)) Hud.RefreshCardHeight();       // Telugu is often taller than English
        }
    }
}
