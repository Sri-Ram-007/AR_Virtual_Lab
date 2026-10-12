// ============================================================
// LiquidFill.cs  —  AR Virtual Lab
// A liquid volume that fits the INSIDE of a hollow container model (mug, beaker...). The container's real mesh is
// measured once (floor, rim, and inner radius at every height), then a tapered lathe mesh is rebuilt to whatever
// fill level is asked for. The liquid therefore always sits on the container's floor, follows its taper, and never
// pokes through the wall — whatever size or pivot the imported model happens to have.
// ============================================================
using System.Collections.Generic;
using UnityEngine;

namespace ARVirtualLab.UI
{
    public class LiquidFill : MonoBehaviour
    {
        private const int Rings = 24;
        private const int Segs = 36;
        private const int Sectors = 12;

        private Transform _container;
        private readonly float[] _ringY = new float[Rings + 1];
        private readonly float[] _ringR = new float[Rings + 1];
        private float _cx, _cz;
        private Mesh _mesh;
        private MeshRenderer _renderer;

        public float FloorLocalY { get; private set; }
        public float RimLocalY { get; private set; }
        public float Fraction { get; private set; }
        /// <summary>The farthest point of the rim from the container's axis (container-local): its pouring spout if it has one.</summary>
        public Vector3 SpoutLocal { get; private set; }

        public float FloorWorldY { get { return _container.TransformPoint(new Vector3(_cx, FloorLocalY, _cz)).y; } }
        public float RimWorldY { get { return _container.TransformPoint(new Vector3(_cx, RimLocalY, _cz)).y; } }
        public float LevelWorldY { get { return _container.TransformPoint(new Vector3(_cx, FloorLocalY + Fraction * (RimLocalY - FloorLocalY), _cz)).y; } }
        /// <summary>World-space centre axis of the container's cavity (x,z), at the given height fraction.</summary>
        public Vector3 CentreWorld(float heightFraction)
        {
            return _container.TransformPoint(new Vector3(_cx, Mathf.Lerp(FloorLocalY, RimLocalY, heightFraction), _cz));
        }
        /// <summary>Inner radius in WORLD units at the floor (used to keep things inside the container).</summary>
        public float FloorRadiusWorld { get { return _ringR[0] * _container.lossyScale.x; } }

        public static LiquidFill Create(Transform container, GameObject model, Material mat, float wallFrac = 0.10f)
        {
            var go = new GameObject("Liquid");
            go.transform.SetParent(container, false);
            var lf = go.AddComponent<LiquidFill>();
            lf._container = container;
            lf.Measure(model, wallFrac);
            lf._mesh = new Mesh { name = "LiquidMesh" };
            lf._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = lf._mesh;
            lf._renderer = go.AddComponent<MeshRenderer>();
            lf._renderer.sharedMaterial = mat;
            lf._renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lf._renderer.receiveShadows = false;
            lf.SetFraction(0f);
            return lf;
        }

        public void SetMaterial(Material m) { if (_renderer != null) _renderer.sharedMaterial = m; }
        public Material CurrentMaterial { get { return _renderer != null ? _renderer.sharedMaterial : null; } }

        private void Measure(GameObject model, float wallFrac)
        {
            var pts = new List<Vector3>(12000);
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m == null || !m.isReadable) continue;
                var vs = m.vertices;
                var tr = mf.transform;
                for (int i = 0; i < vs.Length; i++) pts.Add(_container.InverseTransformPoint(tr.TransformPoint(vs[i])));
            }

            if (pts.Count < 50) { MeasureFromBounds(model, wallFrac); return; }

            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (var p in pts) { if (p.y < minY) minY = p.y; if (p.y > maxY) maxY = p.y; }
            float H = maxY - minY;

            // centre of the cavity = centre of the base (immune to a handle or spout that skews the overall bounds)
            float bx0 = float.MaxValue, bx1 = float.MinValue, bz0 = float.MaxValue, bz1 = float.MinValue;
            foreach (var p in pts)
                if (p.y < minY + 0.05f * H) { bx0 = Mathf.Min(bx0, p.x); bx1 = Mathf.Max(bx1, p.x); bz0 = Mathf.Min(bz0, p.z); bz1 = Mathf.Max(bz1, p.z); }
            _cx = (bx0 + bx1) * 0.5f; _cz = (bz0 + bz1) * 0.5f;

            FloorLocalY = minY + 0.05f * H;
            RimLocalY = maxY - 0.015f * H;

            float bestD = -1f; Vector3 spout = new Vector3(_cx, maxY, _cz);
            foreach (var p in pts)
            {
                if (p.y < maxY - 0.04f * H) continue;
                float d = new Vector2(p.x - _cx, p.z - _cz).magnitude;
                if (d > bestD) { bestD = d; spout = new Vector3(p.x, maxY, p.z); }
            }
            SpoutLocal = spout;

            float bandHalf = H / Rings * 0.9f;
            var sectorMax = new float[Sectors];
            var found = new List<float>(Sectors);
            float last = 0.05f;
            for (int i = 0; i <= Rings; i++)
            {
                float y = Mathf.Lerp(FloorLocalY, RimLocalY, i / (float)Rings);
                for (int s = 0; s < Sectors; s++) sectorMax[s] = -1f;
                foreach (var p in pts)
                {
                    if (Mathf.Abs(p.y - y) > bandHalf) continue;
                    float dx = p.x - _cx, dz = p.z - _cz;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    int sec = Mathf.Clamp((int)((Mathf.Atan2(dz, dx) + Mathf.PI) / (2f * Mathf.PI) * Sectors), 0, Sectors - 1);
                    if (d > sectorMax[sec]) sectorMax[sec] = d;
                }
                found.Clear();
                for (int s = 0; s < Sectors; s++) if (sectorMax[s] > 0f) found.Add(sectorMax[s]);
                if (found.Count > 0)
                {
                    found.Sort();
                    // a lower percentile of the per-sector outer radii ignores a handle / spout on one side
                    last = found[Mathf.FloorToInt(0.35f * (found.Count - 1))];
                }
                _ringY[i] = y;
                _ringR[i] = last * (1f - wallFrac);
            }
            // light smoothing so a stray band does not make the liquid ripple
            for (int pass = 0; pass < 2; pass++)
                for (int i = 1; i < Rings; i++) _ringR[i] = (_ringR[i - 1] + _ringR[i] * 2f + _ringR[i + 1]) * 0.25f;
        }

        private void MeasureFromBounds(GameObject model, float wallFrac)
        {
            var b = ModelLoader.GetBounds(model);
            Vector3 c = _container.InverseTransformPoint(b.center);
            Vector3 lo = _container.InverseTransformPoint(new Vector3(b.center.x, b.min.y, b.center.z));
            Vector3 hi = _container.InverseTransformPoint(new Vector3(b.center.x, b.max.y, b.center.z));
            _cx = c.x; _cz = c.z;
            float H = hi.y - lo.y;
            FloorLocalY = lo.y + 0.05f * H;
            RimLocalY = hi.y - 0.015f * H;
            float r = Mathf.Min(b.size.x, b.size.z) * 0.5f * (1f - wallFrac) * 0.9f;
            for (int i = 0; i <= Rings; i++) { _ringY[i] = Mathf.Lerp(FloorLocalY, RimLocalY, i / (float)Rings); _ringR[i] = r; }
            SpoutLocal = new Vector3(_cx + r, hi.y, _cz);
        }

        private float RadiusAt(float y)
        {
            if (y <= _ringY[0]) return _ringR[0];
            for (int i = 1; i <= Rings; i++)
                if (y <= _ringY[i]) return Mathf.Lerp(_ringR[i - 1], _ringR[i], Mathf.InverseLerp(_ringY[i - 1], _ringY[i], y));
            return _ringR[Rings];
        }

        /// <summary>0 = empty (hidden), 1 = filled right up to the rim.</summary>
        public void SetFraction(float f)
        {
            Fraction = Mathf.Clamp01(f);
            if (Fraction < 0.004f) { _renderer.enabled = false; return; }
            _renderer.enabled = true;

            float levelY = Mathf.Lerp(FloorLocalY, RimLocalY, Fraction);
            var ys = new List<float>(Rings + 2);
            ys.Add(FloorLocalY);
            for (int i = 1; i <= Rings; i++) if (_ringY[i] < levelY - 0.0005f) ys.Add(_ringY[i]);
            ys.Add(levelY);

            int rows = ys.Count;
            var verts = new Vector3[rows * (Segs + 1) + 1 + (Segs + 1)];
            var norms = new Vector3[verts.Length];
            var tris = new List<int>((rows - 1) * Segs * 6 + Segs * 3);

            for (int j = 0; j < rows; j++)
            {
                float r = RadiusAt(ys[j]);
                for (int s = 0; s <= Segs; s++)
                {
                    float a = s / (float)Segs * Mathf.PI * 2f;
                    float cs = Mathf.Cos(a), sn = Mathf.Sin(a);
                    int idx = j * (Segs + 1) + s;
                    verts[idx] = new Vector3(_cx + cs * r, ys[j], _cz + sn * r);
                    norms[idx] = new Vector3(cs, 0f, sn);
                }
            }
            for (int j = 0; j < rows - 1; j++)
                for (int s = 0; s < Segs; s++)
                {
                    int a = j * (Segs + 1) + s, b = a + 1, c = a + (Segs + 1), d = c + 1;
                    tris.Add(a); tris.Add(c); tris.Add(d);
                    tris.Add(a); tris.Add(d); tris.Add(b);
                }

            // flat top surface (separate vertices so its normal points straight up)
            int centre = rows * (Segs + 1);
            int ringStart = centre + 1;
            float topR = RadiusAt(levelY);
            verts[centre] = new Vector3(_cx, levelY, _cz); norms[centre] = Vector3.up;
            for (int s = 0; s <= Segs; s++)
            {
                float a = s / (float)Segs * Mathf.PI * 2f;
                verts[ringStart + s] = new Vector3(_cx + Mathf.Cos(a) * topR, levelY, _cz + Mathf.Sin(a) * topR);
                norms[ringStart + s] = Vector3.up;
            }
            for (int s = 0; s < Segs; s++) { tris.Add(centre); tris.Add(ringStart + s + 1); tris.Add(ringStart + s); }

            _mesh.Clear();
            _mesh.vertices = verts;
            _mesh.normals = norms;
            _mesh.triangles = tris.ToArray();
            _mesh.RecalculateBounds();
        }
    }
}
