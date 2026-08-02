# Google Play Release

Stable Mireya tags are built as signed Android App Bundles and submitted to Google Play by `.github/workflows/publish-google-play.yml`. Pre-release tags continue through other release workflows but are intentionally not packaged or submitted to Play.

## One-time Google Play setup

Create the app in Google Play Console with package name `dev.moritzreis.mireya`, opt in to Play App Signing, complete the required app-content and store-listing forms, and manually upload the first signed AAB. The Google Play publishing API cannot create the app or perform its first upload.

Set these GitHub repository variables under **Settings > Secrets and variables > Actions > Variables**:

| Repository variable        | Required | Value                                                                                                                                                      |
| -------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `GOOGLE_PLAY_PACKAGE_NAME` | Yes      | `dev.moritzreis.mireya`                                                                                                                                    |
| `GOOGLE_PLAY_TRACK`        | No       | Play release track; defaults to `internal`. Use `alpha`, `beta`, `production`, or a custom track name only after that track is configured in Play Console. |

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

| Repository secret                  | Required for          | Value                                                           |
| ---------------------------------- | --------------------- | --------------------------------------------------------------- |
| `ANDROID_SIGNING_KEYSTORE_BASE64`  | Every build           | Base64-encoded contents of `mireya-upload.jks`                  |
| `ANDROID_SIGNING_KEY_ALIAS`        | Every build           | Upload-key alias, for example `mireya-upload`                   |
| `ANDROID_SIGNING_KEY_PASSWORD`     | Every build           | Password for the key alias                                      |
| `ANDROID_SIGNING_STORE_PASSWORD`   | Every build           | Password for the keystore                                       |
| `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON` | Store submission only | Entire JSON key downloaded for the Google Cloud service account |

Do not use the debug keystore. Once the first artifact is uploaded, future updates must use the same upload key unless the key is reset through Play Console.

### Configure publishing API access

1. Enable the **Google Play Android Developer API** in a Google Cloud project.
2. Create a dedicated service account and a JSON key.
3. In Google Play Console, open **Users and permissions**, invite the service-account email, grant it access only to the Mireya app, and allow it to create and publish releases to the selected track.
4. Store the entire downloaded JSON document in `GOOGLE_PLAY_SERVICE_ACCOUNT_JSON`.

The service account does not need broad Google Cloud project roles. Its Play Console app permissions control what the workflow can publish.

## Release behavior

A stable tag such as `v0.2.0` starts the Google Play workflow independently of the Docker and Microsoft Store workflows. It:

1. Validates the stable semantic version, package name, and signing configuration.
2. Installs the .NET 10 Android workload and restores the projects.
3. Restores and verifies the upload keystore.
4. Builds and signs an Android App Bundle (`.aab`) for `net10.0-android`.
5. Extracts the bundle's native libraries into the ABI layout required by Play and creates a matching `native-debug-symbols.zip`.
6. Uploads the signed bundle and native-symbol archive as GitHub Actions artifacts retained for 30 days.
7. Publishes the bundle and available native symbols to `GOOGLE_PLAY_TRACK`, or to the `internal` track when that variable is unset.

Google Play requires a positive, monotonically increasing integer `versionCode`. Mireya maps `major.minor.patch` to `major * 1,000,000 + minor * 1,000 + patch + 1`; for example, `0.2.0` becomes `2001` and `1.0.0` becomes `1000001`. Major versions are therefore limited to 2099 and minor/patch components to 999.

Run **Publish Google Play** manually with `submit_to_store` disabled to build and retain a signed AAB without changing Play Console. Enable `submit_to_store` to publish the manual build. Stable tag pushes publish automatically; pre-release versions such as `1.0.0-rc.1` are skipped.

For the safest rollout, keep `GOOGLE_PLAY_TRACK` set to `internal` until the internal-testing release has been installed and exercised on real Android TV hardware. Promote a verified release in Play Console, or deliberately change the configured track for subsequent releases.

## Play Console diagnostic warnings

Play can show a warning that no deobfuscation file is associated with the bundle. Mireya does not currently run the R8/ProGuard Java code shrinker, so there is no `mapping.txt` to upload and this warning is not actionable. Do not enable R8 only to hide the warning; if R8 is enabled later, preserve the build's generated mapping file because it must match that exact release.

Play also detects the native `.so` libraries supplied by .NET, SkiaSharp, LibVLC, and SQLite. The workflow uploads `mireya-android-<version>-native-debug-symbols.zip` with automated submissions. For a manual AAB submission, upload the matching ZIP from the workflow artifact alongside that release. Some third-party NuGet libraries are already stripped by their publishers, so their private function names and source lines cannot be reconstructed locally; the archive still supplies every symbol present in the exact released binaries.

## Before the first submitted release

- Confirm that the Play Console app package is exactly `dev.moritzreis.mireya`.
- Complete the TV app listing, privacy policy, data-safety declaration, content rating, target-audience declaration, and all other required Play forms.
- Upload a TV banner and screenshots captured without real credentials or customer content.
- Verify backend selection, registration and approval, asset synchronization, image/video/website playback, offline cache behavior, and remote commands on representative 32-bit and 64-bit Android TV devices.
- Keep an offline backup of the upload keystore and both passwords.
