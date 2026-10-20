// ============================================================
// MentorBackend.cs  —  AR Virtual Lab
// Answers a student's question. Uses Google Gemini when a key (or a proxy address) is configured, and falls back to the
// built-in answers in MentorKnowledge when there is no key, no internet, or the AI refuses / fails.
//
// Configuration (never commit a key): create  Assets/Resources/AIConfig.json  with
//     { "apiKey": "YOUR_KEY", "model": "gemini-flash-lite-latest", "endpoint": "" }
// "endpoint" is optional: set it to your own server's address and the app will send the request there without a key
// (the right setup for a published app, since a key inside an APK can be extracted). That server receives the same JSON
// Gemini expects and must answer in Gemini's format.
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace ARVirtualLab.AI
{
    public class MentorMessage { public bool FromStudent; public string Text; }

    public class MentorReply
    {
        public string Text;
        public bool FromAI;        // true = the language model answered, false = a built-in answer
        public string Note;        // short reason shown under offline answers ("no internet", "no key set"...)
    }

    public static class MentorConfig
    {
        [Serializable] private class Cfg { public string apiKey = ""; public string model = "gemini-flash-lite-latest"; public string endpoint = ""; }

        private static Cfg _cfg;

        private static Cfg Get()
        {
            if (_cfg != null) return _cfg;
            _cfg = new Cfg();
            try
            {
                var ta = Resources.Load<TextAsset>("AIConfig");
                if (ta != null) JsonUtility.FromJsonOverwrite(ta.text, _cfg);
                string saved = PlayerPrefs.GetString("mentor.apiKey", "");
                if (string.IsNullOrEmpty(_cfg.apiKey) && !string.IsNullOrEmpty(saved)) _cfg.apiKey = saved;
            }
            catch (Exception e) { Debug.LogWarning("[Mentor] could not read AIConfig: " + e.Message); }
            if (string.IsNullOrEmpty(_cfg.model)) _cfg.model = "gemini-flash-lite-latest";
            return _cfg;
        }

        public static string ApiKey { get { return Get().apiKey ?? ""; } }
        public static string Model { get { return Get().model; } }
        public static string Endpoint { get { return Get().endpoint ?? ""; } }
        public static bool Online { get { return !string.IsNullOrEmpty(ApiKey) || !string.IsNullOrEmpty(Endpoint); } }
    }

    public static class MentorBackend
    {
        private const int MaxHistory = 8;

        // ---------------------------------------------------------------- public entry point
        public static IEnumerator Ask(string experimentId, string stepContext, IList<MentorMessage> history, string question, Action<MentorReply> done)
        {
            var exp = MentorKnowledge.Get(experimentId);

            if (!MentorConfig.Online)
            {
                done(Offline(exp, question, "Offline mode: built-in answers"));
                yield break;
            }

            string url = !string.IsNullOrEmpty(MentorConfig.Endpoint)
                ? MentorConfig.Endpoint
                : "https://generativelanguage.googleapis.com/v1beta/models/" + MentorConfig.Model + ":generateContent";

            byte[] body = Encoding.UTF8.GetBytes(BuildRequest(exp, stepContext, history, question));
            using (var req = new UnityWebRequest(url, "POST"))
            {
                req.uploadHandler = new UploadHandlerRaw(body);
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                if (!string.IsNullOrEmpty(MentorConfig.ApiKey)) req.SetRequestHeader("x-goog-api-key", MentorConfig.ApiKey);
                req.timeout = 25;
                yield return req.SendWebRequest();

                string text = req.downloadHandler != null ? req.downloadHandler.text : "";
                if (req.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogWarning("[Mentor] request failed: " + req.error + " | " + Trim(text));
                    string why = req.result == UnityWebRequest.Result.ConnectionError ? "no internet" : ErrorMessage(text, "the AI could not answer");
                    done(Offline(exp, question, "Offline answer (" + why + ")"));
                    yield break;
                }

                string answer = ParseAnswer(text);
                if (string.IsNullOrEmpty(answer))
                {
                    Debug.LogWarning("[Mentor] empty/blocked answer: " + Trim(text));
                    done(Offline(exp, question, "Offline answer (the AI gave no reply)"));
                    yield break;
                }
                done(new MentorReply { Text = Tidy(answer), FromAI = true, Note = "Answered by AI" });
            }
        }

        // ---------------------------------------------------------------- request
        private static string BuildRequest(MentorExperiment exp, string stepContext, IList<MentorMessage> history, string question)
        {
            var sb = new StringBuilder();
            sb.Append("{\"system_instruction\":{\"parts\":[{\"text\":\"").Append(Esc(SystemPrompt(exp, stepContext))).Append("\"}]},\"contents\":[");

            // recent turns, merged so roles strictly alternate (the API rejects two in a row), ending with the new question
            var turns = new List<MentorMessage>();
            int start = Mathf.Max(0, history.Count - MaxHistory);
            for (int i = start; i < history.Count; i++) turns.Add(history[i]);
            turns.Add(new MentorMessage { FromStudent = true, Text = question });

            var merged = new List<MentorMessage>();
            foreach (var t in turns)
            {
                if (string.IsNullOrEmpty(t.Text)) continue;
                if (merged.Count > 0 && merged[merged.Count - 1].FromStudent == t.FromStudent)
                    merged[merged.Count - 1] = new MentorMessage { FromStudent = t.FromStudent, Text = merged[merged.Count - 1].Text + "\n" + t.Text };
                else merged.Add(new MentorMessage { FromStudent = t.FromStudent, Text = t.Text });
            }
            while (merged.Count > 0 && !merged[0].FromStudent) merged.RemoveAt(0);     // must start with the student

            for (int i = 0; i < merged.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append("{\"role\":\"").Append(merged[i].FromStudent ? "user" : "model").Append("\",\"parts\":[{\"text\":\"").Append(Esc(merged[i].Text)).Append("\"}]}");
            }
            sb.Append("],\"generationConfig\":{\"temperature\":0.4,\"maxOutputTokens\":600}}");
            return sb.ToString();
        }

        private static string SystemPrompt(MentorExperiment exp, string stepContext)
        {
            var sb = new StringBuilder();
            sb.Append("You are the Edu-AR Mentor, a friendly science tutor inside a virtual chemistry lab app for Class 10 students in Andhra Pradesh, India (age 14 to 15). ");
            sb.Append("The student is doing this experiment right now. ").Append(exp.Briefing).Append(' ');
            if (!string.IsNullOrEmpty(stepContext)) sb.Append("The student is currently at: ").Append(stepContext.Trim().TrimEnd((char)46, (char)32)).Append(". ");
            sb.Append("Rules: Answer in simple English a Class 10 student understands. Keep it under 110 words. ");
            sb.Append("Use plain text only: no markdown, no asterisks, no headings. Write chemical formulas in plain letters and numbers, for example H2SO4, and arrows as ->. ");
            sb.Append("Link the answer to what the student can see in the experiment. Explain the idea, do not just state the answer. ");
            sb.Append("If the question is not about science or this lab, kindly steer the student back to the experiment. ");
            sb.Append("You cannot control the experiment or touch the apparatus. If you are not sure, say so instead of guessing. ");
            sb.Append("Never give instructions for making anything dangerous.");
            return sb.ToString();
        }

        private static string Esc(string s)
        {
            if (s == null) return "";
            var sb = new StringBuilder(s.Length + 16);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '"': sb.Append("\\\""); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': break;
                    case '\t': sb.Append("\\t"); break;
                    default: if (c < 0x20) sb.Append(' '); else sb.Append(c); break;
                }
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------- response
        [Serializable] private class GErr { public int code; public string message; public string status; }
        [Serializable] private class GPart { public string text; }
        [Serializable] private class GCont { public GPart[] parts; }
        [Serializable] private class GCand { public GCont content; public string finishReason; }
        [Serializable] private class GResp { public GCand[] candidates; public GErr error; }

        private static string ParseAnswer(string json)
        {
            try
            {
                var r = JsonUtility.FromJson<GResp>(json);
                if (r == null || r.candidates == null || r.candidates.Length == 0) return null;
                var c = r.candidates[0];
                if (c.content == null || c.content.parts == null) return null;
                var sb = new StringBuilder();
                foreach (var p in c.content.parts) if (p != null && !string.IsNullOrEmpty(p.text)) sb.Append(p.text);
                return sb.ToString().Trim();
            }
            catch (Exception e) { Debug.LogWarning("[Mentor] parse error: " + e.Message); return null; }
        }

        private static string ErrorMessage(string json, string fallback)
        {
            try
            {
                var r = JsonUtility.FromJson<GResp>(json);
                if (r != null && r.error != null)
                {
                    if (r.error.code == 429) return "too many questions, try again in a moment";
                    if (r.error.code == 400 || r.error.code == 403) return "the AI key was not accepted";
                    if (r.error.code == 404) return "the AI model name is out of date";
                    if (!string.IsNullOrEmpty(r.error.status)) return r.error.status.ToLower().Replace('_', ' ');
                }
            }
            catch { }
            return fallback;
        }

        private static string Trim(string s) { return string.IsNullOrEmpty(s) ? "" : (s.Length > 300 ? s.Substring(0, 300) : s); }

        /// <summary>Removes the markdown a language model sometimes adds, which the app font would show literally.</summary>
        private static string Tidy(string s)
        {
            s = s.Replace("**", "").Replace("__", "").Replace("`", "");
            var lines = s.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string l = lines[i].TrimStart();
                while (l.StartsWith("#")) l = l.Substring(1);
                if (l.StartsWith("* ")) l = "- " + l.Substring(2);
                lines[i] = l.TrimStart();
            }
            return string.Join("\n", lines).Trim();
        }

        // ---------------------------------------------------------------- built-in answers
        public static MentorReply Offline(MentorExperiment exp, string question, string note)
        {
            string q = (question ?? "").ToLowerInvariant();
            string t = q.Trim().Trim('!', '.', '?');

            if (t == "hi" || t == "hello" || t == "hey" || t.StartsWith("hi ") || t.StartsWith("hello"))
                return new MentorReply { Text = "Hello! I am your lab mentor. Ask me anything about " + exp.Title + ", for example: " + exp.QuickQuestions[0], Note = note };
            if (t.Contains("thank")) return new MentorReply { Text = "You are welcome! Ask me another question whenever you like.", Note = note };

            MentorQA best = null; int bestScore = 0;
            foreach (var qa in exp.Answers)
            {
                int score = 0;
                foreach (var k in qa.Keys) if (q.Contains(k)) score += 1 + k.Length / 4;
                if (score > bestScore) { bestScore = score; best = qa; }
            }
            if (best != null) return new MentorReply { Text = best.Answer, Note = note };

            return new MentorReply
            {
                Text = "I can help with " + exp.Title + ". The key reaction is " + exp.Equation + ".\n\nTry asking: " + exp.QuickQuestions[0] + " or " + exp.QuickQuestions[1],
                Note = note
            };
        }
    }
}
