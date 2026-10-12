using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ARVirtualLab.UI;

namespace ARVirtualLab.AppShell
{
    /// <summary>A small modal dialog on its own canvas, usable from any screen (portrait or landscape).</summary>
    public static class AppDialog
    {
        /// <param name="onChoice">true = primary button, false = secondary button, back button or tap outside</param>
        public static GameObject Show(string title, string body, string primary, string secondary, Action<bool> onChoice)
        {
            bool landscape = Screen.width > Screen.height;
            var canvasGO = new GameObject("AppDialog", typeof(RectTransform));
            var canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200;                                  // above every screen and the AR overlay
            var scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = landscape ? new Vector2(1920f, 1080f) : new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasGO.AddComponent<GraphicRaycaster>();

            bool done = false;
            Action<bool> finish = choice =>
            {
                if (done) return;
                done = true;
                UnityEngine.Object.Destroy(canvasGO);
                if (onChoice != null) onChoice(choice);
            };

            var scrim = UiKit.Box(canvasGO.transform, "Scrim", new Color(0.05f, 0.08f, 0.10f, 0.60f));
            UiKit.Fill(scrim.rectTransform);
            scrim.raycastTarget = true;
            var outside = scrim.gameObject.AddComponent<Button>();
            outside.transition = Selectable.Transition.None;
            outside.onClick.AddListener(() => finish(false));

            var fill = UiKit.Card(scrim.transform, "Card", 30);
            var border = fill.transform.parent.GetComponent<Image>();
            border.raycastTarget = true;
            border.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;   // taps on the card stay on it
            UiKit.Pin(border.rectTransform, 0.5f, 0.5f, 0f, 0f, 880f, 560f);
            var card = fill.rectTransform;

            var t = UiKit.Text(card, "Title", title, 44, EduTheme.Ink, FontStyles.Bold, TextAlignmentOptions.TopLeft);
            UiKit.Pin(t.rectTransform, 0f, 1f, 48f, -40f, 784f, 70f);
            var b = UiKit.Text(card, "Body", body, 32, EduTheme.InkSoft, FontStyles.Normal, TextAlignmentOptions.TopLeft);
            UiKit.Pin(b.rectTransform, 0f, 1f, 48f, -125f, 784f, 270f);
            b.enableAutoSizing = true; b.fontSizeMin = 24f; b.fontSizeMax = 32f;

            float x = -48f;
            var ok = UiKit.Button(card, "Primary", primary, EduTheme.Teal, Color.white, 32, 18);
            UiKit.Pin(ok.GetComponent<RectTransform>(), 1f, 0f, x, 40f, 300f, 96f);
            ok.onClick.AddListener(() => finish(true));
            if (!string.IsNullOrEmpty(secondary))
            {
                var no = UiKit.Button(card, "Secondary", secondary, EduTheme.Neutral, EduTheme.Ink, 32, 18);
                UiKit.Pin(no.GetComponent<RectTransform>(), 1f, 0f, x - 320f, 40f, 300f, 96f);
                no.onClick.AddListener(() => finish(false));
            }

            BackButton.Register(canvasGO, () => { finish(false); return true; });
            return canvasGO;
        }
    }
}
