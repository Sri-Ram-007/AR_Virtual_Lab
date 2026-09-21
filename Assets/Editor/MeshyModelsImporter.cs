#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    public static class MeshyModelsImporter
    {
        public static readonly (string glb, string prefab, string name)[] Models = new[]
        {
            ("Assets/ThirdParty/Meshy_AI_Lab_Burner_Remesh_0827172119_texture.glb", "Assets/Resources/Models/BunsenBurnerModel.prefab", "BunsenBurnerModel"),
            ("Assets/ThirdParty/Meshy_AI_Magnesium_Ribbon_Reme_0827200230_texture.glb", "Assets/Resources/Models/MagnesiumRibbonModel.prefab", "MagnesiumRibbonModel"),
            ("Assets/ThirdParty/Meshy_AI_Crucible_Tongs_Remesh_0827191620_texture.glb", "Assets/Resources/Models/CrucibleTongsModel.prefab", "CrucibleTongsModel"),
            ("Assets/ThirdParty/Meshy_AI_Watch_Glass_Remesh_0827195009_texture.glb", "Assets/Resources/Models/WatchGlassModel.prefab", "WatchGlassModel")
        };

        [MenuItem("AR Lab/Build All Meshy Prefabs", priority = 40)]
        public static void BuildAllPrefabs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BUILDING ALL MESHY PREFABS ===");

            string outDir = "Assets/Resources/Models";
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            foreach (var item in Models)
            {
                if (!File.Exists(item.glb))
                {
                    sb.AppendLine($"ERROR: GLB missing at {item.glb}");
                    continue;
                }

                AssetDatabase.ImportAsset(item.glb, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var glbRoot = AssetDatabase.LoadAssetAtPath<GameObject>(item.glb);
                if (glbRoot == null)
                {
                    sb.AppendLine($"ERROR: Failed to load imported GLB asset {item.glb}");
                    continue;
                }

                var tempGO = Object.Instantiate(glbRoot);
                tempGO.name = item.name;

                var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, item.prefab);
                Object.DestroyImmediate(tempGO);

                if (prefab != null)
                {
                    sb.AppendLine($"SUCCESS: Created {item.prefab}");
                    // Inspect mesh filters
                    var mfs = prefab.GetComponentsInChildren<MeshFilter>(true);
                    foreach (var mf in mfs)
                    {
                        var m = mf.sharedMesh;
                        if (m != null)
                        {
                            sb.AppendLine($"   Mesh '{m.name}': Verts={m.vertexCount}, Bounds Center={m.bounds.center:F4}, Size={m.bounds.size:F4}, Min={m.bounds.min:F4}, Max={m.bounds.max:F4}");
                        }
                    }
                }
                else
                {
                    sb.AppendLine($"ERROR: Failed to save prefab {item.prefab}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("=== FINISHED BUILDING ALL MESHY PREFABS ===");
            Debug.Log(sb.ToString());
            File.WriteAllText("meshy_import_log.txt", sb.ToString());
        }
    }
}
#endif
