// ============================================================
// StandaloneInteractable.cs  —  AR Virtual Lab Standalone
// Attach this to all laboratory items (ribbon, tongs, burner, etc.)
// Handles selection highlight and holds object state tags.
// ============================================================
using UnityEngine;

namespace ARVirtualLab.Standalone
{
    [DisallowMultipleComponent]
    public class StandaloneInteractable : MonoBehaviour
    {
        [Header("Object Type")]
        public StandaloneObjectType objectType = StandaloneObjectType.None;
        public bool isInteractable = true;

        [Header("Selection Color")]
        public Color highlightColor = new Color(0.2f, 1f, 0.2f); // Lime green glow

        public bool IsSelected { get; private set; } = false;

        private Renderer[] _renderers;
        private Color[] _originalEmissions;
        private Color[] _originalColors;
        private bool _hasEmissionsCached = false;

        private void Awake()
        {
            CacheRenderers();
        }

        public void CacheRenderers()
        {
            if (_hasEmissionsCached) return;

            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalEmissions = new Color[_renderers.Length];
            _originalColors = new Color[_renderers.Length];

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].material.HasProperty("_EmissionColor"))
                    _originalEmissions[i] = _renderers[i].material.GetColor("_EmissionColor");
                
                if (_renderers[i].material.HasProperty("_Color"))
                    _originalColors[i] = _renderers[i].material.color;
            }
            _hasEmissionsCached = true;
        }

        public void Highlight(bool enable)
        {
            CacheRenderers();
            IsSelected = enable;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || r.material == null) continue;

                if (enable)
                {
                    if (r.material.HasProperty("_EmissionColor"))
                    {
                        r.material.EnableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor", highlightColor * 0.4f);
                    }
                }
                else
                {
                    if (r.material.HasProperty("_EmissionColor"))
                    {
                        r.material.DisableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor", _originalEmissions[i]);
                    }
                }
            }
        }

        public void SetColor(Color c)
        {
            CacheRenderers();
            foreach (var r in _renderers)
            {
                if (r != null && r.material != null && r.material.HasProperty("_Color"))
                {
                    r.material.color = c;
                }
            }
        }

        public void ResetColor()
        {
            CacheRenderers();
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null && _renderers[i].material != null && _renderers[i].material.HasProperty("_Color"))
                {
                    _renderers[i].material.color = _originalColors[i];
                }
            }
        }
    }
}
