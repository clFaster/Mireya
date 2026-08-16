# Android App Bundle

`.github/workflows/publish-google-play.yml` builds a signed Android App Bundle and uploads it as a GitHub Actions artifact. It does not publish to Google Play. The bundle can be downloaded, inspected, and uploaded to Play Console manually when it is ready.

One `.aab` covers phones, tablets, and Android TV. It contains ARM32 (`armeabi-v7a`), ARM64 (`arm64-v8a`), x86, and x64 (`x86_64`) native libraries; Google Play generates and serves only the device-specific splits needed by each installation.

## One-time Google Play setup

Create the app in Google Play Console with package name `dev.moritzreis.mireya`, opt in to Play App Signing, complete the required app-content and store-listing forms, and manually upload the first signed AAB. The Google Play publishing API cannot create the app or perform its first upload.

### Create the upload key

The workflow signs every release with your Play upload key. Create it once and retain both the keystore and its credentials in a secure backup:

```bash
keytool -genkeypair -v \
  -keystore mireya-upload.jks \
  -alias mireya-upload \
  -keyalg RSA \
  -keysize 4096 \
  -validity 10000
```

Encode the complete keystore file as a single Base64 value. In PowerShell:

```powershell
[Convert]::ToBase64String(
  [IO.File]::ReadAllBytes("mireya-upload.jks")
) | Set-Clipboard
```

Add these GitHub repository secrets:

| Repository secret                 | Value                                          |
| --------------------------------- | ---------------------------------------------- |
| `ANDROID_SIGNING_KEYSTORE_BASE64` | Base64-encoded contents of `mireya-upload.jks` |
| `ANDROID_SIGNING_KEY_ALIAS`       | Upload-key alias, for example `mireya-upload`  |
| `ANDROID_SIGNING_KEY_PASSWORD`    | Password for the key alias                     |
| `ANDROID_SIGNING_STORE_PASSWORD`  | Password for the keystore                      |

Do not use the debug keystore. Once the first artifact is uploaded, future updates must use the same upload key unless the key is reset through Play Console.

## Release behavior

Select **Build the signed Android App Bundle artifact** when running the main **Release Mireya** workflow, or run **Build Android App Bundle** directly and enter a stable semantic version. The workflow:

1. Validates the stable semantic version, package name, and signing configuration.
2. Installs the .NET 10 Android workload and restores the projects.
3. Restores and verifies the upload keystore.
4. Builds and signs an Android App Bundle (`.aab`) for `net10.0-android`.
5. Verifies the merged release manifest has handheld and TV launcher metadata, optional touchscreen/Leanback features, a TV banner, and target API 34 or newer.
6. Verifies the bundle contains native libraries for ARM32, ARM64, x86, and x64.
7. Extracts the bundle's native libraries into the ABI layout accepted by Play and creates a matching `native-debug-symbols.zip`.
8. Uploads the signed bundle and native-symbol archive as GitHub Actions artifacts retained for 30 days.

Google Play requires a positive, monotonically increasing integer `versionCode`. Mireya maps `major.minor.patch` to `major * 1,000,000 + minor * 1,000 + patch + 1`; for example, `0.2.0` becomes `2001` and `1.0.0` becomes `1000001`. Major versions are therefore limited to 2099 and minor/patch components to 999.

Download `mireya-google-play-<version>` from the completed workflow run. It contains `mireya-android-<version>.aab` and the matching native debug-symbol archive. Upload the AAB to an internal testing track first; upload the symbol archive with the same release.

To make the release available on TVs, open **Test and release > Advanced settings > Form factors** in Play Console, add Android TV, upload the required TV screenshot, and opt in to Android TV review. A dedicated Android TV release track is optional but recommended; the same AAB can be used for the mobile and TV releases.

## Play Console diagnostic warnings

Play can show a warning that no deobfuscation file is associated with the bundle. Mireya does not currently run the R8/ProGuard Java code shrinker, so there is no `mapping.txt` to upload and this warning is not actionable. Do not enable R8 only to hide the warning; if R8 is enabled later, preserve the build's generated mapping file because it must match that exact release.

Play also detects the native `.so` libraries supplied by .NET, SkiaSharp, and SQLite. The workflow includes `mireya-android-<version>-native-debug-symbols.zip` in the artifact. Upload the matching ZIP alongside the manually submitted AAB. Some third-party NuGet libraries are already stripped by their publishers, so their private function names and source lines cannot be reconstructed locally; the archive still supplies every symbol present in the exact released binaries.

## Before the first submitted release

- Confirm that the Play Console app package is exactly `dev.moritzreis.mireya`.
- Complete the TV app listing, privacy policy, data-safety declaration, content rating, target-audience declaration, and all other required Play forms.
- Upload a TV banner and screenshots captured without real credentials or customer content.
- Verify backend selection, registration and approval, asset synchronization, image/video/website playback, offline cache behavior, and remote commands on representative 32-bit and 64-bit Android TV devices.
- Keep an offline backup of the upload keystore and both passwords.
