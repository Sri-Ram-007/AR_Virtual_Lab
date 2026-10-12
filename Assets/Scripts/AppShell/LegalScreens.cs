// ============================================================
// LegalScreens.cs  —  EduAR
// First-launch welcome (language, privacy in plain words, safety, agreement), the readers for the privacy policy,
// terms and licences (Assets/Resources/Legal/*.txt), and the About panel. Portrait, for the Home canvas.
// ============================================================
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ARVirtualLab.UI;

namespace ARVirtualLab.AppShell
{
    public static class LegalScreens
    {
        // Raise when the privacy policy or terms change in a way students must agree to again
        public const int CurrentVersion = 1;
        private const string AcceptedKey = "legal.acceptedVersion";

        public static bool Accepted { get { return PlayerPrefs.GetInt(AcceptedKey, 0) >= CurrentVersion; } }

        private static void Accept()
        {
            PlayerPrefs.SetInt(AcceptedKey, CurrentVersion);
            PlayerPrefs.Save();
        }

        // ── first launch ──────────────────────────────────────
        /// <param name="onLanguage">Called with true for Telugu; the caller saves it and rebuilds the screen.</param>
        public static GameObject ShowWelcome(Transform parent, Action<bool> onLanguage, Action onAgree)
        {
            var page = FullScreen(parent, "Welcome");
            var content = ScrollColumn(page.transform, 0f, 200f);

            Text(content, "Title", "Welcome to EduAR", 58, EduTheme.Ink, FontStyles.Bold);
            Text(content, "Subtitle", "A virtual science lab for your Class 10 textbook.", 32, EduTheme.InkSoft, FontStyles.Normal);

            Text(content, "LanguageLabel", "Choose your language", 34, EduTheme.Ink, FontStyles.Bold);
            var row = Row(content, "Languages", 100f);
            bool telugu = AppSettings.Language == AppLanguage.Telugu;
            ChoiceButton(row, "English", !telugu, () => onLanguage(false));
            ChoiceButton(row, "Telugu", telugu, () => onLanguage(true));

            Card(content, "Your privacy",
                "•  No account, no ads, no tracking.\n" +
                "•  The camera works only on your phone. Nothing is recorded, saved or sent.\n" +
                "•  Your settings stay on this phone.");
            Card(content, "Stay safe",
                "These experiments are virtual. Never try them at home or without your teacher.");

            var links = Row(content, "Links", 96f);
            LinkButton(links, "Privacy policy", () => OpenDocument(parent, "privacy"));
            LinkButton(links, "Terms of use", () => OpenDocument(parent, "terms"));

            var bar = UiKit.Box(page.transform, "AgreeBar", EduTheme.Paper);
            var brt = bar.rectTransform;
            brt.anchorMin = Vector2.zero; brt.anchorMax = new Vector2(1f, 0f);
            brt.pivot = new Vector2(0.5f, 0f); brt.sizeDelta = new Vector2(0f, 200f); brt.anchoredPosition = Vector2.zero;
            var agree = UiKit.Button(bar.transform, "Agree", "Agree and continue", EduTheme.Teal, Color.white, 42, 24);
            UiKit.Anchor(agree.GetComponent<RectTransform>(), 0.05f, 0.18f, 0.95f, 0.82f);
            agree.onClick.AddListener(() =>
            {
                Accept();
                UnityEngine.Object.Destroy(page);
                if (onAgree != null) onAgree();
            });
            return page;
        }

        // ── About panel (from Settings) ───────────────────────
        public static GameObject ShowAbout(Transform parent)
        {
            var page = FullScreen(parent, "About");
            TopBar(page.transform, "About & privacy", () => UnityEngine.Object.Destroy(page));
            var content = ScrollColumn(page.transform, 170f, 0f);

            Text(content, "AppName", "EduAR", 58, EduTheme.Ink, FontStyles.Bold);
            Text(content, "Version", "Version " + Application.version, 30, EduTheme.InkSoft, FontStyles.Normal);
            Card(content, "Your privacy",
                "•  No account, no ads, no tracking.\n" +
                "•  The camera works only on your phone. Nothing is recorded, saved or sent.\n" +
                "•  Your settings stay on this phone.");
            WideButton(content, "Privacy policy", () => OpenDocument(parent, "privacy"));
            WideButton(content, "Terms of use", () => OpenDocument(parent, "terms"));
            WideButton(content, "Open-source licences", () => OpenDocument(parent, "licenses"));

            BackButton.Register(page, () => { UnityEngine.Object.Destroy(page); return true; });
            return page;
        }

        // ── document reader ───────────────────────────────────
        /// <summary>Shows Assets/Resources/Legal/{name}.txt: "# " title, "## " headings, other lines paragraphs.</summary>
        public static GameObject OpenDocument(Transform parent, string name)
        {
            var asset = Resources.Load<TextAsset>("Legal/" + name);
            string[] lines = asset != null ? asset.text.Replace("\r", "").Split('\n') : new[] { "# " + name };
            string title = lines.Length > 0 && lines[0].StartsWith("# ") ? lines[0].Substring(2) : name;

            var page = FullScreen(parent, "Document_" + name);
            TopBar(page.transform, title, () => UnityEngine.Object.Destroy(page));
            var content = ScrollColumn(page.transform, 170f, 0f);
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("## ")) Text(content, "Heading", line.Substring(3), 38, EduTheme.Ink, FontStyles.Bold);
                else if (i == 1) Text(content, "Meta", line, 26, EduTheme.InkFaint, FontStyles.Italic);
                else Text(content, "Paragraph", line, 30, EduTheme.InkSoft, FontStyles.Normal);
            }

            BackButton.Register(page, () => { UnityEngine.Object.Destroy(page); return true; });
            return page;
        }

        // ── building blocks ───────────────────────────────────
        private static GameObject FullScreen(Transform parent, string name)
        {
            var bg = UiKit.Box(parent, name, EduTheme.Page);
            UiKit.Fill(bg.rectTransform);
            bg.raycastTarget = true;                          // blocks the screen underneath
            return bg.gameObject;
        }

        private static void TopBar(Transform page, string title, Action onBack)
        {
            var bar = UiKit.Box(page, "TopBar", EduTheme.Paper);
            var rt = bar.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 1f); rt.sizeDelta = new Vector2(0f, 170f); rt.anchoredPosition = Vector2.zero;
            var line = UiKit.Box(bar.transform, "Line", EduTheme.Teal);
            line.rectTransform.anchorMin = Vector2.zero; line.rectTransform.anchorMax = new Vector2(1f, 0f);
            line.rectTransform.pivot = new Vector2(0.5f, 0f); line.rectTransform.sizeDelta = new Vector2(0f, 6f);

            var back = UiKit.Button(bar.transform, "Back", "Back", EduTheme.Neutral, EduTheme.Ink, 32, 18);
            UiKit.Anchor(back.GetComponent<RectTransform>(), 0.035f, 0.22f, 0.24f, 0.78f);
            back.onClick.AddListener(() => onBack());
            var t = UiKit.Text(bar.transform, "Title", title, 40, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.MidlineLeft);
            UiKit.Anchor(t.rectTransform, 0.28f, 0.10f, 0.97f, 0.90f);
        }

        /// <summary>A vertical, scrolling list filling the page between the given top and bottom margins.</summary>
        private static RectTransform ScrollColumn(Transform page, float top, float bottom)
        {
            var scrollRT = UiKit.Node(page, "Scroll");
            scrollRT.anchorMin = Vector2.zero; scrollRT.anchorMax = Vector2.one;
            scrollRT.offsetMin = new Vector2(0f, bottom); scrollRT.offsetMax = new Vector2(0f, -top);
            var scroll = scrollRT.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.scrollSensitivity = 40f;

            var viewport = UiKit.Node(scrollRT, "Viewport");
            UiKit.Fill(viewport);
            viewport.gameObject.AddComponent<RectMask2D>();
            var touch = viewport.gameObject.AddComponent<Image>();
            touch.color = new Color(1f, 1f, 1f, 0f);          // lets a drag anywhere scroll the list
            scroll.viewport = viewport;

            var content = UiKit.Node(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f); content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f); content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(56, 56, 48, 56);
            vlg.spacing = 28f;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            return content;
        }

        private static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color, FontStyles style)
        {
            var t = UiKit.Text(parent, name, text, size, color, style, TextAlignmentOptions.TopLeft);
            t.enableAutoSizing = false;
            return t;
        }

        private static void Card(Transform parent, string heading, string body)
        {
            var fill = UiKit.Card(parent, "Card_" + heading);
            var vlg = fill.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(40, 40, 32, 36);
            vlg.spacing = 14f;
            vlg.childControlWidth = vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
            // the card's border (fill's parent) takes its height from the fill's content
            var border = fill.transform.parent.gameObject;
            var fitter = border.AddComponent<VerticalLayoutGroup>();
            fitter.padding = new RectOffset(2, 2, 2, 2);
            fitter.childControlWidth = fitter.childControlHeight = true;
            fitter.childForceExpandWidth = true; fitter.childForceExpandHeight = false;
            Text(fill.transform, "Heading", heading, 36, EduTheme.Ink, FontStyles.Bold);
            Text(fill.transform, "Body", body, 30, EduTheme.InkSoft, FontStyles.Normal);
        }

        private static RectTransform Row(Transform parent, string name, float height)
        {
            var row = UiKit.Node(parent, name);
            UiKit.Size(row, -1f, height);
            var hlg = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = true;
            return row;
        }

        private static void ChoiceButton(Transform row, string label, bool selected, Action onClick)
        {
            var b = UiKit.Button(row, label, label, selected ? EduTheme.Teal : EduTheme.Neutral,
                selected ? Color.white : EduTheme.Ink, 34, 18);
            b.onClick.AddListener(() => { if (!selected) onClick(); });
        }

        private static void LinkButton(Transform row, string label, Action onClick)
        {
            var b = UiKit.Button(row, label, label, EduTheme.Neutral, EduTheme.Ink, 30, 18);
            b.onClick.AddListener(() => onClick());
        }

        private static void WideButton(Transform parent, string label, Action onClick)
        {
            var b = UiKit.Button(parent, label, label, EduTheme.Neutral, EduTheme.Ink, 34, 18);
            UiKit.Size(b, -1f, 110f);
            b.onClick.AddListener(() => onClick());
        }
    }
}
