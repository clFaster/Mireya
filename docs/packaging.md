# Client Packaging and Platform Status

The Mireya display client uses a shared Avalonia core with platform-specific heads. The shared core owns the signage shell, backend-selection UI, playback view models, settings, and renderer interfaces. Each platform head provides its own composition root and concrete website/video renderers.

| Project | Status | Role |
| --- | --- | --- |
| `Mireya.Client.Core` | Implemented | Shared Avalonia UI, view models, converters, settings, and platform abstractions. |
| `Mireya.Client.Desktop` | Implemented | Windows/Linux desktop head with WebView2 website rendering, LibVLC video rendering, and desktop credential storage. |
| `Mireya.Client.Android` | Implemented | Android TV head with native Android WebView, LibVLC, Leanback launcher metadata, and immersive fullscreen behavior. |

Packaging installers and store-ready artifacts are still roadmap work. The current repo supports building and running the platform heads from source.

## Desktop

The desktop head targets `net10.0` and can be run directly:

```bash
dotnet run --project src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

Publish a Windows build:

```bash
dotnet publish src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj -c Release -r win-x64 -o ./publish
```

Publish a Linux build:

```bash
dotnet publish src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj -c Release -r linux-x64 -o ./publish
```

For Raspberry Pi-style targets, use a Linux ARM runtime such as `linux-arm64` after validating native dependencies.

### Desktop packaging gaps

- Windows MSIX/Microsoft Store packaging has not been added yet.
- The current desktop website renderer uses WebView2. That is appropriate for Windows, but Linux kiosk packaging needs a Linux-specific website renderer such as WebKitGTK or CEF.
- The desktop project references Windows libVLC native packaging. Linux packaging needs the correct native libVLC dependency strategy for the target distribution.

## Android TV

The Android TV head is implemented in `src/Mireya.Client.Android` and targets `net10.0-android` with application id `com.mireya.signage.tv`.

It provides:

- Android TV launcher metadata.
- Immersive fullscreen display behavior.
- Native Android `WebView` for website assets.
- LibVLC-backed video rendering.
- Shared client registration, pairing, approval, sync, cache, playback, and remote-command behavior from `Mireya.Client.Core` and `Mireya.ApiClient`.

### Prerequisites

Install or restore the .NET Android workload:

```bash
dotnet workload install android
dotnet workload restore src/Mireya.Client.Android/Mireya.Client.Android.csproj
```

Use a 64-bit Android TV emulator or real Android TV device. The app ships 64-bit native libraries only, so a 32-bit `x86` system image fails with `INSTALL_FAILED_NO_MATCHING_ABIS`.

On Windows, Android SDK tools commonly live under `%LOCALAPPDATA%\Android\Sdk`.

### Build

```bash
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj
```

### Run on emulator or device

Start the API and the emulator/device first. From the Android emulator, the host machine is reachable at `http://10.0.2.2:5000`; `localhost` points to the emulator itself.

Build a standalone APK with assemblies embedded and install it manually:

```bash
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj -p:EmbedAssembliesIntoApk=true -p:AndroidFastDeploymentType=None
adb install -r src/Mireya.Client.Android/bin/Debug/net10.0-android/com.mireya.signage.tv-Signed.apk
adb shell monkey -p com.mireya.signage.tv -c android.intent.category.LAUNCHER 1
```

The embedded-assemblies path avoids a common manual-install crash where a plain Debug APK expects Fast Deployment assemblies to have been pushed separately by the IDE.

Alternatively, deploy through MSBuild:

```bash
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj -t:Run
```

Useful diagnostics:

```bash
adb logcat --pid=$(adb shell pidof com.mireya.signage.tv)
adb exec-out screencap -p > screen.png
```

## Adding another client head

Use the current desktop and Android heads as the pattern:

1. Create a project referencing `Mireya.Client.Core`.
2. Provide platform implementations for `IAssetViewFactory`, `IWebsiteRenderer`, and `IVideoRenderer`.
3. Provide secure credential storage through `ICredentialStorage`.
4. Register platform services, API client services, and view models in the composition root.
5. Set `App.ServiceProviderFactory` before starting Avalonia.
6. Wire the platform entry point and fullscreen/kiosk behavior.

## Roadmap

- Windows installer/MSIX packaging.
- Linux kiosk packaging with a Linux-native website renderer.
- Raspberry Pi packaging and service setup.
- Android release signing and APK/AAB distribution workflow.
