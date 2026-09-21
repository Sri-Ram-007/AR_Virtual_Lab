#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    [InitializeOnLoad]
    public static class HydrogenGasTestModelImporter
    {
        static HydrogenGasTestModelImporter()
        {
            EditorApplication.delayCall += EnsurePrefabsReady;
        }

        private static void EnsurePrefabsReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            // Check if any of the target prefabs are missing
            bool missing = false;
            foreach (var item in Models)
            {
                string p = $"Assets/Resources/Models/{item.prefabName}.prefab";
                if (!File.Exists(p)) { missing = true; break; }
            }
            if (missing)
            {
                BuildAllPrefabs();
            }
        }
        public static readonly (string glb, string prefabName)[] Models = new[]
        {
            ("Assets/ThirdParty/Meshy_AI_Clamp_Stand_Remesh_0908183224_texture.glb", "ClampStandModel"),
            ("Assets/ThirdParty/Meshy_AI_Test_Tube_Remesh_0908183205_texture.glb", "TestTubeModel"),
            ("Assets/ThirdParty/Meshy_AI_Cork_With_Delivery_Tu_0908183214_texture.glb", "CorkWithDeliveryTubeModel"),
            ("Assets/ThirdParty/Meshy_AI_Basin_With_Soap_Solut_0908183241_texture.glb", "SoapBasinModel"),
            ("Assets/ThirdParty/Meshy_AI_Candle_Remesh_0908183233_texture.glb", "CandleModel")
        };

        [MenuItem("Tools/Hydrogen Gas Test/Build Prefabs", priority = 50)]
        public static void BuildAllPrefabs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BUILDING HYDROGEN GAS TEST PREFABS ===");

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
                tempGO.name = item.prefabName;

                string prefabPath = $"{outDir}/{item.prefabName}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
                Object.DestroyImmediate(tempGO);

                if (prefab != null)
                {
                    sb.AppendLine($"SUCCESS: Created {prefabPath}");
                    var renderers = prefab.GetComponentsInChildren<Renderer>(true);
                    Bounds totalBounds = new Bounds(Vector3.zero, Vector3.zero);
                    bool hasBounds = false;
                    foreach (var r in renderers)
                    {
                        if (!hasBounds) { totalBounds = r.bounds; hasBounds = true; }
                        else totalBounds.Encapsulate(r.bounds);
                        sb.AppendLine($"   Renderer '{r.name}': Type={r.GetType().Name}, Bounds Center={r.bounds.center:F4}, Size={r.bounds.size:F4}, Materials={r.sharedMaterials.Length}");
                        foreach (var m in r.sharedMaterials)
                        {
                            if (m != null) sb.AppendLine($"      Mat '{m.name}': Shader={m.shader?.name}");
                        }
                    }
                    sb.AppendLine($"   TOTAL BOUNDS: Center={totalBounds.center:F4}, Size={totalBounds.size:F4}, Min={totalBounds.min:F4}, Max={totalBounds.max:F4}");
                }
                else
                {
                    sb.AppendLine($"ERROR: Failed to save prefab {prefabPath}");
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            sb.AppendLine("=== FINISHED BUILDING HYDROGEN GAS TEST PREFABS ===");
            Debug.Log(sb.ToString());
            File.WriteAllText("hgt_model_import_log.txt", sb.ToString());
        }
    }
}
#endif
