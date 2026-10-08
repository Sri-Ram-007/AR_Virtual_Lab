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
    }
}
