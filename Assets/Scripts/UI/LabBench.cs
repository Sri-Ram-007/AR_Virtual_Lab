// ============================================================
// LabBench.cs  —  AR Virtual Lab
// The grey laboratory table (laminate top on four legs) that every experiment stands on. It is the LaboratoryTable
// model the Magnesium lab started with, stretched to whatever bench size an experiment needs, so all labs look
// like the same room.
// ============================================================
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace ARVirtualLab.UI
{
    public static class LabBench
    {
        public static readonly Color Laminate = new Color(0.68f, 0.72f, 0.76f, 1f);

        // The model: a 0.52 x 0.36 m top with its surface at y = 0, centred on the origin, legs reaching 0.30 m down
        private const float ModelWidth = 0.52f, ModelDepth = 0.36f;

        private static Mesh _mesh;
        private static Material _material;

        /// <summary>
        /// Builds the table under parent with its top surface centred on topCentre (local position) and
        /// topSize (x = width, y = depth) across. The legs and top thickness grow with it.
        /// </summary>
        public static GameObject Create(Transform parent, Vector3 topCentre, Vector2 topSize)
        {
            var go = new GameObject("LaboratoryTable");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = topCentre;
            float sx = topSize.x / ModelWidth, sz = topSize.y / ModelDepth;
            go.transform.localScale = new Vector3(sx, (sx + sz) * 0.5f, sz);
            go.AddComponent<MeshFilter>().sharedMesh = TableMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = TableMaterial();
            return go;
        }

        private static Material TableMaterial()
        {
            if (_material != null) return _material;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            _material = new Material(shader) { name = "LabBenchLaminate", color = Laminate };
            if (_material.HasProperty("_BaseColor")) _material.SetColor("_BaseColor", Laminate);
            if (_material.HasProperty("_Metallic")) _material.SetFloat("_Metallic", 0.05f);
            if (_material.HasProperty("_Smoothness")) _material.SetFloat("_Smoothness", 0.30f);
            return _material;
        }

        // Resources/Models/LaboratoryTable.txt is a plain OBJ (positions, uvs, normals, quad faces)
        private static Mesh TableMesh()
        {
            if (_mesh != null) return _mesh;
            var obj = Resources.Load<TextAsset>("Models/LaboratoryTable");
            _mesh = obj != null ? ParseObj(obj.text) : FallbackBox();
            return _mesh;
        }

        private static Mesh ParseObj(string text)
        {
            var pos = new List<Vector3>(); var nrm = new List<Vector3>(); var uv = new List<Vector2>();
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
            var tris = new List<int>();
            var seen = new Dictionary<string, int>();
            foreach (var raw in text.Split('\n'))
            {
                var p = raw.Trim().Split((char[])null, System.StringSplitOptions.RemoveEmptyEntries);
                if (p.Length < 2) continue;
                switch (p[0])
                {
                    case "v": if (p.Length >= 4) pos.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); break;
                    case "vn": if (p.Length >= 4) nrm.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); break;
                    case "vt": if (p.Length >= 3) uv.Add(new Vector2(F(p[1]), F(p[2]))); break;
                    case "f":
                        for (int i = 2; i < p.Length - 1; i++)             // fan the polygon into triangles
                            foreach (var token in new[] { p[1], p[i], p[i + 1] })
                            {
                                int idx;
                                if (!seen.TryGetValue(token, out idx))
                                {
                                    var s = token.Split('/');
                                    idx = verts.Count;
                                    verts.Add(At(pos, s, 0, Vector3.zero));
                                    norms.Add(At(nrm, s, 2, Vector3.up));
                                    uvs.Add(At(uv, s, 1, Vector2.zero));
                                    seen[token] = idx;
                                }
                                tris.Add(idx);
                            }
                        break;
                }
            }
            var mesh = new Mesh { name = "LaboratoryTable" };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            if (nrm.Count > 0) mesh.SetNormals(norms); else mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static float F(string s) { float f; float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out f); return f; }

        private static T At<T>(List<T> list, string[] seg, int slot, T fallback)
        {
            int i;
            if (seg.Length <= slot || !int.TryParse(seg[slot], out i)) return fallback;
            i -= 1;
            return i >= 0 && i < list.Count ? list[i] : fallback;
        }

        // only if the model file is missing: a plain slab with the same top
        private static Mesh FallbackBox()
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var src = cube.GetComponent<MeshFilter>().sharedMesh;
            var verts = src.vertices;
            for (int i = 0; i < verts.Length; i++)
                verts[i] = new Vector3(verts[i].x * ModelWidth, verts[i].y * 0.02f - 0.01f, verts[i].z * ModelDepth);
            var mesh = new Mesh { name = "LaboratoryTable_Fallback", vertices = verts, triangles = src.triangles, normals = src.normals };
            Object.Destroy(cube);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
