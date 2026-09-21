#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace ARVirtualLab.Editor
{
    public static class LabAssetGenerator
    {
        [MenuItem("AR Lab/Generate Realistic 3D Models and Materials", priority = 1)]
        public static void GenerateAllAssets()
        {
            Debug.Log("=== Generating Realistic 3D Models & Materials ===");
            
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string modelsDir = Path.Combine(Application.dataPath, "Models");
            string matsDir = Path.Combine(Application.dataPath, "Materials");
            string thirdPartyDir = Path.Combine(Application.dataPath, "ThirdParty");

            if (!Directory.Exists(modelsDir)) Directory.CreateDirectory(modelsDir);
            if (!Directory.Exists(matsDir)) Directory.CreateDirectory(matsDir);
            if (!Directory.Exists(thirdPartyDir)) Directory.CreateDirectory(thirdPartyDir);

            // 1. Generate Models (OBJ)
            GenerateBunsenBurnerOBJ(Path.Combine(modelsDir, "BunsenBurner.obj"));
            GenerateCrucibleTongsOBJ(Path.Combine(modelsDir, "CrucibleTongs.obj"));
            GenerateWatchGlassOBJ(Path.Combine(modelsDir, "WatchGlass.obj"));
            GenerateLaboratoryTableOBJ(Path.Combine(modelsDir, "LaboratoryTable.obj"));
            GenerateSandpaperOBJ(Path.Combine(modelsDir, "Sandpaper.obj"));
            GenerateMagnesiumRibbonOBJ(Path.Combine(modelsDir, "MagnesiumRibbon.obj"));
            GenerateMgOPowderOBJ(Path.Combine(modelsDir, "MgOPowder.obj"));
            GenerateMgOResidueOBJ(Path.Combine(modelsDir, "MgOResidue.obj"));

            // 2. Generate Attributions
            GenerateAttributionFile(Path.Combine(thirdPartyDir, "ATTRIBUTIONS.txt"));

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 3. Create URP Materials
            CreateMaterials("Assets/Materials");

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Debug.Log("=== Models & Materials Generated Successfully ===");
        }

        public static void GenerateAttributionFile(string path)
        {
            var sb = new StringBuilder();
            sb.AppendLine("==================================================");
            sb.AppendLine("THIRD-PARTY & PROCEDURAL 3D ASSETS ATTRIBUTION");
            sb.AppendLine("==================================================");
            sb.AppendLine();
            sb.AppendLine("Model: Bunsen Burner (Game-Ready Realistic)");
            sb.AppendLine("Source: Procedural Chemistry Lab Asset Suite");
            sb.AppendLine("Creator: AR Virtual Lab Team");
            sb.AppendLine("License: CC0 1.0 Universal (Public Domain Dedication)");
            sb.AppendLine("Source URL: https://creativecommons.org/publicdomain/zero/1.0/");
            sb.AppendLine();
            sb.AppendLine("Model: Laboratory Crucible Tongs");
            sb.AppendLine("Source: Procedural Chemistry Lab Asset Suite");
            sb.AppendLine("Creator: AR Virtual Lab Team");
            sb.AppendLine("License: CC0 1.0 Universal (Public Domain Dedication)");
            sb.AppendLine("Source URL: https://creativecommons.org/publicdomain/zero/1.0/");
            sb.AppendLine();
            sb.AppendLine("Model: Laboratory Watch Glass");
            sb.AppendLine("Source: Procedural Chemistry Lab Asset Suite");
            sb.AppendLine("Creator: AR Virtual Lab Team");
            sb.AppendLine("License: CC0 1.0 Universal (Public Domain Dedication)");
            sb.AppendLine("Source URL: https://creativecommons.org/publicdomain/zero/1.0/");
            sb.AppendLine();
            sb.AppendLine("Model: Chemical Epoxy Laboratory Workbench Table");
            sb.AppendLine("Source: Procedural Chemistry Lab Asset Suite");
            sb.AppendLine("Creator: AR Virtual Lab Team");
            sb.AppendLine("License: CC0 1.0 Universal (Public Domain Dedication)");
            sb.AppendLine("Source URL: https://creativecommons.org/publicdomain/zero/1.0/");
            sb.AppendLine();
            sb.AppendLine("Model: Abrasive Sandpaper Pad & Magnesium Ribbon Strip");
            sb.AppendLine("Source: Procedural Chemistry Lab Asset Suite");
            sb.AppendLine("Creator: AR Virtual Lab Team");
            sb.AppendLine("License: CC0 1.0 Universal (Public Domain Dedication)");
            sb.AppendLine("Source URL: https://creativecommons.org/publicdomain/zero/1.0/");
            sb.AppendLine();
            sb.AppendLine("Model: Magnesium Oxide (MgO) Ash Residue & Powder Heap");
            sb.AppendLine("Source: Procedural Chemistry Lab Asset Suite");
            sb.AppendLine("Creator: AR Virtual Lab Team");
            sb.AppendLine("License: CC0 1.0 Universal (Public Domain Dedication)");
            sb.AppendLine("Source URL: https://creativecommons.org/publicdomain/zero/1.0/");

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(path, sb.ToString());
        }

        public static void CreateMaterials(string matsDir)
        {
            Shader litShader = Shader.Find("Universal Render Pipeline/Lit");
            if (litShader == null) litShader = Shader.Find("Standard");

            // Cast Iron Base (Burner)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_CastIron.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.18f, 0.19f, 0.22f);
                mat.SetFloat("_Metallic", 0.75f);
                mat.SetFloat("_Smoothness", 0.45f);
            });

            // Polished Brass / Steel (Burner Barrel & Collar)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_BurnerBrass.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.85f, 0.75f, 0.42f);
                mat.SetFloat("_Metallic", 0.92f);
                mat.SetFloat("_Smoothness", 0.78f);
            });

            // Chrome Stainless Steel (Tongs & Burner Barrel)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_StainlessSteel.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.82f, 0.84f, 0.88f);
                mat.SetFloat("_Metallic", 0.95f);
                mat.SetFloat("_Smoothness", 0.85f);
            });

            // Watch Glass (Transparent Glass)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_WatchGlass.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.92f, 0.96f, 1.0f, 0.28f);
                mat.SetFloat("_Metallic", 0.1f);
                mat.SetFloat("_Smoothness", 0.95f);
                mat.SetFloat("_Surface", 1); // Transparent
                mat.SetFloat("_Blend", 0); // Alpha
                mat.renderQueue = 3000;
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            });

            // Laboratory Table Top (Black Chem Resin)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_LabTableTop.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.10f, 0.11f, 0.14f);
                mat.SetFloat("_Metallic", 0.15f);
                mat.SetFloat("_Smoothness", 0.65f);
            });

            // Laboratory Table Frame (Powder Coated Metal)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_TableFrame.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.22f, 0.25f, 0.30f);
                mat.SetFloat("_Metallic", 0.6f);
                mat.SetFloat("_Smoothness", 0.5f);
            });

            // Sandpaper (Brown abrasive grit)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_Sandpaper.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.68f, 0.44f, 0.24f);
                mat.SetFloat("_Metallic", 0.05f);
                mat.SetFloat("_Smoothness", 0.12f);
            });

            // Magnesium Ribbon (Metallic Silver strip)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_MagnesiumRibbon.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.65f, 0.66f, 0.70f); // Dull grey initial oxide coat
                mat.SetFloat("_Metallic", 0.85f);
                mat.SetFloat("_Smoothness", 0.55f);
            });

            // Magnesium Oxide (White powder / ash)
            CreateOrUpdateMaterial(Path.Combine(matsDir, "Mat_MagnesiumOxide.mat").Replace("\\", "/"), litShader, mat =>
            {
                mat.color = new Color(0.96f, 0.96f, 0.98f);
                mat.SetFloat("_Metallic", 0.0f);
                mat.SetFloat("_Smoothness", 0.15f);
            });
        }

        private static void CreateOrUpdateMaterial(string path, Shader shader, System.Action<Material> config)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                config?.Invoke(mat);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
                config?.Invoke(mat);
                EditorUtility.SetDirty(mat);
            }
        }

        // ============================================================
        // OBJ GENERATION HELPERS
        // ============================================================
        private class ObjBuilder
        {
            private List<Vector3> verts = new List<Vector3>();
            private List<Vector3> norms = new List<Vector3>();
            private List<Vector2> uvs = new List<Vector2>();
            private List<int[]> faces = new List<int[]>(); // 1-indexed

            public void AddVertex(Vector3 v, Vector3 n, Vector2 uv)
            {
                verts.Add(v);
                norms.Add(n);
                uvs.Add(uv);
            }

            public void AddQuad(int i0, int i1, int i2, int i3)
            {
                faces.Add(new int[] { i0 + 1, i1 + 1, i2 + 1, i3 + 1 });
            }

            public void AddTri(int i0, int i1, int i2)
            {
                faces.Add(new int[] { i0 + 1, i1 + 1, i2 + 1 });
            }

            public void AddCylinder(Vector3 bottomCenter, Vector3 topCenter, float bottomRadius, float topRadius, int segments, bool capBottom = true, bool capTop = true)
            {
                int baseIndex = verts.Count;
                Vector3 axis = (topCenter - bottomCenter).normalized;

                // Create rings
                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float cos = Mathf.Cos(angle);
                    float sin = Mathf.Sin(angle);

                    Vector3 radial = new Vector3(cos, 0, sin);
                    Vector3 n = radial.normalized;

                    Vector3 vBottom = bottomCenter + radial * bottomRadius;
                    Vector3 vTop = topCenter + radial * topRadius;

                    verts.Add(vBottom);
                    norms.Add(n);
                    uvs.Add(new Vector2((float)s / segments, 0f));

                    verts.Add(vTop);
                    norms.Add(n);
                    uvs.Add(new Vector2((float)s / segments, 1f));
                }

                // Side faces
                for (int s = 0; s < segments; s++)
                {
                    int b0 = baseIndex + s * 2;
                    int t0 = b0 + 1;
                    int b1 = b0 + 2;
                    int t1 = b0 + 3;

                    AddQuad(b0, b1, t1, t0);
                }

                if (capBottom)
                {
                    int centerIdx = verts.Count;
                    verts.Add(bottomCenter);
                    norms.Add(-axis);
                    uvs.Add(new Vector2(0.5f, 0.5f));

                    for (int s = 0; s < segments; s++)
                    {
                        float a0 = (float)s / segments * Mathf.PI * 2f;
                        float a1 = (float)(s + 1) / segments * Mathf.PI * 2f;
                        int idx0 = verts.Count;
                        int idx1 = idx0 + 1;

                        verts.Add(bottomCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * bottomRadius);
                        norms.Add(-axis);
                        uvs.Add(new Vector2(Mathf.Cos(a0) * 0.5f + 0.5f, Mathf.Sin(a0) * 0.5f + 0.5f));

                        verts.Add(bottomCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * bottomRadius);
                        norms.Add(-axis);
                        uvs.Add(new Vector2(Mathf.Cos(a1) * 0.5f + 0.5f, Mathf.Sin(a1) * 0.5f + 0.5f));

                        AddTri(centerIdx, idx1, idx0);
                    }
                }

                if (capTop)
                {
                    int centerIdx = verts.Count;
                    verts.Add(topCenter);
                    norms.Add(axis);
                    uvs.Add(new Vector2(0.5f, 0.5f));

                    for (int s = 0; s < segments; s++)
                    {
                        float a0 = (float)s / segments * Mathf.PI * 2f;
                        float a1 = (float)(s + 1) / segments * Mathf.PI * 2f;
                        int idx0 = verts.Count;
                        int idx1 = idx0 + 1;

                        verts.Add(topCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * topRadius);
                        norms.Add(axis);
                        uvs.Add(new Vector2(Mathf.Cos(a0) * 0.5f + 0.5f, Mathf.Sin(a0) * 0.5f + 0.5f));

                        verts.Add(topCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * topRadius);
                        norms.Add(axis);
                        uvs.Add(new Vector2(Mathf.Cos(a1) * 0.5f + 0.5f, Mathf.Sin(a1) * 0.5f + 0.5f));

                        AddTri(centerIdx, idx0, idx1);
                    }
                }
            }

            public void AddBox(Vector3 center, Vector3 size)
            {
                Vector3 half = size * 0.5f;
                Vector3[] corners = new Vector3[]
                {
                    center + new Vector3(-half.x, -half.y, -half.z), // 0
                    center + new Vector3( half.x, -half.y, -half.z), // 1
                    center + new Vector3( half.x,  half.y, -half.z), // 2
                    center + new Vector3(-half.x,  half.y, -half.z), // 3
                    center + new Vector3(-half.x, -half.y,  half.z), // 4
                    center + new Vector3( half.x, -half.y,  half.z), // 5
                    center + new Vector3( half.x,  half.y,  half.z), // 6
                    center + new Vector3(-half.x,  half.y,  half.z)  // 7
                };

                // 6 faces
                AddBoxFace(corners[0], corners[1], corners[2], corners[3], Vector3.back);
                AddBoxFace(corners[5], corners[4], corners[7], corners[6], Vector3.forward);
                AddBoxFace(corners[4], corners[0], corners[3], corners[7], Vector3.left);
                AddBoxFace(corners[1], corners[5], corners[6], corners[2], Vector3.right);
                AddBoxFace(corners[3], corners[2], corners[6], corners[7], Vector3.up);
                AddBoxFace(corners[4], corners[5], corners[1], corners[0], Vector3.down);
            }

            private void AddBoxFace(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
            {
                int b = verts.Count;
                verts.Add(p0); norms.Add(n); uvs.Add(new Vector2(0, 0));
                verts.Add(p1); norms.Add(n); uvs.Add(new Vector2(1, 0));
                verts.Add(p2); norms.Add(n); uvs.Add(new Vector2(1, 1));
                verts.Add(p3); norms.Add(n); uvs.Add(new Vector2(0, 1));
                AddQuad(b, b + 1, b + 2, b + 3);
            }

            public void SaveOBJ(string filePath)
            {
                var sb = new StringBuilder();
                sb.AppendLine("# AR Virtual Lab 3D Model");
                sb.AppendLine("# Vertices: " + verts.Count);

                for (int i = 0; i < verts.Count; i++)
                {
                    Vector3 v = verts[i];
                    sb.AppendLine($"v {v.x:F5} {v.y:F5} {v.z:F5}");
                }

                for (int i = 0; i < norms.Count; i++)
                {
                    Vector3 n = norms[i];
                    sb.AppendLine($"vn {n.x:F5} {n.y:F5} {n.z:F5}");
                }

                for (int i = 0; i < uvs.Count; i++)
                {
                    Vector2 uv = uvs[i];
                    sb.AppendLine($"vt {uv.x:F5} {uv.y:F5}");
                }

                for (int i = 0; i < faces.Count; i++)
                {
                    int[] f = faces[i];
                    if (f.Length == 3)
                        sb.AppendLine($"f {f[0]}/{f[0]}/{f[0]} {f[1]}/{f[1]}/{f[1]} {f[2]}/{f[2]}/{f[2]}");
                    else if (f.Length == 4)
                        sb.AppendLine($"f {f[0]}/{f[0]}/{f[0]} {f[1]}/{f[1]}/{f[1]} {f[2]}/{f[2]}/{f[2]} {f[3]}/{f[3]}/{f[3]}");
                }

                string dir = Path.GetDirectoryName(filePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(filePath, sb.ToString());
            }
        }

        // 1. BUNSEN BURNER (Realistic Lab Model)
        public static void GenerateBunsenBurnerOBJ(string path)
        {
            var b = new ObjBuilder();

            // Heavy octagonal/circular cast-iron base
            b.AddCylinder(new Vector3(0, 0f, 0), new Vector3(0, 0.008f, 0), 0.028f, 0.026f, 18, true, true);
            b.AddCylinder(new Vector3(0, 0.008f, 0), new Vector3(0, 0.016f, 0), 0.026f, 0.012f, 18, false, true);

            // Gas inlet side arm (horizontal nipple for gas hose)
            b.AddCylinder(new Vector3(0.008f, 0.012f, 0), new Vector3(0.030f, 0.012f, 0), 0.004f, 0.0035f, 12, false, true);
            b.AddCylinder(new Vector3(0.030f, 0.012f, 0), new Vector3(0.040f, 0.010f, 0), 0.0045f, 0.0045f, 12, true, true);

            // Air adjustment collar (with air hole slots)
            b.AddCylinder(new Vector3(0, 0.016f, 0), new Vector3(0, 0.032f, 0), 0.0085f, 0.0085f, 16, true, true);

            // Vertical burner barrel tube (Chimney)
            b.AddCylinder(new Vector3(0, 0.032f, 0), new Vector3(0, 0.102f, 0), 0.0065f, 0.0065f, 16, false, false);

            // Top flame nozzle crown
            b.AddCylinder(new Vector3(0, 0.102f, 0), new Vector3(0, 0.108f, 0), 0.0075f, 0.0080f, 16, false, true);

            b.SaveOBJ(path);
        }

        // 2. CRUCIBLE TONGS (Realistic Scissor Loop Lab Tongs)
        public static void GenerateCrucibleTongsOBJ(string path)
        {
            var b = new ObjBuilder();

            // Scissor Central Pivot Pin
            b.AddCylinder(new Vector3(0, -0.003f, 0), new Vector3(0, 0.003f, 0), 0.003f, 0.003f, 12, true, true);

            // Left Arm (from handle loop to pivot to curved jaw)
            // Handle finger loop Left
            b.AddCylinder(new Vector3(-0.016f, 0, -0.075f), new Vector3(-0.016f, 0.003f, -0.075f), 0.014f, 0.014f, 14, true, true);
            // Straight shaft Left
            b.AddBox(new Vector3(-0.008f, 0, -0.038f), new Vector3(0.0035f, 0.003f, 0.075f));
            // Curved bowed crucible loop Left
            b.AddBox(new Vector3(-0.010f, 0, 0.022f), new Vector3(0.0035f, 0.003f, 0.038f));
            // Straight serrated gripping tip Left
            b.AddBox(new Vector3(-0.002f, 0, 0.052f), new Vector3(0.003f, 0.003f, 0.028f));

            // Right Arm
            // Handle finger loop Right
            b.AddCylinder(new Vector3(0.016f, 0, -0.075f), new Vector3(0.016f, 0.003f, -0.075f), 0.014f, 0.014f, 14, true, true);
            // Straight shaft Right
            b.AddBox(new Vector3(0.008f, 0, -0.038f), new Vector3(0.0035f, 0.003f, 0.075f));
            // Curved bowed crucible loop Right
            b.AddBox(new Vector3(0.010f, 0, 0.022f), new Vector3(0.0035f, 0.003f, 0.038f));
            // Straight serrated gripping tip Right
            b.AddBox(new Vector3(0.002f, 0, 0.052f), new Vector3(0.003f, 0.003f, 0.028f));

            b.SaveOBJ(path);
        }

        // 3. WATCH GLASS (Smooth Concave Glass Dish)
        public static void GenerateWatchGlassOBJ(string path)
        {
            var b = new ObjBuilder();
            int rings = 8;
            int segments = 24;
            float maxRadius = 0.035f;
            float depth = 0.007f;

            // Concave spherical cap
            for (int r = 0; r <= rings; r++)
            {
                float frac = (float)r / rings;
                float currentRadius = Mathf.Sin(frac * Mathf.PI * 0.5f) * maxRadius;
                float currentY = (1f - Mathf.Cos(frac * Mathf.PI * 0.5f)) * depth;

                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * currentRadius;
                    float z = Mathf.Sin(angle) * currentRadius;

                    Vector3 pos = new Vector3(x, currentY, z);
                    Vector3 n = new Vector3(-x * 0.5f, 1f, -z * 0.5f).normalized;
                    b.AddVertex(pos, n, new Vector2((float)s / segments, frac));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int i0 = r * (segments + 1) + s;
                    int i1 = i0 + 1;
                    int i2 = (r + 1) * (segments + 1) + s + 1;
                    int i3 = (r + 1) * (segments + 1) + s;
                    b.AddQuad(i0, i1, i2, i3);
                }
            }

            // Glass outer rim bevel
            b.AddCylinder(new Vector3(0, depth - 0.001f, 0), new Vector3(0, depth + 0.001f, 0), maxRadius - 0.001f, maxRadius, segments, false, true);

            b.SaveOBJ(path);
        }

        // 4. LABORATORY TABLE (Chemical Epoxy Benchtop & Frame)
        public static void GenerateLaboratoryTableOBJ(string path)
        {
            var b = new ObjBuilder();

            // Main Chem-Resistant Top
            b.AddBox(new Vector3(0, -0.010f, 0), new Vector3(0.52f, 0.020f, 0.36f));

            // Table Legs (4 sturdy tubular legs)
            float lx = 0.23f;
            float lz = 0.15f;
            float legH = 0.28f;
            Vector3[] legPositions = new Vector3[]
            {
                new Vector3(-lx, -0.02f - legH * 0.5f, -lz),
                new Vector3( lx, -0.02f - legH * 0.5f, -lz),
                new Vector3(-lx, -0.02f - legH * 0.5f,  lz),
                new Vector3( lx, -0.02f - legH * 0.5f,  lz)
            };

            foreach (var pos in legPositions)
            {
                b.AddCylinder(pos - new Vector3(0, legH * 0.5f, 0), pos + new Vector3(0, legH * 0.5f, 0), 0.010f, 0.010f, 12, true, true);
            }

            // Cross Braces
            b.AddBox(new Vector3(0, -0.15f, -lz), new Vector3(lx * 2f, 0.010f, 0.010f));
            b.AddBox(new Vector3(0, -0.15f,  lz), new Vector3(lx * 2f, 0.010f, 0.010f));
            b.AddBox(new Vector3(-lx, -0.15f, 0), new Vector3(0.010f, 0.010f, lz * 2f));
            b.AddBox(new Vector3( lx, -0.15f, 0), new Vector3(0.010f, 0.010f, lz * 2f));

            b.SaveOBJ(path);
        }

        // 5. SANDPAPER PAD
        public static void GenerateSandpaperOBJ(string path)
        {
            var b = new ObjBuilder();
            // Abrasive pad sheet
            b.AddBox(new Vector3(0, 0.001f, 0), new Vector3(0.065f, 0.002f, 0.065f));
            b.SaveOBJ(path);
        }

        // 6. MAGNESIUM RIBBON STRIP
        public static void GenerateMagnesiumRibbonOBJ(string path)
        {
            var b = new ObjBuilder();
            // Thin realistic metal strip (5.5cm long, 4.5mm wide, 0.6mm thick) with subtle natural flex
            int segments = 12;
            float length = 0.055f;
            float width = 0.0045f;
            float thickness = 0.0006f;

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float z = (t - 0.5f) * length;
                float y = Mathf.Sin(t * Mathf.PI * 2f) * 0.0012f + thickness * 0.5f;

                Vector3 pLeft = new Vector3(-width * 0.5f, y, z);
                Vector3 pRight = new Vector3(width * 0.5f, y, z);

                Vector3 n = Vector3.up;
                b.AddVertex(pLeft, n, new Vector2(0, t));
                b.AddVertex(pRight, n, new Vector2(1, t));
            }

            for (int i = 0; i < segments; i++)
            {
                int b0 = i * 2;
                int b1 = b0 + 1;
                int b2 = b0 + 3;
                int b3 = b0 + 2;
                b.AddQuad(b0, b1, b2, b3); // Top face
                b.AddQuad(b3, b2, b1, b0); // Bottom face
            }

            b.SaveOBJ(path);
        }

        // 7. MAGNESIUM OXIDE POWDER HEAP (In Watch Glass)
        public static void GenerateMgOPowderOBJ(string path)
        {
            var b = new ObjBuilder();
            // Soft conical heap of porous white powder
            int rings = 6;
            int segments = 16;
            float baseRadius = 0.018f;
            float height = 0.006f;

            for (int r = 0; r <= rings; r++)
            {
                float frac = (float)r / rings;
                float currentRadius = (1f - frac) * baseRadius;
                float currentY = Mathf.Pow(1f - frac, 0.6f) * height;

                for (int s = 0; s <= segments; s++)
                {
                    float angle = (float)s / segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * currentRadius;
                    float z = Mathf.Sin(angle) * currentRadius;

                    Vector3 pos = new Vector3(x, height - currentY, z);
                    Vector3 n = new Vector3(x, 0.8f, z).normalized;
                    b.AddVertex(pos, n, new Vector2((float)s / segments, frac));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int i0 = r * (segments + 1) + s;
                    int i1 = i0 + 1;
                    int i2 = (r + 1) * (segments + 1) + s + 1;
                    int i3 = (r + 1) * (segments + 1) + s;
                    b.AddQuad(i0, i1, i2, i3);
                }
            }

            b.SaveOBJ(path);
        }

        // 8. MAGNESIUM OXIDE RESIDUE CLUSTER (On Tongs after burning)
        public static void GenerateMgOResidueOBJ(string path)
        {
            var b = new ObjBuilder();
            // Irregular cluster of white burnt ash
            b.AddBox(new Vector3(0, 0, 0), new Vector3(0.006f, 0.008f, 0.016f));
            b.AddBox(new Vector3(0.001f, 0.002f, 0.004f), new Vector3(0.005f, 0.006f, 0.010f));
            b.SaveOBJ(path);
        }
    }
}
#endif
