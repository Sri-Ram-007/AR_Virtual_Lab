// ============================================================
// MobileText.cs  —  AR Virtual Lab
// Makes runtime-built TextMeshPro UI readable on phones:
//   * Clean():  the bundled LiberationSans font has no subscript digits or emoji, so H₂SO₄ and ✅ show as empty
//               boxes. Subscripts become <sub> rich text; emoji/dingbats are dropped.
//   * Setup():  text authored for a 1080-wide canvas at 14–22 px is only ~6–8 dp on a phone. Auto-sizing lets each
//               label grow (up to 2x) to fill its box, and never shrinks below the authored size.
// ============================================================
using System.Text;
using TMPro;
using UnityEngine;

namespace ARVirtualLab.UI
{
    public static class MobileText
    {
        private static readonly System.Collections.Generic.Dictionary<char, bool> _glyphCache = new System.Collections.Generic.Dictionary<char, bool>();

        private static bool FontHasGlyph(char c)
        {
            bool ok;
            if (_glyphCache.TryGetValue(c, out ok)) return ok;
            var font = TMP_Settings.defaultFontAsset;
            ok = font == null || font.HasCharacter(c, true, false);
            _glyphCache[c] = ok;
            return ok;
        }

        public static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length + 8);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= '₀' && c <= '₉')                      // subscript digits
                {
                    sb.Append("<sub>").Append((char)('0' + (c - '₀'))).Append("</sub>");
                    continue;
                }
                if (c == '→' && !FontHasGlyph(c)) { sb.Append("->"); continue; }   // arrow: fall back to ASCII
                if (char.IsHighSurrogate(c)) { i++; continue; }          // emoji outside the BMP
                switch (c)
                {
                    case '✅': case '✔': case '✕': case '❌':   // check / cross marks
                    case '☢': case '⚪': case '⚓': case '️':   // misc symbols, variation selector
                        continue;
                }
                if (c > 0x7F && !FontHasGlyph(c)) continue;               // anything else the font cannot draw would show as a box
                sb.Append(c);
            }
            return sb.ToString().TrimStart();
        }

        /// <summary>Bumps very small authored sizes (14–22 px on a 1080 canvas) up to phone-readable ones.</summary>
        public static float PhoneSize(float s)
        {
            if (s < 20f) return s * 1.5f;
            if (s < 30f) return s * 1.35f;
            return s * 1.12f;
        }

        /// <summary>Applies Clean() and phone-friendly auto-sizing to a freshly created label.</summary>
        public static void Setup(TMP_Text t, string name, float baseSize)
        {
            if (t == null) return;
            t.text = Clean(t.text);
            t.enableAutoSizing = true;
            t.fontSizeMin = baseSize;
            t.fontSizeMax = Mathf.Min(baseSize * 2f, 46f);
            if ((name == "InfoBody" || name == "StepDesc" || name == "Body") && t.alignment == TextAlignmentOptions.Left)
                t.alignment = TextAlignmentOptions.TopLeft;   // multi-line copy reads from the top of its box
        }
    }
}
