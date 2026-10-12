#!/usr/bin/env python3
"""
Finds on-screen English text in the C# sources that has no Telugu yet.

    python3 Tools/Localization/extract_strings.py            # write te_missing.txt, print a summary
    python3 Tools/Localization/extract_strings.py --all      # list every key, translated or not

Keys are built exactly as Loc.MakeKey does at runtime (subscripts become <sub>, standalone numbers become
{0} {1} ...), so a row added to Assets/Resources/Localization/te.txt for a key printed here will be found.

Text the scanner cannot see (sentences glued together from pieces at runtime) is collected by the app itself:
play in the editor with Telugu selected (Tools/Localization/te_missing_runtime.txt), or on a phone
(adb pull /sdcard/Android/data/<package>/files/loc_missing_te.txt). Machine translations made on a phone are
saved next to it in loc_mlkit_te.txt, already in table format, to review and copy into te.txt.
"""
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "Assets" / "Scripts"
TABLE = ROOT / "Assets" / "Resources" / "Localization" / "te.txt"
MISSING = ROOT / "Tools" / "Localization" / "te_missing.txt"

# Prototype code that no scene uses: its text never reaches the screen
SKIP = [
    "Core/", "Experiments/", "Results/", "Video/", "Localization/",
    "UI/ARUIController.cs", "AI/AIExplanationManager.cs",
    "AR/ARLabManager.cs", "AR/ImageTargetManager.cs", "AR/ARTargetController.cs",
    "Standalone/StandaloneAppState.cs", "Standalone/StandaloneMagnesiumExperiment.cs",
    "Standalone/StandaloneInteractable.cs", "Standalone/StandaloneUIController.cs",
    "Standalone/ExperimentUIController.cs", "Standalone/BunsenBurnerController.cs",
    "Standalone/SandpaperController.cs", "Standalone/TongsController.cs", "Standalone/HeatingZoneController.cs",
]

# Lines whose strings are never shown to the student
SKIP_LINE = re.compile(r"Debug\.Log|LogWarning|LogError|Shader\.Find|Resources\.Load|MenuItem|AddComponentMenu|"
                       r"GameObject\.Find|PlayerPrefs|ChildControl|\[Header|\[Tooltip|AndroidJava|HasProperty|"
                       r"SetFloat|SetInt|SetColor|SetTexture|EnableKeyword|\.name\s*==|StartsWith|layout\s*!=|Regex")

# The second argument of these UI builders is a GameObject name, not text
NAME_ARG = re.compile(r"\b(?:Text|Box|Button|Card|Node|MakeText|MakeButton|MakePanel|MakeInfoPanel|HudButton|"
                      r"SizedButton|CreateEquipLabel|CreateObjectWithMesh)\(\s*[^,()\"]*(?:\([^()]*\))?[^,()\"]*,\s*\"")
NEW_GO = re.compile(r"new\s+GameObject\(\s*\"")

STRING = re.compile(r'(\$@|@\$|\$|@)?"((?:[^"\\\n]|\\.)*)"')
NUMBER = re.compile(r"(?<![A-Za-z>.\d{])\d+(?:\.\d+)?(?:(?![A-Za-z<\d}])|(?=s\b))")

# Literals the scanner picks up that never reach the screen (debug output, mesh part names, keys)
IGNORE = {
    "camPos=", "F2", "pose=", "XR:", "layout=", "name=", "XR input devices:", "characteristics=",
    "active XR loader:", "settings.language", "settings.darkMode", "Label", "Option", "Lbl", "Error",
    "Base", "Rod", "Body", "Trough", "Cork", "Rise", "Span", "Drop", "Wax", "Wick", "Neck", "Cap", "Mid", "Rim",
    "t:GameObject", "Transparent", "Activity", "Page", "Opening",
    "Telugu",                     # language names are handled by Loc.Endonyms
    "worldPos={0} bottomCorrection={1}", "finalPos={0}",
}
TAG = re.compile(r"<[^<>]+>")
IDENT = re.compile(r"^[A-Za-z_][A-Za-z0-9_]*$")


def unescape(s, verbatim):
    if verbatim:
        return s.replace('""', '"')
    out, i = [], 0
    while i < len(s):
        c = s[i]
        if c == "\\" and i + 1 < len(s):
            n = s[i + 1]
            if n == "n": out.append("\n"); i += 2; continue
            if n == "t": out.append("\t"); i += 2; continue
            if n == "u" and i + 5 < len(s) + 1:
                out.append(chr(int(s[i + 2:i + 6], 16))); i += 6; continue
            out.append(n); i += 2; continue
        out.append(c); i += 1
    return "".join(out)


def clean(s):
    """Mirror of MobileText.Clean for what matters to keys: subscripts become <sub>, emoji are dropped."""
    out = []
    for c in s:
        if "₀" <= c <= "₉":
            out.append("<sub>%d</sub>" % (ord(c) - 0x2080)); continue
        if ord(c) > 0xFFFF or c in "✅✔✕❌☢⚪⚓️":
            continue
        out.append(c)
    return "".join(out).lstrip()


def make_key(text):
    """Mirror of Loc.MakeKey."""
    text = text.replace("->", "→").replace("\r", "").strip()
    tags = [(m.start(), m.end()) for m in TAG.finditer(text)]
    slot = [0]

    def repl(m):
        if any(a <= m.start() < b for a, b in tags):
            return m.group(0)
        r = "{%d}" % slot[0]
        slot[0] += 1
        return r
    return NUMBER.sub(repl, text)


def looks_like_ui_text(s):
    plain = TAG.sub("", s)
    if not re.search(r"[A-Za-z]", plain):
        return False
    if s.startswith("[") or s.startswith("#") or "/" in s and " " not in s:
        return False
    if re.search(r"\.(png|jpg|glb|obj|bytes|asset|unity|cs|txt)\b", s):
        return False
    if IDENT.match(s):
        # single words: keep plain English words ("Back", "Reset"), drop code names ("StepCard", "bar_fill")
        if "_" in s or re.search(r"[a-z][A-Z]", s) or s.islower():
            return False
    return True


def interpolated(body):
    # every {expression} hole becomes a sentinel number, then numbering matches what the app sees at runtime
    return re.sub(r"\{[^{}]*\}", "0", body.replace("{{", "\x01").replace("}}", "\x02")).replace("\x01", "{").replace("\x02", "}")


def scan():
    keys = {}
    for path in sorted(SCRIPTS.rglob("*.cs")):
        rel = path.relative_to(SCRIPTS).as_posix()
        if any(rel.startswith(p) or rel == p for p in SKIP):
            continue
        for lineno, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
            stripped = line.strip()
            if stripped.startswith("//") or SKIP_LINE.search(line):
                continue
            names = {m.end() - 1 for m in NAME_ARG.finditer(line)} | {m.end() - 1 for m in NEW_GO.finditer(line)}
            for m in STRING.finditer(line):
                quote_at = m.start(2) - 1
                if quote_at in names:
                    continue
                prefix, body = m.group(1) or "", m.group(2)
                if "$" in prefix:
                    body = interpolated(body)
                text = clean(unescape(body, "@" in prefix))
                if not looks_like_ui_text(text):
                    continue
                key = make_key(text)
                if key in IGNORE:
                    continue
                keys.setdefault(key, "%s:%d" % (rel, lineno))
    return keys


def table_keys():
    known = set()
    if TABLE.exists():
        for line in TABLE.read_text(encoding="utf-8").splitlines():
            if not line or line.startswith("#") or "\t" not in line:
                continue
            en = line.split("\t", 1)[0].replace("\\n", "\n")
            known.add(make_key(en))
    return known


def esc(s):
    return s.replace("\n", "\\n")


def main():
    keys = scan()
    known = table_keys()
    missing = {k: v for k, v in keys.items() if k not in known}
    if "--all" in sys.argv:
        for k, where in sorted(keys.items(), key=lambda kv: kv[1]):
            print(("   " if k in known else " * ") + esc(k) + "    <" + where + ">")
    MISSING.parent.mkdir(parents=True, exist_ok=True)
    with MISSING.open("w", encoding="utf-8") as f:
        f.write("# English text with no Telugu yet. Translate each line into te.txt as: English<TAB>Telugu\n")
        for k, where in sorted(missing.items(), key=lambda kv: kv[1]):
            f.write(esc(k) + "\t# " + where + "\n")
    print("%d on-screen strings found, %d translated, %d missing -> %s"
          % (len(keys), len(keys) - len(missing), len(missing), MISSING.relative_to(ROOT)))


if __name__ == "__main__":
    main()
