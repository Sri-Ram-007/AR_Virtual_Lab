// ============================================================
// AppNavigation.cs
// Central navigation controller for AR Virtual Chemistry Lab.
// Controls transitions between HomeScene and experiment scenes.
// ============================================================
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace ARVirtualLab.AppShell
{
    public static class AppNavigation
    {
        public const string HOME_SCENE      = "HomeScene";
        public const string LAB_SCENE       = "MagnesiumLabScene";
        public const string HYDROGEN_SCENE          = "HydrogenLabScene";
        public const string HYDROGEN_GAS_TEST_SCENE = "HydrogenGasTestScene";
        public const string SCAN_SCENE              = "TextbookScanScene";

        /// <summary>
        /// When true, MagnesiumLabScene starts directly in AR mode upon launch.
        /// </summary>
        public static bool LaunchDirectlyToAR = false;

        /// <summary>
        /// When true, MagnesiumLabScene starts directly at Step 1 (bypassing the intro card).
        /// </summary>
        public static bool LaunchDirectlyToExperiment = true;

        /// <summary>
        /// Navigates back to the Home Screen.
        /// </summary>
        public static void GoToHome()
        {
            LaunchDirectlyToAR = false;
            LaunchDirectlyToExperiment = true;
            Debug.Log("[AppNavigation] Returning to Home Screen");
            SceneManager.LoadScene(HOME_SCENE);
        }

        /// <summary>Opens the textbook scanner.</summary>
        public static void GoToScan()
        {
            LaunchDirectlyToAR = false;
            SceneManager.LoadScene(SCAN_SCENE);
        }

        /// <summary>Opens any experiment scene directly at Step 1 (used by the textbook scanner and the Home list).</summary>
        public static void LaunchScene(string sceneName, bool arMode = false)
        {
            LaunchDirectlyToAR = arMode;
            LaunchDirectlyToExperiment = true;
            Debug.Log($"[AppNavigation] Launching {sceneName} (AR Mode: {arMode})");
            SceneManager.LoadScene(sceneName);
        }

        /// <summary>
        /// Launches the Magnesium Ribbon experiment.
        /// </summary>
        /// <param name="arMode">If true, launches directly into AR mode.</param>
        public static void LaunchMagnesiumExperiment(bool arMode = false)
        {
            LaunchDirectlyToAR = arMode;
            LaunchDirectlyToExperiment = true;
            Debug.Log($"[AppNavigation] Launching Magnesium Lab (AR Mode: {arMode})");
            SceneManager.LoadScene(LAB_SCENE);
        }

        /// <summary>
        /// Launches the Hydrogen Gas from Zinc experiment.
        /// </summary>
        /// <param name="arMode">If true, launches directly into AR mode.</param>
        public static void LaunchHydrogenExperiment(bool arMode = false)
        {
            LaunchDirectlyToAR = arMode;
            LaunchDirectlyToExperiment = true;
            Debug.Log($"[AppNavigation] Launching Hydrogen Lab (AR Mode: {arMode})");
            SceneManager.LoadScene(HYDROGEN_SCENE);
        }

        /// <summary>
        /// Launches the new Hydrogen Gas Test (Reaction of Zinc with Dilute Sulphuric Acid and Testing of Hydrogen Gas).
        /// </summary>
        /// <param name="arMode">If true, launches directly into AR mode.</param>
        public static void LaunchHydrogenGasTestExperiment(bool arMode = false)
        {
            LaunchDirectlyToAR = arMode;
            LaunchDirectlyToExperiment = true;
            Debug.Log($"[AppNavigation] Launching Hydrogen Gas Test Lab (AR Mode: {arMode})");
            SceneManager.LoadScene(HYDROGEN_GAS_TEST_SCENE);
        }


        /// <summary>
        /// Asynchronously loads a scene with progress reporting.
        /// </summary>
        public static IEnumerator LoadSceneAsyncRoutine(string sceneName, Action<float> onProgress, Action onComplete = null)
        {
            Debug.Log($"[AppNavigation] Loading scene asynchronously: {sceneName}");
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[AppNavigation] Failed to load scene: {sceneName}");
                yield break;
            }

            op.allowSceneActivation = true;

            while (!op.isDone)
            {
                float progress = Mathf.Clamp01(op.progress / 0.9f);
                onProgress?.Invoke(progress);
                yield return null;
            }

            onProgress?.Invoke(1f);
            onComplete?.Invoke();
        }
    }
}
