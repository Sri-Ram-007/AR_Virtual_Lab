// ============================================================
// NewExperimentsModelImporter.cs  —  AR Virtual Lab Editor Script
// Bakes the raw Meshy .glb files for the Electrolysis and Acid-Conductivity experiments (Assets/ThirdParty) into
// Assets/Resources/Models/<Name>.prefab, the same way the earlier experiments' importer scripts do. Anything a scene
// loads with Resources.Load must live under Resources, or it is invisible in a phone build (AssetDatabase search —
// the editor-only fallback — does not exist there).
// Menu: Tools/New Experiments/Build Prefabs
// ============================================================
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    [InitializeOnLoad]
    public static class NewExperimentsModelImporter
    {
        static NewExperimentsModelImporter()
        {
            EditorApplication.delayCall += EnsurePrefabsReady;
        }

        public static readonly (string glb, string prefabName)[] Models = new[]
        {
            ("Assets/ThirdParty/Meshy_AI_Glass_Beaker_Remesh_0924171024_texture.glb",      "GlassBeakerModel"),
            ("Assets/ThirdParty/Meshy_AI_Dilute_HCl_Bottle_Rem_0924171040_texture.glb",     "DiluteHClBottleModel"),
            ("Assets/ThirdParty/Meshy_AI_Nail_Electrode_Remesh_0924171033_texture.glb",     "NailElectrodeModel"),
            ("Assets/ThirdParty/Meshy_AI_Graphite_Rod_Remesh_0924171144_texture.glb",       "GraphiteRodModel"),
            ("Assets/ThirdParty/Meshy_AI_Electrical_Switch_Rem_0924171056_texture.glb",     "ElectricalSwitchModel"),
            ("Assets/ThirdParty/Meshy_AI_Six_Volt_Battery_Reme_0924171048_texture.glb",     "SixVoltBatteryModel"),
            ("Assets/ThirdParty/Meshy_AI_Light_Bulb_Remesh_0924171130_texture.glb",         "LightBulbModel"),
            ("Assets/ThirdParty/Meshy_AI_Rubber_Cork_Stand_Rem_0924171108_texture.glb",     "RubberCorkStandModel"),
            ("Assets/ThirdParty/Meshy_AI_Plastic_Mug_Remesh_0924171219_texture.glb",        "PlasticMugModel"),
            ("Assets/ThirdParty/Meshy_AI_Switch_Battery_Box_Re_0924171225_texture.glb",     "SwitchBatteryBoxModel"),
            ("Assets/ThirdParty/Meshy_AI_Connecting_Wire_Red_R_0924171240_texture.glb",     "ConnectingWireRedModel"),
            ("Assets/ThirdParty/Meshy_AI_Rubber_Stopper_Remesh_0924171205_texture.glb",     "RubberStopperModel"),
            ("Assets/ThirdParty/Meshy_AI_Connecting_Wire_Remes_0924171232_texture.glb",     "ConnectingWireModel"),
        };

        private static void EnsurePrefabsReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            foreach (var item in Models)
            {
                if (!File.Exists($"Assets/Resources/Models/{item.prefabName}.prefab")) { BuildAllPrefabs(); return; }
            }
        }

        [MenuItem("Tools/New Experiments/Build Prefabs", priority = 51)]
        public static void BuildAllPrefabs()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== BUILDING ELECTROLYSIS / CONDUCTIVITY PREFABS ===");
            string outDir = "Assets/Resources/Models";
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            foreach (var item in Models)
            {
                if (!File.Exists(item.glb)) { sb.AppendLine("MISSING GLB: " + item.glb); continue; }

                AssetDatabase.ImportAsset(item.glb, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                var glbRoot = AssetDatabase.LoadAssetAtPath<GameObject>(item.glb);
                if (glbRoot == null) { sb.AppendLine("FAILED to load: " + item.glb); continue; }

                var tempGO = Object.Instantiate(glbRoot);
                tempGO.name = item.prefabName;
                string prefabPath = $"{outDir}/{item.prefabName}.prefab";
                var prefab = PrefabUtility.SaveAsPrefabAsset(tempGO, prefabPath);
                Object.DestroyImmediate(tempGO);

                sb.AppendLine(prefab != null ? "OK: " + prefabPath : "FAILED to save: " + prefabPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log(sb.ToString());
        }
    }
}
#endif
