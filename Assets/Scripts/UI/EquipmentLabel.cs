// ============================================================
// EquipmentLabel.cs  —  AR Virtual Lab
// The small name-tag that floats beside a piece of apparatus ("Conical Flask", "Dil. H2SO4"...). Tags are kept
// compact, can be hidden whenever they are not useful (visibleWhen), and a per-canvas resolver pushes any tags that
// would overlap apart so they never pile up on top of the apparatus or on each other.
// ============================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ARVirtualLab.UI
{
    public enum LabelAnchor { Top, Centre, Bottom }

    public class EquipmentLabel : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _worldOffset;
        private Vector2 _screenOffset;
        private LabelAnchor _anchor;
        private RectTransform _canvasRT;
        private Canvas _canvas;
        private RectTransform _rt;
        private CanvasGroup _cg;
        private System.Func<Camera> _getCamera;
        private System.Func<bool> _visibleWhen;

        internal Vector2 DesiredPos;
        internal bool IsShown;
        internal RectTransform Rt { get { return _rt; } }

        /// <summary>Builds one label under canvasRoot, tracking target (plus offsets) every frame.</summary>
        public static EquipmentLabel Create(RectTransform canvasRoot, System.Func<Camera> getCamera, Transform target,
            string text, LabelAnchor anchor = LabelAnchor.Top, Vector2? screenOffset = null, System.Func<bool> visibleWhen = null)
        {
            var box = UiKit.Box(canvasRoot, "Label_" + text, new Color(1f, 1f, 1f, 0.92f), 8);
            box.raycastTarget = false;
            var outline = box.gameObject.AddComponent<Outline>();
            outline.effectColor = EduTheme.Border;
            outline.effectDistance = new Vector2(1f, -1f);
            var rt = box.rectTransform;
            rt.sizeDelta = new Vector2(Mathf.Clamp(text.Length * 10.5f + 18f, 70f, 230f), 28f);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var label = UiKit.Text(box.transform, "T", text, 15, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.Center);
            UiKit.Fill(label.rectTransform);
            label.text = MobileText.Clean(label.text);
            label.enableAutoSizing = false;
            label.fontSize = 15f;
            label.raycastTarget = false;

            var comp = box.gameObject.AddComponent<EquipmentLabel>();
            comp._target = target;
            comp._anchor = anchor;
            comp._screenOffset = screenOffset ?? Vector2.zero;
            comp._canvasRT = canvasRoot;
            comp._canvas = canvasRoot.GetComponentInParent<Canvas>();
            comp._rt = rt;
            comp._getCamera = getCamera;
            comp._visibleWhen = visibleWhen;
            comp._cg = box.gameObject.AddComponent<CanvasGroup>();
            comp._cg.blocksRaycasts = false; comp._cg.interactable = false;

            var resolver = canvasRoot.GetComponent<LabelResolver>();
            if (resolver == null) resolver = canvasRoot.gameObject.AddComponent<LabelResolver>();
            resolver.Labels.Add(comp);
            return comp;
        }

        private Vector3 AnchorPoint()
        {
            var rs = _target.GetComponentsInChildren<Renderer>(false);
            bool first = true; Bounds b = new Bounds(_target.position, Vector3.zero);
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer || r is LineRenderer || !r.enabled) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            switch (_anchor)
            {
                case LabelAnchor.Bottom: return new Vector3(b.center.x, b.min.y, b.center.z);
                case LabelAnchor.Centre: return b.center;
                default: return new Vector3(b.center.x, b.max.y, b.center.z);
            }
        }

        // runs from LabelResolver (after every label has had a chance to update) — no LateUpdate of its own
        internal void Refresh()
        {
            IsShown = false;
            if (_target == null || _canvasRT == null) { _cg.alpha = 0f; return; }
            var cam = _getCamera != null ? _getCamera() : Camera.main;
            bool visible = cam != null && _target.gameObject.activeInHierarchy && !ARVirtualLab.AR.ARExperimentMode.BlockSceneInput
                           && (_visibleWhen == null || _visibleWhen());
            if (!visible) { _cg.alpha = 0f; return; }

            Vector3 screen = cam.WorldToScreenPoint(AnchorPoint());
            if (screen.z < 0f) { _cg.alpha = 0f; return; }

            Camera uiCam = (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _canvas.worldCamera : null;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRT, screen, uiCam, out local);
            float lift = _anchor == LabelAnchor.Top ? _rt.sizeDelta.y * 0.75f : (_anchor == LabelAnchor.Bottom ? -_rt.sizeDelta.y * 0.75f : 0f);
            DesiredPos = local + _screenOffset + new Vector2(0f, lift);
            IsShown = true;
            _cg.alpha = 1f;
        }
    }

    [DefaultExecutionOrder(1000)]
    public class LabelResolver : MonoBehaviour
    {
        public readonly List<EquipmentLabel> Labels = new List<EquipmentLabel>();
        private readonly List<EquipmentLabel> _shown = new List<EquipmentLabel>();

        private void LateUpdate()
        {
            _shown.Clear();
            foreach (var l in Labels)
            {
                if (l == null) continue;
                l.Refresh();
                if (l.IsShown) _shown.Add(l);
            }
            // highest label first; every later label that would overlap is nudged down (and sideways if needed)
            _shown.Sort((a, b) => b.DesiredPos.y.CompareTo(a.DesiredPos.y));
            for (int i = 0; i < _shown.Count; i++)
            {
                var cur = _shown[i];
                Vector2 p = cur.DesiredPos;
                Vector2 size = cur.Rt.sizeDelta;
                for (int pass = 0; pass < 6; pass++)
                {
                    bool moved = false;
                    for (int j = 0; j < i; j++)
                    {
                        var other = _shown[j];
                        Vector2 op = other.Rt.anchoredPosition, os = other.Rt.sizeDelta;
                        float needX = (size.x + os.x) * 0.5f + 4f, needY = (size.y + os.y) * 0.5f + 3f;
                        if (Mathf.Abs(p.x - op.x) < needX && Mathf.Abs(p.y - op.y) < needY)
                        {
                            p.y = op.y - needY;
                            moved = true;
                        }
                    }
                    if (!moved) break;
                }
                cur.Rt.anchoredPosition = p;
            }
        }
    }
}
