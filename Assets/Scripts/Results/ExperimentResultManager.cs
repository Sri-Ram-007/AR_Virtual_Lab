// ============================================================
// ExperimentResultManager.cs  —  AR Virtual Lab
// Populates and shows the results panel after the experiment.
// ============================================================
using UnityEngine;
using TMPro;
using ARVirtualLab.Experiments;

namespace ARVirtualLab.Results
{
    [DisallowMultipleComponent]
    public class ExperimentResultManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("Results Panel")]
        public GameObject resultsPanel;

        [Header("Result Text Fields")]
        public TMP_Text experimentNameText;
        public TMP_Text materialsText;
        public TMP_Text procedureText;
        public TMP_Text observationText;
        public TMP_Text equationText;
        public TMP_Text finalProductText;
        public TMP_Text resultSummaryText;

        [Header("Dependencies")]
        public ExperimentManager experimentManager;

        // ──────────────────────────────────────────────────────────
        private void Awake()
        {
            if (resultsPanel != null) resultsPanel.SetActive(false);
        }

        // ── Public API ────────────────────────────────────────────
        public void ShowResults()
        {
            if (resultsPanel != null) resultsPanel.SetActive(true);
            PopulateResults();
        }

        public void HideResults()
        {
            if (resultsPanel != null) resultsPanel.SetActive(false);
        }

        // ── Internal ──────────────────────────────────────────────
        private void PopulateResults()
        {
            string name = experimentManager != null
                ? experimentManager.GetExperimentName()
                : "Burning Magnesium Ribbon";

            SetText(experimentNameText, "Experiment: " + name);

            SetText(materialsText,
                "Materials Used:\n" +
                "• Magnesium Ribbon\n" +
                "• Bunsen Burner / Spirit Lamp\n" +
                "• Metal Tongs\n" +
                "• Heat-resistant Surface");

            SetText(procedureText,
                "Procedure:\n" +
                "1. Took the magnesium ribbon.\n" +
                "2. Held it with tongs.\n" +
                "3. Heated it over the burner flame.\n" +
                "4. Observed a bright white flame.\n" +
                "5. Collected the white ash formed.\n" +
                "6. Identified the product as Magnesium Oxide.");

            SetText(observationText,
                "Observation:\n" +
                "• Magnesium burns with a brilliant white flame.\n" +
                "• A white powdery substance is formed.");

            SetText(equationText,
                "Chemical Reaction:\n" +
                "2Mg  +  O₂  →  2MgO");

            SetText(finalProductText,
                "Final Product:\n" +
                "Magnesium Oxide (MgO) — white powder");

            SetText(resultSummaryText,
                "Result:\n" +
                "Magnesium reacts with oxygen in air during combustion " +
                "to form Magnesium Oxide. This is a combination (synthesis) reaction. " +
                "The dazzling white light is due to the high temperature produced " +
                "and the emission of UV radiation from burning magnesium.");
        }

        private void SetText(TMP_Text field, string value)
        {
            if (field != null) field.text = value;
        }
    }
}
