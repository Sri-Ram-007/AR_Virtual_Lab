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
        // ── App identity, applied on every build ──────────────
        // The package name identifies the app on a phone: an APK with a different one installs as a separate app,
        // and once the app is on the Play Store it can never change. Raise VersionCode for every APK handed out,
        // otherwise Android refuses to install it over the previous one.
        public const string AppName     = "EduAR";
        public const string CompanyName = "EduAR";
        public const string PackageName = "com.eduar.virtuallab";
        public const string Version     = "0.3.2";
        public const int    VersionCode = 5;

        // adb from the Android SDK Unity is configured with, so Build And Run works on Windows and macOS alike
        private static string ADB_PATH
        {
            get
            {
                string sdk = UnityEditor.Android.AndroidExternalToolsSettings.sdkRootPath;
                string exe = Application.platform == RuntimePlatform.WindowsEditor ? "adb.exe" : "adb";
                return string.IsNullOrEmpty(sdk) ? exe : Path.Combine(sdk, "platform-tools", exe);
            }
        }

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

        // The APK handed to other people must not carry the developer's Gemini key (Assets/Resources/AIConfig.json): anyone
        // can unpack an APK. This build sets the file aside, builds, and puts it back, so the AI Mentor in the shared app
        // answers from its built-in English and Telugu answers. The file is kept in Library/ meanwhile, never deleted.
        [MenuItem("AR Lab/Build Android APK for sharing (no AI key)", priority = 102)]
        public static void BuildForSharing()
        {
            const string cfg = "Assets/Resources/AIConfig.json";
            const string held = "Library/AIConfig.json.held";
            bool moved = false;
            try
            {
                if (File.Exists(cfg))
                {
                    File.Copy(cfg, held, true);
                    if (File.Exists(cfg + ".meta")) File.Copy(cfg + ".meta", held + ".meta", true);
                    AssetDatabase.DeleteAsset(cfg);
                    moved = true;
                }
                BuildAndRunInternal(false, "-share");
            }
            finally
            {
                if (moved)
                {
                    File.Copy(held, cfg, true);
                    if (File.Exists(held + ".meta")) File.Copy(held + ".meta", cfg + ".meta", true);
                    AssetDatabase.Refresh();
                    UnityEngine.Debug.Log("[Build] AIConfig.json restored after the sharing build");
                }
            }
        }

        private static void BuildAndRunInternal(bool autoRun, string nameSuffix = "")
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== STARTING ANDROID BUILD & RUN DIAGNOSTICS ===");

            // 1. Ensure build output directory
            string buildDir = "Builds/Android";
            if (!Directory.Exists(buildDir)) Directory.CreateDirectory(buildDir);
            string apkPath = Path.GetFullPath(Path.Combine(buildDir, AppName + "-" + Version + nameSuffix + ".apk"));

            // 2. Configure Build Target & Settings
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

            PlayerSettings.productName = AppName;
            PlayerSettings.companyName = CompanyName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, PackageName);
            PlayerSettings.bundleVersion = Version;
            PlayerSettings.Android.bundleVersionCode = VersionCode;
            ApplyIcons(sb);
            ApplySigning(sb);

            ARPluginConfiguration.ConfigureXRAndARPlugins();

            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.forceInternetPermission = true;      // the AI mentor talks to the internet
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            // Native symbols stay on this computer (next to the APK): they turn a tester's crash log into readable stack traces
            UnityEditor.Android.UserBuildSettings.DebugSymbols.level = Unity.Android.Types.DebugSymbolLevel.SymbolTable;

            // 3. Configure Scenes
            // Every experiment scene must be listed, otherwise SceneManager.LoadScene fails on the device.
            string[] scenePaths =
            {
                "Assets/Scenes/HomeScene.unity",
                "Assets/Scenes/MagnesiumLabScene.unity",
                "Assets/Scenes/HydrogenLabScene.unity",
                "Assets/Scenes/HydrogenGasTestScene.unity",
                "Assets/Scenes/ElectrolysisScene.unity",
                "Assets/Scenes/AcidConductivityScene.unity",
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

        // Icon in Assets/Art/Icon: adaptive (background + foreground inside the safe zone). eduar_icon.png and
        // eduar_icon_round.png are the same artwork flattened, for store listings and sharing.
        private static void ApplyIcons(StringBuilder sb)
        {
            const string dir = "Assets/Art/Icon/";
            var background = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "eduar_icon_background.png");
            var foreground = AssetDatabase.LoadAssetAtPath<Texture2D>(dir + "eduar_icon_foreground.png");
            if (background == null || foreground == null)
            {
                sb.AppendLine("[ICON] icon images missing in " + dir + ", keeping the current icons");
                return;
            }
            var android = NamedBuildTarget.Android;
            void Fill(PlatformIconKind kind, params Texture2D[] layers)
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var icon in icons) icon.SetTextures(layers);
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            // min SDK 26 (Android 8): every supported phone uses the adaptive icon, Unity marks round/legacy obsolete
            Fill(UnityEditor.Android.AndroidPlatformIconKind.Adaptive, background, foreground);
            sb.AppendLine("Icons: adaptive from " + dir);
        }

        // ── Signing ───────────────────────────────────────────
        // Every computer has its own debug key, and Android refuses to install an APK signed with a different key over
        // the one on the phone. Builds from different computers become interchangeable when they share one keystore.
        // The keystore and its passwords stay OUT of git (Signing/ is ignored): pass them on privately.
        //   Signing/eduar-test.keystore
        //   Signing/signing.local.json  {"keystorePass": "...", "keyAlias": "eduar", "keyAliasPass": "..."}
        private const string KeystorePath = "Signing/eduar-test.keystore";
        private const string SigningConfigPath = "Signing/signing.local.json";

        [Serializable]
        private class SigningConfig { public string keystorePass; public string keyAlias; public string keyAliasPass; }

        private static void ApplySigning(StringBuilder sb)
        {
            if (!File.Exists(KeystorePath) || !File.Exists(SigningConfigPath))
            {
                PlayerSettings.Android.useCustomKeystore = false;
                sb.AppendLine("[SIGNING] no shared keystore in Signing/: signed with this computer's debug key " +
                              "(phones with a build from another computer must uninstall it first)");
                return;
            }
            var cfg = JsonUtility.FromJson<SigningConfig>(File.ReadAllText(SigningConfigPath));
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(KeystorePath);
            PlayerSettings.Android.keystorePass = cfg.keystorePass;
            PlayerSettings.Android.keyaliasName = cfg.keyAlias;
            PlayerSettings.Android.keyaliasPass = cfg.keyAliasPass;
            sb.AppendLine("Signing: shared keystore " + KeystorePath + " (alias " + cfg.keyAlias + ")");
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
