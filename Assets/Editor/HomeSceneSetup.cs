#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using ARVirtualLab.AppShell;

namespace ARVirtualLab.Editor
{
    public static class HomeSceneSetup
    {
        public const string HOME_SCENE_PATH = "Assets/Scenes/HomeScene.unity";
        public const string LAB_SCENE_PATH  = "Assets/Scenes/MagnesiumLabScene.unity";

        [MenuItem("AR Lab/Setup Professional Home Scene", priority = 2)]
        public static void SetupHomeScene()
        {
            Debug.Log("[HomeSceneSetup] Setting up professional Home Scene...");

            Scene scene;
            if (File.Exists(HOME_SCENE_PATH))
            {
                scene = EditorSceneManager.OpenScene(HOME_SCENE_PATH, OpenSceneMode.Single);
            }
            else
            {
                scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }

            // Remove legacy or prototype objects in the scene
            var rootObjects = scene.GetRootGameObjects();
            foreach (var go in rootObjects)
            {
                if (go.name == "Canvas" || go.name == "PhysicsButton" || go.name == "AppShellController" || go.name == "EventSystem")
                {
                    Object.DestroyImmediate(go);
                }
            }

            // Ensure Main Camera exists with solid dark background
            var cam = Camera.main;
            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                cam = camGO.AddComponent<Camera>();
                camGO.tag = "MainCamera";
                camGO.AddComponent<AudioListener>();
            }
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = UIStyleHelper.BgDark;
            cam.transform.position = new Vector3(0, 1, -10);
            cam.transform.rotation = Quaternion.identity;

            // Ensure Directional Light exists
            var light = Object.FindFirstObjectByType<Light>();
            if (light == null)
            {
                var lightGO = new GameObject("Directional Light");
                light = lightGO.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = 1.0f;
                light.color = Color.white;
                lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            }

            // Ensure EventSystem
            var eventSystem = Object.FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>();
            if (eventSystem == null)
            {
                var esGO = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
            }

            // Ensure AppShellController with HomeScreenController
            var controller = Object.FindFirstObjectByType<HomeScreenController>();
            if (controller == null)
            {
                var controllerGO = new GameObject("AppShellController");
                controllerGO.AddComponent<HomeScreenController>();
            }

            // Save scene
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, HOME_SCENE_PATH);
            Debug.Log($"[HomeSceneSetup] Saved HomeScene at {HOME_SCENE_PATH}");

            // Configure Build Settings
            ConfigureBuildSettings();
        }

        public static void ConfigureBuildSettings()
        {
            var scenes = new[]
            {
                new EditorBuildSettingsScene(HOME_SCENE_PATH, true),
                new EditorBuildSettingsScene(LAB_SCENE_PATH, true)
            };

            EditorBuildSettings.scenes = scenes;
            Debug.Log($"[HomeSceneSetup] EditorBuildSettings configured: (0) {HOME_SCENE_PATH}, (1) {LAB_SCENE_PATH}");
        }
    }
}
#endif
