using UnityEngine;

namespace ARVirtualLab.AR
{
    [DisallowMultipleComponent]
    public class ImageTargetManager : MonoBehaviour
    {
        public bool IsTracked { get; private set; } = false;

        public void SetTargetStatus(bool isTracked)
        {
            if (isTracked && !IsTracked)
            {
                IsTracked = true;
                Core.GameManager.Instance?.OnTargetFound();
            }
            else if (!isTracked && IsTracked)
            {
                IsTracked = false;
                Core.GameManager.Instance?.OnTargetLost();
            }
        }
    }
}
