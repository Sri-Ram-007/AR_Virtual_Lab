// ============================================================
// UIStyleHelper.cs
// Design system and procedural UI element generator for AR Virtual Lab.
// Generates anti-aliased 9-slice rounded rectangles and maintains
// the modern educational app color palette.
// ============================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARVirtualLab.AppShell
{
    public static class UIStyleHelper
    {
        // ── Color Palette: Modern Educational Science Theme ────────
        public static readonly Color BgDark          = new Color32(0xF1, 0xF6, 0xF7, 0xff); // page background (name kept for older callers)
        public static readonly Color SurfaceCard     = new Color32(0x13, 0x1c, 0x2e, 0xf2); // #131c2e
        public static readonly Color SurfaceElevated = new Color32(0x1a, 0x26, 0x3d, 0xf8); // #1a263d
        public static readonly Color SurfaceLight    = new Color32(0x24, 0x33, 0x4f, 0xff); // #24334f
        public static readonly Color CardBorder      = new Color32(0x2a, 0x3b, 0x58, 0x99); // subtle stroke

        // Accents
        public static readonly Color PrimaryCyan     = new Color32(0x0e, 0xa5, 0xe9, 0xff); // #0ea5e9 Vibrant Sky Cyan
        public static readonly Color CyanGlow        = new Color32(0x38, 0xbd, 0xf8, 0xff); // #38bdf8
        public static readonly Color PurpleAR        = new Color32(0x8b, 0x5c, 0xf6, 0xff); // #8b5cf6 AR Accent
        public static readonly Color PurpleGlow      = new Color32(0xa8, 0x55, 0xf7, 0xff); // #a855f7
        public static readonly Color EmeraldSuccess  = new Color32(0x10, 0xb9, 0x81, 0xff); // #10b981
        public static readonly Color AmberWarning    = new Color32(0xf5, 0x9e, 0x0b, 0xff); // #f59e0b
        public static readonly Color RoseDanger      = new Color32(0xf4, 0x3f, 0x5e, 0xff); // #f43f5e

        // Typography Colors
        public static readonly Color TextWhite       = new Color32(0xf8, 0xfa, 0xfc, 0xff); // #f8fafc
        public static readonly Color TextMuted       = new Color32(0x94, 0xa3, 0xb8, 0xff); // #94a3b8
        public static readonly Color TextDim         = new Color32(0x64, 0x74, 0x8b, 0xff); // #64748b

        // ── Procedural Sprite Caching ──────────────────────────────
        private static readonly Dictionary<int, Sprite> _roundedSprites = new Dictionary<int, Sprite>();
        private static Sprite _circleSprite;
        private static TMP_FontAsset _fontAsset;

        public static TMP_FontAsset GetAppFont()
        {
            if (_fontAsset != null) return _fontAsset;
            _fontAsset = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (_fontAsset == null)
            {
                var all = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                if (all.Length > 0) _fontAsset = all[0];
            }
            return _fontAsset;
        }

        /// <summary>
        /// Generates or retrieves a procedural 9-sliced rounded rectangle sprite with antialiasing.
        /// </summary>
        public static Sprite GetRoundedSprite(int cornerRadius = 24, int padding = 2)
        {
            int key = cornerRadius * 100 + padding;
            if (_roundedSprites.TryGetValue(key, out var sprite) && sprite != null)
                return sprite;

            int size = cornerRadius * 2 + padding * 2 + 8; // ensure a central stretch region
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] colors = new Color[size * size];
            float r = cornerRadius;
            float rSqr = r * r;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Center of corner circles
                    float cx = (x < size / 2) ? (padding + r) : (size - padding - r - 1);
                    float cy = (y < size / 2) ? (padding + r) : (size - padding - r - 1);

                    float dx = x - cx;
                    float dy = y - cy;

                    float alpha = 1f;
                    // Check if inside one of the 4 corner quadrants
                    bool inCorner = ((x < padding + r || x > size - padding - r - 1) &&
                                     (y < padding + r || y > size - padding - r - 1));

                    if (inCorner)
                    {
                        float dist = Mathf.Sqrt(dx * dx + dy * dy);
                        alpha = Mathf.Clamp01(r - dist + 0.5f);
                    }
                    else if (x < padding || x > size - padding - 1 || y < padding || y > size - padding - 1)
                    {
                        alpha = 0f;
                    }

                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            // 9-slice border: left, bottom, right, top
            Vector4 border = new Vector4(cornerRadius + padding, cornerRadius + padding, cornerRadius + padding, cornerRadius + padding);
            sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
            _roundedSprites[key] = sprite;
            return sprite;
        }

        /// <summary>
        /// Generates a smooth circular sprite for icons, buttons, and avatars.
        /// </summary>
        public static Sprite GetCircleSprite(int size = 64)
        {
            if (_circleSprite != null) return _circleSprite;

            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            float radius = (size - 4) * 0.5f;
            Vector2 center = new Vector2(size * 0.5f, size * 0.5f);

            Color[] colors = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    float alpha = Mathf.Clamp01(radius - dist + 0.7f);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            tex.SetPixels(colors);
            tex.Apply();

            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleSprite;
        }

        // ── UI Element Construction Helpers ────────────────────────
        public static TextMeshProUGUI CreateText(
            Transform parent,
            string name,
            string content,
            float fontSize,
            Color color,
            FontStyles style = FontStyles.Normal,
            TextAlignmentOptions alignment = TextAlignmentOptions.Left)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.font = GetAppFont();
            tmp.text = ARVirtualLab.UI.MobileText.Clean(content);   // font has no emoji / subscript glyphs
            tmp.fontSize = ARVirtualLab.UI.MobileText.PhoneSize(fontSize);   // authored sizes were too small for phones
            tmp.color = color;
            tmp.fontStyle = style;
            tmp.alignment = alignment;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;

            return tmp;
        }

        public static Image CreateRoundedImage(Transform parent, string name, Color color, int cornerRadius = 24)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var img = go.AddComponent<Image>();
            img.sprite = GetRoundedSprite(cornerRadius);
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = true;

            return img;
        }

        public static Button CreateStyledButton(
            Transform parent,
            string name,
            string label,
            Color bgColor,
            Color textColor,
            float fontSize = 20f,
            int cornerRadius = 20)
        {
            var img = CreateRoundedImage(parent, name, bgColor, cornerRadius);
            var btn = img.gameObject.AddComponent<Button>();

            ColorBlock cb = btn.colors;
            cb.normalColor = bgColor;
            cb.highlightedColor = Color.Lerp(bgColor, Color.white, 0.15f);
            cb.pressedColor = Color.Lerp(bgColor, Color.black, 0.20f);
            cb.selectedColor = bgColor;
            cb.disabledColor = new Color(bgColor.r, bgColor.g, bgColor.b, 0.4f);
            btn.colors = cb;

            var txt = CreateText(img.transform, "Label", label, fontSize, textColor, FontStyles.Bold, TextAlignmentOptions.Center);
            var txtRT = txt.rectTransform;
            txtRT.anchorMin = Vector2.zero;
            txtRT.anchorMax = Vector2.one;
            txtRT.offsetMin = txtRT.offsetMax = Vector2.zero;

            return btn;
        }

        public static GameObject CreatePillBadge(
            Transform parent,
            string label,
            Color bgColor,
            Color textColor,
            float fontSize = 14f)
        {
            var img = CreateRoundedImage(parent, "PillBadge", bgColor, 12);
            var hlg = img.gameObject.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(16, 16, 6, 6);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            var csf = img.gameObject.AddComponent<ContentSizeFitter>();
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var txt = CreateText(img.transform, "PillText", label, fontSize, textColor, FontStyles.Bold, TextAlignmentOptions.Center);
            return img.gameObject;
        }
    }
}
