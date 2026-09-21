// ============================================================
// OpenMagnesiumSceneOnLoad.cs  -  Editor Only
// Automatically ensures MagnesiumLabScene is open and active
// in the Unity Editor so pressing Play immediately runs
// the interactive Magnesium Ribbon experiment.
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARVirtualLab.Editor
{
    [InitializeOnLoad]
    public static class OpenMagnesiumSceneOnLoad
    {
        private const string SCENE_PATH = "Assets/Scenes/MagnesiumLabScene.unity";

        static OpenMagnesiumSceneOnLoad()
        {
            EditorApplication.delayCall += EnsureMagnesiumSceneOpen;
            // Ensure the Meshy GLB is imported and the Resources prefab is current
            EditorApplication.delayCall += EnsureBurnerPrefabReady;
        }

        private static void EnsureBurnerPrefabReady()
        {
            const string prefabPath = "Assets/Resources/Models/BunsenBurnerModel.prefab";
            // Only rebuild if the prefab doesn't exist yet (avoid reimport on every startup)
            if (!System.IO.File.Exists(prefabPath))
            {
                Debug.Log("[OpenMagnesiumSceneOnLoad] BunsenBurnerModel.prefab missing - rebuilding...");
                BunsenBurnerGLBImporter.RebuildBurnerPrefab();
            }
        }

        [MenuItem("AR Lab/Open Magnesium Experiment Scene", priority = 0)]
        public static void EnsureMagnesiumSceneOpen()
        {
            // CRITICAL: Never call EditorSceneManager.OpenScene while Play mode is active,
            // changing state, or compiling.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
            {
                return;
            }

            Scene activeScene = SceneManager.GetActiveScene();

            // Only auto-open MagnesiumLabScene on initial launch if no scene is loaded
            // or an untitled scene is active. Do NOT override HydrogenLabScene or HomeScene
            // when the user has opened or selected them!
            if (string.IsNullOrEmpty(activeScene.path) || activeScene.name == "Untitled")
            {
                if (System.IO.File.Exists(SCENE_PATH))
                {
                    Debug.Log($"[OpenMagnesiumSceneOnLoad] Opening default experiment scene: {SCENE_PATH}");
                    EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
                }
            }

            // Ensure EditorBuildSettings has all valid lab scenes registered
            EnsureBuildSettings();
        }

        [MenuItem("AR Lab/Rebuild & Open Magnesium Scene", priority = 1)]
        public static void RebuildAndOpen()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[OpenMagnesiumSceneOnLoad] Cannot rebuild scene during Play mode.");
                return;
            }

            if (System.IO.File.Exists(SCENE_PATH))
            {
                EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            }

            StandaloneSetupHelper.SetupStandaloneInternal();
            EditorSceneManager.SaveOpenScenes();
            EnsureBuildSettings();
            Debug.Log("[OpenMagnesiumSceneOnLoad] Rebuilt and saved MagnesiumLabScene!");
        }

        private static void EnsureBuildSettings()
        {
            var currentScenes = EditorBuildSettings.scenes;
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();

            // Always ensure standard scenes are present without wiping HydrogenLabScene
            string[] requiredPaths = new string[]
            {
                "Assets/Scenes/HomeScene.unity",
                SCENE_PATH,
                "Assets/Scenes/HydrogenLabScene.unity"
            };

            foreach (var path in requiredPaths)
            {
                if (System.IO.File.Exists(path))
                {
                    list.Add(new EditorBuildSettingsScene(path, true));
                }
            }

            // Preserve any other existing scenes
            foreach (var s in currentScenes)
            {
                bool alreadyIn = false;
                foreach (var req in list)
                {
                    if (req.path == s.path) { alreadyIn = true; break; }
                }
                if (!alreadyIn) list.Add(s);
            }

            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
