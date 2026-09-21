// ============================================================
// ExperimentManager.cs  —  AR Virtual Lab
// Loads and drives the active IExperiment implementation.
// Adding a new experiment: create a new IExperiment class and
// register it here; ExperimentManager handles the rest.
// ============================================================
using UnityEngine;
using ARVirtualLab.Core;
using ARVirtualLab.Experiments.MagnesiumRibbon;

namespace ARVirtualLab.Experiments
{
    [DisallowMultipleComponent]
    public class ExperimentManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("Experiment Root (parent of all lab objects)")]
        public GameObject experimentRoot;

        [Header("Active Experiment")]
        [Tooltip("Drop MagnesiumExperiment (or any IExperiment component) here.")]
        public MagnesiumExperiment magnesiumExperiment;

        // ── Private ───────────────────────────────────────────────
        private IExperiment _activeExperiment;
        private bool _initialized = false;

        // ──────────────────────────────────────────────────────────
        private void Awake()
        {
            // Default to magnesium experiment.
            _activeExperiment = magnesiumExperiment;
        }

        // ── Public API ────────────────────────────────────────────
        public void ShowExperiment()
        {
            if (experimentRoot != null) experimentRoot.SetActive(true);

            if (!_initialized)
            {
                _activeExperiment?.Initialize();
                _initialized = true;
            }
        }

        public void HideExperiment()
        {
            if (experimentRoot != null) experimentRoot.SetActive(false);
        }

        public void StartExperiment()
        {
            if (experimentRoot != null) experimentRoot.SetActive(true);
            _activeExperiment?.Initialize();
            _initialized = true;
        }

        public void NextStep()
        {
            if (!_initialized) StartExperiment();
            _activeExperiment?.NextStep();
        }

        public void ResetExperiment()
        {
            _activeExperiment?.ResetExperiment();
            _initialized = true;
        }

        public bool IsCompleted() => _activeExperiment?.IsCompleted() ?? false;

        public int GetCurrentStep() => _activeExperiment?.GetCurrentStep() ?? 0;
        public int GetTotalSteps()  => _activeExperiment?.GetTotalSteps() ?? 0;
        public string GetExperimentName() => _activeExperiment?.ExperimentName ?? "Unknown Experiment";

        public void OnTargetFound()
        {
            if (experimentRoot != null && _initialized)
                experimentRoot.SetActive(true);
            _activeExperiment?.OnTargetFound();
        }

        public void OnTargetLost()
        {
            _activeExperiment?.OnTargetLost();
            if (experimentRoot != null) experimentRoot.SetActive(false);
        }
    }
}
