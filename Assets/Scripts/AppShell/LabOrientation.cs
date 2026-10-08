using System.Collections;
using UnityEngine;

namespace ARVirtualLab.AppShell
{
    /// <summary>Experiment screens run in landscape; the home and scan screens stay portrait.</summary>
    public static class LabOrientation
    {
        public static void Landscape(MonoBehaviour host)
        {
            Screen.autorotateToPortrait = false;
            Screen.autorotateToPortraitUpsideDown = false;
            Screen.autorotateToLandscapeLeft = true;
            Screen.autorotateToLandscapeRight = true;
            Screen.orientation = ScreenOrientation.LandscapeLeft;
            // Switching straight to AutoRotation from portrait can leave the screen upright on some phones,
            // so rotate first, then let the student flip between the two landscape sides.
            host.StartCoroutine(AllowBothLandscapeSides());
        }

        public static void Portrait()
        {
            Screen.orientation = ScreenOrientation.Portrait;
        }

        // Runs on the experiment's own MonoBehaviour, so it stops if the student leaves within the delay.
        private static IEnumerator AllowBothLandscapeSides()
        {
            yield return new WaitForSecondsRealtime(0.5f);
            Screen.orientation = ScreenOrientation.AutoRotation;
            Debug.Log("[LabOrientation] landscape auto-rotation on (now " + Screen.orientation + ")");
        }
    }
}
