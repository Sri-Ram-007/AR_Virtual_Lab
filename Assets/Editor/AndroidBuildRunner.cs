#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    public static class AndroidBuildRunner
    {
        private const string ADB_PATH = @"C:\Program Files\Unity\Hub\Editor\6000.3.5f2\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe";

        [MenuItem("AR Lab/Build Android APK", priority = 100)]
        public static void BuildAndroid()
        {
            BuildAndRunInternal(false);
        }

        [MenuItem("AR Lab/Build And Run Android APK", priority = 101)]
        public static void BuildAndRunAndroid()
        {
            BuildAndRunInternal(true);
        }

        private static void BuildAndRunInternal(bool autoRun)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== STARTING ANDROID BUILD & RUN DIAGNOSTICS ===");

            // 1. Ensure build output directory
            string buildDir = "Builds/Android";
            if (!Directory.Exists(buildDir)) Directory.CreateDirectory(buildDir);
            string apkPath = Path.GetFullPath(Path.Combine(buildDir, "ARVirtualLab.apk"));

            // 2. Configure Build Target & Settings
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            ARPluginConfiguration.ConfigureXRAndARPlugins();

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;

            // 3. Configure Scenes
            // Every experiment scene must be listed, otherwise SceneManager.LoadScene fails on the device.
            string[] scenePaths =
            {
                "Assets/Scenes/HomeScene.unity",
                "Assets/Scenes/MagnesiumLabScene.unity",
                "Assets/Scenes/HydrogenLabScene.unity",
                "Assets/Scenes/HydrogenGasTestScene.unity",
                "Assets/Scenes/TextbookScanScene.unity"
            };
            var scenes = new EditorBuildSettingsScene[scenePaths.Length];
            for (int i = 0; i < scenePaths.Length; i++)
                scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            EditorBuildSettings.scenes = scenes;

            sb.AppendLine("Building Scenes: " + string.Join(", ", scenePaths));
            sb.AppendLine($"Output APK: {apkPath}");
            sb.AppendLine($"Min SDK: {PlayerSettings.Android.minSdkVersion}");
            sb.AppendLine($"Scripting Backend: {PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android)}");
            sb.AppendLine($"Target Architectures: {PlayerSettings.Android.targetArchitectures}");

            // 4. Run Build
            var buildOptions = new BuildPlayerOptions
            {
                scenes = scenePaths,
                locationPathName = apkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            sb.AppendLine($"\n=== BUILD RESULT: {summary.result} ===");
            sb.AppendLine($"Total Time: {summary.totalTime.TotalSeconds:F1}s");
            sb.AppendLine($"Total Size: {summary.totalSize / (1024 * 1024):F2} MB");
            sb.AppendLine($"Total Errors: {summary.totalErrors}");
            sb.AppendLine($"Total Warnings: {summary.totalWarnings}");

            if (summary.result == BuildResult.Succeeded)
            {
                sb.AppendLine("\nAndroid APK generated successfully!");
                if (autoRun)
                {
                    sb.AppendLine("\nDeploying and launching APK on connected Android device...");
                    DeployAndLaunch(apkPath, sb);
                }
            }
            else
            {
                sb.AppendLine("\n--- BUILD STEPS & ERRORS ---");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            sb.AppendLine($"[ERROR in {step.name}] {msg.content}");
                        }
                    }
                }
            }

            UnityEngine.Debug.Log(sb.ToString());
            File.WriteAllText("android_build_report.txt", sb.ToString());
        }

        private static void DeployAndLaunch(string apkPath, StringBuilder sb)
        {
            if (!File.Exists(ADB_PATH))
            {
                sb.AppendLine($"[DEPLOY ERROR] ADB not found at: {ADB_PATH}");
                return;
            }

            string pkgId = Application.identifier;
            sb.AppendLine($"Package Identifier: {pkgId}");

            // Install APK
            RunCmd(ADB_PATH, $"install -r \"{apkPath}\"", sb);

            // Launch main activity
            RunCmd(ADB_PATH, $"shell monkey -p {pkgId} -c android.intent.category.LAUNCHER 1", sb);
            sb.AppendLine("Launch command executed via monkey launcher.");
        }

        private static void RunCmd(string exe, string args, StringBuilder sb)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exe,
                    Arguments = args,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var proc = Process.Start(psi))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    string stderr = proc.StandardError.ReadToEnd();
                    proc.WaitForExit(30000);
                    if (!string.IsNullOrEmpty(stdout)) sb.AppendLine($"[ADB OUT] {stdout.Trim()}");
                    if (!string.IsNullOrEmpty(stderr)) sb.AppendLine($"[ADB ERR] {stderr.Trim()}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"[ADB EXCEPTION] {ex.Message}");
            }
        }
    }
}
#endif
