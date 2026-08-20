# Android client agent guidance

These instructions supplement the repository-level `AGENTS.md` for work under
`src/Mireya.Client.Android`.

## Scope

- The application id is `dev.moritzreis.mireya`.
- The project targets `net10.0-android` and supports `android-arm`, `android-arm64`,
  `android-x86`, and `android-x64`.
- `MainApplication.cs` configures the Avalonia application and Android service
  composition root.
- `MainActivity.cs` owns Android TV launcher, lifecycle, orientation, and immersive
  full-screen behavior.
- `Platform/` supplies Android-specific services and native renderer factories.
- `Views/Components/` contains native Android WebView and Media3/ExoPlayer hosts.
- Shared UI, scheduling, caching, and playback logic belongs in
  `../Mireya.Client.Core` unless it genuinely depends on Android APIs.

## Build and debug rules

- Follow `../../docs/debugging/android.md` for the repeatable workflow.
- Discover the connected device serial and ABI; do not assume `emulator-5554` or
  `android-x86`.
- Android emulators reach the host API at `http://10.0.2.2:5000`. `localhost` refers
  to the emulator itself.
- Prefer `adb install -r` so pairing, backend selection, and downloaded assets remain
  available. Do not use `adb uninstall` or `adb shell pm clear` without explicit
  permission.
- Clear logcat only before a planned reproduction. Never clear it after an already
  observed crash until the evidence has been captured.
- Debug/Fast Deployment builds are useful for iteration but are not sufficient for
  performance or memory acceptance. Confirm those findings with an embedded Release
  APK.
- Record the device, Android version, ABI, display size, build configuration, runtime
  identifier, and PID with debugging results.

## Verification

- Exercise the real affected flow: backend selection, registration/approval, asset
  synchronization, image/video/website playback, offline cache, or remote commands.
- Inspect logcat for managed exceptions, Java exceptions, Android Runtime failures,
  native crashes, process death, and low-memory kills.
- Run `Mireya.Client.Core.Tests` when shared client behavior changes.
- For memory or lifecycle fixes, verify a stable PID and bounded memory over a timed
  soak. Use `../../docs/debugging/android-memory.md` for the established memory
  procedure and acceptance criteria.
