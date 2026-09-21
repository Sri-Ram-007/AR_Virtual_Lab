// ============================================================
// SandpaperController.cs  —  Standalone Virtual Lab
// Monitors rubbing interactions and tracks cleaning progress (0-100%).
// ============================================================
using UnityEngine;

namespace ARVirtualLab.Standalone
{
    [DisallowMultipleComponent]
    public class SandpaperController : MonoBehaviour
    {
        public float cleaningThreshold = 0.6f; // total delta distance required

        private float _currentProgress = 0f;
        private float _accumulatedMovement = 0f;
        private Vector3 _lastPosition;
        private bool _isCleaningStarted = false;

        public void StartCleaning(Vector3 initialPosition)
        {
            if (_isCleaningStarted) return;
            _lastPosition = initialPosition;
            _isCleaningStarted = true;
            Debug.Log("[Sandpaper] Cleaning started");
        }

        public float RecordMovement(Vector3 currentPosition)
        {
            if (!_isCleaningStarted)
            {
                StartCleaning(currentPosition);
                return 0f;
            }

            float delta = Vector3.Distance(new Vector3(currentPosition.x, 0, currentPosition.z), 
                                           new Vector3(_lastPosition.x, 0, _lastPosition.z));
            
            if (delta > 0.001f)
            {
                _accumulatedMovement += delta;
                _currentProgress = Mathf.Min(100f, (_accumulatedMovement / cleaningThreshold) * 100f);
                _lastPosition = currentPosition;
            }

            return _currentProgress;
        }

        public bool IsClean()
        {
            return _currentProgress >= 100f;
        }

        public void ResetProgress()
        {
            _currentProgress = 0f;
            _accumulatedMovement = 0f;
            _isCleaningStarted = false;
            Debug.Log("[Sandpaper] Cleaning progress reset");
        }
    }
}
