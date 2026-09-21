#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Rendering.Universal;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.Management;

namespace ARVirtualLab.Editor
{
    public static class ARPluginConfiguration
    {
        [MenuItem("AR Lab/Configure XR & AR Plugins", priority = 50)]
        public static void ConfigureXRAndARPlugins()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== CONFIGURING XR PLUG-IN MANAGEMENT & AR FOUNDATION ===");

            // 1. Configure XR Plug-in Management for Android
            try
            {
                XRGeneralSettingsPerBuildTarget generalSettingsPerTarget = null;
                EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out generalSettingsPerTarget);
                if (generalSettingsPerTarget == null)
                {
                    var assets = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget");
                    if (assets.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(assets[0]);
                        generalSettingsPerTarget = AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(path);
                        if (generalSettingsPerTarget != null)
                        {
                            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, generalSettingsPerTarget, true);
                        }
                    }
                    else
                    {
                        generalSettingsPerTarget = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                        string dir = "Assets/XR";
                        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                        string assetPath = Path.Combine(dir, "XRGeneralSettingsPerBuildTarget.asset");
                        AssetDatabase.CreateAsset(generalSettingsPerTarget, assetPath);
                        EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, generalSettingsPerTarget, true);
                    }
                }
                if (generalSettingsPerTarget == null)
                {
                    sb.AppendLine("[ERROR] Could not get or create XRGeneralSettingsPerBuildTarget.");
                }
                else
                {
                    if (!generalSettingsPerTarget.HasSettingsForBuildTarget(BuildTargetGroup.Android))
                    {
                        generalSettingsPerTarget.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
                        sb.AppendLine("Created default XRGeneralSettings for Android.");
                    }

                    var generalSettings = generalSettingsPerTarget.SettingsForBuildTarget(BuildTargetGroup.Android);
                    if (generalSettings == null)
                    {
                        sb.AppendLine("[ERROR] XRGeneralSettings for Android is null.");
                    }
                    else
                    {
                        var managerSettings = generalSettings.Manager;
                        if (managerSettings == null)
                        {
                            generalSettingsPerTarget.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
                            managerSettings = generalSettings.Manager;
                            sb.AppendLine("Created default XRManagerSettings for Android.");
                        }

                        if (managerSettings != null)
                        {
                            string arcoreLoaderTypeName = "UnityEngine.XR.ARCore.ARCoreLoader";
                            bool assigned = XRPackageMetadataStore.AssignLoader(managerSettings, arcoreLoaderTypeName, BuildTargetGroup.Android);
                            sb.AppendLine($"Assigned ARCoreLoader to Android XRManagerSettings: {assigned}");

                            EditorUtility.SetDirty(managerSettings);
                        }

                        generalSettings.InitManagerOnStart = true;
                        EditorUtility.SetDirty(generalSettings);
                    }

                    EditorUtility.SetDirty(generalSettingsPerTarget);
                    AssetDatabase.SaveAssets();
                    sb.AppendLine("XR Plug-in Management settings saved successfully.");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[XR Management Exception] {ex.Message}\n{ex.StackTrace}");
            }

            // 2. Ensure ARBackgroundRendererFeature is present in Universal Renderer Data
            try
            {
                string[] rendererPaths = new[]
                {
                    "Assets/Settings/Mobile_Renderer.asset",
                    "Assets/Settings/PC_Renderer.asset"
                };

                foreach (var path in rendererPaths)
                {
                    if (!File.Exists(path)) continue;

                    var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
                    if (rendererData == null)
                    {
                        sb.AppendLine($"[WARN] Could not load UniversalRendererData at {path}");
                        continue;
                    }

                    bool hasARFeature = false;
                    foreach (var f in rendererData.rendererFeatures)
                    {
                        if (f != null && f.GetType().Name == nameof(ARBackgroundRendererFeature))
                        {
                            hasARFeature = true;
                            break;
                        }
                    }

                    if (!hasARFeature)
                    {
                        var feature = ScriptableObject.CreateInstance<ARBackgroundRendererFeature>();
                        feature.name = "ARBackgroundRendererFeature";
                        AssetDatabase.AddObjectToAsset(feature, rendererData);
                        rendererData.rendererFeatures.Add(feature);
                        rendererData.SetDirty();
                        EditorUtility.SetDirty(rendererData);
                        sb.AppendLine($"Added ARBackgroundRendererFeature to {path}");
                    }
                    else
                    {
                        sb.AppendLine($"ARBackgroundRendererFeature already present in {path}");
                    }
                }

                AssetDatabase.SaveAssets();
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[Renderer Feature Exception] {ex.Message}\n{ex.StackTrace}");
            }

            // 3. Android Player Settings & Graphics API (ARCore requires OpenGLES3)
            try
            {
                PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
                PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
                PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
                sb.AppendLine("Android PlayerSettings configured: MinSDK=26, Backend=IL2CPP, Arch=ARM64, GraphicsAPI=OpenGLES3.");
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[PlayerSettings Exception] {ex.Message}");
            }

            sb.AppendLine("=== CONFIGURATION COMPLETE ===");
            Debug.Log(sb.ToString());
            File.WriteAllText("ar_plugin_config_report.txt", sb.ToString());
        }
    }
}
#endif
