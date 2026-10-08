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
        // Every colour has a light and a dark value; screens read them when built, so a theme change takes
        // effect when the screen is rebuilt (the settings panel reloads the scene).
        public static bool Dark { get { return ARVirtualLab.AppShell.AppSettings.DarkMode; } }

        private static Color Pick(uint light, uint dark)
        {
            uint v = Dark ? dark : light;
            return new Color32((byte)(v >> 16), (byte)(v >> 8), (byte)v, 0xFF);
        }

        public static Color Page      { get { return Pick(0xF1F6F7, 0x11181B); } }   // screen background
        public static Color Paper     { get { return Pick(0xFFFFFF, 0x1B2429); } }   // cards
        public static Color PaperTint { get { return Pick(0xECF2F4, 0x243038); } }   // boxes inside cards
        public static Color Border    { get { return Pick(0xCFDADF, 0x33434C); } }
        public static Color Ink       { get { return Pick(0x1F2933, 0xE6EDF0); } }   // main text
        public static Color InkSoft   { get { return Pick(0x4B5B68, 0xAEBBC4); } }   // secondary text
        public static Color InkFaint  { get { return Pick(0x7B8794, 0x84929C); } }

        public static Color Teal      { get { return Pick(0x2B9AA0, 0x2B9AA0); } }   // textbook footer bar
        public static Color TealDark  { get { return Pick(0x1E7A80, 0x5CC3C8); } }   // also used for headings, so lighter in dark
        public static Color TealTint  { get { return Pick(0xE3F2F3, 0x173A3D); } }
        public static Color Blue      { get { return Pick(0x1E7FC0, 0x2B88C8); } }   // "Activity" bars
        public static Color BlueDark  { get { return Pick(0x1A6298, 0x8CC2EC); } }
        public static Color BlueTint  { get { return Pick(0xE4F0F9, 0x1B3346); } }
        public static Color Orange    { get { return Pick(0xD97A22, 0xD97A22); } }
        public static Color Green     { get { return Pick(0x3B9458, 0x3B9458); } }
        public static Color Red       { get { return Pick(0xC4473A, 0xC4473A); } }
        public static Color Neutral   { get { return Pick(0xE2EAEE, 0x2D3A42); } }   // quiet buttons

        // step-progress pills
        public static Color PillIdle   { get { return Pick(0xD5DFE4, 0x34434B); } }
        public static Color PillActive { get { return Teal; } }
        public static Color PillDone   { get { return Green; } }

        public static Color CameraBackground { get { return Pick(0xE9F0F2, 0x151D21); } }

        // coloured headings on paper
        private static Color HeadingOrange { get { return Pick(0xB85F12, 0xF0A35E); } }
        private static Color HeadingGreen  { get { return Pick(0x2E8B4E, 0x6CC88C); } }

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
            Color bg = EffectiveBackground(t.transform);
            float bh, bs, bv; Color.RGBToHSV(bg, out bh, out bs, out bv);

            // sits on a saturated button / chip. (In dark mode the cards themselves are dark, so luminance
            // alone would turn every label white; saturation tells a coloured chip from a dark card.)
            bool onChip = Dark ? (bs > 0.35f && bv > 0.3f) : Luminance(bg) < 0.62f;
            if (onChip)
            {
                t.color = Color.white;
                return;
            }

            if (s > 0.35f && v > 0.5f)                // a coloured heading
            {
                if (h < 0.04f || h > 0.94f) t.color = Red;
                else if (h < 0.16f) t.color = HeadingOrange;
                else if (h < 0.46f) t.color = HeadingGreen;
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
                // already dark (e.g. set explicitly): fine on paper, unreadable on dark paper
                if (Dark) t.color = Ink;
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
