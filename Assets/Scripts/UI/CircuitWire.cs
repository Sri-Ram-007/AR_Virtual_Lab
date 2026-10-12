// ============================================================
// CircuitWire.cs  —  AR Virtual Lab
// A connecting wire the STUDENT joins up: one end is fixed to a device terminal, the other end is a coloured clip
// that lies loose on the bench until it is dragged to the matching terminal (marked by a pulsing dot of the same
// colour). Dropping the clip close enough to that terminal (judged on screen) clips it on; otherwise it falls back.
// ============================================================
using System;
using UnityEngine;
using UnityEngine.UI;

namespace ARVirtualLab.UI
{
    public class WireClip : MonoBehaviour { public CircuitWire Wire; }

    public class CircuitWire : MonoBehaviour
    {
        public Func<Vector3> FixedEnd;
        public Func<Vector3> TargetEnd;
        public Vector3 RestPoint;
        public bool ShowTarget;

        public bool Connected { get; private set; }
        public bool Dragging { get; private set; }
        public GameObject Clip { get { return _clip; } }

        private LineRenderer _lr;
        private GameObject _clip;
        private SphereCollider _clipCol;
        private Vector3 _dragPos;
        private float _tableY;
        private Color _color;

        private static Material MakeLit(Color c, float smooth)
        {
            var s = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }

        private static Material MakeGlow(Color c)
        {
            var s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(s);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            if (m.HasProperty("_Surface"))
            {
                m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f);
                m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                m.SetInt("_ZWrite", 0);
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                m.renderQueue = 3200;
            }
            return m;
        }

        public static CircuitWire Create(Transform parent, string name, Color color, Func<Vector3> fixedEnd, Func<Vector3> targetEnd, Vector3 restPoint, float tableY)
        {
            var go = new GameObject("Wire_" + name);
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<CircuitWire>();
            w.FixedEnd = fixedEnd; w.TargetEnd = targetEnd; w.RestPoint = restPoint; w._tableY = tableY; w._color = color;

            w._lr = go.AddComponent<LineRenderer>();
            w._lr.material = MakeLit(color * 0.85f, 0.35f);
            w._lr.widthMultiplier = 0.0055f;
            w._lr.positionCount = 16;
            w._lr.useWorldSpace = true;
            w._lr.numCapVertices = 4;
            w._lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // the clip on the loose end — a generous collider so it is easy to grab with a finger
            w._clip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            w._clip.name = "Clip_" + name;
            w._clip.transform.SetParent(go.transform, false);
            w._clip.transform.localScale = Vector3.one * 0.022f;
            w._clip.GetComponent<Renderer>().sharedMaterial = MakeLit(color, 0.6f);
            w._clip.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            UnityEngine.Object.Destroy(w._clip.GetComponent<Collider>());
            var sc = w._clip.AddComponent<SphereCollider>(); sc.radius = 2.4f; w._clipCol = sc;
            w._clip.AddComponent<WireClip>().Wire = w;

            return w;
        }

        // ---- the target marker: a ring + dot drawn on the UI layer, so it is always on top of the 3D scene and can
        //      never be hidden inside (or behind) the device it marks ----
        public static RectTransform UiRoot;
        public static Func<Camera> UiCamera;
        private RectTransform _ring;
        private Image _ringColour, _ringShadow, _ringDot;
        private static Sprite _ringSprite, _dotSprite;

        private static Sprite MakeSprite(bool ring)
        {
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (ring) a = Mathf.Clamp01((0.96f - r) * 22f) * Mathf.Clamp01((r - 0.66f) * 22f);   // soft-edged ring
                    else a = Mathf.Clamp01((0.30f - r) * 22f);                                          // centre dot
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            t.Apply();
            return Sprite.Create(t, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
        }

        private void EnsureRing()
        {
            if (_ring != null || UiRoot == null) return;
            if (_ringSprite == null) _ringSprite = MakeSprite(true);
            if (_dotSprite == null) _dotSprite = MakeSprite(false);
            var go = new GameObject("TargetRing_" + name, typeof(RectTransform));
            _ring = go.GetComponent<RectTransform>();
            _ring.SetParent(UiRoot, false);
            _ring.sizeDelta = new Vector2(96f, 96f);
            _ring.anchorMin = _ring.anchorMax = new Vector2(0.5f, 0.5f);
            Image Mk(string n, Sprite s, Color c, float scale)
            {
                var g = new GameObject(n, typeof(RectTransform), typeof(Image));
                var rt = g.GetComponent<RectTransform>(); rt.SetParent(_ring, false);
                rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
                rt.localScale = Vector3.one * scale;
                var im = g.GetComponent<Image>(); im.sprite = s; im.color = c; im.raycastTarget = false;
                return im;
            }
            _ringShadow = Mk("Shadow", _ringSprite, new Color(0f, 0f, 0f, 0.65f), 1.22f);    // dark halo so it reads on any background
            _ringColour = Mk("Ring", _ringSprite, new Color(color_r(), color_g(), color_b(), 1f), 1f);
            _ringDot = Mk("Dot", _dotSprite, new Color(1f, 1f, 1f, 0.95f), 1f);
            _ring.gameObject.SetActive(false);
        }
        private float color_r() { return Mathf.Clamp01(_color.r * 1.25f + 0.10f); }
        private float color_g() { return Mathf.Clamp01(_color.g * 1.25f + 0.10f); }
        private float color_b() { return Mathf.Clamp01(_color.b * 1.25f + 0.10f); }

        private void UpdateRing(bool show)
        {
            if (show) EnsureRing();
            if (_ring == null) return;
            var cam = UiCamera != null ? UiCamera() : Camera.main;
            Vector3 sp = (show && cam != null) ? cam.WorldToScreenPoint(TargetEnd()) : Vector3.zero;
            bool visible = show && cam != null && sp.z > 0f;
            if (_ring.gameObject.activeSelf != visible) _ring.gameObject.SetActive(visible);
            if (!visible) return;
            var canvas = UiRoot.GetComponentInParent<Canvas>();
            Camera uiCam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(UiRoot, sp, uiCam, out Vector2 local);
            _ring.anchoredPosition = local;
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f + (Dragging ? 0.25f : 0f);
            _ring.localScale = Vector3.one * pulse;
        }

        private void OnDestroy() { if (_ring != null) UnityEngine.Object.Destroy(_ring.gameObject); }
        private void OnDisable() { if (_ring != null) _ring.gameObject.SetActive(false); }

        private void Update()
        {
            if (FixedEnd == null || TargetEnd == null) return;
            Vector3 a = FixedEnd();
            Vector3 b = Connected ? TargetEnd() : (Dragging ? _dragPos : RestPoint);
            float sag = Connected ? 0.012f : 0.022f;
            int n = _lr.positionCount;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)(n - 1);
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= Mathf.Sin(t * Mathf.PI) * sag;
                if (!Dragging && !Connected) p.y = Mathf.Max(p.y, _tableY + 0.004f);   // a loose wire lies on the bench, not under it
                _lr.SetPosition(i, p);
            }
            _clip.transform.position = b;
            // once clipped on the wire is done: its collider must not block taps on the apparatus behind it
            if (_clipCol.enabled == Connected) _clipCol.enabled = !Connected;

            UpdateRing(ShowTarget && !Connected && !ARVirtualLab.AR.ARExperimentMode.BlockSceneInput);
        }

        public void BeginDrag(Vector3 worldPos) { Dragging = true; _dragPos = worldPos; }
        public void DragTo(Vector3 worldPos) { _dragPos = worldPos; }

        /// <summary>Releases the clip; returns true if it landed close enough (on screen) to its terminal and clipped on.</summary>
        public bool Drop(Camera cam, float limitPixels)
        {
            Dragging = false;
            if (cam == null) return false;
            Vector2 c = cam.WorldToScreenPoint(_dragPos);
            Vector2 t = cam.WorldToScreenPoint(TargetEnd());
            if (Vector2.Distance(c, t) < limitPixels) { Connected = true; return true; }
            return false;
        }

        public void ConnectNow() { Connected = true; Dragging = false; }
        public void ResetWire() { Connected = false; Dragging = false; }
        public void SetVisible(bool v) { gameObject.SetActive(v); }
    }
}
