// ============================================================
// StandaloneUIController.cs  —  AR Virtual Lab Standalone
// Controls standard Canvas UI panels, buttons, descriptions,
// progress indicators, and feedback messages for Mobile Portrait.
// ============================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARVirtualLab.Standalone
{
    [DisallowMultipleComponent]
    public class StandaloneUIController : MonoBehaviour
    {
        [Header("Introduction Panel")]
        public GameObject introPanel;
        public Button startButton;
        public Button introSafetyButton;
        public Button introInstructionsButton;

        [Header("Experiment UI")]
        public GameObject experimentPanel;
        public TMP_Text stepTitleText;
        public TMP_Text stepDescriptionText;
        public TMP_Text progressText;
        public Button uiNextButton;
        public Button uiResetButton;
        public Button uiSafetyButton;

        [Header("Secondary Panels")]
        public GameObject observationPanel;
        public GameObject equationPanel;
        public GameObject resultPanel;
        public GameObject safetyPanel;
        public GameObject instructionsPanel;

        [Header("Close Buttons for Panels")]
        public Button closeObservationButton;
        public Button closeEquationButton;
        public Button closeResultButton;
        public Button closeSafetyButton;
        public Button closeInstructionsButton;

        [Header("Panel Toggle Buttons")]
        public Button openObservationButton;
        public Button openEquationButton;
        public Button openResultButton;

        [Header("Feedback Toasts")]
        public CanvasGroup feedbackToastGroup;
        public TMP_Text feedbackToastText;

        private Coroutine _toastCoroutine;

        private void Awake()
        {
            // Set initial panel states
            ShowPanel(introPanel, true);
            ShowPanel(experimentPanel, false);
            ShowPanel(observationPanel, false);
            ShowPanel(equationPanel, false);
            ShowPanel(resultPanel, false);
            ShowPanel(safetyPanel, false);
            ShowPanel(instructionsPanel, false);

            if (feedbackToastGroup != null)
                feedbackToastGroup.alpha = 0f;

            // Wire UI toggle buttons
            if (openObservationButton != null) openObservationButton.onClick.AddListener(() => ShowPanel(observationPanel, true));
            if (openEquationButton != null) openEquationButton.onClick.AddListener(() => ShowPanel(equationPanel, true));
            if (openResultButton != null) openResultButton.onClick.AddListener(() => ShowPanel(resultPanel, true));

            if (closeObservationButton != null) closeObservationButton.onClick.AddListener(() => ShowPanel(observationPanel, false));
            if (closeEquationButton != null) closeEquationButton.onClick.AddListener(() => ShowPanel(equationPanel, false));
            if (closeResultButton != null) closeResultButton.onClick.AddListener(() => ShowPanel(resultPanel, false));
            if (closeSafetyButton != null) closeSafetyButton.onClick.AddListener(() => ShowPanel(safetyPanel, false));
            if (closeInstructionsButton != null) closeInstructionsButton.onClick.AddListener(() => ShowPanel(instructionsPanel, false));

            if (introSafetyButton != null) introSafetyButton.onClick.AddListener(() => ShowPanel(safetyPanel, true));
            if (introInstructionsButton != null) introInstructionsButton.onClick.AddListener(() => ShowPanel(instructionsPanel, true));
            if (uiSafetyButton != null) uiSafetyButton.onClick.AddListener(() => ShowPanel(safetyPanel, true));
        }

        public void ShowPanel(GameObject panel, bool active)
        {
            if (panel != null)
                panel.SetActive(active);
        }

        public void SetStepInfo(string title, string description, string progress)
        {
            if (stepTitleText != null) stepTitleText.text = title;
            if (stepDescriptionText != null) stepDescriptionText.text = description;
            if (progressText != null) progressText.text = progress;
        }

        public void ToggleNextButton(bool visible)
        {
            if (uiNextButton != null)
                uiNextButton.gameObject.SetActive(visible);
        }

        public void TogglePanelButtons(bool showObservation, bool showEquation, bool showResult)
        {
            if (openObservationButton != null) openObservationButton.gameObject.SetActive(showObservation);
            if (openEquationButton != null) openEquationButton.gameObject.SetActive(showEquation);
            if (openResultButton != null) openResultButton.gameObject.SetActive(showResult);
        }

        public void ShowFeedback(string message, bool isSuccess)
        {
            if (feedbackToastText != null)
            {
                feedbackToastText.text = message;
                feedbackToastText.color = isSuccess ? new Color(0.35f, 1f, 0.45f) : new Color(1f, 0.4f, 0.4f);
            }

            if (_toastCoroutine != null) StopCoroutine(_toastCoroutine);
            if (feedbackToastGroup != null)
                _toastCoroutine = StartCoroutine(FadeToast());
        }

        private IEnumerator FadeToast()
        {
            feedbackToastGroup.alpha = 1f;
            yield return new WaitForSeconds(2.8f);
            float t = 1f;
            while (t > 0f)
            {
                t -= Time.deltaTime * 2.0f;
                feedbackToastGroup.alpha = Mathf.Max(0f, t);
                yield return null;
            }
            feedbackToastGroup.alpha = 0f;
        }
    }
}
