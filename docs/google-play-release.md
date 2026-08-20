# Android App Bundle

Mireya's release workflow builds a signed Android App Bundle and retains it as a
GitHub Actions artifact. It does not publish to Google Play; submission remains a
manual Play Console step.

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

## Build and submit a release

Enable **Build the signed Android App Bundle artifact** in **Release Mireya**, or run
**Build Android App Bundle** directly with a stable semantic version. The workflow is
the source of truth for build flags, version-code mapping, package validation, and
artifact retention.

Download `mireya-google-play-<version>` from the completed workflow run. It contains `mireya-android-<version>.aab` and the matching native debug-symbol archive. Upload the AAB to an internal testing track first; upload the symbol archive with the same release.

To make the release available on TVs, open **Test and release > Advanced settings > Form factors** in Play Console, add Android TV, upload the required TV screenshot, and opt in to Android TV review. A dedicated Android TV release track is optional but recommended; the same AAB can be used for the mobile and TV releases.

## Play Console diagnostic warnings

Play can show a warning that no deobfuscation file is associated with the bundle. Mireya does not currently run the R8/ProGuard Java code shrinker, so there is no `mapping.txt` to upload and this warning is not actionable. Do not enable R8 only to hide the warning; if R8 is enabled later, preserve the build's generated mapping file because it must match that exact release.

Play also detects native `.so` libraries supplied by .NET and third-party packages.
Upload the matching `native-debug-symbols.zip` from the workflow artifact alongside
the AAB. Some dependencies are already stripped by their publishers, so not every
private function name or source line can be recovered.

## Before the first submitted release

- Confirm that the Play Console app package is exactly `dev.moritzreis.mireya`.
- Complete the TV app listing, privacy policy, data-safety declaration, content rating, target-audience declaration, and all other required Play forms.
- Upload a TV banner and screenshots captured without real credentials or customer content.
- Verify backend selection, registration and approval, asset synchronization, image/video/website playback, offline cache behavior, and remote commands on representative 32-bit and 64-bit Android TV devices.
- Keep an offline backup of the upload keystore and both passwords.
