using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARVirtualLab.Localization
{
    /// <summary>
    /// Gives every TextMeshPro label in every scene a NativeText, so translation needs no changes in the screens
    /// that build their UI in code. Labels created later (dialogs, the AR overlay) are picked up within half a second.
    /// </summary>
    public class NativeTextManager : MonoBehaviour
    {
        private const float ScanInterval = 0.5f;
        private float _nextScan;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("NativeTextManager");
            DontDestroyOnLoad(go);
            go.AddComponent<NativeTextManager>();
        }

        private void OnEnable() { SceneManager.sceneLoaded += OnSceneLoaded; }
        private void OnDisable() { SceneManager.sceneLoaded -= OnSceneLoaded; }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { AttachAll(); }

        private void Update()
        {
            MlKitTranslator.Pump();
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + ScanInterval;
            AttachAll();
        }

        private static void AttachAll()
        {
            foreach (var t in FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (!t.TryGetComponent<NativeText>(out _)) t.gameObject.AddComponent<NativeText>();
        }
    }
}
