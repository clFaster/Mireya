# Development

This guide is for contributors and maintainers running Mireya locally.

## Prerequisites

- .NET 10 SDK
- Git
- SQLite for the default development database
- Docker Desktop or compatible Docker runtime for Docker Compose and Aspire PostgreSQL runs
- Android SDK plus the .NET Android workload when building the Android TV client

Restore local .NET tools before using NSwag:

```bash
dotnet tool restore
```

## Project structure

| Project | Purpose |
| --- | --- |
| `src/Mireya.Api` | ASP.NET Core API, Blazor Server admin UI, Identity, SignalR hub, OpenAPI, static uploads, and startup wiring. |
| `src/Mireya.Application` | Business services for assets, campaigns, scheduling, screens, zones, audit, playback reporting, alerting, and synchronization. |
| `src/Mireya.Database` | Shared EF Core models and `MireyaDbContext`. |
| `src/Mireya.Database.Sqlite` | SQLite provider and migrations for local development. |
| `src/Mireya.Database.Postgres` | PostgreSQL provider and migrations for Docker/production-like runs. |
| `src/Mireya.ApiClient` | Generated NSwag API client plus client-side services, auth, SignalR, local SQLite store, backend management, and asset sync. |
| `src/Mireya.ApiClient.TestConsole` | Small console harness for manual API-client checks. |
| `src/Mireya.Client.Core` | Shared Avalonia display-client UI, view models, settings, converters, and platform abstractions. |
| `src/Mireya.Client.Desktop` | Windows/Linux desktop head with WebView2 website rendering, LibVLC video rendering, and desktop credential storage. |
| `src/Mireya.Client.Android` | Android TV head with native Android WebView, LibVLC, Leanback launcher metadata, and immersive fullscreen behavior. |
| `src/Mireya.Application.Tests` | xUnit tests for application services using in-memory SQLite and NSubstitute. |
| `src/MireyaDigitalSignage.AppHost` | .NET Aspire orchestration for the API plus PostgreSQL. |
| `src/MireyaDigitalSignage.ServiceDefaults` | Shared Aspire service defaults, health endpoints, telemetry, resilience, and service discovery. |

## Run locally

### API/admin with SQLite

The development appsettings select SQLite by default:

```bash
dotnet run --project src/Mireya.Api/Mireya.Api.csproj --launch-profile https
```

The API/admin app listens on:

- `https://localhost:5001`
- `http://localhost:5000`

Open `https://localhost:5001/login`.

Development credentials are seeded from `src/Mireya.Api/appsettings.Development.json`:

- Email: `admin@mireya.local`
- Password: `Admin123!`

Migrations run automatically during API startup.

### API/admin with Aspire and PostgreSQL

The Aspire AppHost starts PostgreSQL, waits for it, and runs the API with `provider=Postgres`:

```bash
dotnet run --project src/MireyaDigitalSignage.AppHost/MireyaDigitalSignage.AppHost.csproj
```

Use this when you want a production-like database without manually configuring PostgreSQL.

### Desktop client

Start the API first, then run:

```bash
dotnet run --project src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

On first launch, enter the backend URL. For local HTTPS development use `https://localhost:5001`; for local HTTP use `http://localhost:5000`.

The client can also be preconfigured for unattended/kiosk deployments:

```bash
# PowerShell
$env:MIREYA_BACKEND_URL = "http://localhost:5000"
dotnet run --project src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

```bash
# bash
MIREYA_BACKEND_URL=http://localhost:5000 dotnet run --project src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

## Database providers and migrations

The active provider is selected by the `provider` configuration key:

- `Sqlite`: reads `ConnectionStrings:Sqlite`
- `Postgres`: reads `ConnectionStrings:Postgres`

`appsettings.Development.json` uses SQLite. `appsettings.json`, Docker Compose, and Aspire are oriented around PostgreSQL.

Apply migrations manually when needed:

```bash
# SQLite
dotnet ef database update --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api
```

PowerShell:

```powershell
$env:provider = "Postgres"
dotnet ef database update --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api
Remove-Item Env:\provider
```

bash:

```bash
provider=Postgres dotnet ef database update --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api
```

Add migrations to both provider projects for schema changes:

```bash
# SQLite
dotnet ef migrations add YourMigrationName --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api
```

```powershell
# PostgreSQL in PowerShell
$env:provider = "Postgres"
dotnet ef migrations add YourMigrationName --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api
Remove-Item Env:\provider
```

```bash
# PostgreSQL in bash
provider=Postgres dotnet ef migrations add YourMigrationName --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api
```

Keep SQLite and PostgreSQL migrations in sync whenever the shared model changes.

## Build and test

```bash
# Backend API
dotnet build src/Mireya.Api/Mireya.Api.csproj -c Release

# Generated API client and supporting client services
dotnet build src/Mireya.ApiClient/Mireya.ApiClient.csproj -c Release

# Desktop display client
dotnet build src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj -c Release

# Application tests
dotnet test src/Mireya.Application.Tests/Mireya.Application.Tests.csproj -c Release
```

The PR workflow restores local tools, restores/builds the API, API client, desktop client, and runs `Mireya.Application.Tests`.

## API client generation

The generated client lives in `src/Mireya.ApiClient/Generated/MireyaApiClient.cs` and is produced from `src/Mireya.ApiClient/nswag.json`.

Restore tools first, then run NSwag from the API client directory:

```bash
dotnet tool restore
cd src/Mireya.ApiClient
dotnet nswag run nswag.json
```

The NSwag config builds `../Mireya.Api/Mireya.Api.csproj` in the Development environment and writes the generated C# client to `Generated/MireyaApiClient.cs`.

## Admin UI development

The admin UI is built with Blazor Server interactive components and Bootstrap 5.

- Pages and shared UI: `src/Mireya.Api/Components`
- Static assets: `src/Mireya.Api/wwwroot`
- Main theme: `src/Mireya.Api/wwwroot/app.css`

Prefer the existing CSS custom properties in `app.css` for colors, spacing, radius, shadows, motion, and fonts. Keep new admin UI code consistent with the current Control Room design.

## Client development

The display client is split into a shared Avalonia core and platform heads:

- `Mireya.Client.Core` owns the shared shell, views, view models, playback flow, backend-selection UI, local settings UI, and interfaces for website/video renderers.
- `Mireya.Client.Desktop` wires desktop services, WebView2, LibVLC, and desktop credential storage.
- `Mireya.Client.Android` wires Android services, native Android WebView, LibVLC, and Android TV entry points.

The client workflow is:

1. Choose or receive a backend URL.
2. Register with `/api/screenmanagement/register`.
3. Show a pairing code while awaiting approval.
4. Authenticate as a screen after approval.
5. Connect to `/hubs/screen`.
6. Receive configuration and asset-sync requests.
7. Cache image/video assets locally and mark sync progress.
8. Play the active playlist and report now-playing/proof-of-play events.

### Android TV

Install or restore the Android workload:

```bash
dotnet workload install android
dotnet workload restore src/Mireya.Client.Android/Mireya.Client.Android.csproj
```

Use a 64-bit Android TV emulator or device. The APK includes 64-bit native libraries, so 32-bit `x86` images fail with `INSTALL_FAILED_NO_MATCHING_ABIS`.

Build:

```bash
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj
```

For a local API running on the host machine, Android emulators reach the host at `http://10.0.2.2:5000`.

Build and install a standalone Debug APK with embedded assemblies:

```bash
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj -p:EmbedAssembliesIntoApk=true -p:AndroidFastDeploymentType=None
adb install -r src/Mireya.Client.Android/bin/Debug/net10.0-android/dev.moritzreis.mireya-Signed.apk
adb shell monkey -p dev.moritzreis.mireya -c android.intent.category.LAUNCHER 1
```

Alternatively, let MSBuild deploy to the selected device:

```bash
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj -t:Run
```

Useful diagnostics:

```bash
adb logcat --pid=$(adb shell pidof dev.moritzreis.mireya)
adb exec-out screencap -p > screen.png
```

## Docker development

Docker Compose builds the API image and runs it with PostgreSQL:

```bash
docker compose up --build
```

The API listens on `http://localhost:8080`. Uploaded media and PostgreSQL data are stored in the `mireya-uploads` and `mireya-db` named volumes.

## Operational endpoints

- `GET /api/info`: public server identity used by clients to recognize a Mireya backend.
- `GET /alive`: liveness check.
- `GET /health`: readiness check including database connectivity. It is exposed by the service defaults and intended for development/infrastructure use.
- OpenAPI/Swagger UI: enabled in the Development environment.

## Troubleshooting

- **Admin login fails**: confirm migrations ran and `DefaultAdminUser:Password` is configured for first startup.
- **PostgreSQL migration uses SQLite**: set `provider=Postgres` in the same shell command/session that runs EF.
- **Android emulator cannot reach backend**: use `http://10.0.2.2:5000` instead of `localhost`.
- **Android APK crashes after manual install**: build with `EmbedAssembliesIntoApk=true` and `AndroidFastDeploymentType=None`, or deploy with `-t:Run`.
- **Video/website rendering differs by platform**: desktop uses WebView2 and LibVLC; Android uses native Android WebView and LibVLC.
