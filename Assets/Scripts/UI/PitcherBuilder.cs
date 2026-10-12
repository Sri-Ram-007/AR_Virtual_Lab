// ============================================================
// PitcherBuilder.cs  —  AR Virtual Lab
// Builds a proper water jug at runtime: a tapered, rounded glass body with a pulled pouring lip and a curved handle.
// No imported model is needed (and none exists for this prop).
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace ARVirtualLab.UI
{
    public static class PitcherBuilder
    {
        // profile of the OUTER wall: (radius, height) from the base up to the rim, in metres
        private static readonly Vector2[] PitcherProfile =
        {
            new Vector2(0.000f, 0.000f), new Vector2(0.044f, 0.000f), new Vector2(0.052f, 0.008f), new Vector2(0.058f, 0.030f),
            new Vector2(0.062f, 0.070f), new Vector2(0.060f, 0.115f), new Vector2(0.053f, 0.155f), new Vector2(0.048f, 0.178f),
            new Vector2(0.050f, 0.186f),
        };
        private const float PitcherSpoutBump = 0.032f;
        public const float PitcherHeight = 0.186f;

        /// <summary>Creates the jug under parent (body + handle). Returns the root; spoutTipLocal is the tip of the pouring lip.</summary>
        public static GameObject Build(Transform parent, Material glass, Material handleMat, out Vector3 spoutTipLocal, out GameObject body)
        {
            var root = new GameObject("Pitcher");
            root.transform.SetParent(parent, false);

            body = new GameObject("PitcherBody");
            body.transform.SetParent(root.transform, false);
            body.AddComponent<MeshFilter>().sharedMesh = BuildBody(PitcherProfile, PitcherSpoutBump, PitcherHeight);
            body.AddComponent<MeshRenderer>().sharedMaterial = glass;
            body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var handle = new GameObject("PitcherHandle");
            handle.transform.SetParent(root.transform, false);
            handle.AddComponent<MeshFilter>().sharedMesh = BuildHandle();
            handle.AddComponent<MeshRenderer>().sharedMaterial = handleMat;

            spoutTipLocal = new Vector3(PitcherProfile[PitcherProfile.Length - 1].x + PitcherSpoutBump, PitcherHeight, 0f);
            return root;
        }

        // a plain straight-sided open glass jar (used as the waste jar)
        private static readonly Vector2[] JarProfile =
        {
            new Vector2(0.000f, 0.000f), new Vector2(0.050f, 0.000f), new Vector2(0.056f, 0.008f), new Vector2(0.058f, 0.04f),
            new Vector2(0.058f, 0.080f), new Vector2(0.060f, 0.098f),
        };

        public static GameObject BuildJar(Transform parent, Material glass, out GameObject body)
        {
            body = new GameObject("JarBody");
            body.transform.SetParent(parent, false);
            body.AddComponent<MeshFilter>().sharedMesh = BuildBody(JarProfile, 0f, 0.098f);
            body.AddComponent<MeshRenderer>().sharedMaterial = glass;
            body.GetComponent<MeshRenderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return body;
        }

        private static Mesh BuildBody(Vector2[] Profile, float SpoutBump, float Height)
        {
            const int Segs = 44;
            int rows = Profile.Length;
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();

            // outer wall, then the inner wall (slightly inset, normals flipped) so the glass has visible thickness
            for (int pass = 0; pass < 2; pass++)
            {
                bool inner = pass == 1;
                int baseIdx = verts.Count;
                for (int j = 0; j < rows; j++)
                {
                    for (int s = 0; s <= Segs; s++)
                    {
                        float a = s / (float)Segs * Mathf.PI * 2f;
                        float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                        float r = Profile[j].x;
                        float y = Profile[j].y;
                        // pulled pouring lip on the +x side, only near the rim
                        float lipAmount = Mathf.Pow(Mathf.Max(0f, cs), 7f) * Mathf.SmoothStep(0.10f, Height, y);
                        r += SpoutBump * lipAmount;
                        if (inner) { r = Mathf.Max(0f, r - 0.0035f); y = Mathf.Max(y, j == 0 ? 0.004f : y); }
                        verts.Add(new Vector3(cs * r, y, sn * r));
                        norms.Add(inner ? new Vector3(-cs, 0f, -sn) : new Vector3(cs, 0f, sn));
                    }
                }
                for (int j = 0; j < rows - 1; j++)
                    for (int s = 0; s < Segs; s++)
                    {
                        int a = baseIdx + j * (Segs + 1) + s, b = a + 1, c = a + (Segs + 1), d = c + 1;
                        if (!inner) { tris.Add(a); tris.Add(c); tris.Add(d); tris.Add(a); tris.Add(d); tris.Add(b); }
                        else        { tris.Add(a); tris.Add(d); tris.Add(c); tris.Add(a); tris.Add(b); tris.Add(d); }
                    }
            }

            // rim ring joining the outer and inner walls at the top
            int outerTop = (rows - 1) * (Segs + 1);
            int innerTop = rows * (Segs + 1) + (rows - 1) * (Segs + 1);
            for (int s = 0; s < Segs; s++)
            {
                int a = outerTop + s, b = a + 1, c = innerTop + s, d = c + 1;
                tris.Add(a); tris.Add(d); tris.Add(c); tris.Add(a); tris.Add(b); tris.Add(d);
            }

            var m = new Mesh { name = "PitcherBody" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }

        // a tube swept along a curve on the -x side of the jug
        private static Mesh BuildHandle()
        {
            const int Along = 18, Around = 10;
            Vector3 p0 = new Vector3(-0.055f, 0.150f, 0f), p1 = new Vector3(-0.125f, 0.150f, 0f),
                    p2 = new Vector3(-0.125f, 0.035f, 0f), p3 = new Vector3(-0.060f, 0.040f, 0f);
            var verts = new List<Vector3>(); var norms = new List<Vector3>(); var tris = new List<int>();
            for (int i = 0; i <= Along; i++)
            {
                float t = i / (float)Along, u = 1f - t;
                Vector3 p = u * u * u * p0 + 3f * u * u * t * p1 + 3f * u * t * t * p2 + t * t * t * p3;
                Vector3 tan = (-3f * u * u * p0 + 3f * (u * u - 2f * u * t) * p1 + 3f * (2f * u * t - t * t) * p2 + 3f * t * t * p3).normalized;
                Vector3 side = Vector3.forward;                              // the handle sits in the x-y plane
                Vector3 up = Vector3.Cross(tan, side).normalized;
                float rad = 0.0085f;
                for (int k = 0; k <= Around; k++)
                {
                    float a = k / (float)Around * Mathf.PI * 2f;
                    Vector3 n = Mathf.Cos(a) * side + Mathf.Sin(a) * up;
                    verts.Add(p + n * rad); norms.Add(n);
                }
            }
            for (int i = 0; i < Along; i++)
                for (int k = 0; k < Around; k++)
                {
                    int a = i * (Around + 1) + k, b = a + 1, c = a + (Around + 1), d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(d); tris.Add(a); tris.Add(d); tris.Add(b);
                }
            var m = new Mesh { name = "PitcherHandle" };
            m.SetVertices(verts); m.SetNormals(norms); m.SetTriangles(tris, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
