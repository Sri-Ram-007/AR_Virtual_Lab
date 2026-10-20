// ============================================================
// Loc.cs  —  AR Virtual Lab
// English → Telugu lookup. The English sentence on screen is its own key, so the experiments keep their
// plain English strings in code and nothing has to be rewritten to be translated.
//
//   1. Assets/Resources/Localization/te.txt   — the checked-in table (English<TAB>Telugu)
//   2. ML Kit cache on the phone               — on-device machine translation of anything not in the table
//   3. otherwise the text stays English, and the sentence is recorded as missing
//
// Standalone numbers are placeholders ("STEP 3 OF 9" → "STEP {0} OF {1}"), so one row covers every value.
// Tools/Localization/extract_strings.py builds the same keys from the source code to find untranslated text.
// ============================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using ARVirtualLab.AppShell;

namespace ARVirtualLab.Localization
{
    public static class Loc
    {
        /// <summary>Bumped whenever a translation becomes available, so labels resolve their text again.</summary>
        public static int Version { get; private set; }

        private static Dictionary<string, string> _table;
        private static readonly Dictionary<string, string> _machine = new Dictionary<string, string>();
        private static readonly HashSet<string> _missing = new HashSet<string>();
        private static bool _machineLoaded;

        // A number on its own (not part of a formula like 2Mg, O2 or <sub>2</sub>, not an existing {0}), with an
        // optional decimal or version part (1.1, 0.2.0); a seconds unit may follow ("3s remaining")
        private static readonly Regex Number = new Regex(@"(?<![A-Za-z>.\d{])\d+(?:\.\d+)*(?:(?![A-Za-z<\d}])|(?=s\b))", RegexOptions.Compiled);
        private static readonly Regex Tag = new Regex(@"<[^<>]+>", RegexOptions.Compiled);
        private static readonly Regex Slot = new Regex(@"\{(\d+)\}", RegexOptions.Compiled);

        public static bool TeluguMode { get { return AppSettings.Language == AppLanguage.Telugu; } }

        /// <summary>
        /// What to show instead of <paramref name="source"/>: the Telugu text, or null to keep the English as it is.
        /// Text that already contains Telugu (e.g. the language option "తెలుగు") is returned unchanged.
        /// </summary>
        public static string Resolve(string source)
        {
            if (string.IsNullOrEmpty(source)) return null;
            if (ContainsTelugu(source)) return source;
            string endonym;
            if (Endonyms.TryGetValue(source, out endonym)) return endonym;
            if (!TeluguMode || !HasLetters(source)) return null;

            string te = Lookup(source);
            if (te != null) return te;

            // text glued together from several translated lines (e.g. a bulleted card): translate line by line
            if (source.IndexOf('\n') >= 0)
            {
                var lines = source.Split('\n');
                bool all = true;
                for (int i = 0; i < lines.Length && all; i++)
                {
                    if (!HasLetters(lines[i])) continue;
                    string line = Lookup(lines[i]);
                    if (line == null) all = false; else lines[i] = line;
                }
                if (all) return string.Join("\n", lines);
            }

            string key = MakeKey(source, null);
            RecordMissing(key);
            MlKitTranslator.Request(key);
            return null;
        }

        // Language names are always shown in their own language, so a student can find theirs whatever is selected
        private static readonly Dictionary<string, string> Endonyms = new Dictionary<string, string>
        {
            { "Telugu", "తెలుగు" },     // తెలుగు
        };

        private static string Lookup(string text)
        {
            var args = new List<string>();
            string key = MakeKey(text, args);
            string te;
            if (Table.TryGetValue(key, out te) || Machine.TryGetValue(key, out te))
            {
                // keep the leading spaces of an indented line
                int indent = 0;
                while (indent < text.Length && text[indent] == ' ') indent++;
                return text.Substring(0, indent) + Fill(te, args);
            }
            return null;
        }

        /// <summary>The table key for a piece of on-screen text. Must stay in step with extract_strings.py.</summary>
        public static string MakeKey(string text, List<string> args)
        {
            text = text.Replace("->", "→").Replace("\r", "").Trim();
            var tags = Tag.Matches(text);
            int slot = 0;
            return Number.Replace(text, m =>
            {
                foreach (Match t in tags)                              // numbers inside tags (colours, sizes) stay
                    if (m.Index >= t.Index && m.Index < t.Index + t.Length) return m.Value;
                if (args != null) args.Add(m.Value);
                return "{" + slot++ + "}";
            });
        }

        private static string Fill(string translated, List<string> args)
        {
            if (args.Count == 0) return translated;
            return Slot.Replace(translated, m =>
            {
                int i = int.Parse(m.Groups[1].Value);
                return i < args.Count ? args[i] : m.Value;
            });
        }

        public static bool ContainsTelugu(string s)
        {
            foreach (char c in s) if (c >= 'ఀ' && c <= '౿') return true;
            return false;
        }

        private static bool HasLetters(string s)
        {
            string plain = Tag.Replace(s, "");
            foreach (char c in plain) if (char.IsLetter(c)) return true;
            return false;
        }

        // ── the checked-in table ──────────────────────────────
        private static Dictionary<string, string> Table
        {
            get
            {
                if (_table != null) return _table;
                _table = new Dictionary<string, string>();
                var asset = Resources.Load<TextAsset>("Localization/te");
                if (asset != null) Parse(asset.text, _table);
                return _table;
            }
        }

        /// <summary>One row per line: English, a tab, Telugu. "#" starts a comment; "\n" means a line break.</summary>
        public static void Parse(string text, Dictionary<string, string> into)
        {
            foreach (var raw in text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line[0] == '#') continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0 || tab == line.Length - 1) continue;
                string en = line.Substring(0, tab).Replace("\\n", "\n");
                string te = line.Substring(tab + 1).Replace("\\n", "\n");
                into[MakeKey(en, null)] = te;
            }
        }

        // ── machine translations cached on the phone ──────────
        private static string MachinePath { get { return Path.Combine(Application.persistentDataPath, "loc_mlkit_te.txt"); } }

        private static Dictionary<string, string> Machine
        {
            get
            {
                if (_machineLoaded) return _machine;
                _machineLoaded = true;
                try { if (File.Exists(MachinePath)) Parse(File.ReadAllText(MachinePath), _machine); }
                catch (IOException e) { Debug.LogWarning("[Loc] could not read the machine-translation cache: " + e.Message); }
                return _machine;
            }
        }

        internal static void AddMachineTranslation(string key, string telugu)
        {
            if (string.IsNullOrEmpty(telugu)) return;
            Machine[key] = telugu;
            Version++;
            try { File.AppendAllText(MachinePath, Escape(key) + "\t" + Escape(telugu) + "\n"); }
            catch (IOException e) { Debug.LogWarning("[Loc] could not save a machine translation: " + e.Message); }
        }

        // ── untranslated text, collected for the table ────────
        // On a phone: <persistentDataPath>/loc_missing_te.txt (adb pull it). In the editor: Tools/Localization/.
        private static string MissingPath
        {
            get
            {
                return Application.isEditor
                    ? Path.Combine(Application.dataPath, "..", "Tools", "Localization", "te_missing_runtime.txt")
                    : Path.Combine(Application.persistentDataPath, "loc_missing_te.txt");
            }
        }

        private static void RecordMissing(string key)
        {
            if (!_missing.Add(key)) return;
            Debug.Log("[Loc] no Telugu yet for: " + key);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MissingPath));
                File.AppendAllText(MissingPath, Escape(key) + "\n");
            }
            catch (IOException) { }
        }

        private static string Escape(string s) { return s.Replace("\t", " ").Replace("\n", "\\n"); }
    }
}
