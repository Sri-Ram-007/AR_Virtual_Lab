// ============================================================
// UiKit.cs  —  AR Virtual Lab
// Small helpers for building the light, textbook-style screens (Home, Textbook Scan) in code:
// flat rounded cards, plain buttons, readable text. Sizes are for a 1080 px wide reference canvas.
// ============================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ARVirtualLab.AppShell;

namespace ARVirtualLab.UI
{
    public static class UiKit
    {
        public static RectTransform Node(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go.GetComponent<RectTransform>();
        }

        public static void Fill(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        public static void Anchor(RectTransform rt, float x0, float y0, float x1, float y1)
        {
            rt.anchorMin = new Vector2(x0, y0); rt.anchorMax = new Vector2(x1, y1);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }

        /// <summary>Fixed-size box pinned to a point of its parent: (ax, ay) is both anchor and pivot, (x, y) the offset from it.</summary>
        public static RectTransform Pin(RectTransform rt, float ax, float ay, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(ax, ay);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Flat filled rectangle; radius &gt; 0 gives softly rounded corners.</summary>
        public static Image Box(Transform parent, string name, Color color, int radius = 0)
        {
            var rt = Node(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            if (radius > 0)
            {
                img.sprite = UIStyleHelper.GetRoundedSprite(radius);
                img.type = Image.Type.Sliced;
            }
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        public static TextMeshProUGUI Text(Transform parent, string name, string text, float size, Color color,
            FontStyles style = FontStyles.Normal, TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
        {
            var rt = Node(parent, name);
            var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
            var font = UIStyleHelper.GetAppFont();
            if (font != null) t.font = font;
            t.text = MobileText.Clean(text);
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = align;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            return t;
        }

        public static Button Button(Transform parent, string name, string label, Color bg, Color fg, float size,
            int radius = 18, FontStyles style = FontStyles.Bold)
        {
            var img = Box(parent, name, bg, radius);
            img.raycastTarget = true;
            var btn = img.gameObject.AddComponent<Button>();
            btn.targetGraphic = img;
            var cb = btn.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.96f, 0.96f, 0.96f, 1f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor = Color.white;
            cb.disabledColor = new Color(1f, 1f, 1f, 0.5f);
            btn.colors = cb;

            var t = Text(img.transform, "Label", label, size, fg, style, TextAlignmentOptions.Center);
            Fill(t.rectTransform);
            t.rectTransform.offsetMin = new Vector2(10f, 4f);
            t.rectTransform.offsetMax = new Vector2(-10f, -4f);
            return btn;
        }

        /// <summary>A white card with a hairline border, the basic building block of every screen.</summary>
        public static Image Card(Transform parent, string name, int radius = 26)
        {
            var border = Box(parent, name, EduTheme.Border, radius);
            var fill = Box(border.transform, "Fill", EduTheme.Paper, Mathf.Max(0, radius - 2));
            Fill(fill.rectTransform);
            fill.rectTransform.offsetMin = new Vector2(2f, 2f);
            fill.rectTransform.offsetMax = new Vector2(-2f, -2f);
            return fill;      // children go on the white fill; its parent is the border
        }

        private static Sprite _gear;

        /// <summary>White gear icon (tint it through Image.color).</summary>
        public static Sprite GearSprite()
        {
            if (_gear != null) return _gear;
            const int N = 128;
            var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            float px = 2f / N;                                                  // one pixel, for anti-aliasing
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float tooth = Mathf.Clamp01((Mathf.Cos(Mathf.Atan2(dy, dx) * 8f) - 0.35f) * 3f);   // 8 flat-topped teeth
                float body = Mathf.Clamp01((0.70f + 0.20f * tooth - r) / px + 0.5f);
                float hole = Mathf.Clamp01((r - 0.30f) / px + 0.5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, body * hole));
            }
            tex.Apply();
            _gear = Sprite.Create(tex, new Rect(0f, 0f, N, N), new Vector2(0.5f, 0.5f), 100f);
            return _gear;
        }

        public static LayoutElement Size(Component c, float width = -1f, float height = -1f)
        {
            var le = c.GetComponent<LayoutElement>();
            if (le == null) le = c.gameObject.AddComponent<LayoutElement>();
            if (width >= 0f) le.preferredWidth = width;
            if (height >= 0f) le.preferredHeight = height;
            return le;
        }
    }
}
