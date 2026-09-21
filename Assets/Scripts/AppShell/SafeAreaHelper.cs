// ============================================================
// SafeAreaHelper.cs
// Automatically offsets RectTransform to respect mobile safe areas
// (cutouts, camera notches, navigation gestures).
// ============================================================
using UnityEngine;

namespace ARVirtualLab.AppShell
{
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class SafeAreaHelper : MonoBehaviour
    {
        private RectTransform _rectTransform;
        private Rect _lastSafeArea = Rect.zero;
        private Vector2Int _lastScreenSize = Vector2Int.zero;

        [Header("Options")]
        public bool conformX = true;
        public bool conformY = true;
        public float topPaddingOffset = 0f;
        public float bottomPaddingOffset = 0f;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea();
        }

        private void Update()
        {
            if (_lastSafeArea != Screen.safeArea || _lastScreenSize.x != Screen.width || _lastScreenSize.y != Screen.height)
            {
                ApplySafeArea();
            }
        }

        public void ApplySafeArea()
        {
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform == null) return;

            Rect safeArea = Screen.safeArea;
            _lastSafeArea = safeArea;
            _lastScreenSize = new Vector2Int(Screen.width, Screen.height);

            Vector2 minAnchor = safeArea.position;
            Vector2 maxAnchor = safeArea.position + safeArea.size;

            minAnchor.x /= Screen.width;
            minAnchor.y /= Screen.height;
            maxAnchor.x /= Screen.width;
            maxAnchor.y /= Screen.height;

            if (!conformX)
            {
                minAnchor.x = 0f;
                maxAnchor.x = 1f;
            }

            if (!conformY)
            {
                minAnchor.y = 0f;
                maxAnchor.y = 1f;
            }

            _rectTransform.anchorMin = minAnchor;
            _rectTransform.anchorMax = maxAnchor;
            _rectTransform.offsetMin = new Vector2(0f, bottomPaddingOffset);
            _rectTransform.offsetMax = new Vector2(0f, -topPaddingOffset);
        }
    }
}
