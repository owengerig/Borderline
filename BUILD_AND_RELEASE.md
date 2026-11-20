# Borderline – Build & Release Workflows (iOS + Android)

This covers how a solo or small studio ships a 2D mobile strategy game the way the big ones do: build, test internally, run a closed beta, soft launch, then go global, with live updates after that.

Store requirements as of Nov 2026:
- **Google Play:** New apps and updates must target **Android 16 (API 36)** or higher (enforced since Aug 31, 2026).
- **Apple:** Uploads must be built with **Xcode 26+ and the iOS 26 SDK** (enforced since Apr 28, 2026).

---

## 0. Identity & versioning (set once)

| Field | Where | Value |
|---|---|---|
| Version (shown in-game) | Player > Version | `0.1.0` (all OG Software apps start here), then `0.1.1`, `0.2.0`… for each release |
| Android Bundle Version Code | Player > Android > Other Settings | `1`, **+1 on every upload** to Play |
| iOS Build | Player > iOS > Other Settings | `1`, **+1 on every upload** to App Store Connect |
| Package / Bundle ID | Player > Other Settings (both platforms) | `com.ogsoftware.borderline` (Game Center / Play Games IDs use it), which can never change after the first upload |
| Privacy Policy URL | Store listings | https://sites.google.com/view/og-software/privacy-policy |
| Support URL | Store listings | https://sites.google.com/view/og-software/support |

Suggested Git branches: `main` (always shippable), `develop` (daily work), and feature branches. Tag each store upload, e.g. `v0.1.0-build1`.

---

## 1. Android (you can do all of this on your Windows PC)

**Player Settings > Android**
- Scripting Backend: **IL2CPP**. Target Architectures: **ARM64** (add ARMv7 only if you need very old phones).
- Target API Level: **Automatic (highest installed)**. It must be at least API 36 for Play uploads.
- Minimum API Level: leave Unity's default unless you have a reason to change it.
- Publishing Settings: **Keystore Manager > Create New**. Store the `.keystore` file and its passwords somewhere safe *outside* the repo (e.g. a password manager). If you lose it, updating the app becomes painful.

**Build**
1. Open **File > Build Profiles > Android** and click **Switch Platform**.
2. For quick on-device tests, plug in your phone (with USB debugging on) and use **Build And Run** to produce an `.apk`.
3. For store builds, tick **Build App Bundle (Google Play)** to produce an `.aab`.

**Google Play Console** (one-time $25 developer registration, plus identity verification)
1. Create the app. Fill in the store listing, privacy policy URL, **Data safety** form, content rating questionnaire, and target audience.
2. Upload the `.aab` to **Internal testing** (up to 100 testers, available in minutes).
3. **Closed testing**: New *personal* developer accounts must run a closed test with a minimum number of testers for a minimum number of days before production access. Check the current numbers in Play Console.
4. Promote to **Production** with a staged rollout (e.g. 10% → 50% → 100%).

## 2. iOS (needs macOS + Xcode 26 at the build step)

Unity on Windows can *generate* the Xcode project, but compiling, signing and uploading need a Mac. Your options:

| Option | Cost | Notes |
|---|---|---|
| **Unity Build Automation** (cloud) | Usage-based | No Mac needed. Connect your Git repo, upload your Apple certificate and provisioning profile, and it builds and can deliver to App Store Connect. |
| **GitHub Actions macOS runner** + GameCI + fastlane | Free minutes, then paid | Fully automated. See §3. |
| **A Mac** (e.g. a used Mac mini) | One-time | The simplest debugging experience. You'll want one eventually for device testing. |

**Apple Developer Program:** $99/year. Then, in **App Store Connect**, create the app with the same Bundle ID.

**Manual flow (on a Mac)**
1. In Unity, open **Build Profiles > iOS > Build**. This produces an Xcode project folder.
2. Open `Unity-iPhone.xcodeproj` and go to **Signing & Capabilities**: choose your Team and enable automatic signing.
3. Choose **Product > Archive**, then **Distribute App > App Store Connect**.
4. In App Store Connect, the build goes to **TestFlight**: internal testers right away, external testers after a short beta review.
5. Fill in **App Privacy** ("nutrition labels"), screenshots, the support and privacy URLs, and the age rating, then **Submit for Review**.

## 3. Cloud builds (GitHub Actions)

Two manual workflows, the same as Card Combat's (Actions tab → choose one → **Run workflow**):

- **Build Android APK** (`.github/workflows/build-android.yml`): GameCI builds the APK on Linux and publishes it
  under **Releases** as `v<version>-build<n>`, signed with the upload keystore when the `ANDROID_KEY…` secrets are
  set.
- **Build iOS app** (`.github/workflows/build-ios.yml`): Unity exports the Xcode project on Linux, then a macOS job
  builds it. Without signing secrets you get a Simulator build; with them, a signed `.ipa` (ad hoc, App Store or
  development, chosen when you run it), published as `ios-v<version>-build<n>`.

Both skip the build when nothing in `Assets/`, `Packages/` or `ProjectSettings/` changed since the last release
(tick "Build even if nothing changed" to override), and both **stop with an error until `ProjectSettings/` is
committed** (see `SETUP.md`).

Secrets (pushed by OG Software's `push-github-secrets.ps1`): `UNITY_LICENSE`, `UNITY_EMAIL`, `UNITY_PASSWORD`; the four
`ANDROID_KEY…` secrets (alias `borderline`); for signed iOS builds `IOS_CERTIFICATE_P12_BASE64`,
`IOS_CERTIFICATE_PASSWORD`, `IOS_PROVISIONING_PROFILE_BASE64` and `IOS_TEAM_ID`.

## 4. The release ladder (how the big mobile strategy games ship)

1. **Prototype** (now): Validate that claiming, fortifying and attacking is fun. Do internal test tracks only.
2. **Vertical slice:** Real map tiles + GPS + server, with 20–50 friends and family on TestFlight / closed testing.
3. **Soft launch:** Release in 1–3 small markets (commonly Canada, Australia, New Zealand, the Philippines). Measure retention (D1/D7/D30), session length and crash rate, and tune the dwell and combat numbers.
4. **Global launch:** Use store featuring requests, press, and creator outreach.
5. **Live ops:** Ship a regular update cadence (events, seasons, balance patches) and use remote config so you can rebalance without a store release.

Pre-submission checklist for every release:
- [ ] Version and both build numbers bumped
- [ ] Footer shows the right version (`Created by OG Software v…`)
- [ ] Game Center / Play Games achievements and leaderboards created (`GAME_SERVICES.md`)
- [ ] Privacy / Support links open correctly
- [ ] Privacy policy updated if any new data is collected (location!)
- [ ] Tested on a small phone, a notched iPhone and a tablet (safe area)
- [ ] Crash-free on internal track for at least 24 h
