// ============================================================
// HydrogenSceneSetup.cs  —  AR Virtual Lab Editor Script
// Creates Assets/Scenes/HydrogenLabScene.unity programmatically
// and adds it to EditorBuildSettings.
// Run via: Tools > Hydrogen Lab > Create HydrogenLabScene
// or Unity batch: -executeMethod HydrogenSceneSetup.CreateScene
// ============================================================
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

public static class HydrogenSceneSetup
{
    private const string SCENE_PATH = "Assets/Scenes/HydrogenLabScene.unity";
    private const string SCENE_NAME = "HydrogenLabScene";

    // ── Menu item ─────────────────────────────────────────────
    [MenuItem("Tools/Hydrogen Lab/Create HydrogenLabScene")]
    public static void CreateSceneFromMenu()
    {
        CreateScene();
        Debug.Log("[HydrogenSceneSetup] Done via menu.");
    }

    // ── Called by -executeMethod in batch mode ─────────────────
    public static void CreateScene()
    {
        // 1. Create a new empty scene
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // 2. Create the root GameObject that holds the HydrogenLabScene script
        var rootGO = new GameObject("HydrogenLabSceneRoot");
        rootGO.AddComponent<ARVirtualLab.Hydrogen.HydrogenLabScene>();

        Debug.Log("[HydrogenSceneSetup] Created HydrogenLabScene MonoBehaviour on root GO.");

        // 3. Save the scene
        bool saved = EditorSceneManager.SaveScene(scene, SCENE_PATH);
        if (!saved)
        {
            Debug.LogError($"[HydrogenSceneSetup] Failed to save scene at: {SCENE_PATH}");
            return;
        }
        Debug.Log($"[HydrogenSceneSetup] Saved scene to: {SCENE_PATH}");

        // 4. Add to build settings
        AddToBuildSettings();
    }

    // ── Separately callable to (re-)register the scene ────────
    [MenuItem("Tools/Hydrogen Lab/Add to Build Settings")]
    public static void AddToBuildSettingsMenu()
    {
        AddToBuildSettings();
    }

    public static void AddToBuildSettings()
    {
        var scenes = EditorBuildSettings.scenes;

        // Check if already registered
        foreach (var s in scenes)
        {
            if (s.path == SCENE_PATH)
            {
                Debug.Log($"[HydrogenSceneSetup] {SCENE_PATH} already in build settings.");
                return;
            }
        }

        // Append as a new enabled scene
        var updated = new EditorBuildSettingsScene[scenes.Length + 1];
        for (int i = 0; i < scenes.Length; i++) updated[i] = scenes[i];
        updated[scenes.Length] = new EditorBuildSettingsScene(SCENE_PATH, true);
        EditorBuildSettings.scenes = updated;

        Debug.Log($"[HydrogenSceneSetup] Added {SCENE_PATH} to build settings (index {scenes.Length}).");
        AssetDatabase.SaveAssets();
    }

    // ── Validate: only available when scene file doesn't exist yet
    [MenuItem("Tools/Hydrogen Lab/Create HydrogenLabScene", true)]
    private static bool ValidateCreateScene()
    {
        return !System.IO.File.Exists(System.IO.Path.Combine(
            Application.dataPath.Replace("Assets", ""), SCENE_PATH));
    }
}
