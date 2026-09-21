// ============================================================
// InteractiveObject.cs  —  Standalone Virtual Lab
// Attach this to all selectable/draggable 3D objects in the lab.
// ============================================================
using UnityEngine;

namespace ARVirtualLab.Standalone
{
    public enum ObjectType
    {
        None,
        MagnesiumRibbon,
        Sandpaper,
        Tongs,
        BunsenBurner,
        WatchGlass,
        MagnesiumOxide
    }

    [RequireComponent(typeof(Collider))]
    public class InteractiveObject : MonoBehaviour
    {
        public ObjectType objectType = ObjectType.None;
        public bool isInteractable = true;
        public Color hoverColor = new Color(0.2f, 1f, 0.2f); // Green glow

        private Renderer[] _renderers;
        private Color[] _originalEmissions;
        private bool _isHighlighted = false;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _originalEmissions = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].material.HasProperty("_EmissionColor"))
                    _originalEmissions[i] = _renderers[i].material.GetColor("_EmissionColor");
            }
        }

        public void Highlight(bool enable)
        {
            if (!isInteractable) return;
            _isHighlighted = enable;

            for (int i = 0; i < _renderers.Length; i++)
            {
                var r = _renderers[i];
                if (r == null || r.material == null) continue;

                if (enable)
                {
                    if (r.material.HasProperty("_EmissionColor"))
                    {
                        r.material.EnableKeyword("_EMISSION");
                        r.material.SetColor("_EmissionColor", hoverColor * 0.4f);
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
    }
}
