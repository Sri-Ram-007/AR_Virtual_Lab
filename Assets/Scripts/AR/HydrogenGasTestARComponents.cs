// ============================================================
// HydrogenGasTestARComponents.cs  —  AR Virtual Lab
// Provides an independent AR component tray and placement
// system for the Hydrogen Gas Test experiment (Zinc + H2SO4 with Soap Bubbles & Candle Pop Test).
// Namespace: ARVirtualLab.HydrogenGasTest
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARVirtualLab.HydrogenGasTest
{
    // ---------------------------------------------------------
    //  Hydrogen Gas Test AR component types (7 components)
    // ---------------------------------------------------------
    public enum HydrogenGasTestARComponent
    {
        None,
        Stand,
        TestTube,
        ZincGranules,
        DiluteAcid,
        CorkDeliveryTube,
        SoapContainer,
        Candle
    }

    // ---------------------------------------------------------
    //  HydrogenGasTestARTray — bottom tray UI for AR mode.
    // ---------------------------------------------------------
    public class HydrogenGasTestARTray : MonoBehaviour
    {
        private GameObject _trayRoot;
        private readonly Dictionary<HydrogenGasTestARComponent, Image>    _cardBGs  = new Dictionary<HydrogenGasTestARComponent, Image>();
        private readonly Dictionary<HydrogenGasTestARComponent, TMP_Text> _cardTxts = new Dictionary<HydrogenGasTestARComponent, TMP_Text>();
        private readonly HashSet<HydrogenGasTestARComponent>              _placed   = new HashSet<HydrogenGasTestARComponent>();

        public void BuildTray(Transform canvasParent)
        {
            _trayRoot = new GameObject("HydrogenGasTestARTray", typeof(RectTransform));
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
            MakeText(_trayRoot.transform, "HYDROGEN GAS TEST AR TRAY",
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

            var scrollRect = scrollGO.AddComponent<ScrollRect>();
            scrollRect.horizontal = true;
            scrollRect.vertical   = false;

            var contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(scrollGO.transform, false);
            var cRT = contentGO.GetComponent<RectTransform>();
            cRT.anchorMin = new Vector2(0f, 0f);
            cRT.anchorMax = new Vector2(0f, 1f);
            cRT.pivot     = new Vector2(0f, 0.5f);
            cRT.sizeDelta = new Vector2(1100f, 0f);

            scrollRect.content = cRT;

            var hlg = contentGO.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing = 14f;
            hlg.padding = new RectOffset(16, 16, 8, 8);
            hlg.childAlignment    = TextAnchor.MiddleLeft;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;

            // 7 Component cards
            CreateCard(contentGO.transform, HydrogenGasTestARComponent.Stand,           "Lab Stand",         "\u2693");
            CreateCard(contentGO.transform, HydrogenGasTestARComponent.TestTube,        "Test Tube",         "\uD83E\uDDEA");
            CreateCard(contentGO.transform, HydrogenGasTestARComponent.ZincGranules,    "Zinc Granules",     "\u26AA");
            CreateCard(contentGO.transform, HydrogenGasTestARComponent.DiluteAcid,      "Dil. H\u2082SO\u2084",    "\u2622");
            CreateCard(contentGO.transform, HydrogenGasTestARComponent.CorkDeliveryTube,"Cork + Tube",      "\uD83D\uDD12");
            CreateCard(contentGO.transform, HydrogenGasTestARComponent.SoapContainer,   "Soap Basin",        "\uD83E\uDDE7");
            CreateCard(contentGO.transform, HydrogenGasTestARComponent.Candle,          "Candle",            "\uD83D\uDD6F");

            _trayRoot.SetActive(false);
        }

        private void CreateCard(Transform parent, HydrogenGasTestARComponent type, string title, string icon)
        {
            var cardGO = new GameObject($"Card_{type}", typeof(RectTransform));
            cardGO.transform.SetParent(parent, false);

            var rt = cardGO.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(136f, 210f);

            var bg = cardGO.AddComponent<Image>();
            bg.color = new Color(0.10f, 0.16f, 0.28f, 0.95f);

            var outl = cardGO.AddComponent<Outline>();
            outl.effectColor    = new Color(0.18f, 0.45f, 0.80f, 0.55f);
            outl.effectDistance = new Vector2(1.5f, 1.5f);

            MakeText(cardGO.transform, icon,
                new Vector2(0f, 0.42f), new Vector2(1f, 0.95f), new Vector2(0.5f, 0.5f),
                Vector2.zero, Vector2.zero,
                42, FontStyles.Normal, Color.white);

            var lbl = MakeText(cardGO.transform, title,
                new Vector2(0f, 0.05f), new Vector2(1f, 0.42f), new Vector2(0.5f, 0.5f),
                new Vector2(6f, 0f), new Vector2(-12f, 0f),
                17, FontStyles.Bold, new Color(0.80f, 0.90f, 1f));

            var btn = cardGO.AddComponent<Button>();
            btn.onClick.AddListener(() => OnCardClicked(type));

            _cardBGs[type]  = bg;
            _cardTxts[type] = lbl;
        }

        private void OnCardClicked(HydrogenGasTestARComponent type)
        {
            if (_placed.Contains(type))
            {
                _placed.Remove(type);
                SetCardState(type, false);
            }
            else
            {
                _placed.Add(type);
                SetCardState(type, true);
            }
        }

        public void SetCardState(HydrogenGasTestARComponent type, bool placed)
        {
            if (_cardBGs.TryGetValue(type, out var bg))
            {
                bg.color = placed
                    ? new Color(0.06f, 0.50f, 0.35f, 0.95f)
                    : new Color(0.10f, 0.16f, 0.28f, 0.95f);
            }
            if (_cardTxts.TryGetValue(type, out var txt))
            {
                txt.color = placed ? Color.white : new Color(0.80f, 0.90f, 1f);
            }
        }

        public void Show()  { if (_trayRoot != null) _trayRoot.SetActive(true); }
        public void Hide()  { if (_trayRoot != null) _trayRoot.SetActive(false); }
        public void Toggle(){ if (_trayRoot != null) _trayRoot.SetActive(!_trayRoot.activeSelf); }

        public void ResetTray()
        {
            _placed.Clear();
            foreach (var kvp in _cardBGs)
            {
                kvp.Value.color = new Color(0.10f, 0.16f, 0.28f, 0.95f);
            }
            foreach (var kvp in _cardTxts)
            {
                kvp.Value.color = new Color(0.80f, 0.90f, 1f);
            }
        }

        private static TMP_Text MakeText(Transform parent, string text,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
            Vector2 anchoredPosition, Vector2 sizeDelta,
            float fontSize, FontStyles style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot     = pivot;
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta        = sizeDelta;

            var t = go.AddComponent<TextMeshProUGUI>();
            t.text      = text;
            t.fontSize  = fontSize;
            t.fontStyle = style;
            t.color     = color;
            t.alignment = TextAlignmentOptions.Center;
            t.enableWordWrapping = true;
            t.raycastTarget = false;
            return t;
        }
    }
}
