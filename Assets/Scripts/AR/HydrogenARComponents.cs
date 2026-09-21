// ============================================================
// HydrogenARComponents.cs  —  AR Virtual Lab
// Provides an independent AR component tray and placement
// system for the Hydrogen Gas experiment.
// Deliberately does NOT modify ARLabManager.cs or the existing
// ARComponentType enum — all Hydrogen AR logic is self-contained.
// Namespace: ARVirtualLab.Hydrogen
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARVirtualLab.Hydrogen
{
    // ---------------------------------------------------------
    //  Hydrogen-specific AR component types (independent enum)
    // ---------------------------------------------------------
    public enum HydrogenARComponent
    {
        None,
        ConicalFlask,
        ZincGranules,
        DiluteAcid,
        TubeCork,
        CollectionJar
    }

    // ---------------------------------------------------------
    //  HydrogenARTray — bottom tray UI for Hydrogen AR mode.
    //  Add this MonoBehaviour to a scene root object when the
    //  HydrogenLabScene is active in AR mode.
    // ---------------------------------------------------------
    public class HydrogenARTray : MonoBehaviour
    {
        // ── Internal state ─────────────────────────────────────
        private GameObject _trayRoot;
        private readonly Dictionary<HydrogenARComponent, Image>         _cardBGs   = new Dictionary<HydrogenARComponent, Image>();
        private readonly Dictionary<HydrogenARComponent, TMP_Text>      _cardTxts  = new Dictionary<HydrogenARComponent, TMP_Text>();
        private readonly HashSet<HydrogenARComponent>                   _placed    = new HashSet<HydrogenARComponent>();

        private HydrogenARComponent _dragging = HydrogenARComponent.None;

        // ---------------------------------------------------------
        //  Build tray UI
        // ---------------------------------------------------------
        public void BuildTray(Transform canvasParent)
        {
            _trayRoot = new GameObject("HydrogenARTray", typeof(RectTransform));
            _trayRoot.transform.SetParent(canvasParent, false);

            var rt = _trayRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 20f);
            rt.sizeDelta = new Vector2(-40f, 310f);

            var bg = _trayRoot.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.09f, 0.16f, 0.94f);

            var outline = _trayRoot.AddComponent<Outline>();
            outline.effectColor    = new Color(0.20f, 0.50f, 0.85f, 0.70f);
            outline.effectDistance = new Vector2(2, 2);

            // Header label
            var hdr = MakeText(_trayRoot.transform, "HYDROGEN LAB COMPONENTS",
                new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f),
                new Vector2(10f, -12f), new Vector2(-20f, 42f),
                20, FontStyles.Bold, Color.white);

            // Card scroll row
            var scrollGO = new GameObject("CardRow", typeof(RectTransform));
            scrollGO.transform.SetParent(_trayRoot.transform, false);
            var sRT = scrollGO.GetComponent<RectTransform>();
            sRT.anchorMin = new Vector2(0f, 0f);
            sRT.anchorMax = new Vector2(1f, 0f);
            sRT.pivot     = new Vector2(0.5f, 0f);
            sRT.anchoredPosition = new Vector2(0f, 14f);
            sRT.sizeDelta = new Vector2(-30f, 240f);

            var hlg = scrollGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.childAlignment    = TextAnchor.MiddleCenter;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // Build one card per component
            CreateCard(scrollGO.transform, HydrogenARComponent.ConicalFlask, "Conical Flask",  "\u2697");
            CreateCard(scrollGO.transform, HydrogenARComponent.ZincGranules, "Zinc Granules",  "\u26AA");
            CreateCard(scrollGO.transform, HydrogenARComponent.DiluteAcid,   "Dil. H\u2082SO\u2084", "\uD83E\uDDEA");
            CreateCard(scrollGO.transform, HydrogenARComponent.TubeCork,     "Tube + Cork",    "\uD83D\uDD12");
            CreateCard(scrollGO.transform, HydrogenARComponent.CollectionJar,"Collection Jar", "\uD83E\uDD9B");

            _trayRoot.SetActive(false);
        }

        private void CreateCard(Transform parent, HydrogenARComponent type, string title, string icon)
        {
            var cardGO = new GameObject($"Card_{type}", typeof(RectTransform));
            cardGO.transform.SetParent(parent, false);
            var cRT = cardGO.GetComponent<RectTransform>();
            cRT.sizeDelta = new Vector2(175f, 220f);

            var cImg = cardGO.AddComponent<Image>();
            cImg.color = new Color(0.12f, 0.18f, 0.28f, 0.90f);
            var cOut = cardGO.AddComponent<Outline>();
            cOut.effectColor    = new Color(0.30f, 0.60f, 0.90f, 0.55f);
            cOut.effectDistance = new Vector2(2, -2);
            _cardBGs[type] = cImg;

            // Icon
            MakeText(cardGO.transform, icon,
                new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.70f), new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(70f, 70f),
                36, FontStyles.Normal, new Color(1.0f, 0.88f, 0.42f));

            // Title
            MakeText(cardGO.transform, title,
                new Vector2(0f, 0.28f), new Vector2(1f, 0.46f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                17, FontStyles.Bold, Color.white);

            // Status label
            var statusTxt = MakeText(cardGO.transform, "Drag to Place",
                new Vector2(0f, 0.05f), new Vector2(1f, 0.23f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                14, FontStyles.Normal, new Color(0.45f, 0.80f, 1.0f));
            _cardTxts[type] = statusTxt;

            // EventTrigger for drag interaction
            var trigger = cardGO.AddComponent<UnityEngine.EventSystems.EventTrigger>();

            var entryDown = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerDown };
            entryDown.callback.AddListener((d) => OnCardDragStart(type));
            trigger.triggers.Add(entryDown);

            var entryUp = new UnityEngine.EventSystems.EventTrigger.Entry
                { eventID = UnityEngine.EventSystems.EventTriggerType.PointerUp };
            entryUp.callback.AddListener((d) => OnCardDragEnd(type));
            trigger.triggers.Add(entryUp);
        }

        // ---------------------------------------------------------
        //  Card Drag Events
        // ---------------------------------------------------------
        private void OnCardDragStart(HydrogenARComponent type)
        {
            if (_placed.Contains(type)) return;
            _dragging = type;
            Debug.Log($"[HydrogenARTray] Dragging: {type}");
        }

        private void OnCardDragEnd(HydrogenARComponent type)
        {
            if (_dragging == HydrogenARComponent.None) return;
            // In a full AR integration this would ray-cast against the AR plane
            // and instantiate the equipment. For now mark as placed when released.
            MarkPlaced(_dragging);
            _dragging = HydrogenARComponent.None;
        }

        // ---------------------------------------------------------
        //  Public API
        // ---------------------------------------------------------
        public void Show()  { if (_trayRoot != null) _trayRoot.SetActive(true); }
        public void Hide()  { if (_trayRoot != null) _trayRoot.SetActive(false); }

        public void MarkPlaced(HydrogenARComponent type)
        {
            _placed.Add(type);
            if (_cardBGs.TryGetValue(type, out var img))
                img.color = new Color(0.05f, 0.30f, 0.12f, 0.90f); // green tint
            if (_cardTxts.TryGetValue(type, out var txt))
                txt.text = "\u2705 Placed";
            Debug.Log($"[HydrogenARTray] Placed: {type} ({_placed.Count}/5)");
        }

        public bool AllPlaced() => _placed.Count >= 5;

        public void Reset()
        {
            _placed.Clear();
            _dragging = HydrogenARComponent.None;
            foreach (var kv in _cardBGs)
                kv.Value.color = new Color(0.12f, 0.18f, 0.28f, 0.90f);
            foreach (var kv in _cardTxts)
                kv.Value.text = "Drag to Place";
        }

        // ---------------------------------------------------------
        //  Utility: create a TextMeshProUGUI on a RectTransform
        // ---------------------------------------------------------
        private TMP_Text MakeText(Transform parent, string text,
            Vector2 amin, Vector2 amax, Vector2 pivot,
            Vector2 ap, Vector2 sd,
            float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject("Txt_" + text.Substring(0, Mathf.Min(8, text.Length)), typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = amin;
            rt.anchorMax        = amax;
            rt.pivot            = pivot;
            rt.anchoredPosition = ap;
            rt.sizeDelta        = sd;

            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text      = text;
            tmp.fontSize  = fontSize;
            tmp.fontStyle = style;
            tmp.color     = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = true;
            return tmp;
        }
    }
}
