// ============================================================
// AIMentorUI.cs  —  AR Virtual Lab
// The student-facing AI mentor: a floating "Ask AI" button inside every experiment that opens a chat panel with
// suggested questions. It knows which experiment and which step the student is on (stepProvider), keeps the last few
// turns for follow-up questions, and works offline with built-in answers (see MentorBackend).
//
//   AIMentorUI.Attach(this, MentorKnowledge.Electrolysis, () => currentStepText);   // call once when the experiment's UI is built
//
// It owns its own overlay canvas, so it works over any experiment screen and in AR mode.
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ARVirtualLab.UI;

namespace ARVirtualLab.AI
{
    public class AIMentorUI : MonoBehaviour
    {
        // ---- set up ----
        private string _experimentId;
        private Func<string> _stepProvider;
        private MentorExperiment _exp;

        // ---- ui ----
        private Canvas _canvas;
        private CanvasScaler _scaler;
        private RectTransform _root;
        private Button _fab;
        private GameObject _backdrop;
        private RectTransform _panel;
        private TMP_Text _status;
        private ScrollRect _scroll;
        private RectTransform _content;
        private TMP_InputField _input;
        private Button _send;
        private Image _sendImg;

        // ---- chat state ----
        private readonly List<MentorMessage> _display = new List<MentorMessage>();    // everything shown (greeting included)
        private readonly List<MentorMessage> _history = new List<MentorMessage>();    // real turns, sent to the model
        private bool _open, _pending;
        private RectTransform _typingRow;
        private TMP_Text _typingText;
        private float _builtWidth = -1f;
        private bool? _landscapeLayout;
        private const int PanelPad = 24;

        /// <summary>Tests only: pretend the screen has this size (width,height in pixels). Zero = use the real screen.</summary>
        public static Vector2 ScreenOverride;
        private static Vector2 ScreenSz { get { return ScreenOverride.x > 0f ? ScreenOverride : new Vector2(Screen.width, Screen.height); } }
        private RectTransform ViewRect { get { return (RectTransform)_content.parent; } }      // the scroll viewport the messages sit in

        public static AIMentorUI Attach(MonoBehaviour host, string experimentId, Func<string> stepProvider)
        {
            var existing = host.GetComponentInChildren<AIMentorUI>(true);
            if (existing != null) { existing._stepProvider = stepProvider; return existing; }
            var go = new GameObject("AIMentor", typeof(RectTransform));
            go.transform.SetParent(host.transform, false);
            var ui = go.AddComponent<AIMentorUI>();
            ui._experimentId = experimentId;
            ui._stepProvider = stepProvider;
            ui._exp = MentorKnowledge.Get(experimentId);
            ui.Build();
            return ui;
        }

        // =========================================================
        //  BUILD
        // =========================================================
        private void Build()
        {
            _canvas = gameObject.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 60;
            _scaler = gameObject.AddComponent<CanvasScaler>();
            _scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.matchWidthOrHeight = 0.5f;
            _scaler.referenceResolution = new Vector2(1080f, 1920f);
            gameObject.AddComponent<GraphicRaycaster>();
            _root = GetComponent<RectTransform>();

            // floating button
            _fab = UiKit.Button(_root, "AskAI", "Ask AI", EduTheme.PurpleAR, Color.white, 34, 40);
            _fab.onClick.AddListener(Open);
            PlaceFab();

            // dimmed backdrop (a Button on purpose: the older experiment screens ignore touches that land on a Button,
            // so nothing behind the chat reacts while it is open)
            var back = UiKit.Box(_root, "Backdrop", new Color(0f, 0f, 0f, 0.5f));
            back.raycastTarget = true;
            var backBtn = back.gameObject.AddComponent<Button>();
            backBtn.transition = Selectable.Transition.None;
            UiKit.Fill(back.rectTransform);
            _backdrop = back.gameObject;

            var card = UiKit.Card(back.transform, "Panel", 36);
            _panel = card.rectTransform.parent as RectTransform;        // Card returns the inner fill; its parent is the bordered box
            _panel.anchorMin = _panel.anchorMax = _panel.pivot = new Vector2(0.5f, 0f);
            card.raycastTarget = true;
            var inner = card.rectTransform;

            BuildHeader(inner);
            BuildMessages(inner);
            BuildChips(inner);
            BuildInput(inner);

            _backdrop.SetActive(false);
            AddDisplay(false, "Hi! I am your lab mentor for " + _exp.Title + ". Ask me anything about it, or tap one of the questions below.");
            RefreshStatus();
        }

        private void PlaceFab()
        {
            var rt = _fab.GetComponent<RectTransform>();
            bool land = ScreenSz.x > ScreenSz.y;
            UiKit.Pin(rt, 1f, 0f, -24f, land ? 300f : 340f, 190f, 92f);      // clear of the experiments' own bottom buttons in both shapes
        }

        private void BuildHeader(RectTransform parent)
        {
            var title = UiKit.Text(parent, "Title", "Ask the AI Mentor", 42, EduTheme.TealDark, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            var tr = title.rectTransform;
            tr.anchorMin = new Vector2(0f, 1f); tr.anchorMax = new Vector2(1f, 1f); tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -18f); tr.sizeDelta = new Vector2(-190f, 64f);
            tr.offsetMin = new Vector2(PanelPad + 6f, tr.offsetMin.y);

            var sub = UiKit.Text(parent, "Subtitle", _exp.Title, 28, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            var sr = sub.rectTransform;
            sr.anchorMin = new Vector2(0f, 1f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(0.5f, 1f);
            sr.anchoredPosition = new Vector2(0f, -78f); sr.sizeDelta = new Vector2(-190f, 40f);
            sr.offsetMin = new Vector2(PanelPad + 6f, sr.offsetMin.y);

            var close = UiKit.Button(parent, "Close", "X", EduTheme.Neutral, EduTheme.Ink, 38, 30);
            UiKit.Pin(close.GetComponent<RectTransform>(), 1f, 1f, -PanelPad, -22f, 84f, 84f);
            close.onClick.AddListener(Close);

            var line = UiKit.Box(parent, "Line", EduTheme.Border);
            var lr = line.rectTransform;
            lr.anchorMin = new Vector2(0f, 1f); lr.anchorMax = new Vector2(1f, 1f); lr.pivot = new Vector2(0.5f, 1f);
            lr.anchoredPosition = new Vector2(0f, -122f); lr.sizeDelta = new Vector2(-2f * PanelPad, 2f);

            _status = UiKit.Text(parent, "Status", "", 24, EduTheme.InkFaint, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            var st = _status.rectTransform;
            st.anchorMin = new Vector2(0f, 1f); st.anchorMax = new Vector2(1f, 1f); st.pivot = new Vector2(0.5f, 1f);
            st.anchoredPosition = new Vector2(0f, -128f); st.sizeDelta = new Vector2(-2f * PanelPad, 36f);
        }

        private void BuildMessages(RectTransform parent)
        {
            var view = UiKit.Node(parent, "Messages");
            view.anchorMin = Vector2.zero; view.anchorMax = Vector2.one;
            view.offsetMin = new Vector2(PanelPad, 12f + 120f + 8f + 92f + 8f);
            view.offsetMax = new Vector2(-PanelPad, -(122f + 44f + 8f));

            var viewport = UiKit.Node(view, "Viewport");
            UiKit.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vImg = viewport.gameObject.AddComponent<Image>();      // gives the scroll area something to receive drags on
            vImg.color = new Color(1f, 1f, 1f, 0.004f); vImg.raycastTarget = true;

            _content = UiKit.Node(viewport, "Content");
            _content.anchorMin = new Vector2(0f, 1f); _content.anchorMax = new Vector2(1f, 1f); _content.pivot = new Vector2(0.5f, 1f);
            _content.anchoredPosition = Vector2.zero; _content.sizeDelta = Vector2.zero;
            var vlg = _content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 16f; vlg.padding = new RectOffset(0, 0, 8, 12);
            vlg.childControlWidth = true; vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            var fit = _content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _scroll = view.gameObject.AddComponent<ScrollRect>();
            _scroll.viewport = viewport; _scroll.content = _content;
            _scroll.horizontal = false; _scroll.vertical = true;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 40f;
        }

        private void BuildChips(RectTransform parent)
        {
            var strip = UiKit.Node(parent, "Suggestions");
            strip.anchorMin = new Vector2(0f, 0f); strip.anchorMax = new Vector2(1f, 0f); strip.pivot = new Vector2(0.5f, 0f);
            strip.anchoredPosition = new Vector2(0f, 12f + 120f + 8f); strip.sizeDelta = new Vector2(-2f * PanelPad, 92f);

            var viewport = UiKit.Node(strip, "Viewport");
            UiKit.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var vImg = viewport.gameObject.AddComponent<Image>(); vImg.color = new Color(1f, 1f, 1f, 0.004f); vImg.raycastTarget = true;

            var content = UiKit.Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 0f); content.anchorMax = new Vector2(0f, 1f); content.pivot = new Vector2(0f, 0.5f);
            content.anchoredPosition = Vector2.zero; content.sizeDelta = Vector2.zero;
            var hlg = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f; hlg.padding = new RectOffset(0, 0, 8, 8);
            hlg.childControlWidth = true; hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            var sr = strip.gameObject.AddComponent<ScrollRect>();
            sr.viewport = viewport; sr.content = content; sr.horizontal = true; sr.vertical = false;
            sr.movementType = ScrollRect.MovementType.Clamped; sr.scrollSensitivity = 30f;

            foreach (var q in _exp.QuickQuestions)
            {
                string question = q;
                var chip = UiKit.Button(content, "Chip", q, EduTheme.TealTint, EduTheme.TealDark, 28, 26, FontStyles.Normal);
                var label = chip.GetComponentInChildren<TMP_Text>();
                float w = label.GetPreferredValues(MobileText.Clean(q)).x + 56f;
                var le = chip.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = Mathf.Min(w, 760f); le.minWidth = 160f;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                chip.onClick.AddListener(() => Submit(question));
            }
        }

        private void BuildInput(RectTransform parent)
        {
            var row = UiKit.Node(parent, "InputRow");
            row.anchorMin = new Vector2(0f, 0f); row.anchorMax = new Vector2(1f, 0f); row.pivot = new Vector2(0.5f, 0f);
            row.anchoredPosition = new Vector2(0f, 12f); row.sizeDelta = new Vector2(-2f * PanelPad, 120f);

            var fieldBg = UiKit.Box(row, "Field", EduTheme.PaperTint, 30);
            fieldBg.raycastTarget = true;
            var fr = fieldBg.rectTransform;
            fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(0f, 8f); fr.offsetMax = new Vector2(-214f, -8f);

            var area = UiKit.Node(fieldBg.transform, "Text Area");
            UiKit.Fill(area); area.offsetMin = new Vector2(24f, 6f); area.offsetMax = new Vector2(-24f, -6f);
            area.gameObject.AddComponent<RectMask2D>();

            var text = UiKit.Text(area, "Text", "", 34, EduTheme.Ink, FontStyles.Normal, TextAlignmentOptions.MidlineLeft);
            UiKit.Fill(text.rectTransform);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            var ph = UiKit.Text(area, "Placeholder", "Type your question...", 34, EduTheme.InkFaint, FontStyles.Italic, TextAlignmentOptions.MidlineLeft);
            UiKit.Fill(ph.rectTransform);
            ph.textWrappingMode = TextWrappingModes.NoWrap;

            _input = fieldBg.gameObject.AddComponent<TMP_InputField>();
            _input.textViewport = area;
            _input.textComponent = text;
            _input.placeholder = ph;
            _input.lineType = TMP_InputField.LineType.SingleLine;
            _input.characterLimit = 240;
            _input.caretColor = EduTheme.Ink;
            _input.selectionColor = new Color(EduTheme.Teal.r, EduTheme.Teal.g, EduTheme.Teal.b, 0.35f);
            _input.onSubmit.AddListener(_ => SubmitField());

            _send = UiKit.Button(row, "Send", "Send", EduTheme.Teal, Color.white, 34, 30);
            _sendImg = _send.GetComponent<Image>();
            var sr = _send.GetComponent<RectTransform>();
            sr.anchorMin = new Vector2(1f, 0f); sr.anchorMax = new Vector2(1f, 1f); sr.pivot = new Vector2(1f, 0.5f);
            sr.offsetMin = new Vector2(-200f, 8f); sr.offsetMax = new Vector2(0f, -8f);
            _send.onClick.AddListener(SubmitField);
        }

        // =========================================================
        //  OPEN / CLOSE
        // =========================================================
        public void Open()
        {
            _open = true;
            _backdrop.SetActive(true);
            _fab.gameObject.SetActive(false);
            _builtWidth = -1f;                  // lay the messages out again at the panel's real width
            StartCoroutine(ScrollToEnd());
        }

        public void Close()
        {
            _open = false;
            if (_input != null) _input.DeactivateInputField();
            _backdrop.SetActive(false);
        }

        // =========================================================
        //  FRAME
        // =========================================================
        private void LateUpdate()
        {
            // layout follows the screen shape
            bool land = ScreenSz.x > ScreenSz.y;
            if (_landscapeLayout == null || land != _landscapeLayout.Value)
            {
                _landscapeLayout = land;
                _scaler.referenceResolution = land ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
                PlaceFab();
                _builtWidth = -1f;
            }

            // the button is only offered while the experiment is running and the student is free to use it
            bool showFab = !_open && _stepProvider != null && _stepProvider() != null && !ARVirtualLab.AR.ARExperimentMode.BlockSceneInput;
            if (_fab.gameObject.activeSelf != showFab) _fab.gameObject.SetActive(showFab);

            if (!_open) return;

            Rect cr = _root.rect;
            if (ScreenOverride.x > 0f)
            {
                Vector2 refRes = _scaler.referenceResolution;
                float sc = Mathf.Sqrt((ScreenOverride.x / refRes.x) * (ScreenOverride.y / refRes.y));      // the same scale Unity uses at match 0.5
                cr = new Rect(0f, 0f, ScreenOverride.x / sc, ScreenOverride.y / sc);
            }
            float kb = 0f;
            if (TouchScreenKeyboard.visible)
            {
                float area = TouchScreenKeyboard.area.height;
                if (area <= 0f) area = Screen.height * 0.42f;
                kb = area / Mathf.Max(0.01f, _canvas.scaleFactor);
            }
            float w = Mathf.Min(cr.width - 40f, land ? 1500f : 1020f);
            float availH = cr.height - kb;
            float h = Mathf.Max(480f, Mathf.Min(availH - 60f, land ? 900f : 1500f));
            _panel.sizeDelta = new Vector2(w, h);
            _panel.anchoredPosition = new Vector2(0f, kb + Mathf.Max(20f, (availH - h) * 0.5f));

            if (Mathf.Abs(ViewRect.rect.width - _builtWidth) > 12f && ViewRect.rect.width > 50f) RebuildMessages();

            if (_typingText != null) _typingText.text = "Thinking" + new string('.', 1 + (int)(Time.unscaledTime * 3f) % 3);
            if (_sendImg != null) _sendImg.color = _pending ? EduTheme.InkFaint : EduTheme.Teal;
        }

        // =========================================================
        //  SENDING
        // =========================================================
        private void SubmitField()
        {
            if (_input == null) return;
            string q = _input.text.Trim();
            if (string.IsNullOrEmpty(q)) return;
            _input.text = "";
            Submit(q);
        }

        private void Submit(string question)
        {
            if (_pending || string.IsNullOrWhiteSpace(question)) return;
            question = question.Trim();
            var snapshot = new List<MentorMessage>(_history);          // earlier turns only; the backend adds the new question
            AddDisplay(true, question);
            _history.Add(new MentorMessage { FromStudent = true, Text = question });
            StartCoroutine(AskRoutine(question, snapshot));
        }

        private IEnumerator AskRoutine(string question, List<MentorMessage> snapshot)
        {
            _pending = true;
            _typingRow = AddRow(false, "Thinking...", out _typingText);
            StartCoroutine(ScrollToEnd());

            MentorReply reply = null;
            string step = _stepProvider != null ? _stepProvider() : null;
            yield return MentorBackend.Ask(_experimentId, step, snapshot, question, r => reply = r);

            if (_typingRow != null) Destroy(_typingRow.gameObject);
            _typingRow = null; _typingText = null;
            if (reply == null) reply = new MentorReply { Text = "Sorry, I could not answer that. Please try again.", Note = "" };

            _history.Add(new MentorMessage { FromStudent = false, Text = reply.Text });
            AddDisplay(false, reply.Text);
            if (_status != null) _status.text = reply.Note ?? "";
            _pending = false;
            StartCoroutine(ScrollToEnd());
        }

        private void RefreshStatus()
        {
            if (_status != null) _status.text = MentorConfig.Online ? "AI mentor online" : "Offline mode: built-in answers";
        }

        // =========================================================
        //  MESSAGES
        // =========================================================
        private void AddDisplay(bool student, string text)
        {
            _display.Add(new MentorMessage { FromStudent = student, Text = text });
            TMP_Text dummy;
            if (_content != null && ViewRect.rect.width > 50f) AddRow(student, text, out dummy);
        }

        private void RebuildMessages()
        {
            _builtWidth = ViewRect.rect.width;
            for (int i = _content.childCount - 1; i >= 0; i--) Destroy(_content.GetChild(i).gameObject);
            _typingRow = null; _typingText = null;
            TMP_Text dummy;
            foreach (var m in _display) AddRow(m.FromStudent, m.Text, out dummy);
            if (_pending) _typingRow = AddRow(false, "Thinking...", out _typingText);
            StartCoroutine(ScrollToEnd());
        }

        /// <summary>One chat bubble: sized to its text, student on the right (teal), mentor on the left.</summary>
        private RectTransform AddRow(bool student, string text, out TMP_Text label)
        {
            float viewW = ViewRect.rect.width;
            float maxBubble = viewW * 0.84f;
            const float pad = 24f;

            var row = UiKit.Node(_content, student ? "Student" : "Mentor");
            var bubble = UiKit.Box(row, "Bubble", student ? EduTheme.Teal : EduTheme.PaperTint, 28);
            label = UiKit.Text(bubble.transform, "Text", text, 34, student ? Color.white : EduTheme.Ink, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            Vector2 pref = label.GetPreferredValues(label.text, maxBubble - 2f * pad, 0f);
            float bw = Mathf.Min(maxBubble, pref.x + 2f * pad + 6f);
            float bh = pref.y + 2f * pad;
            UiKit.Pin(bubble.rectTransform, student ? 1f : 0f, 1f, 0f, 0f, bw, bh);
            UiKit.Fill(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(pad, pad); label.rectTransform.offsetMax = new Vector2(-pad, -pad);

            var le = row.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = bh; le.minHeight = bh;
            return row;
        }

        private IEnumerator ScrollToEnd()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (_scroll != null) _scroll.verticalNormalizedPosition = 0f;
        }
    }
}
