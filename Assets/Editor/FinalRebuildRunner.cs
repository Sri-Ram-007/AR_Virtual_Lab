#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARVirtualLab.Editor
{
    /// <summary>
    /// Final full rebuild: generates models, creates materials, builds scene, saves.
    /// Called via: Unity.exe -batchmode -nographics -quit -projectPath PATH -executeMethod ARVirtualLab.Editor.FinalRebuildRunner.Run
    /// </summary>
    public static class FinalRebuildRunner
    {
        public static void Run()
        {
            string logPath = Path.Combine(Application.dataPath, "../final_rebuild_log.txt");
            var log = new System.Text.StringBuilder();
            log.AppendLine("=== FINAL REBUILD START: " + System.DateTime.Now + " ===");

            try
            {
                // Step 1: Generate 3D Models
                log.AppendLine("[Step 1] Generating realistic 3D OBJ models & URP materials...");
                LabAssetGenerator.GenerateAllAssets();
                log.AppendLine("[Step 1] DONE");

                // Step 2: Create fresh scene
                log.AppendLine("[Step 2] Creating clean scene...");
                Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                log.AppendLine("[Step 2] DONE");

                // Step 3: Full standalone lab setup
                log.AppendLine("[Step 3] Running StandaloneSetupHelper.SetupStandaloneInternal()...");
                ARVirtualLab.Editor.StandaloneSetupHelper.SetupStandaloneInternal();
                log.AppendLine("[Step 3] DONE");

                // Step 4: Save scene
                string scenePath = "Assets/Scenes/MagnesiumLabScene.unity";
                bool saved = EditorSceneManager.SaveScene(scene, scenePath);
                log.AppendLine("[Step 4] Scene save: " + (saved ? "SUCCESS -> " + scenePath : "FAILED"));

                // Step 5: Register in build settings
                log.AppendLine("[Step 5] Updating EditorBuildSettings...");
                EditorBuildSettings.scenes = new EditorBuildSettingsScene[]
                {
                    new EditorBuildSettingsScene("Assets/Scenes/MagnesiumLabScene.unity", true),
                    new EditorBuildSettingsScene("Assets/ARExperimentScene.unity", true),
                    new EditorBuildSettingsScene("Assets/Scenes/HomeScene.unity", false),
                    new EditorBuildSettingsScene("Assets/PhysicsScene.unity", false),
                };
                log.AppendLine("[Step 5] DONE");

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                // Step 6: Verify key objects in the saved scene
                log.AppendLine("[Step 6] Verifying saved scene objects...");
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                GameObject labRoot = GameObject.Find("StandaloneVirtualLab");
                log.AppendLine($"  StandaloneVirtualLab: {(labRoot != null ? "FOUND" : "MISSING")}");

                if (labRoot != null)
                {
                    var exp = labRoot.GetComponent<ARVirtualLab.Standalone.StandaloneMagnesiumExperiment>();
                    log.AppendLine($"  StandaloneMagnesiumExperiment: {(exp != null ? "FOUND" : "MISSING")}");
                    if (exp != null)
                    {
                        log.AppendLine($"  tableObject:      {(exp.tableObject != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  burnerObject:     {(exp.burnerObject != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  flameObject:      {(exp.flameObject != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  tongsObject:      {(exp.tongsObject != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  watchGlassObject: {(exp.watchGlassObject != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  sandpaperObject:  {(exp.sandpaperObject != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  ribbonObject:     {(exp.ribbonObject != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  burnParticles:    {(exp.burnParticles != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  burnLight:        {(exp.burnLight != null ? "OK" : "MISSING")}");
                        log.AppendLine($"  uiController:     {(exp.uiController != null ? "OK" : "MISSING")}");
                    }
                }

                // Step 7: Count generated models
                string modelsDir = Path.Combine(Application.dataPath, "Models");
                string[] objFiles = Directory.GetFiles(modelsDir, "*.obj");
                log.AppendLine($"[Step 7] OBJ models in Assets/Models: {objFiles.Length}");
                foreach (var f in objFiles)
                    log.AppendLine($"  {Path.GetFileName(f)}");

                // Step 8: Count materials
                string matsDir = Path.Combine(Application.dataPath, "Materials");
                string[] matFiles = Directory.GetFiles(matsDir, "Mat_*.mat");
                log.AppendLine($"[Step 8] Lab URP materials in Assets/Materials: {matFiles.Length}");
                foreach (var f in matFiles)
                    log.AppendLine($"  {Path.GetFileName(f)}");

                log.AppendLine();
                log.AppendLine("=== FINAL REBUILD COMPLETE SUCCESSFULLY ===");
            }
            catch (System.Exception ex)
            {
                log.AppendLine("[ERROR] " + ex);
                Debug.LogError("FinalRebuildRunner ERROR: " + ex);
            }
            finally
            {
                File.WriteAllText(logPath, log.ToString());
                Debug.Log("FinalRebuildRunner log written to: " + logPath);
            }
        }
    }
}
#endif
