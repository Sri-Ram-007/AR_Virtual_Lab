// ============================================================
// AIExplanationManager.cs  —  AR Virtual Lab
// Integrates Gemini REST API for student Q&A.
// 
// Configuration:
//   • Set GeminiApiKey in the Inspector (or leave blank for offline mode).
//   • The system prompt injects experiment context automatically.
//   • If no key / no internet: uses cached local fallback answers.
// ============================================================
using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;

namespace ARVirtualLab.AI
{
    [DisallowMultipleComponent]
    public class AIExplanationManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────
        [Header("Gemini Configuration")]
        [Tooltip("Paste your Gemini API key here. Leave blank for offline fallback mode.")]
        public string GeminiApiKey = "";   // Replace with your key

        [Tooltip("Gemini model endpoint.")]
        public string GeminiModel = "gemini-2.0-flash";

        [Header("UI References")]
        public GameObject aiPanel;
        public TMP_Text   responseText;
        public TMP_InputField questionInput;
        public TMP_Text   statusText;

        [Header("Quick Question Buttons (optional)")]
        public UnityEngine.UI.Button q1Button;  // "Why does Mg burn brightly?"
        public UnityEngine.UI.Button q2Button;  // "What is formed?"
        public UnityEngine.UI.Button q3Button;  // "Type of reaction?"
        public UnityEngine.UI.Button q4Button;  // "Simple explanation"

        // ── Private ───────────────────────────────────────────────
        private const string API_URL_TEMPLATE =
            "https://generativelanguage.googleapis.com/v1beta/models/{0}:generateContent?key={1}";

        private const string SYSTEM_CONTEXT =
            "You are an educational AI assistant for a chemistry virtual laboratory application. " +
            "Students have just performed the experiment: Burning Magnesium Ribbon. " +
            "In this experiment, magnesium ribbon was heated in air (oxygen), " +
            "producing a bright white flame and forming magnesium oxide (MgO). " +
            "Chemical equation: 2Mg + O₂ → 2MgO. " +
            "This is a combination/synthesis reaction. " +
            "Answer student questions simply, clearly, and at a Grade 9-10 level. " +
            "Keep answers under 150 words. Do not control the experiment.";

        // Cached offline answers keyed by simplified question patterns.
        private static readonly (string keyword, string answer)[] OfflineAnswers =
        {
            ("bright", "Magnesium burns with a bright white flame because it releases a huge amount of energy when it reacts with oxygen. The burning temperature is so high (around 3100°C) that it produces intense white light, including ultraviolet radiation. This is why magnesium was used in old camera flashbulbs!"),
            ("form", "When magnesium burns, it reacts with oxygen in the air to form Magnesium Oxide (MgO). This appears as a white powdery ash. The reaction is: 2Mg + O₂ → 2MgO."),
            ("type", "This is a Combination Reaction (also called a Synthesis Reaction) because two substances (Magnesium and Oxygen) combine to form a single product (Magnesium Oxide). It is also a Combustion Reaction since magnesium burns in oxygen."),
            ("simple", "We heated magnesium ribbon in air. When hot enough, it caught fire and burned with a bright white light. The magnesium combined with oxygen from the air. After burning, a white powder was left — this is Magnesium Oxide. The equation is: 2Mg + O₂ → 2MgO."),
            ("clean", "Magnesium ribbon is cleaned before the experiment to remove the layer of magnesium oxide already on its surface. This oxide layer forms naturally when magnesium is exposed to air. If not cleaned, the oxide layer acts as a barrier and slows down or prevents ignition."),
            ("insufficient", "If there is not enough oxygen, the magnesium will not burn completely. It may produce magnesium nitride (Mg₃N₂) or incompletely react, leaving some magnesium unburned. Complete combustion requires sufficient oxygen supply."),
        };

        // ──────────────────────────────────────────────────────────
        private void Awake()
        {
            if (aiPanel != null) aiPanel.SetActive(false);
        }

        private void Start()
        {
            // Wire quick question buttons.
            if (q1Button != null) q1Button.onClick.AddListener(() => AskQuestion("Why does magnesium burn with a bright white flame?"));
            if (q2Button != null) q2Button.onClick.AddListener(() => AskQuestion("What is formed when magnesium reacts with oxygen?"));
            if (q3Button != null) q3Button.onClick.AddListener(() => AskQuestion("What type of reaction is this?"));
            if (q4Button != null) q4Button.onClick.AddListener(() => AskQuestion("Explain the experiment in simple words."));
        }

        // ── Public API ────────────────────────────────────────────
        public void ShowAI()
        {
            if (aiPanel != null) aiPanel.SetActive(true);
            SetStatus(string.IsNullOrEmpty(GeminiApiKey)
                ? "Offline mode – using built-in explanations."
                : "Connected to Gemini AI. Ask your question!");
        }

        public void HideAI()
        {
            if (aiPanel != null) aiPanel.SetActive(false);
        }

        /// <summary>Called by Submit button in the Inspector or quick-question buttons.</summary>
        public void OnSubmitQuestion()
        {
            string question = questionInput != null ? questionInput.text.Trim() : "";
            if (string.IsNullOrEmpty(question)) return;
            AskQuestion(question);
        }

        // ── Core Logic ────────────────────────────────────────────
        public void AskQuestion(string question)
        {
            if (questionInput != null) questionInput.text = question;
            SetResponse("Thinking…");
            SetStatus("Processing your question…");

            if (!string.IsNullOrEmpty(GeminiApiKey))
                StartCoroutine(CallGeminiAPI(question));
            else
                UseOfflineFallback(question);
        }

        private IEnumerator CallGeminiAPI(string question)
        {
            string url = string.Format(API_URL_TEMPLATE, GeminiModel, GeminiApiKey);

            // Build request body.
            string jsonBody = BuildRequestJson(question);
            byte[] bodyRaw  = Encoding.UTF8.GetBytes(jsonBody);

            using var request = new UnityWebRequest(url, "POST");
            request.uploadHandler   = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[AIExplanationManager] Gemini request failed: {request.error}. Using offline fallback.");
                UseOfflineFallback(question);
                yield break;
            }

            string responseJson = request.downloadHandler.text;
            string answer = ParseGeminiResponse(responseJson);

            if (string.IsNullOrEmpty(answer))
            {
                Debug.LogWarning("[AIExplanationManager] Could not parse Gemini response. Using offline fallback.");
                UseOfflineFallback(question);
            }
            else
            {
                SetResponse(answer);
                SetStatus("Answered by Gemini AI");
            }
        }

        private string BuildRequestJson(string question)
        {
            // Minimal Gemini REST payload.
            string combined = SYSTEM_CONTEXT + "\n\nStudent question: " + question;
            // Escape JSON special characters.
            combined = combined.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n");
            return $"{{\"contents\":[{{\"parts\":[{{\"text\":\"{combined}\"}}]}}]}}";
        }

        private string ParseGeminiResponse(string json)
        {
            try
            {
                // Simple substring parse – no external JSON library needed.
                const string textKey = "\"text\":\"";
                int start = json.IndexOf(textKey, StringComparison.Ordinal);
                if (start < 0) return null;
                start += textKey.Length;
                int end = start;
                bool escape = false;
                while (end < json.Length)
                {
                    char c = json[end];
                    if (escape) { escape = false; }
                    else if (c == '\\') { escape = true; }
                    else if (c == '"') break;
                    end++;
                }
                string raw = json.Substring(start, end - start);
                // Unescape common sequences.
                raw = raw.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
                return raw;
            }
            catch (Exception ex)
            {
                Debug.LogError("[AIExplanationManager] JSON parse error: " + ex.Message);
                return null;
            }
        }

        private void UseOfflineFallback(string question)
        {
            string lower = question.ToLower();
            foreach (var (keyword, answer) in OfflineAnswers)
            {
                if (lower.Contains(keyword))
                {
                    SetResponse(answer);
                    SetStatus("Offline explanation (no API key configured)");
                    return;
                }
            }

            // Generic fallback.
            SetResponse(
                "Magnesium (Mg) reacts with oxygen (O₂) when heated to produce Magnesium Oxide (MgO).\n\n" +
                "Equation: 2Mg + O₂ → 2MgO\n\n" +
                "The bright white flame is due to the very high combustion temperature. " +
                "MgO is a white powdery solid — a combination/synthesis reaction.");
            SetStatus("Offline explanation – set a Gemini API key for full AI answers");
        }

        private void SetResponse(string text)
        {
            if (responseText != null) responseText.text = text;
        }

        private void SetStatus(string text)
        {
            if (statusText != null) statusText.text = text;
        }
    }
}
