// ============================================================
// GameManager.cs  —  AR Virtual Lab
// Singleton orchestrator. Owns all manager references and drives
// the top-level state machine.
// ============================================================
using UnityEngine;
using ARVirtualLab.Core;
using ARVirtualLab.AR;
using ARVirtualLab.Video;
using ARVirtualLab.Experiments;
using ARVirtualLab.Results;
using ARVirtualLab.AI;
using ARVirtualLab.UI;

namespace ARVirtualLab.Core
{
    [DisallowMultipleComponent]
    public class GameManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────
        public static GameManager Instance { get; private set; }

        // ── Inspector References ──────────────────────────────────
        [Header("Managers")]
        public ImageTargetManager imageTargetManager;
        public ARVideoController  videoController;
        public ExperimentManager  experimentManager;
        public ExperimentResultManager resultManager;
        public AIExplanationManager aiManager;
        public ARUIController     uiController;

        // ── State ─────────────────────────────────────────────────
        private AppState _state = AppState.Initializing;

        public AppState CurrentState => _state;

        // ──────────────────────────────────────────────────────────
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            // Do NOT DontDestroyOnLoad – everything lives in one scene.
        }

        private void Start()
        {
            // Initial state: scanning for image target.
            SetState(AppState.Scanning);
        }

        // ── State Machine ─────────────────────────────────────────
        public void SetState(AppState newState)
        {
            Debug.Log($"[GameManager] {_state} → {newState}");
            _state = newState;

            switch (_state)
            {
                case AppState.Scanning:
                    uiController?.ShowScanningOverlay(true);
                    uiController?.ShowMenu(false);
                    videoController?.HideVideo();
                    experimentManager?.HideExperiment();
                    resultManager?.HideResults();
                    aiManager?.HideAI();
                    break;

                case AppState.Menu:
                    uiController?.ShowScanningOverlay(false);
                    uiController?.ShowMenu(true);
                    videoController?.HideVideo();
                    experimentManager?.HideExperiment();
                    resultManager?.HideResults();
                    aiManager?.HideAI();
                    break;

                case AppState.VideoPhase:
                    uiController?.ShowMenu(false);
                    videoController?.ShowAndPlay();
                    break;

                case AppState.ExperimentPhase:
                    videoController?.StopVideo();
                    videoController?.HideVideo();
                    experimentManager?.ShowExperiment();
                    uiController?.ShowExperimentUI(true);
                    break;

                case AppState.ResultPhase:
                    experimentManager?.HideExperiment();
                    uiController?.ShowExperimentUI(false);
                    resultManager?.ShowResults();
                    break;

                case AppState.AIPhase:
                    aiManager?.ShowAI();
                    break;
            }
        }

        // ── Called by ImageTargetManager ──────────────────────────
        public void OnTargetFound()
        {
            Debug.Log("[GameManager] Target FOUND");
            if (_state == AppState.Scanning || _state == AppState.Initializing)
                SetState(AppState.Menu);

            // Restore video / experiment if they were active before target loss.
            videoController?.OnTargetFound();
            experimentManager?.OnTargetFound();
        }

        public void OnTargetLost()
        {
            Debug.Log("[GameManager] Target LOST");
            videoController?.OnTargetLost();
            experimentManager?.OnTargetLost();
            uiController?.ShowScanningOverlay(true);
        }

        // ── UI Button Callbacks ───────────────────────────────────
        public void RequestWatchVideo()
        {
            if (_state == AppState.Menu || _state == AppState.ExperimentPhase)
                SetState(AppState.VideoPhase);
        }

        public void RequestStartExperiment()
        {
            SetState(AppState.ExperimentPhase);
            experimentManager?.StartExperiment();
        }

        public void RequestNextStep()
        {
            if (_state == AppState.ExperimentPhase)
            {
                experimentManager?.NextStep();
                if (experimentManager != null && experimentManager.IsCompleted())
                    SetState(AppState.ResultPhase);
            }
        }

        public void RequestResetExperiment()
        {
            SetState(AppState.ExperimentPhase);
            experimentManager?.ResetExperiment();
        }

        public void RequestShowResults()
        {
            SetState(AppState.ResultPhase);
        }

        public void RequestShowAI()
        {
            SetState(AppState.AIPhase);
        }

        public void RequestBackToMenu()
        {
            // Only if target is still tracked.
            if (imageTargetManager != null && imageTargetManager.IsTracked)
                SetState(AppState.Menu);
            else
                SetState(AppState.Scanning);
        }
    }
}
