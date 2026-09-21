// ============================================================
// TextbookScanSceneSetup.cs  —  AR Virtual Lab Editor Script
// Creates Assets/Scenes/TextbookScanScene.unity (one empty root object carrying TextbookScanScene) the first time the
// editor loads, and keeps every app scene in the Build Settings list.
// Menu: AR Lab > Create Textbook Scan Scene
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    [InitializeOnLoad]
    public static class TextbookScanSceneSetup
    {
        private const string SCENE_PATH = "Assets/Scenes/TextbookScanScene.unity";

        static TextbookScanSceneSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            if (!File.Exists(SCENE_PATH)) CreateScene();
            EnsureBuildScenes();
        }

        [MenuItem("AR Lab/Create Textbook Scan Scene", priority = 4)]
        public static void CreateScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("TextbookScanRoot");
            root.AddComponent<ARVirtualLab.Scan.TextbookScanScene>();
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            Debug.Log("[TextbookScanSceneSetup] Created " + SCENE_PATH);
            EnsureBuildScenes();
        }

        /// <summary>Home first, then every experiment and the scanner.</summary>
        public static void EnsureBuildScenes()
        {
            string[] paths =
            {
                "Assets/Scenes/HomeScene.unity",
                "Assets/Scenes/MagnesiumLabScene.unity",
                "Assets/Scenes/HydrogenLabScene.unity",
                "Assets/Scenes/HydrogenGasTestScene.unity",
                SCENE_PATH
            };
            var list = new System.Collections.Generic.List<EditorBuildSettingsScene>();
            foreach (var p in paths)
                if (File.Exists(p)) list.Add(new EditorBuildSettingsScene(p, true));
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
#endif
