// ============================================================
// ExperimentUIController.cs  —  Standalone Virtual Lab
// Manages all screen-space UI panels, descriptions, buttons,
// observation texts, equations, and safety tips.
// ============================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ARVirtualLab.Standalone
{
    [DisallowMultipleComponent]
    public class ExperimentUIController : MonoBehaviour
    {
        [Header("Main HUD Panels")]
        public GameObject titlePanel;
        public GameObject instructionPanel;
        public GameObject progressPanel;
        public GameObject buttonPanel;

        [Header("Instruction / Progress Text")]
        public TMP_Text instructionText;
        public TMP_Text progressText;
        public TMP_Text stepCounterText;

        [Header("Popup Info Panels")]
        public GameObject observationPanel;
        public GameObject resultPanel;
        public GameObject safetyPanel;

        [Header("Control Buttons")]
        public Button startButton;
        public Button nextButton;
        public Button backButton;
        public Button resetButton;
        public Button safetyButton;

        [Header("Observation / Equation Panel Texts")]
        public TMP_Text observationContentText;
        public TMP_Text equationContentText;
        public TMP_Text resultContentText;

        private void Awake()
        {
            // Initial UI panel state (only show TitlePanel at first)
            ShowPanel(titlePanel, true);
            ShowPanel(instructionPanel, false);
            ShowPanel(progressPanel, false);
            ShowPanel(buttonPanel, false);

            ShowPanel(observationPanel, false);
            ShowPanel(resultPanel, false);
            ShowPanel(safetyPanel, false);
        }

        public void ShowPanel(GameObject panel, bool show)
        {
            if (panel != null)
                panel.SetActive(show);
        }

        public void UpdateStepText(string stepTitle, string instruction, string progress)
        {
            if (stepCounterText != null) stepCounterText.text = stepTitle;
            if (instructionText != null) instructionText.text = instruction;
            if (progressText != null) progressText.text = progress;
        }
    }
}
