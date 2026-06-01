# Development

This page contains information for developers who want to contribute to Mireya or run it locally for development purposes.

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 10.0 SDK** - Download from [Microsoft's .NET website](https://dotnet.microsoft.com/download/dotnet/10.0)
- **PostgreSQL** (optional, for production-like setup) - Download from [postgresql.org](https://www.postgresql.org/download/) or use Docker Image

## Project Structure

Mireya consists of several components:

- **Mireya.Api** - ASP.NET Core Web API (Carter minimal API modules) and Blazor Server admin interface
- **Mireya.Application** - Application services, SignalR hubs and business logic
- **Mireya.Database** - Entity Framework Core database models and `MireyaDbContext`
- **Mireya.Database.Sqlite** - SQLite database provider and migrations (development)
- **Mireya.Database.Postgres** - PostgreSQL database provider and migrations (production)
- **Mireya.ApiClient** - API wrapper to be used in clients
- **Mireya.Client.Core** - Shared Avalonia UI, view-models and platform-abstraction interfaces for the client
- **Mireya.Client.Desktop** - Windows/Linux desktop head (WebView2, LibVLC, DPAPI) that hosts the shared core
- **Mireya.Client.Android** - Android TV head (native System WebView, LibVLC) that hosts the shared core
- **Mireya.Application.Tests** - xUnit unit tests for the application services
- **MireyaDigitalSignage.AppHost** / **MireyaDigitalSignage.ServiceDefaults** - .NET Aspire orchestration and shared service defaults (telemetry, health checks)

## Running Mireya Locally

### 1. Clone and Setup

```bash
git clone https://github.com/clFaster/Mireya.git
cd Mireya
```

### 2. Database Setup

Mireya supports two database providers. The active provider is selected with the
`provider` configuration key (`Sqlite` or `Postgres`), and each provider reads its
own connection string from the `ConnectionStrings` section.

#### SQLite (Recommended for Development)

SQLite is used by default for local development and requires no additional setup.
`appsettings.Development.json` sets `"provider": "Sqlite"` with a `Sqlite` connection string.

#### PostgreSQL (Production-like Setup)

1. Install PostgreSQL and create a database:

```bash
# Create database (adjust connection string as needed)
createdb mireya_dev
```

2. Set `provider` to `Postgres` and configure the `Postgres` connection string in
   `src/Mireya.Api/appsettings.Development.json`:

```json
{
  "provider": "Postgres",
  "ConnectionStrings": {
    "Postgres": "Host=localhost;Database=mireya_dev;Username=your_username;Password=your_password"
  }
}
```

### 3. Run Database Migrations

Migrations are applied automatically on API startup. To apply them manually:

```bash
# For SQLite (development)
dotnet ef database update --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api

# For PostgreSQL (production) - the provider is read from the `provider` env var / config
provider=Postgres dotnet ef database update --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api
```

### 4. Run the API Server

```bash
cd src/Mireya.Api
dotnet run
```

The API will be available at:

- HTTP: `http://localhost:5000`
- HTTPS: `https://localhost:5001`

### 5. Access the Admin Interface

Once the API is running, access the admin interface at:

```
https://localhost:5001/login
```

Default admin credentials (development):

- **Email:** `admin@mireya.local`
- **Password:** configured via `DefaultAdminUser:Password` (set to `Admin123!` in
  `appsettings.Development.json`). In production, provide it through user secrets or
  environment variables (`DefaultAdminUser__Password`) instead of committing it.

### 6. Run the Desktop Client (Optional)

```bash
cd src/Mireya.Client.Desktop
dotnet run
```

## Development Workflow

### Making API Changes

1. Modify controllers, models, or services in `src/Mireya.Api/`
2. Update database models in `src/Mireya.Database/`
3. Create and run migrations if database schema changes:

```bash
# Add migration (SQLite)
dotnet ef migrations add YourMigrationName --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api

# Add the equivalent migration for PostgreSQL (note the provider env var)
provider=Postgres dotnet ef migrations add YourMigrationName --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api

# Update database
dotnet ef database update --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api
```

> When you change the schema, always add a migration to **both** the SQLite and
> PostgreSQL provider projects so the two databases stay in sync.

### Admin Interface Development

The admin interface is built with **Blazor Server** (interactive server components) and
styled with **Bootstrap 5**. Files are located in:

- `src/Mireya.Api/Components/Pages/` - Razor (Blazor) pages and components
- `src/Mireya.Api/wwwroot/` - Static assets (CSS/JS) served to the browser

The "Control Room" theme in `wwwroot/app.css` layers a cohesive design system over
Bootstrap. It exposes design tokens as CSS custom properties under `:root` — colours
(`--ink-*`, `--brand*`, semantic `--ok/--warn/--bad/--info`), geometry (`--r-*`),
a 4px-based spacing scale (`--sp-1`…`--sp-8`), elevation (`--shadow-*`), motion
(`--ease*`, `--t*`) and fonts (`--font-display/body/mono`). Prefer these tokens over
ad-hoc values when adding styles so spacing and colour stay consistent.

### Client Development

#### Avalonia Client

The Avalonia client is split into a shared core and per-platform heads so it can be
shipped to Windows (Store/MSIX), Linux (incl. Raspberry Pi) and Android (Android TV):

- `src/Mireya.Client.Core/` - Shared Avalonia UI, view-models, converters and the
  platform-abstraction interfaces (`Platform/IAssetViewFactory`, `IWebsiteRenderer`,
  `IVideoRenderer`). References Avalonia but no platform-only packages.
  - ViewModels: `src/Mireya.Client.Core/ViewModels/`
  - Views: `src/Mireya.Client.Core/Views/`
  - Services: `src/Mireya.Client.Core/Services/`
- `src/Mireya.Client.Desktop/` - Windows/Linux desktop head. Provides the desktop
  composition root (`Platform/DesktopServices`) and the platform implementations
  (WebView2 website renderer, LibVLC video renderer, DPAPI credential storage).
  This is the project you build/run for the desktop app.
- `src/Mireya.Client.Android/` - Android TV head (`net10.0-android`, application id
  `com.mireya.signage.tv`). Provides the Android composition root
  (`Platform/AndroidServices`) and the platform implementations (native `Android.Webkit`
  WebView website renderer and a `LibVLCSharp` video renderer, both hosted in a
  `NativeControlHost`). Registers in the Leanback (TV) launcher and runs as an immersive
  full-screen kiosk.

Both projects intentionally keep the historical `Mireya.Client.Avalonia` root namespace
(only the assembly names differ) to avoid churn across the moved XAML and code.

```bash
dotnet build src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

##### Android TV

The Android TV head reuses the shared core and adds Android-specific renderers. Use the
Android TV emulator (or a real Android TV device) to build, deploy and smoke-test it.

**Prerequisites**

- The .NET Android workload:

  ```bash
  dotnet workload install android
  # If a restore fails with a workload-band mismatch (NETSDK1147), realign the bands:
  dotnet workload restore src/Mireya.Client.Android/Mireya.Client.Android.csproj
  ```

- A **64-bit** Android TV emulator or device. The app ships **64-bit native libraries
  only** (SkiaSharp and LibVLC), so a 32-bit `x86` system image fails to install with
  `INSTALL_FAILED_NO_MATCHING_ABIS`. Create an AVD from a `x86_64` Android TV
  (`google-atv`/`android-tv`) system image. `adb`, `emulator` and `sdkmanager` live under
  `$ANDROID_HOME` (`%LOCALAPPDATA%\Android\Sdk` on Windows).

**Build**

```bash
dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj
```

**Run on the emulator**

1. Start the API server (see "Run the API Server" above) and the TV emulator. Make sure
   the backend is reachable from the emulator. From the emulator, the host machine is
   `http://10.0.2.2:5000` (not `localhost`, which points at the emulator itself). The
   manifest enables cleartext HTTP so plain `http://` LAN backends work.
2. Build a **standalone APK with the assemblies embedded** and install it with `adb`.
   This is the most reliable path — a plain Debug APK installed by hand crashes on launch
   with *"No assemblies found … Assuming this is part of Fast Deployment"* because Fast
   Deployment expects the assemblies to be pushed separately by the IDE.

   ```bash
   dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj \
     -p:EmbedAssembliesIntoApk=true -p:AndroidFastDeploymentType=None

   adb install -r src/Mireya.Client.Android/bin/Debug/net10.0-android/com.mireya.signage.tv-Signed.apk
   adb shell monkey -p com.mireya.signage.tv -c android.intent.category.LAUNCHER 1
   ```

   Alternatively, deploy via the MSBuild target (handles Fast Deployment for you):

   ```bash
   dotnet build src/Mireya.Client.Android/Mireya.Client.Android.csproj -t:Run
   ```

3. On first launch the app shows the backend-selection screen. Enter the backend URL
   (`http://10.0.2.2:5000` for the local API) and connect. The app then establishes a
   SignalR connection and starts displaying the assigned campaign.

**Useful diagnostics**

```bash
# Follow the app's own log output (Serilog is routed to logcat under the DOTNET tag)
adb logcat --pid=$(adb shell pidof com.mireya.signage.tv)

# Capture a screenshot of the current screen
adb exec-out screencap -p > screen.png
```

> **Android dispatcher-priority note:** the shared playlist code advances assets with a
> `DispatcherTimer`. The parameterless `DispatcherTimer` ctor ticks at
> `DispatcherPriority.Background`, which sits below `Input` and is starved on Android by
> the continuous compositor/video render loop — so the playlist never advanced
> automatically there (only render-time-independent paths such as the remote "next"
> command worked). The timer is therefore created at `DispatcherPriority.Default` bound to
> `Dispatcher.UIThread` (see `ContentDisplayViewModel.StartAdvanceTimer`). Keep this in
> mind for any other time-based UI work added to the shared core: prefer `Default` (or
> higher) over `Background` so it isn't starved on mobile.

## Running Tests

Unit tests live in `src/Mireya.Application.Tests` (xUnit + in-memory SQLite + NSubstitute):

```bash
dotnet test src/Mireya.Application.Tests/Mireya.Application.Tests.csproj
```

Tests also run automatically in the PR build workflow.

## Operational Endpoints

The API exposes a few endpoints useful for monitoring and client discovery:

- `GET /api/info` - public server identity, returns `{ "application": "Mireya", "version": "..." }`.
  Clients use this to confirm a host is a Mireya backend.
- `GET /alive` - liveness probe (always available); returns `Healthy` when the process is up.
- `GET /health` - readiness probe including database connectivity. Exposed in the
  Development environment only; wire it up behind your infrastructure in production.

## Running with Docker

A multi-stage `Dockerfile` builds the API, and `docker-compose.yml` runs it against a
PostgreSQL container:

```bash
# Optionally set credentials/admin password
export POSTGRES_PASSWORD=change-me
export MIREYA_ADMIN_PASSWORD=Admin123!

docker compose up --build
```

The API is published on `http://localhost:8080`. Uploaded media and database data are
persisted in the `mireya-uploads` and `mireya-db` named volumes. Database migrations are
applied automatically on startup.

## Building for Production

### API

```bash
cd src/Mireya.Api
dotnet publish -c Release -o ./publish
```

### Desktop Client

The Avalonia client currently targets Windows and Linux. Publish a self-contained build
for a specific runtime identifier:

```bash
cd src/Mireya.Client.Desktop

# Windows
dotnet publish -c Release -r win-x64 -o ./publish

# Linux (e.g. Raspberry Pi uses linux-arm64)
dotnet publish -c Release -r linux-x64 -o ./publish
```

> Packaging for the Windows Store (MSIX), Linux, and Android TV is on the roadmap; see
> [docs/packaging.md](packaging.md) for the client topology and the planned phased rollout.

## Troubleshooting

### Common Issues

1. **Database connection errors**: Ensure your connection string is correct and the database exists
2. **Migration errors**: Make sure you're using the correct database provider project
3. **Admin login fails**: Check that the database is seeded with the default admin user

### Getting Help

- Check existing issues on [GitHub](https://github.com/clFaster/Mireya/issues)
- Join the discussion in GitHub Discussions

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes and test thoroughly
4. Submit a pull request with a clear description of your changes

Please ensure all tests pass and follow the existing code style and patterns.
