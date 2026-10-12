using UnityEngine;

namespace ARVirtualLab.UI
{
    /// <summary>The space the apparatus of an experiment takes up: its meshes, without the bench, UI or effects.</summary>
    public static class ApparatusBounds
    {
        public static bool TryGet(Transform root, GameObject bench, out Bounds bounds)
        {
            bounds = default(Bounds);
            if (root == null) return false;
            bool any = false;
            foreach (var r in root.GetComponentsInChildren<Renderer>())
            {
                if (!(r is MeshRenderer || r is SkinnedMeshRenderer) || !r.enabled) continue;
                if (bench != null && r.transform.IsChildOf(bench.transform)) continue;
                if (r.GetComponentInParent<Canvas>() != null || r.GetComponent<HintRing>() != null) continue;
                if (!any) { bounds = r.bounds; any = true; } else bounds.Encapsulate(r.bounds);
            }
            return any;
        }
    }
}
