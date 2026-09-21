// ============================================================
// TongsController.cs  —  Standalone Virtual Lab
// Manages tongs grabbing and holding the magnesium ribbon or residue.
// ============================================================
using UnityEngine;

namespace ARVirtualLab.Standalone
{
    [DisallowMultipleComponent]
    public class TongsController : MonoBehaviour
    {
        public Transform grabPoint; // Child object representing the tip of the tongs
        
        private GameObject _heldObject = null;

        public bool IsHoldingObject => _heldObject != null;

        public void GrabObject(GameObject obj)
        {
            if (obj == null) return;
            _heldObject = obj;

            obj.transform.SetParent(grabPoint != null ? grabPoint : transform, true);
            obj.transform.localPosition = Vector3.zero;
            obj.transform.localRotation = Quaternion.identity;

            // Remove/disable direct object interaction while held in tongs
            var io = obj.GetComponent<StandaloneInteractable>();
            if (io != null)
            {
                io.Highlight(false);
                io.isInteractable = false;
            }

            Debug.Log("[Tongs] Attached object: " + obj.name);
        }

        public void ReleaseObject()
        {
            if (_heldObject != null)
            {
                var io = _heldObject.GetComponent<StandaloneInteractable>();
                if (io != null) io.isInteractable = true;
                _heldObject.transform.SetParent(null, true);
                _heldObject = null;
            }
        }

        public GameObject GetHeldObject()
        {
            return _heldObject;
        }

        public void ForceSetHeldObject(GameObject obj)
        {
            _heldObject = obj;
        }
    }
}
