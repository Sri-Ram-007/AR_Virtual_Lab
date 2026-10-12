// ============================================================
// ModelLoader.cs  —  AR Virtual Lab
// Shared helper for loading a baked Resources/Models/<Name>.prefab at runtime and sizing it correctly, without
// needing to know the model's native units or pivot in advance (every Meshy export is a different size/origin).
// Used by the newer experiment scenes; the three original scenes keep their own local copies of this logic.
// ============================================================
using UnityEngine;

namespace ARVirtualLab.UI
{
    public static class ModelLoader
    {
        /// <summary>Loads Resources/Models/&lt;prefabName&gt;.prefab under sceneRoot and strips its non-trigger colliders.</summary>
        public static bool TryLoad(Transform sceneRoot, string prefabName, out GameObject go)
        {
            var prefab = Resources.Load<GameObject>("Models/" + prefabName);
            if (prefab == null) { go = null; return false; }
            go = Object.Instantiate(prefab);
            go.name = prefabName;
            // worldPositionStays MUST be false: true keeps the model at wherever the raw prefab's own transform sits
            // (near the world origin for a plain imported asset), so every caller's container ends up empty and
            // every model piles up near (0,0,0) instead of moving to its intended bench position.
            go.transform.SetParent(sceneRoot, false);
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (!c.isTrigger) Object.Destroy(c);
            return true;
        }

        /// <summary>Combined world-space bounds of every renderer on the object (particle systems excluded).</summary>
        public static Bounds GetBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>(true);
            bool first = true;
            Bounds b = new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs)
            {
                if (r is ParticleSystemRenderer) continue;
                if (first) { b = r.bounds; first = false; } else b.Encapsulate(r.bounds);
            }
            return b;
        }

        /// <summary>Uniformly scales the model so its measured height matches targetHeight (metres). Returns the scale factor applied.</summary>
        public static float AutoScaleToHeight(GameObject go, float targetHeight)
        {
            float h = GetBounds(go).size.y;
            if (h < 0.0001f) return 1f;
            float k = targetHeight / h;
            go.transform.localScale *= k;
            return k;
        }

        /// <summary>Uniformly scales the model so its measured widest horizontal extent matches targetWidth (metres).</summary>
        public static float AutoScaleToWidth(GameObject go, float targetWidth)
        {
            var b = GetBounds(go);
            float w = Mathf.Max(b.size.x, b.size.z);
            if (w < 0.0001f) return 1f;
            float k = targetWidth / w;
            go.transform.localScale *= k;
            return k;
        }

        /// <summary>
        /// Uniformly scales the model so its single longest dimension (whichever local axis that turns out to be)
        /// matches targetLength. Use this for a long thin object (a nail, a rod, a wire) when you cannot be sure
        /// which axis the export considers "up" — AutoScaleToHeight would scale the wrong (short) axis and leave
        /// the true length huge if the model was authored lying on its side.
        /// </summary>
        public static float AutoScaleToLongestAxis(GameObject go, float targetLength)
        {
            var b = GetBounds(go);
            float longest = Mathf.Max(b.size.x, b.size.y, b.size.z);
            if (longest < 0.0001f) return 1f;
            float k = targetLength / longest;
            go.transform.localScale *= k;
            return k;
        }

        /// <summary>Moves the object straight up/down so the bottom of its bounds sits exactly at targetY.</summary>
        public static void AlignBottomTo(GameObject go, float targetY)
        {
            float minY = GetBounds(go).min.y;
            go.transform.position += new Vector3(0f, targetY - minY, 0f);
        }

        /// <summary>
        /// If the model's longest dimension isn't already its local Y axis (i.e. it was exported lying on its
        /// side), rotates it so that axis points up. Uses the raw MESH bounds (unaffected by the object's current
        /// rotation), so call this before relying on AutoScaleToLongestAxis's result to mean "height".
        /// </summary>
        public static void OrientLongestAxisUp(GameObject go)
        {
            var mf = go.GetComponentInChildren<MeshFilter>(true);
            if (mf == null || mf.sharedMesh == null) return;
            Vector3 s = mf.sharedMesh.bounds.size;
            if (s.y >= s.x && s.y >= s.z) return;                                     // already the tallest local axis
            if (s.x >= s.y && s.x >= s.z) go.transform.localRotation *= Quaternion.Euler(0f, 0f, 90f);   // X was longest
            else go.transform.localRotation *= Quaternion.Euler(90f, 0f, 0f);                            // Z was longest
        }

        public static float GetTopY(GameObject go)
        {
            return GetBounds(go).max.y;
        }

        /// <summary>
        /// Gives the container a BoxCollider that tightly wraps everything it currently renders (call while the
        /// container is upright and before adding liquid), so tapping/dragging works wherever the model really is
        /// instead of relying on a hand-guessed centre that depends on the model's pivot.
        /// </summary>
        public static BoxCollider FitCollider(GameObject container, float pad = 0.008f)
        {
            var b = GetBounds(container);
            var col = container.GetComponent<BoxCollider>();
            if (col == null) col = container.AddComponent<BoxCollider>();
            Vector3 s = container.transform.lossyScale;
            col.center = container.transform.InverseTransformPoint(b.center);
            col.size = new Vector3(
                (b.size.x + pad * 2f) / Mathf.Max(0.0001f, s.x),
                (b.size.y + pad * 2f) / Mathf.Max(0.0001f, s.y),
                (b.size.z + pad * 2f) / Mathf.Max(0.0001f, s.z));
            return col;
        }
    }
}
