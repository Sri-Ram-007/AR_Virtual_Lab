// ============================================================
// MlKitTranslator.cs  —  AR Virtual Lab
// Fallback for text that is not in the Telugu table yet: Google ML Kit translates it on the phone.
// The Telugu model (~30 MB) downloads once, the first time it is needed; after that it works offline.
// Results are cached by Loc, so each sentence is translated only once per phone.
// Java side and the ML Kit dependency: Assets/Plugins/Android/ArVirtualLabText.androidlib.
// ============================================================
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace ARVirtualLab.Localization
{
    public static class MlKitTranslator
    {
        private const float RetryAfterFailure = 60f;      // e.g. no internet for the model download yet

        private static readonly HashSet<string> _inFlight = new HashSet<string>();
        private static readonly Dictionary<string, float> _failedAt = new Dictionary<string, float>();
        private static readonly Queue<Result> _results = new Queue<Result>();
        private static readonly Regex Slot = new Regex(@"\{\d+\}");

        private struct Result { public string Key, Text; public bool Ok; }

#if UNITY_ANDROID && !UNITY_EDITOR
        private static readonly Regex Tag = new Regex(@"<[^<>]+>");
        private static AndroidJavaClass _java;
        private static Callback _callback;

        private class Callback : AndroidJavaProxy
        {
            public Callback() : base("com.arvirtuallab.text.MlKitTranslator$Callback") { }
            // called on an Android thread: hand the result to the Unity thread
            public void onResult(string key, string translated) { lock (_results) _results.Enqueue(new Result { Key = key, Text = translated, Ok = true }); }
            public void onFailure(string key, string error) { lock (_results) _results.Enqueue(new Result { Key = key, Text = error, Ok = false }); }
        }
#endif

        public static void Request(string key)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_inFlight.Contains(key)) return;
            float failed;
            if (_failedAt.TryGetValue(key, out failed) && Time.unscaledTime - failed < RetryAfterFailure) return;

            // tags would be mangled by the translator; the placeholders {0} usually survive and are checked on return
            string text = Tag.Replace(key, "");
            if (text.Trim().Length == 0) return;
            try
            {
                if (_java == null) { _java = new AndroidJavaClass("com.arvirtuallab.text.MlKitTranslator"); _callback = new Callback(); }
                _java.CallStatic("translate", key, text, _callback);
                _inFlight.Add(key);
            }
            catch (System.Exception e)
            {
                _failedAt[key] = Time.unscaledTime;
                Debug.LogWarning("[MlKit] could not start a translation: " + e.Message);
            }
#endif
        }

        /// <summary>Called every frame on the Unity thread (by NativeTextManager).</summary>
        public static void Pump()
        {
            while (true)
            {
                Result r;
                lock (_results)
                {
                    if (_results.Count == 0) return;
                    r = _results.Dequeue();
                }
                _inFlight.Remove(r.Key);
                if (!r.Ok)
                {
                    _failedAt[r.Key] = Time.unscaledTime;
                    Debug.LogWarning("[MlKit] translation failed (" + r.Text + ") for: " + r.Key);
                    continue;
                }
                if (Slot.Matches(r.Text).Count != Slot.Matches(r.Key).Count)
                {
                    Debug.LogWarning("[MlKit] dropped a translation that lost its number placeholders: " + r.Key);
                    continue;
                }
                Loc.AddMachineTranslation(r.Key, r.Text);
            }
        }
    }
}
