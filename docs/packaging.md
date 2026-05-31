# Client Packaging & Platform Rollout

The Mireya display client is built with [Avalonia](https://avaloniaui.net/) and is structured
so it can be shipped to several platforms from a shared codebase:

| Project | Role |
| --- | --- |
| `Mireya.Client.Core` | Shared, platform-agnostic Avalonia UI: views, view-models, converters, settings, the application shell and the platform-abstraction interfaces (`IAssetViewFactory`, `IWebsiteRenderer`, `IVideoRenderer`). References Avalonia but **no** platform-only packages. |
| `Mireya.Client.Desktop` | Windows/Linux desktop head. Provides the composition root (`DesktopServices`) and the desktop platform implementations: WebView2 website renderer, LibVLC video renderer and DPAPI/AES-GCM credential storage. |

Each platform head supplies its own composition root (via `App.ServiceProviderFactory`) and an
`IAssetViewFactory` implementation, so the shared core never needs to reference WebView2, LibVLC
or any other platform-only dependency.

> **Status:** The Core/Desktop split (phase 0) is complete. The packaging targets below are the
> planned rollout and are **not yet implemented** — this document is the roadmap.

## 1. Windows (Microsoft Store / MSIX)

- **Head:** `Mireya.Client.Desktop` (`win-x64`).
- **Renderers:** WebView2 (requires the Evergreen WebView2 Runtime) + LibVLC.
- **Plan:**
  - Add a Windows Application Packaging project (or `dotnet` MSIX tooling) that wraps the
    self-contained `win-x64` publish output.
  - Declare the WebView2 runtime dependency / bootstrapper.
  - Provide Store assets (logos, manifest, identity) and configure signing.
- **Publish (today):**
  ```bash
  cd src/Mireya.Client.Desktop
  dotnet publish -c Release -r win-x64 -o ./publish
  ```

## 2. Linux (incl. Raspberry Pi)

- **Head:** `Mireya.Client.Desktop` (`linux-x64`, `linux-arm64` for Raspberry Pi).
- **Renderers:** LibVLC works cross-platform; the **website renderer must be replaced** — WebView2
  is Windows-only. A Linux head should provide an `IWebsiteRenderer` backed by CEF
  (e.g. CefGlue) or a WebKitGTK control.
- **Plan:**
  - Add a Linux `IAssetViewFactory` that returns a CEF/WebKit-based website renderer.
  - Replace `VideoLAN.LibVLC.Windows` with the appropriate native libVLC for the target distro.
  - Package as a self-contained tarball and/or a systemd kiosk service for Raspberry Pi.

## 3. Android (Android TV)

- **Head:** a new `Mireya.Client.Android` project (Avalonia.Android, `ISingleViewApplicationLifetime`).
- **Renderers:** native Android `WebView` for websites and Android `MediaPlayer`/`ExoPlayer` for video,
  exposed through `IWebsiteRenderer` / `IVideoRenderer`.
- **Credential storage:** Android Keystore-backed implementation of `ICredentialStorage`.
- **Plan:**
  - Add the Android head referencing `Mireya.Client.Core`.
  - Implement the Android `IAssetViewFactory` and secure storage.
  - Package as an APK/AAB targeting Android TV (leanback launcher metadata).

## Adding a new platform head — checklist

1. Create a project referencing `Mireya.Client.Core`.
2. Implement `IAssetViewFactory` returning controls that implement `IWebsiteRenderer` and
   `IVideoRenderer`.
3. Implement `ICredentialStorage` (or reuse an existing one) for the platform's secure store.
4. Build a composition root that registers the above plus the API client, and assign it to
   `App.ServiceProviderFactory` before starting Avalonia.
5. Wire the platform entry point (desktop lifetime, single-view lifetime, …).
