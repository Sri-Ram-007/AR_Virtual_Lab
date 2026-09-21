// ============================================================
// ARTargetController.cs  —  AR Virtual Lab  
// Compatibility alias — the actual implementation is in
// ImageTargetManager.cs (Assets/Scripts/AR/).
// This file exists so that if any existing scene has an
// ARTargetController reference, it resolves without error.
// ============================================================
using UnityEngine;

/// <summary>
/// Legacy alias. Use ImageTargetManager.cs going forward.
/// </summary>
[AddComponentMenu("AR Virtual Lab/AR Target Controller (Legacy)")]
public class ARTargetController : MonoBehaviour
{
    private void Awake()
    {
        Debug.Log("[ARTargetController] This is a legacy alias. " +
                  "The real implementation is ARVirtualLab.AR.ImageTargetManager.");
    }
}
