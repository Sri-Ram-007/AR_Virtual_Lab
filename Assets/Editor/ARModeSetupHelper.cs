#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using ARVirtualLab.AR;
using ARVirtualLab.Lab;

namespace ARVirtualLab.Editor
{
    public static class ARModeSetupHelper
    {
        [MenuItem("AR Lab/Setup and Validate AR Mode", priority = 85)]
        public static void SetupAndValidateAR()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== CONFIGURING & VALIDATING AR MODE ===");

            string scenePath = "Assets/Scenes/MagnesiumLabScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Ensure MagnesiumLab and ARLabManager
            var labGO = GameObject.Find("MagnesiumLab");
            if (labGO == null)
            {
                labGO = new GameObject("MagnesiumLab");
                labGO.AddComponent<MagnesiumLabScene>();
            }

            var arMgr = Object.FindFirstObjectByType<ARLabManager>();
            if (arMgr == null)
            {
                var arMgrGO = new GameObject("ARLabManager");
                arMgr = arMgrGO.AddComponent<ARLabManager>();
            }

            // 2. Configure Android Player Settings for AR
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26; // Android 8.0+
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            sb.AppendLine("Android Min SDK set to API Level 26 (Oreo) and ARM64 for ARCore.");

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            sb.AppendLine("Scene saved with ARLabManager integration.");
            sb.AppendLine("=== CONFIGURATION COMPLETE ===");
            Debug.Log(sb.ToString());
            File.WriteAllText("ar_setup_log.txt", sb.ToString());
        }
    }
}
#endif
