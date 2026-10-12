// ============================================================
// ModelTextureOptimizer.cs  —  AR Virtual Lab
// The Meshy .glb models each carry an uncompressed 4096x4096 texture (~85 MB in a player build). With 27 models that
// is several GB, which Gradle cannot package (heap exhaustion, >4 GB archive) and no phone could load.
// This tool gives every model prefab in Resources/Models its own copy of each large texture, shrunk to 1024x1024 and
// ASTC-compressed (~0.5 MB), plus a copy of the material that points at it. The original .glb assets are untouched, so
// the step can be repeated at any time (it skips anything already optimised).
//   Menu:  AR Lab > Optimize Model Textures        Batch:  -executeMethod ARVirtualLab.Editor.ModelTextureOptimizer.Run
// ============================================================
#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace ARVirtualLab.Editor
{
    public static class ModelTextureOptimizer
    {
        private const string BakeDir = "Assets/ModelBake";
        private const int TargetSize = 1024;

        [MenuItem("AR Lab/Optimize Model Textures", priority = 102)]
        public static void Run()
        {
            Directory.CreateDirectory(BakeDir + "/Textures");
            Directory.CreateDirectory(BakeDir + "/Materials");
            AssetDatabase.Refresh();

            var log = new StringBuilder();
            int texCount = 0, matCount = 0, prefabCount = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Resources/Models" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string pname = Path.GetFileNameWithoutExtension(path);
                var root = PrefabUtility.LoadPrefabContents(path);
                bool changed = false;
                int ri = 0;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        var m = mats[i];
                        if (m == null) continue;
                        string mpath = AssetDatabase.GetAssetPath(m);
                        if (mpath.StartsWith(BakeDir)) continue;                       // already optimised
                        Material nm = null;
                        foreach (var prop in m.GetTexturePropertyNames())
                        {
                            var t = m.GetTexture(prop) as Texture2D;
                            if (t == null) continue;
                            string tpath = AssetDatabase.GetAssetPath(t);
                            if (!tpath.EndsWith(".glb") || t.width <= TargetSize) continue;
                            var baked = Bake(t, pname + "_" + ri + "_" + i + "_" + prop, prop);
                            if (baked == null) continue;
                            if (nm == null) nm = new Material(m);
                            nm.SetTexture(prop, baked);
                            texCount++;
                        }
                        if (nm == null) continue;
                        string newPath = BakeDir + "/Materials/" + pname + "_" + ri + "_" + i + ".mat";
                        AssetDatabase.DeleteAsset(newPath);
                        AssetDatabase.CreateAsset(nm, newPath);
                        mats[i] = AssetDatabase.LoadAssetAtPath<Material>(newPath);
                        matCount++; changed = true;
                    }
                    if (changed) r.sharedMaterials = mats;
                    ri++;
                }
                if (changed) { PrefabUtility.SaveAsPrefabAsset(root, path); prefabCount++; log.AppendLine("optimised " + pname); }
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            log.AppendLine($"done: {prefabCount} prefabs, {matCount} materials, {texCount} textures baked to {TargetSize}px");
            Debug.Log("[ModelTextureOptimizer] " + log);
            File.WriteAllText("model_texture_optimizer.txt", log.ToString());
        }

        private static Texture2D Bake(Texture2D src, string name, string prop)
        {
            bool srgb = GraphicsFormatUtility.IsSRGBFormat(src.graphicsFormat);
            bool normal = prop.ToLower().Contains("bump") || prop.ToLower().Contains("normal");
            var rt = RenderTexture.GetTemporary(TargetSize, TargetSize, 0, RenderTextureFormat.ARGB32, srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var copy = new Texture2D(TargetSize, TargetSize, TextureFormat.RGBA32, false, !srgb);
            copy.ReadPixels(new Rect(0, 0, TargetSize, TargetSize), 0, 0);
            copy.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            string safe = name.Replace(" ", "_");
            string file = BakeDir + "/Textures/" + safe + ".png";
            File.WriteAllBytes(file, copy.EncodeToPNG());
            Object.DestroyImmediate(copy);
            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);

            var ti = (TextureImporter)AssetImporter.GetAtPath(file);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = srgb && !normal;
            ti.mipmapEnabled = true;
            ti.maxTextureSize = TargetSize;
            ti.textureCompression = TextureImporterCompression.Compressed;
            ti.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android", overridden = true, maxTextureSize = TargetSize,
                format = TextureImporterFormat.ASTC_6x6, textureCompression = TextureImporterCompression.Compressed
            });
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(file);
        }
    }
}
#endif
