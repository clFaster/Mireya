# Mireya agent guide

This file is the repository-level working contract for coding agents. Keep it concise;
put detailed procedures in `docs/` and link to them from here.

## Start here

- Read `README.md` for the product overview.
- Read `docs/development.md` before changing an unfamiliar project.
- For Android work, read `docs/debugging/android.md` and
  `src/Mireya.Client.Android/AGENTS.md` before building or debugging.
- For Android native-memory investigations, also read
  `docs/debugging/android-memory.md`.
- Inspect `git status --short` before editing. Preserve unrelated and pre-existing
  changes; never discard them to make a task easier.

## Architecture and ownership

- `src/Mireya.Api`: ASP.NET Core API, Blazor admin UI, Identity, SignalR, OpenAPI,
  uploads, and application startup.
- `src/Mireya.Application`: business services and application behavior.
- `src/Mireya.Database`: shared EF Core entities and `MireyaDbContext`.
- `src/Mireya.Database.Sqlite` and `src/Mireya.Database.Postgres`: provider-specific
  migrations and configuration.
- `src/Mireya.ApiClient`: generated NSwag client plus client-side authentication,
  SignalR, local storage, synchronization, and caching services.
- `src/Mireya.Client.Core`: shared Avalonia views, view models, playback flow, and
  platform abstractions.
- `src/Mireya.Client.Desktop` and `src/Mireya.Client.Android`: platform composition
  roots and native renderers.

Put behavior in the narrowest correct layer. Cross-platform playback and UI behavior
normally belongs in `Mireya.Client.Core`; Android lifecycle, manifest, WebView,
Media3/ExoPlayer, and native-control behavior belongs in `Mireya.Client.Android`.

## Working agreements

- Prefer the smallest change that addresses the verified cause.
- Reproduce bugs and collect evidence before changing behavior when practical.
- Add a regression test for a bug fix when the behavior can be exercised outside the
  platform UI.
- Do not edit `src/Mireya.ApiClient/Generated/MireyaApiClient.cs` manually. Restore
  local tools and regenerate it through `src/Mireya.ApiClient/nswag.json`.
- Use `/p:SkipNSwag=true` only when the API contract and generated client are not part
  of the change.
- When the shared server model changes, keep the SQLite and PostgreSQL migrations in
  sync. Client-local database migrations under `Mireya.ApiClient` are separate.
- Do not add secrets, signing material, copied client databases, device tokens, or
  local environment files to source control.
- Do not uninstall the Android app or clear its data unless the task explicitly allows
  losing pairing, settings, and cached assets.

## Verification

Run the checks relevant to the changed area and report both completed and omitted
checks in the handoff.

```powershell
# API and application behavior
dotnet test src/Mireya.Application.Tests/Mireya.Application.Tests.csproj -c Release

# Generated API client and client-side services
dotnet test src/Mireya.ApiClient.Tests/Mireya.ApiClient.Tests.csproj -c Release

# Shared display-client behavior
dotnet test src/Mireya.Client.Core.Tests/Mireya.Client.Core.Tests.csproj -c Release

# Backend build
dotnet build src/Mireya.Api/Mireya.Api.csproj -c Release

# Desktop client build
dotnet build src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj -c Release
```

For Android changes, build the Android project for the connected device's ABI, test
the affected behavior on an emulator or device, inspect logcat, and run the shared
client tests when shared code is involved. Follow `docs/debugging/android.md` rather
than assuming a device serial, ABI, or APK output path.

## Documentation

Update the relevant document when setup, configuration, commands, public behavior, or
operational expectations change. Avoid duplicating a complete runbook in this file.
