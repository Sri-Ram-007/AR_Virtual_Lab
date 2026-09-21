// ============================================================
// HydrogenModelImporter.cs  —  AR Virtual Lab Editor Script
// Creates Resources/Models/ prefabs for the 3 Hydrogen GLBs
// located in Assets/Models/Hydrogen/, exactly matching the
// Magnesium prefab workflow.
//
// Run via: Tools > Hydrogen Lab > Create Hydrogen Model Prefabs
// The prefabs are then loaded at runtime by HydrogenLabScene.cs
// using Resources.Load<GameObject>("Models/<PrefabName>").
//
// Models used (NOT moved or duplicated):
//   Assets/Models/Hydrogen/Meshy_AI_Conical_Flask_Remesh_0907162658_texture.glb
//   Assets/Models/Hydrogen/Meshy_AI_Zinc_Granules_Remesh_0907162706_texture.glb
//   Assets/Models/Hydrogen/Meshy_AI_Cork_with_Glass_Tube__0907162715_texture.glb
// ============================================================
using UnityEngine;
using UnityEditor;
using System.IO;

public static class HydrogenModelImporter
{
    // Paths to the source GLBs (relative to project root)
    private static readonly string[] GLB_DIRS = new[]
    {
        "Assets/Models/Hydrogen",
        "Assets/ThirdParty"
    };
    private const string PREFAB_OUT_DIR = "Assets/Resources/Models";

    // GLB filename fragments → prefab name mapping
    private static readonly (string glbFragment, string prefabName)[] _map = new[]
    {
        ("Conical_Flask",       "ConicalFlaskModel"),
        ("Zinc_Granules",       "ZincGranulesModel"),
        ("Cork_with_Glass",     "CorkWithTubeModel"),
        ("Sulfuric_Acid_Bottle","SulfuricAcidBottleModel"),
        ("Gas_Collection_Jar",  "CollectionJarModel"),
    };

    // ── Menu item ─────────────────────────────────────────────
    [MenuItem("Tools/Hydrogen Lab/Create Hydrogen Model Prefabs")]
    public static void CreatePrefabsFromMenu()
    {
        CreatePrefabs();
    }

    // ── Called by HydrogenSceneSetup or standalone ────────────
    public static void CreatePrefabs()
    {
        // Ensure output directory exists
        if (!AssetDatabase.IsValidFolder(PREFAB_OUT_DIR))
        {
            string parent = Path.GetDirectoryName(PREFAB_OUT_DIR).Replace('\\', '/');
            string child  = Path.GetFileName(PREFAB_OUT_DIR);
            AssetDatabase.CreateFolder(parent, child);
            Debug.Log($"[HydrogenModelImporter] Created folder: {PREFAB_OUT_DIR}");
        }

        // Find all GLB files across GLB directories
        string[] guids = AssetDatabase.FindAssets("t:Object", GLB_DIRS);

        int created = 0;
        foreach (var (glbFragment, prefabName) in _map)
        {
            string glbPath = FindGLB(guids, glbFragment);
            if (string.IsNullOrEmpty(glbPath))
            {
                Debug.LogWarning($"[HydrogenModelImporter] GLB with '{glbFragment}' not found in search directories.");
                continue;
            }

            string prefabPath = $"{PREFAB_OUT_DIR}/{prefabName}.prefab";

            // Skip if prefab already exists
            if (File.Exists(prefabPath))
            {
                Debug.Log($"[HydrogenModelImporter] Prefab already exists, skipping: {prefabPath}");
                continue;
            }

            // Load the imported GLB as a GameObject
            GameObject glbAsset = AssetDatabase.LoadAssetAtPath<GameObject>(glbPath);
            if (glbAsset == null)
            {
                Debug.LogWarning($"[HydrogenModelImporter] Could not load GLB as GameObject: {glbPath}");
                continue;
            }

            // Instantiate a temporary copy to use as prefab source
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(glbAsset);
            if (instance == null)
            {
                instance = Object.Instantiate(glbAsset);
            }
            instance.name = prefabName;

            // Save as a prefab
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            Object.DestroyImmediate(instance);

            if (saved != null)
            {
                Debug.Log($"[HydrogenModelImporter] Created prefab: {prefabPath}  (from {glbPath})");
                created++;
            }
            else
            {
                Debug.LogError($"[HydrogenModelImporter] Failed to save prefab: {prefabPath}");
            }
        }

        AssetDatabase.Refresh();
        AssetDatabase.SaveAssets();

        Debug.Log($"[HydrogenModelImporter] Done. {created} prefab(s) created in {PREFAB_OUT_DIR}");
        EditorUtility.DisplayDialog("Hydrogen Model Prefabs",
            $"{created} prefab(s) created successfully in {PREFAB_OUT_DIR}.\n\nYou can now play HydrogenLabScene and the Meshy models will be used automatically.",
            "OK");
    }

    // ── Find GLB path containing a keyword ───────────────────
    private static string FindGLB(string[] guids, string fragment)
    {
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path.EndsWith(".glb", System.StringComparison.OrdinalIgnoreCase) &&
                path.Contains(fragment, System.StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
        }
        return null;
    }
}
