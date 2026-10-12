# EduAR privacy rules (for everyone working on the app)

The in-app Privacy Policy (`Assets/Resources/Legal/privacy.txt`) makes promises to students. Any change that would
break one of them must change the policy first, raise `LegalScreens.CurrentVersion` (so students are asked to agree
again), and be discussed with the team.

## What the app promises today

1. **No accounts, no ads, no tracking, no analytics.** No advertising ID, no analytics SDK, no Unity Analytics or
   engine diagnostics (both are switched off in Project Settings; keep them off).
2. **The camera never leaves the phone.** Camera frames are only used by ARCore on the device, while the scan or AR
   screen is open. Nothing records, saves or uploads a frame.
3. **Only two things touch the network:** ARCore (Google Play Services for AR) and the one-time ML Kit Telugu model
   download. Both are named in the policy, including the ARCore disclosure Google requires.
4. **Everything else stays on the phone:** settings (PlayerPrefs) and the translation cache (`persistentDataPath`).

## Permissions

- Ask only when the feature is opened, never at launch, and always through `CameraPermission.Request` (it explains
  why first and offers Settings if the student said "don't ask again").
- Adding any new permission (microphone, location, storage, notifications...) needs a policy update.

## The AI tutor (to be decided)

The privacy approach for the AI tutor has not been decided yet: it may need to work online, and the team will agree
on what it can promise before anything is built or written into the policy. Until then:

- Do not add a feature that sends student data off the phone.
- Whatever is decided, never ship an API key inside the app: anything in an APK can be extracted. Online AI calls
  should go through a small server that holds the key.

## Before a release

- [ ] `docs/PRIVACY.md` and the in-app policy still describe what the app does.
- [ ] Project Settings: Unity Analytics and engine diagnostics off; no new SDKs that collect data.
- [ ] The APK requests only the permissions the policy lists (`aapt2 dump permissions <apk>`).
- [ ] No API keys, passwords or keystores in git (`Signing/` is ignored on purpose).
