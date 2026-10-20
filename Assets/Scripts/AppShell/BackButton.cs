using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARVirtualLab.AppShell
{
    /// <summary>
    /// Android's back button / back gesture. Open dialogs register a handler and close first (newest first); then AR
    /// mode is left, then an experiment returns to Home, and on Home the app goes to the background like any app.
    /// </summary>
    public class BackButton : MonoBehaviour
    {
        private struct Entry { public GameObject Owner; public Func<bool> Handle; }
        private static readonly List<Entry> _handlers = new List<Entry>();

        /// <summary>Handle returns true if it used the press (e.g. closed its dialog). Dropped when owner is destroyed.</summary>
        public static void Register(GameObject owner, Func<bool> handle)
        {
            _handlers.Add(new Entry { Owner = owner, Handle = handle });
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("BackButton");
            DontDestroyOnLoad(go);
            go.AddComponent<BackButton>();
        }

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;      // Android reports back as Escape
            if (kb == null || !kb.escapeKey.wasPressedThisFrame) return;

            _handlers.RemoveAll(e => e.Owner == null);
            for (int i = _handlers.Count - 1; i >= 0; i--)
            {
                var e = _handlers[i];
                if (e.Owner.activeInHierarchy && e.Handle()) return;
            }

            if (ARVirtualLab.AR.ARExperimentMode.Active) { ARVirtualLab.AR.ARExperimentMode.End(); return; }
            if (SceneManager.GetActiveScene().name != AppNavigation.HOME_SCENE) { AppNavigation.GoToHome(); return; }
            MoveToBackground();
        }

        private static void MoveToBackground()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                activity.Call<bool>("moveTaskToBack", true);
#endif
        }
    }
}
