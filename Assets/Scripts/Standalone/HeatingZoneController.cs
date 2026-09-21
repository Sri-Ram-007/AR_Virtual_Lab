// ============================================================
// HeatingZoneController.cs  —  Standalone Virtual Lab
// Attached to the burner flame's trigger zone.
// ============================================================
using UnityEngine;
using System;

namespace ARVirtualLab.Standalone
{
    [RequireComponent(typeof(Collider))]
    public class HeatingZoneController : MonoBehaviour
    {
        public event Action<GameObject> OnRibbonEntered;

        private void OnTriggerEnter(Collider other)
        {
            // Check for StandaloneInteractable (the correct component used in this project)
            var io = other.GetComponentInParent<StandaloneInteractable>();
            if (io != null && io.objectType == StandaloneObjectType.MagnesiumRibbon)
            {
                OnRibbonEntered?.Invoke(io.gameObject);
            }
        }
    }
}
