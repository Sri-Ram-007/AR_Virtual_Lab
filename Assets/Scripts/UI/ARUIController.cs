// ============================================================
// ARUIController.cs  —  AR Virtual Lab
// Central UI orchestrator for all panels and buttons.
// All panels are World-Space Canvas children of ImageTarget
// so they follow the physical marker.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARVirtualLab.UI
{
    [DisallowMultipleComponent]
    public class ARUIController : MonoBehaviour
    {
        // ── Inspector: Panels ─────────────────────────────────────
        [Header("Panels")]
        public GameObject scanningOverlay;          // Screen-space overlay
        public GameObject mainMenuPanel;            // "AR Experiment Menu"
        public GameObject videoControlPanel;        // Play/Pause/Replay/Continue
        public GameObject experimentStepPanel;      // Step text + Next/Reset
        public GameObject resultsPanel;             // filled by ResultManager
        public GameObject aiPanel;                  // Gemini Q&A

        [Header("Scanning Overlay")]
        public TMP_Text scanningText;

        [Header("Menu Buttons")]
        public Button watchVideoButton;
        public Button performExperimentButton;
        public Button experimentStepsButton;
        public Button observeResultButton;
        public Button aiExplanationButton;
        public Button resetExperimentButton;

        [Header("Video Control Buttons")]
        public Button videoPlayButton;
        public Button videoPauseButton;
        public Button videoReplayButton;
        public Button videoContinueButton;
        public Button videoStartExperimentButton;   // appears when video ends

        [Header("Experiment Step Buttons")]
        public Button nextStepButton;
        public Button resetButton;
        public Button viewResultsButton;
        public TMP_Text stepCounterText;
        public TMP_Text feedbackText;

        // ── Feedback Toast ────────────────────────────────────────
        [Header("Feedback Toast")]
        public CanvasGroup feedbackToastGroup;
        public TMP_Text    feedbackToastText;

        // ──────────────────────────────────────────────────────────
        private Coroutine _feedbackCoroutine;

        private void Awake()
        {
            // All panels start hidden.
            SetPanel(scanningOverlay,        false);
            SetPanel(mainMenuPanel,          false);
            SetPanel(videoControlPanel,      false);
            SetPanel(experimentStepPanel,    false);
            SetPanel(resultsPanel,           false);
            SetPanel(aiPanel,                false);

            if (feedbackToastGroup != null)
                feedbackToastGroup.alpha = 0f;

            if (videoStartExperimentButton != null)
                videoStartExperimentButton.gameObject.SetActive(false);

            if (viewResultsButton != null)
                viewResultsButton.gameObject.SetActive(false);
        }

        private void Start()
        {
            WireMenuButtons();
            WireVideoButtons();
            WireExperimentButtons();
        }

        // ── Panel Helpers ─────────────────────────────────────────
        private void SetPanel(GameObject panel, bool active)
        {
            if (panel != null) panel.SetActive(active);
        }

        public void ShowScanningOverlay(bool show)
        {
            SetPanel(scanningOverlay, show);
        }

        public void ShowMenu(bool show)
        {
            SetPanel(mainMenuPanel, show);
        }

        public void ShowVideoControls(bool show)
        {
            SetPanel(videoControlPanel, show);
            if (!show && videoStartExperimentButton != null)
                videoStartExperimentButton.gameObject.SetActive(false);
        }

        public void ShowExperimentUI(bool show)
        {
            SetPanel(experimentStepPanel, show);
        }

        public void ShowResultsPanel(bool show)
        {
            SetPanel(resultsPanel, show);
        }

        public void ShowAIPanel(bool show)
        {
            SetPanel(aiPanel, show);
        }

        // ── Video Events ──────────────────────────────────────────
        /// <summary>Called by ARVideoController when the video finishes.</summary>
        public void OnVideoCompleted()
        {
            if (videoStartExperimentButton != null)
                videoStartExperimentButton.gameObject.SetActive(true);
        }

        // ── Experiment Events ─────────────────────────────────────
        public void UpdateStepCounter(int current, int total)
        {
            if (stepCounterText != null)
                stepCounterText.text = $"Step {current} / {total}";
        }

        public void ShowViewResultsButton(bool show)
        {
            if (viewResultsButton != null)
                viewResultsButton.gameObject.SetActive(show);
        }

        // ── Feedback Toast ────────────────────────────────────────
        public void ShowFeedback(string message, bool success)
        {
            if (feedbackText != null) feedbackText.text = message;

            if (_feedbackCoroutine != null) StopCoroutine(_feedbackCoroutine);
            if (feedbackToastGroup != null)
                _feedbackCoroutine = StartCoroutine(FadeToast());
        }

        private IEnumerator FadeToast()
        {
            feedbackToastGroup.alpha = 1f;
            yield return new WaitForSeconds(2.5f);
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.deltaTime * 1.5f;
                feedbackToastGroup.alpha = Mathf.Max(0f, t);
                yield return null;
            }
            feedbackToastGroup.alpha = 0f;
        }

        // ── Button Wiring ─────────────────────────────────────────
        private void WireMenuButtons()
        {
            if (watchVideoButton != null)
                watchVideoButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestWatchVideo());

            if (performExperimentButton != null)
                performExperimentButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestStartExperiment());

            if (experimentStepsButton != null)
                experimentStepsButton.onClick.AddListener(() =>
                {
                    Core.GameManager.Instance?.RequestStartExperiment();
                });

            if (observeResultButton != null)
                observeResultButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestShowResults());

            if (aiExplanationButton != null)
                aiExplanationButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestShowAI());

            if (resetExperimentButton != null)
                resetExperimentButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestResetExperiment());
        }

        private void WireVideoButtons()
        {
            // These call ARVideoController directly; wired via Inspector or below.
            // If wired in Inspector, these AddListeners will be ignored gracefully.
            if (videoContinueButton != null)
                videoContinueButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestBackToMenu());

            if (videoStartExperimentButton != null)
                videoStartExperimentButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestStartExperiment());
        }

        private void WireExperimentButtons()
        {
            if (nextStepButton != null)
                nextStepButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestNextStep());

            if (resetButton != null)
                resetButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestResetExperiment());

            if (viewResultsButton != null)
                viewResultsButton.onClick.AddListener(() => Core.GameManager.Instance?.RequestShowResults());
        }
    }
}
