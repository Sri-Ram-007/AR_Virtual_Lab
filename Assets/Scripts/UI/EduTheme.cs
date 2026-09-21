// ============================================================
// EduTheme.cs  —  AR Virtual Lab
// A light, textbook-style look for the whole app: white "paper" cards on a soft page background,
// the teal / blue of the Andhra Pradesh Class 10 textbook, dark ink text, flat fills and thin borders
// (no glow, no neon, no gradients).
//
// The experiment screens build their UI in code with a dark palette. Apply() walks a finished canvas and
// re-colours it, so every panel, button, label and outline follows this theme without touching each call site.
// ============================================================
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARVirtualLab.UI
{
    public static class EduTheme
    {
        // ── Palette ────────────────────────────────────────────
        public static readonly Color Page      = new Color32(0xF1, 0xF6, 0xF7, 0xFF);   // screen background
        public static readonly Color Paper     = new Color32(0xFF, 0xFF, 0xFF, 0xFF);   // cards
        public static readonly Color PaperTint = new Color32(0xEC, 0xF2, 0xF4, 0xFF);   // boxes inside cards
        public static readonly Color Border    = new Color32(0xCF, 0xDA, 0xDF, 0xFF);
        public static readonly Color Ink       = new Color32(0x1F, 0x29, 0x33, 0xFF);   // main text
        public static readonly Color InkSoft   = new Color32(0x4B, 0x5B, 0x68, 0xFF);   // secondary text
        public static readonly Color InkFaint  = new Color32(0x7B, 0x87, 0x94, 0xFF);

        public static readonly Color Teal      = new Color32(0x2B, 0x9A, 0xA0, 0xFF);   // textbook footer bar
        public static readonly Color TealDark  = new Color32(0x1E, 0x7A, 0x80, 0xFF);
        public static readonly Color TealTint  = new Color32(0xE3, 0xF2, 0xF3, 0xFF);
        public static readonly Color Blue      = new Color32(0x1E, 0x7F, 0xC0, 0xFF);   // "Activity" bars
        public static readonly Color BlueDark  = new Color32(0x1A, 0x62, 0x98, 0xFF);
        public static readonly Color BlueTint  = new Color32(0xE4, 0xF0, 0xF9, 0xFF);
        public static readonly Color Orange    = new Color32(0xD9, 0x7A, 0x22, 0xFF);
        public static readonly Color Green     = new Color32(0x3B, 0x94, 0x58, 0xFF);
        public static readonly Color Red       = new Color32(0xC4, 0x47, 0x3A, 0xFF);
        public static readonly Color Neutral   = new Color32(0xE2, 0xEA, 0xEE, 0xFF);   // quiet buttons

        // step-progress pills
        public static readonly Color PillIdle   = new Color32(0xD5, 0xDF, 0xE4, 0xFF);
        public static readonly Color PillActive = Teal;
        public static readonly Color PillDone   = Green;

        public static readonly Color CameraBackground = new Color32(0xE9, 0xF0, 0xF2, 0xFF);

        public const string GreenHex = "#2E8B4E";
        public const string RedHex   = "#C4473A";

        // ── Public entry point ────────────────────────────────
        /// <summary>Re-colours every Image, Outline and TMP label under root. Safe to call once after a UI is built.</summary>
        public static void Apply(Transform root)
        {
            if (root == null) return;

            // 1) images (top-down so we can look at nesting)
            var images = root.GetComponentsInChildren<Image>(true);
            foreach (var img in images) RecolourImage(img, root);

            // 2) outlines become thin neutral borders
            foreach (var o in root.GetComponentsInChildren<Outline>(true))
            {
                bool onButton = o.GetComponent<Button>() != null;
                o.effectColor = onButton ? new Color(0f, 0f, 0f, 0.10f) : Border;
                o.effectDistance = new Vector2(1.5f, -1.5f);
            }

            // 3) text
            foreach (var t in root.GetComponentsInChildren<TMP_Text>(true)) RecolourText(t);
        }

        // ── Images ─────────────────────────────────────────────
        private static void RecolourImage(Image img, Transform canvasRoot)
        {
            Color c = img.color;
            if (c.a < 0.01f) return;
            if (img.name.StartsWith("Pill_")) return;      // progress pills are coloured by their owner
            float h, s, v; Color.RGBToHSV(c, out h, out s, out v);

            // black scrims behind dialogs stay scrims
            if (c.r < 0.02f && c.g < 0.02f && c.b < 0.02f)
            {
                img.color = new Color(0.10f, 0.15f, 0.18f, Mathf.Min(c.a, 0.55f));
                return;
            }

            bool isButton = img.GetComponent<Button>() != null;
            if (isButton)
            {
                img.color = (s >= 0.5f && v >= 0.44f) ? Accent(h) : Neutral;   // dark, quiet buttons stay neutral
                return;
            }

            // saturated chips, progress fills, badges
            if (s > 0.5f && v > 0.30f)
            {
                img.color = Accent(h);
                return;
            }

            // dark panels → paper. Screen-sized panel = page background, then white cards, then tinted boxes.
            int depth = 0;
            for (Transform p = img.transform.parent; p != null && p != canvasRoot.parent; p = p.parent)
                if (p.GetComponent<Image>() != null) depth++;

            if (depth == 0 && IsFullScreen(img.rectTransform)) img.color = Page;
            else img.color = (depth % 2 == 0) ? Paper : PaperTint;
            if (depth == 0 && !IsFullScreen(img.rectTransform)) img.color = Paper;
        }

        private static bool IsFullScreen(RectTransform rt)
        {
            return rt.anchorMin.x <= 0.001f && rt.anchorMin.y <= 0.001f && rt.anchorMax.x >= 0.999f && rt.anchorMax.y >= 0.999f;
        }

        /// <summary>Solid textbook colour for a saturated hue.</summary>
        public static Color Accent(float hue)
        {
            if (hue < 0.04f || hue > 0.94f) return Red;
            if (hue < 0.16f) return Orange;
            if (hue < 0.46f) return Green;
            if (hue < 0.56f) return Teal;
            if (hue < 0.68f) return Blue;
            return TealDark;                       // violet → teal, keeps the palette to teal / blue
        }

        // ── Text ───────────────────────────────────────────────
        private static void RecolourText(TMP_Text t)
        {
            Color c = t.color;
            float h, s, v; Color.RGBToHSV(c, out h, out s, out v);
            float bgLum = Luminance(EffectiveBackground(t.transform));

            if (bgLum < 0.62f)                        // sits on a saturated button / chip
            {
                t.color = Color.white;
                return;
            }

            if (s > 0.35f && v > 0.5f)                // a coloured heading
            {
                if (h < 0.04f || h > 0.94f) t.color = Red;
                else if (h < 0.16f) t.color = new Color32(0xB8, 0x5F, 0x12, 0xFF);
                else if (h < 0.46f) t.color = new Color32(0x2E, 0x8B, 0x4E, 0xFF);
                else if (h < 0.56f) t.color = TealDark;
                else if (h < 0.68f) t.color = BlueDark;
                else t.color = TealDark;
                return;
            }

            if (s <= 0.35f && v > 0.5f)
            {
                bool whiteish = s < 0.09f && v > 0.93f;
                t.color = whiteish ? Ink : InkSoft;
            }
            else if (v <= 0.5f && c.a > 0.5f && Luminance(c) < 0.3f)
            {
                // already dark (e.g. set explicitly) – leave alone
            }
        }

        private static Color EffectiveBackground(Transform t)
        {
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                var img = p.GetComponent<Image>();
                if (img != null && img.color.a > 0.4f && img.enabled) return img.color;
            }
            return Page;
        }

        private static float Luminance(Color c)
        {
            return 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;
        }
    }
}
