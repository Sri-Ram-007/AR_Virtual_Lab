// ============================================================
// NewExperimentsSceneSetup.cs  —  AR Virtual Lab Editor Script
// Creates Assets/Scenes/ElectrolysisScene.unity and Assets/Scenes/AcidConductivityScene.unity the first time the
// editor loads (one empty root object per scene, carrying the experiment's MonoBehaviour — same pattern as the
// other experiment scenes), and keeps every app scene registered in Build Settings.
// Menu: AR Lab/Create New Experiment Scenes
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    [InitializeOnLoad]
    public static class NewExperimentsSceneSetup
    {
        private const string ELECTROLYSIS_PATH = "Assets/Scenes/ElectrolysisScene.unity";
        private const string CONDUCTIVITY_PATH = "Assets/Scenes/AcidConductivityScene.unity";

        static NewExperimentsSceneSetup()
        {
            EditorApplication.delayCall += AutoSetup;
        }

        private static void AutoSetup()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            bool created = false;
            if (!File.Exists(ELECTROLYSIS_PATH)) { CreateScene(ELECTROLYSIS_PATH, "ElectrolysisRoot", typeof(ARVirtualLab.Electrolysis.ElectrolysisScene)); created = true; }
            if (!File.Exists(CONDUCTIVITY_PATH)) { CreateScene(CONDUCTIVITY_PATH, "AcidConductivityRoot", typeof(ARVirtualLab.AcidConductivity.AcidConductivityScene)); created = true; }
            if (created) TextbookScanSceneSetup.EnsureBuildScenes();
        }

        [MenuItem("AR Lab/Create New Experiment Scenes", priority = 6)]
        public static void CreateAll()
        {
            CreateScene(ELECTROLYSIS_PATH, "ElectrolysisRoot", typeof(ARVirtualLab.Electrolysis.ElectrolysisScene));
            CreateScene(CONDUCTIVITY_PATH, "AcidConductivityRoot", typeof(ARVirtualLab.AcidConductivity.AcidConductivityScene));
            TextbookScanSceneSetup.EnsureBuildScenes();
            Debug.Log("[NewExperimentsSceneSetup] Done.");
        }

        private static void CreateScene(string path, string rootName, System.Type componentType)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject(rootName);
            root.AddComponent(componentType);
            EditorSceneManager.SaveScene(scene, path);
            Debug.Log("[NewExperimentsSceneSetup] Created " + path);
        }
    }
}
#endif
