// ============================================================
// IExperiment.cs
// Interface that every experiment module must implement.
// This allows future experiments (Acid-Base, Pendulum, Ohm's Law)
// to be swapped in without changing ExperimentManager.
// ============================================================
using UnityEngine;

namespace ARVirtualLab.Core
{
    public interface IExperiment
    {
        /// <summary>Called once when the experiment is first activated.</summary>
        void Initialize();

        /// <summary>Advance to the next step in the experiment sequence.</summary>
        void NextStep();

        /// <summary>Reset experiment back to Step 1.</summary>
        void ResetExperiment();

        /// <summary>Returns the current step index (1-based).</summary>
        int GetCurrentStep();

        /// <summary>Returns the total number of steps.</summary>
        int GetTotalSteps();

        /// <summary>Returns true when all steps have been completed.</summary>
        bool IsCompleted();

        /// <summary>Called when the Image Target is lost – pause/hide as needed.</summary>
        void OnTargetLost();

        /// <summary>Called when the Image Target is found/restored.</summary>
        void OnTargetFound();

        /// <summary>Human-readable name, shown on the results panel.</summary>
        string ExperimentName { get; }
    }
}
