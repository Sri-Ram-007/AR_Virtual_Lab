// ============================================================
// MagnesiumSceneBatchBuilder.cs  —  Editor Only
// Called via Unity batch mode:
//   Unity.exe -batchmode -nographics -quit -projectPath <path>
//            -executeMethod ARVirtualLab.Editor.MagnesiumSceneBatchBuilder.BuildAndSave
// ============================================================
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ARVirtualLab.Standalone;

namespace ARVirtualLab.Editor
{
    public static class MagnesiumSceneBatchBuilder
    {
        public static void BuildAndSave()
        {
            Debug.Log("=== MagnesiumSceneBatchBuilder START ===");

            try
            {
                string scenePath = "Assets/Scenes/MagnesiumLabScene.unity";
                
                // Create clean scene
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

                // Run standalone lab setup (Camera, EventSystem, Lights, Table, Objects, UI, Wiring)
                StandaloneSetupHelper.SetupStandaloneInternal();

                // Inspect scene hierarchy and validate flow
                SceneHierarchyInspector.InspectScene();
                MagnesiumExperimentPlayValidator.ValidateFlow();

                // Save scene
                bool saved = EditorSceneManager.SaveScene(scene, scenePath);
                if (saved)
                    Debug.Log("=== Scene saved to: " + scenePath + " ===");
                else
                    Debug.LogError("=== Scene SAVE FAILED ===");

                // Open scene as active
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

                // Set EditorBuildSettings scenes
                EditorBuildSettingsScene[] scenes = new EditorBuildSettingsScene[]
                {
                    new EditorBuildSettingsScene(scenePath, true),
                    new EditorBuildSettingsScene("Assets/ARExperimentScene.unity", true),
                    new EditorBuildSettingsScene("Assets/Scenes/HomeScene.unity", false),
                    new EditorBuildSettingsScene("Assets/PhysicsScene.unity", false)
                };
                EditorBuildSettings.scenes = scenes;

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("=== MagnesiumSceneBatchBuilder ERROR: " + ex);
            }

            Debug.Log("=== MagnesiumSceneBatchBuilder DONE ===");
        }
    }
}
#endif
