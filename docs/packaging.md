# Client Packaging and Platform Status

The Mireya display client uses a shared Avalonia core with platform-specific heads. The shared core owns the signage shell, backend-selection UI, playback view models, settings, and renderer interfaces. Each platform head provides its own composition root and concrete website/video renderers.

| Project                 | Status      | Role                                                                                                                |
| ----------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------- |
| `Mireya.Client.Core`    | Implemented | Shared Avalonia UI, view models, converters, settings, and platform abstractions.                                   |
| `Mireya.Client.Desktop` | Implemented | Windows/Linux desktop head with WebView2 website rendering, LibVLC video rendering, and desktop credential storage. |
| `Mireya.Client.Android` | Implemented | Android TV head with native Android WebView, Media3/ExoPlayer, Leanback launcher metadata, and immersive fullscreen behavior. |

The Windows desktop head has x64/ARM64 Microsoft Store packaging, and the Android
head has a signed Google Play App Bundle workflow. Other installers remain roadmap
work.

## Desktop

The desktop head targets `net10.0` and can be run directly:

```bash
dotnet run --project src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

Publish a Windows build:

```bash
dotnet publish src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj -c Release -r win-x64 -o ./publish
```

The packaging project contains the Store manifest and artwork. See
[Microsoft Store Release](microsoft-store-release.md) for the local package command,
Partner Center setup, certification, and listing content.

Publish a Linux build:

```bash
dotnet publish src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj -c Release -r linux-x64 -o ./publish
```

For Raspberry Pi-style targets, use a Linux ARM runtime such as `linux-arm64` after validating native dependencies.

### Desktop packaging gaps

- Clean-device playback validation and the first Partner Center certification must be completed for each public release.
- The current desktop website renderer uses WebView2. That is appropriate for Windows, but Linux kiosk packaging needs a Linux-specific website renderer such as WebKitGTK or CEF.
- The desktop project references Windows libVLC native packaging. Linux packaging needs the correct native libVLC dependency strategy for the target distribution.

## Android TV

The Android TV head is implemented in `src/Mireya.Client.Android` and targets `net10.0-android` with application id `dev.moritzreis.mireya`.

It provides:

- Android TV launcher metadata.
- Immersive fullscreen display behavior.
- Native Android `WebView` for website assets.
- Jetpack Media3/ExoPlayer video rendering backed by Android's platform codecs.
- Shared client registration, pairing, approval, sync, cache, playback, and remote-command behavior from `Mireya.Client.Core` and `Mireya.ApiClient`.

The app supports ARM32, ARM64, x86, and x64 runtimes. Google Play serves the
device-specific split generated from a single AAB. See [Android
debugging](debugging/android.md) for local build and deployment commands, and
[Google Play Release](google-play-release.md) for signing and Play Console setup.

## Adding another client head

Use the current desktop and Android heads as the pattern:

1. Create a project referencing `Mireya.Client.Core`.
2. Provide platform implementations for `IAssetViewFactory`, `IWebsiteRenderer`, and `IVideoRenderer`.
3. Provide secure credential storage through `ICredentialStorage`.
4. Register platform services, API client services, and view models in the composition root.
5. Set `App.ServiceProviderFactory` before starting Avalonia.
6. Wire the platform entry point and fullscreen/kiosk behavior.

## Roadmap

- Additional Windows installer formats.
- Linux kiosk packaging with a Linux-native website renderer.
- Raspberry Pi packaging and service setup.
