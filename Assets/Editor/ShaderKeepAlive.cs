// ============================================================
// ShaderKeepAlive.cs  —  AR Virtual Lab Editor Script
// The experiment scenes create their materials in code with Shader.Find("Universal Render Pipeline/Lit") etc.
// A phone build only contains shaders (and shader variants) that some asset in the build references, so without
// help those lookups return null and the scene stops at start-up (blank grey screen).
//
// This creates a handful of tiny materials in Assets/Resources/KeepAlive. Anything in a Resources folder is always
// packed into the build, and each material switches on the same keywords the code switches on at runtime
// (transparent surface, emission), so those variants are kept too.
// Menu: AR Lab > Rebuild Shader Keep-Alive Materials
// ============================================================
#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    [InitializeOnLoad]
    public static class ShaderKeepAlive
    {
        private const string DIR = "Assets/Resources/KeepAlive";

        static ShaderKeepAlive()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
                if (!File.Exists(DIR + "/KeepAlive_Lit_Transparent.mat")) Rebuild();
            };
        }

        [MenuItem("AR Lab/Rebuild Shader Keep-Alive Materials", priority = 5)]
        public static void Rebuild()
        {
            Directory.CreateDirectory(DIR);
            Make("KeepAlive_Lit_Opaque",          "Universal Render Pipeline/Lit",             false, false);
            Make("KeepAlive_Lit_Transparent",     "Universal Render Pipeline/Lit",             true,  false);
            Make("KeepAlive_Lit_Emission",        "Universal Render Pipeline/Lit",             false, true);
            Make("KeepAlive_Unlit_Opaque",        "Universal Render Pipeline/Unlit",           false, false);
            Make("KeepAlive_Unlit_Transparent",   "Universal Render Pipeline/Unlit",           true,  false);
            Make("KeepAlive_ParticlesUnlit",      "Universal Render Pipeline/Particles/Unlit", true,  false);
            Make("KeepAlive_SimpleLit",           "Universal Render Pipeline/Simple Lit",      false, false);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[ShaderKeepAlive] Keep-alive materials written to " + DIR);
        }

        private static void Make(string name, string shaderName, bool transparent, bool emission)
        {
            var shader = Shader.Find(shaderName);
            if (shader == null) { Debug.LogWarning("[ShaderKeepAlive] shader not found: " + shaderName); return; }

            string path = DIR + "/" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = mat == null;
            if (isNew) mat = new Material(shader);
            mat.shader = shader;

            if (transparent)
            {
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
                if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                mat.SetOverrideTag("RenderType", "Transparent");
            }
            if (emission)
            {
                mat.EnableKeyword("_EMISSION");
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", new Color(0.2f, 0.2f, 0.2f));
            }

            if (isNew) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);

            // EnableKeyword alone is not always written to the asset; store the keywords explicitly.
            var so = new SerializedObject(mat);
            var kw = so.FindProperty("m_ValidKeywords");
            foreach (var k in new[] { transparent ? "_SURFACE_TYPE_TRANSPARENT" : null, emission ? "_EMISSION" : null })
            {
                if (k == null) continue;
                bool has = false;
                for (int i = 0; i < kw.arraySize; i++) if (kw.GetArrayElementAtIndex(i).stringValue == k) has = true;
                if (!has) { kw.arraySize++; kw.GetArrayElementAtIndex(kw.arraySize - 1).stringValue = k; }
            }
            so.ApplyModifiedProperties();
        }
    }
}
#endif
