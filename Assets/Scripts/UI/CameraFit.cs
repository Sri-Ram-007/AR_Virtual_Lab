// ============================================================
// CameraFit.cs  —  AR Virtual Lab
// The experiment tables were framed for a 9:16 screen with a fixed vertical field of view. Modern phones are taller
// and narrower (20:9), so the same vertical view showed much less of the width and the sides of the bench were cut
// off. This keeps the *width* of the view constant instead, and zooms out a little so nothing sits on the edge.
// The AR camera is left alone (ARCore controls its field of view).
// ============================================================
using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace ARVirtualLab.UI
{
    public static class CameraFit
    {
        private const float ReferenceAspect = 9f / 16f;

        /// <param name="referenceVerticalFov">The vertical FOV the scene was designed with at a 9:16 screen.</param>
        /// <param name="zoomOut">1 = the original framing; 1.15 shows 15% more width.</param>
        /// <param name="landscapeVerticalFov">Used instead when the screen is wider than tall: the HUD sits at the sides,
        /// so the apparatus can fill most of the (now short) screen height.</param>
        public static void Apply(Camera cam, float referenceVerticalFov = 48f, float zoomOut = 1.15f, float landscapeVerticalFov = 20f)
        {
            if (cam == null || cam.GetComponent<ARCameraManager>() != null) return;

            float aspect = cam.targetTexture != null
                ? (float)cam.targetTexture.width / cam.targetTexture.height
                : (float)Screen.width / Mathf.Max(1, Screen.height);
            if (aspect <= 0.01f) return;
            if (aspect > 1f)
            {
                cam.fieldOfView = landscapeVerticalFov;
                return;
            }

            float halfWidthTan = Mathf.Tan(referenceVerticalFov * 0.5f * Mathf.Deg2Rad) * ReferenceAspect * zoomOut;
            float fov = 2f * Mathf.Atan(halfWidthTan / aspect) * Mathf.Rad2Deg;
            cam.fieldOfView = Mathf.Clamp(fov, referenceVerticalFov, 85f);
        }

        // In landscape the ExperimentHud cards cover the left and right edges and a strip top and bottom;
        // the apparatus is kept inside this share of the screen
        private const float ClearWidth = 0.56f, ClearHeight = 0.80f;

        /// <summary>
        /// Landscape experiments: the field of view that shows the whole apparatus (measured once, e.g. with
        /// ApparatusBounds) in the clear middle of the screen, whatever its size and the phone's shape. Call every frame:
        /// it follows the screen while the phone turns, and leaves AR cameras alone.
        /// </summary>
        public static void Frame(Camera cam, Bounds apparatus)
        {
            if (cam == null || cam.GetComponent<ARCameraManager>() != null) return;
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            if (aspect <= 1f) return;                                   // still turning to landscape

            float maxX = 0f, maxY = 0f;
            Vector3 c = apparatus.center, e = apparatus.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                Vector3 v = cam.transform.InverseTransformPoint(corner);
                if (v.z < 0.01f) continue;
                maxX = Mathf.Max(maxX, Mathf.Abs(v.x) / v.z);
                maxY = Mathf.Max(maxY, Mathf.Abs(v.y) / v.z);
            }
            float tanHalf = Mathf.Max(maxY / ClearHeight, maxX / (ClearWidth * aspect));
            if (tanHalf <= 0f) return;
            cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(tanHalf) * Mathf.Rad2Deg, 14f, 70f);
        }
    }
}
