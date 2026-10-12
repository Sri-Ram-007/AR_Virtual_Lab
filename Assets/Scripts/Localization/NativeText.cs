// ============================================================
// NativeText.cs  —  AR Virtual Lab
// TextMeshPro cannot shape Telugu (conjuncts such as ప్ర or క్ష come out as loose pieces), but Android's own
// text engine can. When a label should show Telugu, this draws it with Android into a texture, shows that in a
// RawImage over the label and hides the TextMeshPro glyphs. The label's English text is left untouched, so
// the experiment code that sets and reads it keeps working.
// Only active on Android; in the editor the English stays visible (missing translations are still recorded).
// ============================================================
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARVirtualLab.Localization
{
    [DisallowMultipleComponent]
    public class NativeText : MonoBehaviour
    {
#pragma warning disable 0067   // only raised on Android
        /// <summary>Raised after a label is drawn natively, with its size in canvas units (layouts can resize to fit).</summary>
        public static event Action<TMP_Text, Vector2> Rendered;
#pragma warning restore 0067

        private TMP_Text _tmp;
#pragma warning disable 0649   // assigned only on Android
        private RawImage _image;
        private Texture2D _texture;
#pragma warning restore 0649

        private string _source;
        private int _locVersion = -1;
        private string _display;
#pragma warning disable 0649   // set only on Android
        private bool _setLayoutWidth;
#pragma warning restore 0649

#if UNITY_ANDROID && !UNITY_EDITOR
        private const float MinInterval = 0.12f;      // labels that change every frame (percentages) redraw at most this often
        private string _drawn;
        private float _drawnSize = -1f;
        private Color _drawnColor;
        private Vector2 _drawnRect;
        private float _nextDraw;
#endif

        public bool ShowingNative { get { return _image != null && _image.enabled; } }
        public Vector2 RenderedSize { get; private set; }

        private void Awake()
        {
            _tmp = GetComponent<TMP_Text>();
        }

        private void LateUpdate()
        {
            if (_tmp == null) return;

            string source = _tmp.text;
            if (!ReferenceEquals(source, _source) || _locVersion != Loc.Version)
            {
                _source = source;
                _locVersion = Loc.Version;
                _display = Loc.Resolve(source);
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            if (_display == null) { ShowTmp(); return; }
            HideTmp();

            var rt = _tmp.rectTransform;
            var canvas = _tmp.canvas;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            float size = _tmp.enableAutoSizing ? _tmp.fontSizeMax : _tmp.fontSize;
            bool changed = _display != _drawn || !Mathf.Approximately(size, _drawnSize) || _tmp.color != _drawnColor
                           || (rt.rect.size - _drawnRect).sqrMagnitude > 1f;
            if (!changed || Time.unscaledTime < _nextDraw) return;
            _nextDraw = Time.unscaledTime + MinInterval;
            Draw(rt, scale, size);
#endif
        }

        private void OnDisable() { ShowTmp(); }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private void ShowTmp()
        {
            if (_image != null && _image.enabled) _image.enabled = false;
            SetTmpAlpha(1f);
            LayoutElement le;
            if (_setLayoutWidth && TryGetComponent(out le)) le.preferredWidth = -1f;
            _setLayoutWidth = false;
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private void HideTmp() { SetTmpAlpha(0f); }
#endif

        private void SetTmpAlpha(float a)
        {
            if (_tmp == null || _tmp.canvasRenderer == null) return;
            if (!Mathf.Approximately(_tmp.canvasRenderer.GetAlpha(), a)) _tmp.canvasRenderer.SetAlpha(a);
            foreach (var sub in _tmp.GetComponentsInChildren<TMP_SubMeshUI>())
                if (sub.canvasRenderer != null) sub.canvasRenderer.SetAlpha(a);
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaClass _renderer;

        private void Draw(RectTransform rt, float scale, float size)
        {
            Vector2 box = rt.rect.size;
            bool wrap = _tmp.textWrappingMode != TextWrappingModes.NoWrap;
            float minSize = _tmp.enableAutoSizing ? _tmp.fontSizeMin : size;
            int hAlign = HAlign(_tmp.horizontalAlignment);

            sbyte[] data;      // Unity's Java bridge passes byte arrays as sbyte[] (byte[] is deprecated)
            try
            {
                if (_renderer == null) _renderer = new AndroidJavaClass("com.arvirtuallab.text.NativeTextRenderer");
                data = _renderer.CallStatic<sbyte[]>("render", ToHtml(_display),
                    size * scale, minSize * scale, ToArgb(_tmp.color),
                    Mathf.Max(1, Mathf.RoundToInt(box.x * scale)),
                    _tmp.enableAutoSizing && wrap ? Mathf.RoundToInt(box.y * scale) : 0,
                    hAlign, (_tmp.fontStyle & FontStyles.Bold) != 0, (_tmp.fontStyle & FontStyles.Italic) != 0, wrap);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NativeText] Android text rendering failed: " + e.Message);
                _display = null;            // fall back to the English label until the text changes
                ShowTmp();
                return;
            }
            if (data == null || data.Length < 8) { ShowTmp(); return; }

            int w = ((byte)data[0] << 24) | ((byte)data[1] << 16) | ((byte)data[2] << 8) | (byte)data[3];
            int h = ((byte)data[4] << 24) | ((byte)data[5] << 16) | ((byte)data[6] << 8) | (byte)data[7];
            if (w <= 0 || h <= 0 || data.Length < 8 + w * h * 4) { ShowTmp(); return; }

            if (_texture == null || _texture.width != w || _texture.height != h)
            {
                if (_texture != null) Destroy(_texture);
                _texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            }
            var pixels = new byte[w * h * 4];
            Buffer.BlockCopy(data, 8, pixels, 0, pixels.Length);
            _texture.LoadRawTextureData(pixels);
            _texture.Apply(false);

            EnsureImage();
            _image.texture = _texture;
            _image.uvRect = new Rect(0f, 1f, 1f, -1f);           // Android rows run top-down
            _image.enabled = true;

            Vector2 drawn = new Vector2(w / scale, h / scale);
            Place(_image.rectTransform, drawn, hAlign, VAlign(_tmp.verticalAlignment));
            RenderedSize = drawn;

            // one-line labels size their chip through a layout group: report the Telugu width, not the English one
            if (!wrap)
            {
                LayoutElement le;
                if (!TryGetComponent(out le)) le = gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = drawn.x;
                _setLayoutWidth = true;
            }

            _drawn = _display;
            _drawnSize = size;
            _drawnColor = _tmp.color;
            _drawnRect = box;
            if (Rendered != null) Rendered(_tmp, drawn);
        }

        private void EnsureImage()
        {
            if (_image != null) return;
            var go = new GameObject("NativeText", typeof(RectTransform), typeof(RawImage));
            go.transform.SetParent(transform, false);
            _image = go.GetComponent<RawImage>();
            _image.raycastTarget = false;
        }

        // Pin the drawn text inside the label's box the way TextMeshPro would align it
        private static void Place(RectTransform img, Vector2 size, int hAlign, int vAlign)
        {
            float ax = hAlign == 0 ? 0f : hAlign == 1 ? 0.5f : 1f;
            float ay = vAlign == 0 ? 1f : vAlign == 1 ? 0.5f : 0f;
            img.anchorMin = img.anchorMax = img.pivot = new Vector2(ax, ay);
            img.anchoredPosition = Vector2.zero;
            img.sizeDelta = size;
        }

        private static int HAlign(HorizontalAlignmentOptions a)
        {
            switch (a)
            {
                case HorizontalAlignmentOptions.Center:
                case HorizontalAlignmentOptions.Geometry: return 1;
                case HorizontalAlignmentOptions.Right: return 2;
                default: return 0;
            }
        }

        private static int VAlign(VerticalAlignmentOptions a)
        {
            switch (a)
            {
                case VerticalAlignmentOptions.Top: return 0;
                case VerticalAlignmentOptions.Bottom:
                case VerticalAlignmentOptions.Baseline: return 2;
                default: return 1;
            }
        }

        private static int ToArgb(Color c)
        {
            Color32 k = c;
            return (k.a << 24) | (k.r << 16) | (k.g << 8) | k.b;
        }

        // TextMeshPro rich text → the HTML subset Android understands (colour, bold, italic, sub/superscript)
        private static string ToHtml(string tmp)
        {
            var sb = new System.Text.StringBuilder(tmp.Length + 16);
            int i = 0;
            while (i < tmp.Length)
            {
                char c = tmp[i];
                if (c == '<')
                {
                    int end = tmp.IndexOf('>', i);
                    if (end > i)
                    {
                        string tag = tmp.Substring(i + 1, end - i - 1).Trim();
                        string lower = tag.ToLowerInvariant();
                        if (lower.StartsWith("color=")) sb.Append("<font color=\"").Append(tag.Substring(6).Trim('"', '\'')).Append("\">");
                        else if (lower == "/color") sb.Append("</font>");
                        else if (lower == "b" || lower == "/b" || lower == "i" || lower == "/i" || lower == "sub" || lower == "/sub"
                                 || lower == "sup" || lower == "/sup" || lower == "u" || lower == "/u")
                            sb.Append('<').Append(lower).Append('>');
                        else if (lower == "br") sb.Append("<br>");
                        // other TextMeshPro tags (size, voffset, space...) have no Android equivalent: dropped
                        i = end + 1;
                        continue;
                    }
                }
                if (c == '\n') sb.Append("<br>");
                else if (c == '&') sb.Append("&amp;");
                else if (c == '<') sb.Append("&lt;");
                else if (c == '>') sb.Append("&gt;");
                else sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
#endif
    }
}
