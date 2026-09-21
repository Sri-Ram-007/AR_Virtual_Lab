// ============================================================
// HintRing.cs  —  AR Virtual Lab
// A soft pulsing ring that faces the camera and circles the object the student should touch next.
// On a phone the apparatus is small, so this makes "what do I tap now?" obvious in every experiment.
// ============================================================
using UnityEngine;

namespace ARVirtualLab.UI
{
    public class HintRing : MonoBehaviour
    {
        private Transform _target;
        private float _fixedRadius = -1f;
        private Vector3 _offset;
        private bool _visible = true;
        private Renderer _renderer;
        private Material _mat;

        private static Texture2D _ringTex;

        public static HintRing Create(Transform parent)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "HintRing";
            go.transform.SetParent(parent, true);
            Object.Destroy(go.GetComponent<Collider>());
            var ring = go.AddComponent<HintRing>();
            ring.Init(go.GetComponent<Renderer>());
            return ring;
        }

        /// <summary>Circle this object (null hides the ring). fixedRadius/offset override the auto-sized bounds.</summary>
        public void Target(GameObject t, float fixedRadius = -1f, Vector3 worldOffset = default(Vector3))
        {
            _target = t != null ? t.transform : null;
            _fixedRadius = fixedRadius;
            _offset = worldOffset;
        }

        public void SetVisible(bool v) { _visible = v; }

        private void Init(Renderer r)
        {
            _renderer = r;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shader s = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            _mat = new Material(s);
            if (_mat.HasProperty("_BaseMap")) _mat.SetTexture("_BaseMap", RingTex());
            _mat.mainTexture = RingTex();
            if (_mat.HasProperty("_Surface"))
            {
                _mat.SetFloat("_Surface", 1f);
                _mat.SetFloat("_Blend", 0f);
                _mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                _mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                _mat.SetInt("_ZWrite", 0);
                _mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            }
            _mat.renderQueue = 3500;
            _renderer.sharedMaterial = _mat;
            _renderer.enabled = false;
        }

        private static float SS(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static Texture2D RingTex()
        {
            if (_ringTex != null) return _ringTex;
            const int N = 128;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = SS(0.72f, 0.84f, r) * (1f - SS(0.90f, 0.97f, r));      // bright band
                float glow = SS(0.30f, 0.88f, r) * (1f - SS(0.88f, 1.0f, r)) * 0.25f; // soft inner glow
                float a = Mathf.Clamp01(ring + glow);
                t.SetPixel(x, y, new Color(0.45f, 0.92f, 1f, a));
            }
            t.Apply();
            return _ringTex = t;
        }

        private void LateUpdate()
        {
            bool show = _visible && _target != null && _target.gameObject.activeInHierarchy && !ARVirtualLab.AR.ARExperimentMode.BlockSceneInput;
            if (_renderer.enabled != show) _renderer.enabled = show;
            if (!show) return;

            Vector3 center; float radius;
            if (_fixedRadius > 0f)
            {
                center = _target.position + _offset;
                radius = _fixedRadius;
            }
            else
            {
                var rs = _target.GetComponentsInChildren<Renderer>();
                bool first = true; Bounds b = new Bounds(_target.position, Vector3.zero);
                foreach (var r in rs)
                {
                    if (r is ParticleSystemRenderer || r == _renderer || !r.enabled) continue;
                    if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
                }
                center = b.center;
                radius = Mathf.Clamp(0.6f * Mathf.Max(b.size.x, b.size.y, b.size.z) + 0.012f, 0.03f, 0.09f);
            }

            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = (cam.transform.position - center).normalized;
                center += toCam * (radius * 0.5f);            // in front of the object so it is not buried in it
                transform.rotation = cam.transform.rotation;
            }
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 4f);
            transform.position = center;
            transform.localScale = Vector3.one * (radius * 2f * (0.92f + 0.14f * pulse));
            var c = new Color(1f, 1f, 1f, 0.55f + 0.45f * pulse);
            if (_mat.HasProperty("_BaseColor")) _mat.SetColor("_BaseColor", c); else _mat.color = c;
        }
    }
}
