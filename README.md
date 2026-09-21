# AR Virtual Lab

An Android app that turns the experiments in the **Class 10 Physical Science** textbook
(Andhra Pradesh) into interactive virtual and AR lab activities.
Point the phone at a textbook page and the matching experiment opens.

Built with **Unity 6 (6000.3.5f2)**, the Universal Render Pipeline, and **AR Foundation 6 / ARCore**.

## Experiments

| Textbook page | Activity | Experiment | Scene |
|---|---|---|---|
| 4  | Activity 1.1 | Burning of a magnesium ribbon in air | `MagnesiumLabScene` |
| 6  | Activity 1.3 | Zinc granules with dilute acid (hydrogen gas) | `HydrogenLabScene` |
| 42 | Activity 2.3 | Testing hydrogen gas by burning | `HydrogenGasTestScene` |

Each experiment runs in two modes: **standalone** (a virtual lab bench on screen) and
**AR** (equipment placed on a real table surface).

## How it works

1. **Home** → **Textbook Scan** (image tracking recognises the page) → experiment scene → results.
2. Each experiment is a step-by-step state machine. The student taps and drags the apparatus,
   guided by a step card, a pulsing hint ring, equipment labels, and the reaction equation.
3. Scenes and most of the UI are **built in code** (uGUI + TextMeshPro), not hand-placed in the
   editor. Shared styling lives in `EduTheme.cs` and `UiKit.cs`.
4. `TextbookCatalog.cs` is the single list mapping textbook pages to experiment scenes.

## Project layout

```
Assets/
  Scenes/            Home, TextbookScan, MagnesiumLab, HydrogenLab, HydrogenGasTest
  Scripts/
    AppShell/        navigation, home screen, safe area
    Scan/            textbook catalog + scan scene (AR image tracking)
    AR/              AR tabletop placement (planes, tray, gestures)
    Standalone/      experiment scenes and controllers
    UI/              EduTheme, UiKit, HintRing
    AI/              optional AI tutor (Gemini) with offline fallback
  Editor/            scene builders and model importers (menu: "AR Lab")
  Models/, ThirdParty/, Resources/   3D models (OBJ / GLB) and textbook page images
```

## Getting started

1. Install **Unity Hub** and **Unity 6000.3.5f2** with the **Android Build Support**
   module (including OpenJDK and Android SDK & NDK Tools).
2. Clone the repo. It is about 300 MB because of the 3D models.
3. In Unity Hub choose **Add → Add project from disk** and select the cloned folder.
   The first open is slow while Unity rebuilds `Library/`.
4. Open `Assets/Scenes/HomeScene.unity` and press Play. Camera scanning only works on a phone;
   in the editor, use the list of experiments on the scan screen.

### Building for Android

- Minimum API level 26, IL2CPP, ARM64.
- Needs an **ARCore-supported** device.
- Scenes in build order: Home, MagnesiumLab, HydrogenLab, HydrogenGasTest, TextbookScan.

## Not included in the repo (add locally if you need them)

- **Vuforia license / config** – not needed; the project uses AR Foundation.
- **Gemini API key** – optional, for the AI tutor. Without a key it falls back to built-in answers.
  Set it in the Inspector on your machine and **never commit a key**.
- Build outputs (`Library/`, `Builds/`, `*.apk`) are git-ignored.

## Working as a team

- `main` is the stable, always-working version. Do not push to it directly.
- Create a branch for your work: `feature/<short-name>` (for example `feature/new-experiment`).
- Open a **pull request** into `main`; the maintainer reviews and merges.
- **Scenes and prefabs do not merge well.** Give each experiment its own scene, and only edit
  scenes you own. Small edits to shared files (`TextbookCatalog.cs`, `AppNavigation.cs`,
  the Home scene, `ProjectSettings/`) should be coordinated with the team.
- Always commit the `.meta` files that Unity creates next to your assets.

### Adding an experiment

1. Add the textbook page image to `Assets/Resources/Textbook/` (see the notes at the top of
   `TextbookCatalog.cs`) and a row in `TextbookCatalog.Pages`.
2. Create the experiment scene and script, following an existing one such as `MagnesiumLabScene`.
3. Add the scene to Build Settings and to `AppNavigation`.

## Assets and licenses

Some 3D models were generated with Meshy or built procedurally (`Assets/ThirdParty`,
`LabAssetGenerator`). Check the license terms of any asset before reusing it outside this project.
