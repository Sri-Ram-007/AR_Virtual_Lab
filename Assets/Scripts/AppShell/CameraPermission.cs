using System;
using System.Collections;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace ARVirtualLab.AppShell
{
    /// <summary>
    /// Asks for the camera the way phones expect: say why first, then show Android's prompt; and if the student chose
    /// "don't ask again", offer the phone's settings instead of failing silently.
    /// </summary>
    public static class CameraPermission
    {
        public const string Title = "Use the camera?";
        public const string Privacy = "The picture stays on your phone. EduAR never records, saves or sends it.";

        /// <param name="why">One sentence on what the camera is for here, e.g. "to find your textbook page".</param>
        public static IEnumerator Request(string why, Action<bool> done)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (Permission.HasUserAuthorizedPermission(Permission.Camera)) { done(true); yield break; }

            bool answered = false, proceed = false;
            AppDialog.Show(Title, why + "\n\n" + Privacy, "Continue", "Not now", c => { proceed = c; answered = true; });
            while (!answered) yield return null;
            if (!proceed) { done(false); yield break; }

            bool granted = false, denied = false;
            var cb = new PermissionCallbacks();
            cb.PermissionGranted += _ => granted = true;
            cb.PermissionDenied += _ => denied = true;
            Permission.RequestUserPermission(Permission.Camera, cb);
            float waited = 0f;
            while (!granted && !denied && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                if (Permission.HasUserAuthorizedPermission(Permission.Camera)) granted = true;
                yield return null;
            }
            if (granted) { done(true); yield break; }

            // Refused again after an earlier refusal: Android stops showing its prompt, only Settings can turn it on
            if (!Permission.ShouldShowRequestPermissionRationale(Permission.Camera))
            {
                answered = false; proceed = false;
                AppDialog.Show("Camera is turned off",
                    "To use this, allow the camera for EduAR in your phone's Settings, under Permissions.",
                    "Open Settings", "Close", c => { proceed = c; answered = true; });
                while (!answered) yield return null;
                if (proceed) OpenAppSettings();
            }
            done(false);
#else
            done(true);
            yield break;
#endif
        }

        public static void OpenAppSettings()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                using (var uriClass = new AndroidJavaClass("android.net.Uri"))
                using (var uri = uriClass.CallStatic<AndroidJavaObject>("fromParts", "package", Application.identifier, null))
                using (var intent = new AndroidJavaObject("android.content.Intent", "android.settings.APPLICATION_DETAILS_SETTINGS", uri))
                {
                    intent.Call<AndroidJavaObject>("addFlags", 0x10000000);         // FLAG_ACTIVITY_NEW_TASK
                    activity.Call("startActivity", intent);
                }
            }
            catch (Exception e) { Debug.LogWarning("[CameraPermission] could not open Settings: " + e.Message); }
#endif
        }
    }
}
