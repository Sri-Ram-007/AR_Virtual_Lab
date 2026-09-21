#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ARVirtualLab.Standalone;

namespace ARVirtualLab.Editor
{
    public static class SceneHierarchyInspector
    {
        public static void InspectScene()
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("INSPECTING MAGNESIUM LAB SCENE HIERARCHY");
            sb.AppendLine("==================================================");

            string scenePath = "Assets/Scenes/MagnesiumLabScene.unity";
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Camera & Canvas
            Camera cam = Camera.main;
            sb.AppendLine($"Camera: Pos={cam.transform.position}, Rot={cam.transform.eulerAngles}, FOV={cam.fieldOfView}");

            CanvasScaler cs = Object.FindFirstObjectByType<CanvasScaler>();
            sb.AppendLine($"CanvasScaler: Mode={cs.uiScaleMode}, RefRes={cs.referenceResolution}, Match={cs.matchWidthOrHeight}");

            // 2. StandaloneVirtualLab
            GameObject labRoot = GameObject.Find("StandaloneVirtualLab");
            sb.AppendLine($"StandaloneVirtualLab: Pos={labRoot.transform.position}");

            StandaloneMagnesiumExperiment exp = labRoot.GetComponent<StandaloneMagnesiumExperiment>();
            sb.AppendLine($"Table: {exp.tableObject?.name} (Pos={exp.tableObject?.transform.localPosition}, Scale={exp.tableObject?.transform.localScale})");
            sb.AppendLine($"Burner: {exp.burnerObject?.name} (Pos={exp.burnerObject?.transform.localPosition}, Scale={exp.burnerObject?.transform.localScale})");
            sb.AppendLine($"FlamePoint: {exp.flamePoint?.name} (Pos={exp.flamePoint?.localPosition})");
            sb.AppendLine($"Flame: {exp.flameObject?.name}");
            sb.AppendLine($"HeatingZone: {exp.flameHeatZoneObject?.name} (Radius={exp.flameHeatZoneObject?.GetComponent<SphereCollider>()?.radius})");
            sb.AppendLine($"Tongs: {exp.tongsObject?.name} (Pos={exp.tongsObject?.transform.localPosition})");
            sb.AppendLine($"GripPoint: {exp.tongsGripPoint?.name} (Pos={exp.tongsGripPoint?.localPosition})");
            sb.AppendLine($"WatchGlass: {exp.watchGlassObject?.name} (Pos={exp.watchGlassObject?.transform.localPosition})");
            sb.AppendLine($"CollectionPoint: {exp.watchGlassCollectionPoint?.name} (Pos={exp.watchGlassCollectionPoint?.localPosition})");
            sb.AppendLine($"Sandpaper: {exp.sandpaperObject?.name} (Pos={exp.sandpaperObject?.transform.localPosition})");
            sb.AppendLine($"MagnesiumRibbon: {exp.ribbonObject?.name} (Pos={exp.ribbonObject?.transform.localPosition})");
            sb.AppendLine($"MgOResidue: {exp.mgoResidueObject?.name}");
            sb.AppendLine($"MgOPowder: {exp.mgoInGlassObject?.name}");
            sb.AppendLine($"Particles: {exp.burnParticles?.name}");
            sb.AppendLine($"Light: {exp.burnLight?.name}");

            // 3. UI Controller Panels
            StandaloneUIController ui = exp.uiController;
            sb.AppendLine($"UI IntroPanel: {ui.introPanel?.name}, ExpPanel: {ui.experimentPanel?.name}");
            sb.AppendLine($"Observation: {ui.observationPanel?.name}, Equation: {ui.equationPanel?.name}, Result: {ui.resultPanel?.name}, Safety: {ui.safetyPanel?.name}, Instructions: {ui.instructionsPanel?.name}");

            sb.AppendLine("==================================================");
            sb.AppendLine("SCENE HIERARCHY INSPECTION FINISHED SUCCESSFULLY");
            sb.AppendLine("==================================================");

            string outPath = Path.Combine(Application.dataPath, "../scene_inspection_result.txt");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("Scene hierarchy inspection written to " + outPath);
        }
    }
}
#endif
