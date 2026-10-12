// ============================================================
// ExperimentHud.cs  —  AR Virtual Lab
// The chrome every experiment screen shares — intro card, top status bar with step pills, toast feedback, bottom
// action bar, and the Safety/Observation/Equation/Result modal cards — built once in the light textbook theme so new
// experiments don't have to re-author ~800 lines of near-identical UI code each time (the three original experiments
// grew that UI ad hoc, then had the light theme patched on afterwards; this is the same layout, built light from the
// start, with the scene-specific text and callbacks passed in as configuration).
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
        public string AppLabel;                 // top bar title, e.g. "ELECTROLYSIS OF WATER"
        public string IntroTitle;
        public string IntroSubtitle;
        public string Equation;
        public string[] OverviewSteps;           // numbered list on the intro card
        public int StepCount;                    // number of progress pills
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
        public Button PrevBtn, NextBtn, ObsBtn, EqBtn, ResultBtn;
        public GameObject SafetyPanel, ObsPanel, EqPanel, ResultPanel, AboutPanel, ResetConfirmDialog;

        private MonoBehaviour _owner;
        private HudConfig _cfg;
        private GameObject _toast;
        private TMP_Text _toastText;
        private Coroutine _toastCo;

        public static ExperimentHud Build(MonoBehaviour owner, RectTransform canvasRoot, HudConfig cfg)
        {
            var hud = new ExperimentHud { _owner = owner, _cfg = cfg };
            hud.BuildIntro(canvasRoot);
            hud.BuildHud(canvasRoot);
            hud.BuildModals(canvasRoot);
            return hud;
        }

        // ── Intro card ─────────────────────────────────────────
        private void BuildIntro(RectTransform root)
        {
            var overlay = UiKit.Box(root, "IntroPanel", EduTheme.Page);
            UiKit.Fill(overlay.rectTransform);
            IntroPanel = overlay.gameObject;

            var card = UiKit.Card(overlay.transform, "Card", 30);
            UiKit.Anchor(card.transform.parent.GetComponent<RectTransform>(), 0.05f, 0.08f, 0.95f, 0.94f);

            var badge = UiKit.Box(card.transform, "Badge", EduTheme.TealTint, 16);
            UiKit.Anchor(badge.rectTransform, 0.10f, 0.90f, 0.90f, 0.96f);
            var badgeText = UiKit.Text(badge.transform, "T", "VIRTUAL CHEMISTRY LABORATORY", 22, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(badgeText.rectTransform);

            var title = UiKit.Text(card.transform, "Title", _cfg.IntroTitle, 40, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Top);
            UiKit.Anchor(title.rectTransform, 0.06f, 0.76f, 0.94f, 0.89f);
            MobileText.Setup(title, "Title", 34);

            var sub = UiKit.Text(card.transform, "Sub", _cfg.IntroSubtitle, 26, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.Top);
            UiKit.Anchor(sub.rectTransform, 0.06f, 0.70f, 0.94f, 0.76f);

            var eqPill = UiKit.Box(card.transform, "EqPill", EduTheme.TealTint, 14);
            UiKit.Anchor(eqPill.rectTransform, 0.08f, 0.60f, 0.92f, 0.69f);
            var eqText = UiKit.Text(eqPill.transform, "Eq", _cfg.Equation, 26, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(eqText.rectTransform);
            MobileText.Setup(eqText, "Eq", 22);

            var infoBox = UiKit.Box(card.transform, "InfoBox", EduTheme.PaperTint, 16);
            UiKit.Anchor(infoBox.rectTransform, 0.06f, 0.27f, 0.94f, 0.58f);
            var infoTitle = UiKit.Text(infoBox.transform, "InfoTitle", "EXPERIMENT OVERVIEW", 22, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(infoTitle.rectTransform, 0.06f, 0.86f, 0.94f, 0.97f);
            var body = new System.Text.StringBuilder();
            for (int i = 0; i < _cfg.OverviewSteps.Length; i++) body.Append(i + 1).Append(". ").Append(_cfg.OverviewSteps[i]).Append('\n');
            var infoBody = UiKit.Text(infoBox.transform, "InfoBody", body.ToString().TrimEnd(), 22, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(infoBody.rectTransform, 0.06f, 0.04f, 0.94f, 0.84f);

            var startBtn = UiKit.Button(card.transform, "StartBtn", "Start Experiment", EduTheme.Teal, Color.white, 30, 18);
            UiKit.Anchor(startBtn.GetComponent<RectTransform>(), 0.08f, 0.155f, 0.92f, 0.245f);
            startBtn.onClick.AddListener(() => _cfg.OnStart?.Invoke());

            var arBtn = UiKit.Button(card.transform, "ARIntroBtn", "AR Lab", EduTheme.PurpleAR, Color.white, 28, 18);
            UiKit.Anchor(arBtn.GetComponent<RectTransform>(), 0.08f, 0.065f, 0.92f, 0.145f);
            arBtn.onClick.AddListener(() => _cfg.OnArToggle?.Invoke());

            var homeLink = UiKit.Button(card.transform, "HomeLink", "Home", EduTheme.Neutral, EduTheme.Ink, 22, 14);
            UiKit.Anchor(homeLink.GetComponent<RectTransform>(), 0.08f, 0.01f, 0.34f, 0.055f);
            homeLink.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());

            var safetyLink = UiKit.Button(card.transform, "SafetyLink", "Safety", EduTheme.Neutral, EduTheme.Ink, 22, 14);
            UiKit.Anchor(safetyLink.GetComponent<RectTransform>(), 0.37f, 0.01f, 0.63f, 0.055f);
            safetyLink.onClick.AddListener(() => TogglePanel(SafetyPanel));

            var aboutLink = UiKit.Button(card.transform, "AboutLink", "About", EduTheme.Neutral, EduTheme.Ink, 22, 14);
            UiKit.Anchor(aboutLink.GetComponent<RectTransform>(), 0.66f, 0.01f, 0.92f, 0.055f);
            aboutLink.onClick.AddListener(() => TogglePanel(AboutPanel));
        }

        // ── HUD (top bar + toast + bottom bar) ─────────────────
        private void BuildHud(RectTransform root)
        {
            HudPanel = UiKit.Node(root, "HUD").gameObject;
            UiKit.Fill(HudPanel.GetComponent<RectTransform>());
            HudPanel.SetActive(false);

            var topBar = UiKit.Box(HudPanel.transform, "TopBar", EduTheme.Paper, 0);
            UiKit.Anchor(topBar.rectTransform, 0.0f, 0.81f, 1.0f, 1.0f);
            var topLine = UiKit.Box(topBar.transform, "Line", EduTheme.Teal);
            topLine.rectTransform.anchorMin = new Vector2(0f, 0f); topLine.rectTransform.anchorMax = new Vector2(1f, 0f);
            topLine.rectTransform.pivot = new Vector2(0.5f, 0f); topLine.rectTransform.sizeDelta = new Vector2(0f, 5f);

            var backBtn = UiKit.Button(topBar.transform, "BackButton", "Back", EduTheme.Neutral, EduTheme.Ink, 26, 14);
            UiKit.Anchor(backBtn.GetComponent<RectTransform>(), 0.03f, 0.80f, 0.24f, 0.97f);
            backBtn.onClick.AddListener(() => ARVirtualLab.AppShell.AppNavigation.GoToHome());

            var appLabel = UiKit.Text(topBar.transform, "AppLabel", _cfg.AppLabel, 26, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            UiKit.Anchor(appLabel.rectTransform, 0.26f, 0.80f, 0.70f, 0.97f);
            MobileText.Setup(appLabel, "AppLabel", 20);

            var arBtn = UiKit.Button(topBar.transform, "ARButton", "AR LAB", EduTheme.PurpleAR, Color.white, 24, 14);
            UiKit.Anchor(arBtn.GetComponent<RectTransform>(), 0.72f, 0.80f, 0.97f, 0.97f);
            arBtn.onClick.AddListener(() => _cfg.OnArToggle?.Invoke());

            var pillRow = UiKit.Node(topBar.rectTransform, "Pills");
            UiKit.Anchor(pillRow, 0.04f, 0.69f, 0.96f, 0.76f);
            Pills.Clear();
            float w = 1f / Mathf.Max(1, _cfg.StepCount);
            for (int i = 0; i < _cfg.StepCount; i++)
            {
                var pill = UiKit.Box(pillRow, "Pill_" + (i + 1), EduTheme.PillIdle, 4);
                UiKit.Anchor(pill.rectTransform, i * w + 0.010f, 0f, (i + 1) * w - 0.010f, 1f);
                Pills.Add(pill);
            }

            var stepBadge = UiKit.Box(topBar.transform, "StepBadge", EduTheme.Blue, 12);
            UiKit.Anchor(stepBadge.rectTransform, 0.04f, 0.44f, 0.32f, 0.63f);
            StepNumberBadge = UiKit.Text(stepBadge.transform, "T", "STEP 1", 20, Color.white, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(StepNumberBadge.rectTransform);
            MobileText.Setup(StepNumberBadge, "StepNumberBadge", 18);

            StepTitle = UiKit.Text(topBar.transform, "StepTitle", "", 28, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            UiKit.Anchor(StepTitle.rectTransform, 0.35f, 0.44f, 0.96f, 0.65f);
            MobileText.Setup(StepTitle, "StepTitle", 22);

            StepDesc = UiKit.Text(topBar.transform, "StepDesc", "", 24, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(StepDesc.rectTransform, 0.04f, 0.04f, 0.96f, 0.42f);
            MobileText.Setup(StepDesc, "StepDesc", 20);

            var toastBg = UiKit.Box(HudPanel.transform, "Toast", EduTheme.Paper, 22);
            UiKit.Anchor(toastBg.rectTransform, 0.08f, 0.115f, 0.92f, 0.185f);
            _toast = toastBg.gameObject;
            _toastText = UiKit.Text(toastBg.transform, "Text", "", 24, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(_toastText.rectTransform);
            _toastText.rectTransform.offsetMin = new Vector2(20f, 6f);
            _toastText.rectTransform.offsetMax = new Vector2(-20f, -6f);
            MobileText.Setup(_toastText, "Toast", 22);
            _toast.SetActive(false);

            var botBar = UiKit.Box(HudPanel.transform, "BotBar", EduTheme.Paper, 0);
            UiKit.Anchor(botBar.rectTransform, 0.0f, 0.0f, 1.0f, 0.095f);
            var botLine = UiKit.Box(botBar.transform, "Line", EduTheme.Border);
            botLine.rectTransform.anchorMin = new Vector2(0f, 1f); botLine.rectTransform.anchorMax = new Vector2(1f, 1f);
            botLine.rectTransform.pivot = new Vector2(0.5f, 1f); botLine.rectTransform.sizeDelta = new Vector2(0f, 3f);

            var resetBtn = UiKit.Button(botBar.transform, "Reset", "Reset", EduTheme.Red, Color.white, 24, 12);
            UiKit.Anchor(resetBtn.GetComponent<RectTransform>(), 0.02f, 0.14f, 0.22f, 0.86f);
            resetBtn.onClick.AddListener(() => { if (ResetConfirmDialog != null) ResetConfirmDialog.SetActive(true); else _cfg.OnReset?.Invoke(); });

            var safetyBtn = UiKit.Button(botBar.transform, "Safety", "Safety", EduTheme.Orange, Color.white, 24, 12);
            UiKit.Anchor(safetyBtn.GetComponent<RectTransform>(), 0.25f, 0.14f, 0.45f, 0.86f);
            safetyBtn.onClick.AddListener(() => TogglePanel(SafetyPanel));

            PrevBtn = UiKit.Button(botBar.transform, "Prev", "Previous", EduTheme.Neutral, EduTheme.Ink, 22, 12);
            UiKit.Anchor(PrevBtn.GetComponent<RectTransform>(), 0.48f, 0.14f, 0.70f, 0.86f);
            PrevBtn.onClick.AddListener(() => _cfg.OnPrev?.Invoke());
            PrevBtn.gameObject.SetActive(false);

            NextBtn = UiKit.Button(botBar.transform, "Next", "Next", EduTheme.Blue, Color.white, 24, 12);
            UiKit.Anchor(NextBtn.GetComponent<RectTransform>(), 0.73f, 0.14f, 0.98f, 0.86f);
            NextBtn.onClick.AddListener(() => _cfg.OnNext?.Invoke());

            ObsBtn = UiKit.Button(botBar.transform, "Obs", "Observation", EduTheme.Green, Color.white, 20, 12);
            UiKit.Anchor(ObsBtn.GetComponent<RectTransform>(), 0.48f, 0.14f, 0.64f, 0.86f);
            ObsBtn.onClick.AddListener(() => TogglePanel(ObsPanel));
            ObsBtn.gameObject.SetActive(false);

            EqBtn = UiKit.Button(botBar.transform, "Eq", "Equation", EduTheme.PurpleAR, Color.white, 20, 12);
            UiKit.Anchor(EqBtn.GetComponent<RectTransform>(), 0.66f, 0.14f, 0.82f, 0.86f);
            EqBtn.onClick.AddListener(() => TogglePanel(EqPanel));
            EqBtn.gameObject.SetActive(false);

            ResultBtn = UiKit.Button(botBar.transform, "Result", "Result", EduTheme.TealDark, Color.white, 20, 12);
            UiKit.Anchor(ResultBtn.GetComponent<RectTransform>(), 0.84f, 0.14f, 0.98f, 0.86f);
            ResultBtn.onClick.AddListener(() => TogglePanel(ResultPanel));
            ResultBtn.gameObject.SetActive(false);
        }

        // ── Modal cards ─────────────────────────────────────────
        private void BuildModals(RectTransform root)
        {
            ObsPanel = MakeInfoPanel(root, "ObsPanel", "OBSERVATION", _cfg.ObservationBody);
            EqPanel = MakeInfoPanel(root, "EqPanel", "CHEMICAL EQUATION", _cfg.EquationBody);
            ResultPanel = MakeInfoPanel(root, "ResultPanel", "EXPERIMENT RESULT", _cfg.ResultBody);
            SafetyPanel = MakeInfoPanel(root, "SafetyPanel", "SAFETY PRECAUTIONS", _cfg.SafetyBody);
            AboutPanel = MakeInfoPanel(root, "AboutPanel", "ABOUT EXPERIMENT", _cfg.AboutBody);

            var overlay = UiKit.Box(root, "ResetConfirmOverlay", new Color(0.10f, 0.15f, 0.18f, 0.55f));
            UiKit.Fill(overlay.rectTransform);
            overlay.raycastTarget = true;
            ResetConfirmDialog = overlay.gameObject;

            var dlg = UiKit.Card(overlay.transform, "Bg", 24);
            UiKit.Anchor(dlg.transform.parent.GetComponent<RectTransform>(), 0.12f, 0.40f, 0.88f, 0.60f);
            var t = UiKit.Text(dlg.transform, "Title", "Reset Experiment?", 28, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Anchor(t.rectTransform, 0.05f, 0.55f, 0.95f, 0.90f);
            var m = UiKit.Text(dlg.transform, "Msg", "All progress will be reset to Step 1.", 22, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.Center);
            UiKit.Anchor(m.rectTransform, 0.05f, 0.35f, 0.95f, 0.55f);
            var cancel = UiKit.Button(dlg.transform, "Cancel", "Cancel", EduTheme.Neutral, EduTheme.Ink, 24, 14);
            UiKit.Anchor(cancel.GetComponent<RectTransform>(), 0.08f, 0.08f, 0.46f, 0.28f);
            cancel.onClick.AddListener(() => ResetConfirmDialog.SetActive(false));
            var confirm = UiKit.Button(dlg.transform, "Confirm", "Reset", EduTheme.Red, Color.white, 24, 14);
            UiKit.Anchor(confirm.GetComponent<RectTransform>(), 0.54f, 0.08f, 0.92f, 0.28f);
            confirm.onClick.AddListener(() => { ResetConfirmDialog.SetActive(false); _cfg.OnReset?.Invoke(); });
            ResetConfirmDialog.SetActive(false);
        }

        private GameObject MakeInfoPanel(RectTransform root, string name, string heading, string body)
        {
            var overlay = UiKit.Box(root, name + "_Overlay", new Color(0.10f, 0.15f, 0.18f, 0.55f));
            UiKit.Fill(overlay.rectTransform);
            overlay.raycastTarget = true;

            var bg = UiKit.Card(overlay.transform, "Bg", 26);
            UiKit.Anchor(bg.transform.parent.GetComponent<RectTransform>(), 0.06f, 0.10f, 0.94f, 0.90f);

            var head = UiKit.Text(bg.transform, "Heading", heading, 34, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Anchor(head.rectTransform, 0.06f, 0.88f, 0.94f, 0.97f);

            var bodyT = UiKit.Text(bg.transform, "Body", body, 24, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Anchor(bodyT.rectTransform, 0.06f, 0.12f, 0.94f, 0.86f);

            var close = UiKit.Button(bg.transform, "CloseBtn", "Close", EduTheme.Neutral, EduTheme.Ink, 24, 14);
            UiKit.Anchor(close.GetComponent<RectTransform>(), 0.30f, 0.02f, 0.70f, 0.09f);
            close.onClick.AddListener(() => overlay.gameObject.SetActive(false));

            overlay.gameObject.SetActive(false);
            return overlay.gameObject;
        }

        // ── Runtime helpers used by the experiment scene ────────
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

        /// <summary>Updates the pills, step badge, title and description, and shows/hides Prev/Next vs Obs/Eq/Result.</summary>
        public void SetProgress(int stepIndexOneBased, string title, string desc, bool isResults)
        {
            for (int i = 0; i < Pills.Count; i++)
            {
                if (Pills[i] == null) continue;
                Pills[i].color = i < stepIndexOneBased - 1 ? EduTheme.PillDone : i == stepIndexOneBased - 1 ? EduTheme.PillActive : EduTheme.PillIdle;
            }
            if (StepNumberBadge != null)
                StepNumberBadge.text = MobileText.Clean(isResults ? "DONE" : "STEP " + stepIndexOneBased + " OF " + Pills.Count);
            if (StepTitle != null) StepTitle.text = MobileText.Clean(title);
            if (StepDesc != null) StepDesc.text = MobileText.Clean(desc);

            if (PrevBtn != null) PrevBtn.gameObject.SetActive(!isResults && stepIndexOneBased > 1);
            if (NextBtn != null) NextBtn.gameObject.SetActive(!isResults);
            if (ObsBtn != null) ObsBtn.gameObject.SetActive(isResults);
            if (EqBtn != null) EqBtn.gameObject.SetActive(isResults);
            if (ResultBtn != null) ResultBtn.gameObject.SetActive(isResults);
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
    }
}
