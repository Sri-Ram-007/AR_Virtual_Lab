// ============================================================
// HydrogenGasTestSceneSetup.cs  —  AR Virtual Lab Editor Script
// Creates Assets/Scenes/HydrogenGasTestScene.unity programmatically
// and adds it to EditorBuildSettings.
// Run via: Tools > Hydrogen Gas Test > Create Scene
// or Unity batch: -executeMethod ARVirtualLab.Editor.HydrogenGasTestSceneSetup.CreateScene
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace ARVirtualLab.Editor
{
    [InitializeOnLoad]
    public static class HydrogenGasTestSceneSetup
    {
        private const string SCENE_PATH = "Assets/Scenes/HydrogenGasTestScene.unity";
        private const string SCENE_NAME = "HydrogenGasTestScene";

        static HydrogenGasTestSceneSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;

            // Ensure the scene file exists
            if (!File.Exists(SCENE_PATH))
            {
                Debug.Log("[HydrogenGasTestSceneSetup] Scene missing, creating automatically...");
                CreateScene();
            }
            else
            {
                // Ensure it is in build settings
                AddToBuildSettings();
            }
        }

        // ── Menu item ─────────────────────────────────────────────
        [MenuItem("Tools/Hydrogen Gas Test/Create Scene", priority = 10)]
        public static void CreateSceneFromMenu()
        {
            CreateScene();
            Debug.Log("[HydrogenGasTestSceneSetup] Done via menu.");
        }

        // ── Called by -executeMethod or menu ───────────────────────
        public static void CreateScene()
        {
            // 1. Create a new empty scene
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Create the root GameObject that holds the HydrogenGasTestScene script
            var rootGO = new GameObject("HydrogenGasTestSceneRoot");
            rootGO.AddComponent<ARVirtualLab.HydrogenGasTest.HydrogenGasTestScene>();

            Debug.Log("[HydrogenGasTestSceneSetup] Created HydrogenGasTestScene MonoBehaviour on root GO.");

            // Ensure directory exists
            string dir = Path.GetDirectoryName(SCENE_PATH);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            // 3. Save the scene
            bool saved = EditorSceneManager.SaveScene(scene, SCENE_PATH);
            if (!saved)
            {
                Debug.LogError($"[HydrogenGasTestSceneSetup] Failed to save scene at: {SCENE_PATH}");
                return;
            }
            Debug.Log($"[HydrogenGasTestSceneSetup] Saved scene to: {SCENE_PATH}");

            // 4. Add to build settings
            AddToBuildSettings();
        }

        // ── Separately callable to (re-)register the scene ────────
        [MenuItem("Tools/Hydrogen Gas Test/Add to Build Settings", priority = 11)]
        public static void AddToBuildSettingsMenu()
        {
            AddToBuildSettings();
        }

        public static void AddToBuildSettings()
        {
            var scenes = EditorBuildSettings.scenes;

            // Check if already registered
            foreach (var s in scenes)
            {
                if (s.path == SCENE_PATH)
                {
                    Debug.Log($"[HydrogenGasTestSceneSetup] {SCENE_PATH} already in build settings.");
                    return;
                }
            }

            // Append as a new enabled scene
            var updated = new EditorBuildSettingsScene[scenes.Length + 1];
            for (int i = 0; i < scenes.Length; i++) updated[i] = scenes[i];
            updated[scenes.Length] = new EditorBuildSettingsScene(SCENE_PATH, true);
            EditorBuildSettings.scenes = updated;

            Debug.Log($"[HydrogenGasTestSceneSetup] Added {SCENE_PATH} to build settings (index {scenes.Length}).");
            AssetDatabase.SaveAssets();
        }
    }
}
#endif
