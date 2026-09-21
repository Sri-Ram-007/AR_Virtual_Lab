// ============================================================
// InteractableObject.cs  —  AR Virtual Lab
// Attach this to each lab GameObject (Ribbon, Tongs, Burner …).
// Handles highlight, pick-up state and type tagging.
// ============================================================
using UnityEngine;
using ARVirtualLab.Core;

namespace ARVirtualLab.Experiments
{
    [DisallowMultipleComponent]
    public class InteractableObject : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("Object Settings")]
        public LabObjectType objectType = LabObjectType.None;
        public bool isInteractable = true;

        [Header("Highlight")]
        [Tooltip("Color applied to the emission when highlighted.")]
        public Color highlightColor = new Color(1f, 0.85f, 0.1f);

        // ── State ─────────────────────────────────────────────────
        public bool IsPickedUp  { get; private set; } = false;
        public bool IsSelected  { get; private set; } = false;

        // ── Private ───────────────────────────────────────────────
        private Renderer[] _renderers;
        private Color[]    _originalEmissions;

        // ──────────────────────────────────────────────────────────
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

        // ── Public API ────────────────────────────────────────────
        public void Highlight()
        {
            IsSelected = true;
            foreach (var r in _renderers)
            {
                if (r.material.HasProperty("_EmissionColor"))
                {
                    r.material.EnableKeyword("_EMISSION");
                    r.material.SetColor("_EmissionColor", highlightColor * 0.5f);
                }
            }
        }

        public void Unhighlight()
        {
            IsSelected = false;
            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i].material.HasProperty("_EmissionColor"))
                {
                    _renderers[i].material.DisableKeyword("_EMISSION");
                    _renderers[i].material.SetColor("_EmissionColor", _originalEmissions[i]);
                }
            }
        }

        public void PickUp()
        {
            IsPickedUp = true;
        }

        public void Drop()
        {
            IsPickedUp = false;
            Unhighlight();
        }

        /// <summary>
        /// Changes the visual appearance of this object (e.g. ribbon → MgO powder).
        /// </summary>
        public void SetMaterial(Material newMaterial)
        {
            foreach (var r in _renderers)
                r.material = newMaterial;
        }

        public void SetColor(Color c)
        {
            foreach (var r in _renderers)
                r.material.color = c;
        }
    }
}
