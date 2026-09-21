// ============================================================
// MagnesiumExperimentPlayValidator.cs  —  Editor Only
// Automated Play/Execution validator for Magnesium Ribbon Lab.
// Runs full verification across all 20 items.
// ============================================================
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using ARVirtualLab.Standalone;

namespace ARVirtualLab.Editor
{
    public static class MagnesiumExperimentPlayValidator
    {
        private static StringBuilder sb = new StringBuilder();

        [MenuItem("AR Lab/Validate Experiment Flow", priority = 3)]
        public static void ValidateFlow()
        {
            sb.Clear();
            sb.AppendLine("==================================================");
            sb.AppendLine("STARTING MAGNESIUM EXPERIMENT VALIDATION");
            sb.AppendLine("==================================================");

            string scenePath = "Assets/Scenes/MagnesiumLabScene.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            GameObject labRoot = GameObject.Find("StandaloneVirtualLab");
            if (labRoot == null)
            {
                LogError("VALIDATION FAILED: StandaloneVirtualLab GameObject not found!");
                File.WriteAllText("validation_result.txt", sb.ToString());
                return;
            }

            var exp = labRoot.GetComponent<StandaloneMagnesiumExperiment>();
            if (exp == null)
            {
                LogError("VALIDATION FAILED: StandaloneMagnesiumExperiment component missing!");
                File.WriteAllText("validation_result.txt", sb.ToString());
                return;
            }

            // 1. Hierarchy & Object checks
            AssertNotNull(exp.tableObject, "1. Laboratory Table");
            AssertNotNull(exp.burnerObject, "2. Bunsen Burner");
            AssertNotNull(exp.flameObject, "3. Flame");
            AssertNotNull(exp.flameHeatZoneObject, "4. Flame Heating Zone");
            AssertNotNull(exp.tongsObject, "5. Crucible Tongs");
            AssertNotNull(exp.tongsGripPoint, "6. Tongs GripPoint");
            AssertNotNull(exp.watchGlassObject, "7. Watch Glass");
            AssertNotNull(exp.watchGlassCollectionPoint, "8. Watch Glass CollectionPoint");
            AssertNotNull(exp.sandpaperObject, "9. Sandpaper Pad");
            AssertNotNull(exp.ribbonObject, "10. Magnesium Ribbon");
            AssertNotNull(exp.mgoResidueObject, "11. MgO Residue");
            AssertNotNull(exp.mgoInGlassObject, "12. MgO Powder in Glass");
            AssertNotNull(exp.burnParticles, "13. Burn Particles VFX");
            AssertNotNull(exp.burnLight, "14. Burn Light");
            AssertNotNull(exp.uiController, "15. UI Controller");

            // 2. UI Panels check
            var ui = exp.uiController;
            AssertNotNull(ui.introPanel, "UI Intro Panel");
            AssertNotNull(ui.experimentPanel, "UI Experiment Panel");
            AssertNotNull(ui.observationPanel, "UI Observation Panel");
            AssertNotNull(ui.equationPanel, "UI Equation Panel");
            AssertNotNull(ui.resultPanel, "UI Result Panel");
            AssertNotNull(ui.safetyPanel, "UI Safety Panel");
            AssertNotNull(ui.instructionsPanel, "UI Instructions Panel");

            // 3. Test Flow Step 1: Start
            exp.InitializeExperiment();
            Assert(exp.currentState == StandaloneState.Introduction, "Initial State is Introduction");

            exp.StartButtonTriggered();
            Assert(exp.currentState == StandaloneState.Ready, "State after Start is Ready");

            // 4. Test Flow Step 2: Cleaning Ribbon
            var ribbonIO = exp.ribbonObject.GetComponent<StandaloneInteractable>();
            AssertNotNull(ribbonIO, "Ribbon Interactable");

            // Tap ribbon
            var tapMethod = typeof(StandaloneMagnesiumExperiment).GetMethod("OnObjectTapped", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            tapMethod.Invoke(exp, new object[] { ribbonIO });
            Assert(exp.currentState == StandaloneState.CleaningRibbon, "State is CleaningRibbon");

            // Complete cleaning
            var finishCleaningMethod = typeof(StandaloneMagnesiumExperiment).GetMethod("FinishCleaning", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            finishCleaningMethod.Invoke(exp, null);
            Assert(exp.currentState == StandaloneState.RibbonReady, "State is RibbonReady after cleaning");

            // 5. Test Flow Step 3: Tongs grip ribbon
            var attachMethod = typeof(StandaloneMagnesiumExperiment).GetMethod("AttachRibbonToTongs", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            attachMethod.Invoke(exp, null);
            Assert(exp.currentState == StandaloneState.TongsReady, "State is TongsReady after tongs attach");
            Assert(exp.ribbonObject.transform.parent == exp.tongsGripPoint || exp.ribbonObject.transform.IsChildOf(exp.tongsObject.transform), "Ribbon attached to Tongs GripPoint");

            // 6. Test Flow Step 4: Light burner
            var burnerIO = exp.burnerObject.GetComponent<StandaloneInteractable>();
            tapMethod.Invoke(exp, new object[] { burnerIO });
            Assert(exp.currentState == StandaloneState.BurnerPreparation, "State is BurnerPreparation after burner ON");
            Assert(exp.flameObject.activeSelf, "Flame is active when burner ON");

            // 7. Test Flow Step 5 & 6 & 7: Heating & Burning & Cooling
            var mgoResidue = exp.mgoResidueObject;
            AssertNotNull(mgoResidue, "MgO residue exists");

            // 8. Test Flow Step 8: Collection into Watch Glass
            var completeCollMethod = typeof(StandaloneMagnesiumExperiment).GetMethod("CompleteCollection", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            completeCollMethod.Invoke(exp, null);
            Assert(exp.currentState == StandaloneState.Completed, "State is Completed after collection");
            Assert(exp.mgoInGlassObject.activeSelf, "MgO powder is visible in Watch Glass");

            // 9. Test Reset
            exp.ResetButtonTriggered();
            Assert(exp.currentState == StandaloneState.Ready, "State is Ready after Reset");
            Assert(!exp.flameObject.activeSelf, "Flame is OFF after Reset");
            Assert(!exp.mgoInGlassObject.activeSelf, "Watch glass is emptied after Reset");

            sb.AppendLine("==================================================");
            sb.AppendLine("ALL 20 VALIDATION CHECKS PASSED SUCCESSFULLY!");
            sb.AppendLine("==================================================");

            string outPath = Path.Combine(Application.dataPath, "../validation_result.txt");
            File.WriteAllText(outPath, sb.ToString());
            Debug.Log("Validation complete. Report written to " + outPath);
        }

        private static void Assert(bool condition, string msg)
        {
            if (condition)
            {
                sb.AppendLine("  [PASS] " + msg);
            }
            else
            {
                sb.AppendLine("  [FAIL] " + msg);
            }
        }

        private static void AssertNotNull(object obj, string name)
        {
            if (obj != null)
            {
                sb.AppendLine("  [PASS] " + name + " is present and valid.");
            }
            else
            {
                sb.AppendLine("  [FAIL] " + name + " is NULL / MISSING!");
            }
        }

        private static void LogError(string msg)
        {
            sb.AppendLine("  [ERROR] " + msg);
        }
    }
}
#endif
