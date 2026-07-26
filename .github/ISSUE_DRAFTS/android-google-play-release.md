## Overview

Work package: publish the Mireya Android TV display client to Google Play for the first public release.

The current Android head targets `net10.0-android` with package id `com.mireya.signage.tv` and currently configures APK output. This issue covers release signing, Android App Bundle output, target API compliance, TV listing metadata, testing tracks, and production release.

## Requirements

- Configure a release build that produces a signed Android App Bundle (`.aab`) suitable for Google Play.
- Set up release signing and upload-key handling without committing keystores, passwords, or private credentials.
- Validate target API compliance for Android TV and confirm the final manifest values produced by the .NET Android build.
- Confirm the Android TV launcher metadata, banner/icon assets, immersive fullscreen behavior, WebView rendering, LibVLC video playback, and local asset cache all work in a release build.
- Prepare Play Console listing content: app name, descriptions, screenshots/TV assets, category, privacy policy URL, data safety form inputs, content rating, and support contact.
- Run internal testing on at least one Android TV emulator or device before production release.
- Submit to the Google Play production track and capture any policy or review feedback as follow-up issues.

## Acceptance Criteria

- A reproducible release command or CI workflow builds a signed `.aab` for `com.mireya.signage.tv`.
- The release artifact uploads successfully to Play Console internal testing.
- The app can be installed from an internal test release on Android TV, connect to a Mireya backend, register, receive approval, sync assets, play scheduled image/video/website content, and report proof-of-play events.
- The app meets current Google Play target API requirements for Android TV.
- Store listing, privacy/data safety, content rating, and release notes are complete enough for production submission.

## Additional Notes

- Existing project: `src/Mireya.Client.Android/Mireya.Client.Android.csproj`.
- The project currently sets `<AndroidPackageFormat>apk</AndroidPackageFormat>`; Google Play publishing should use Android App Bundle for TV apps.
- Google states Android App Bundle is required for new Google Play apps and TV apps: https://developer.android.com/guide/app-bundle
- Google Play target API requirements from August 31, 2025 require Android TV apps to target Android 14 / API 34 or higher: https://developer.android.com/google/play/requirements/target-sdk
