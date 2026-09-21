// ============================================================
// BunsenBurnerGLBImporter.cs  -  Editor Only
// Utility for building BunsenBurnerModel.prefab from the
// imported Meshy GLB asset using Unity's glTFast importer.
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    /// <summary>
    /// Editor utility for building the Bunsen Burner prefab from the imported Meshy GLB asset.
    /// Uses Unity's glTFast importer for GLB loading.
    /// </summary>
    public static class BunsenBurnerGLBImporter
    {
        public const string SOURCE_GLB_PATH =
            "Assets/ThirdParty/Meshy_AI_Lab_Burner_Remesh_0827172119_texture.glb";
        public const string PREFAB_PATH =
            "Assets/Resources/Models/BunsenBurnerModel.prefab";

        [MenuItem("AR Lab/Rebuild Bunsen Burner Prefab", priority = 50)]
        public static void RebuildBurnerPrefabMenu()
        {
            bool success = RebuildBurnerPrefab();
            if (success)
            {
                EditorUtility.DisplayDialog(
                    "Bunsen Burner Prefab",
                    "BunsenBurnerModel prefab rebuilt successfully at:\n" + PREFAB_PATH +
                    "\n\nPress Play to see the 3D model.",
                    "OK");
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "Bunsen Burner Prefab Error",
                    "Failed to rebuild BunsenBurnerModel prefab from:\n" + SOURCE_GLB_PATH +
                    "\n\nPlease ensure the GLB file is imported and glTFast package is installed.",
                    "OK");
            }
        }

        public static bool RebuildBurnerPrefab()
        {
            if (!File.Exists(SOURCE_GLB_PATH))
            {
                Debug.LogError("[BunsenBurnerGLBImporter] Source GLB not found at: " + SOURCE_GLB_PATH);
                return false;
            }

            // Load the imported GameObject hierarchy from the original Meshy GLB
            var glbRoot = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_GLB_PATH);
            if (glbRoot == null)
            {
                // Force an import of the GLB if not loaded yet
                AssetDatabase.ImportAsset(SOURCE_GLB_PATH, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                glbRoot = AssetDatabase.LoadAssetAtPath<GameObject>(SOURCE_GLB_PATH);
            }

            if (glbRoot == null)
            {
                Debug.LogError("[BunsenBurnerGLBImporter] Cannot load imported GLB asset: " + SOURCE_GLB_PATH +
                    ". Ensure glTFast package (com.unity.cloud.gltfast) is installed.");
                return false;
            }

            string prefabDir = Path.GetDirectoryName(PREFAB_PATH);
            if (!Directory.Exists(prefabDir)) Directory.CreateDirectory(prefabDir);

            // Instantiate and save as standard prefab asset
            var tempGO = UnityEngine.Object.Instantiate(glbRoot);
            tempGO.name = "BunsenBurnerModel";

            var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, PREFAB_PATH);
            UnityEngine.Object.DestroyImmediate(tempGO);

            if (prefab != null)
            {
                Debug.Log("[BunsenBurnerGLBImporter] BunsenBurnerModel prefab saved successfully to: " + PREFAB_PATH);
                AssetDatabase.SaveAssets();
                return true;
            }
            else
            {
                Debug.LogError("[BunsenBurnerGLBImporter] Failed to save prefab at: " + PREFAB_PATH);
                return false;
            }
        }
    }
}
#endif
