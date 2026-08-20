# Mireya

> Active development: Mireya is usable for local evaluation and development, but is not production ready.

Mireya is an open digital-signage platform with a web admin backend and lightweight display clients. It manages screens, uploads and organizes content assets, schedules campaigns, pushes configuration to approved screens, caches media locally, and records proof-of-play activity.

[Hosted documentation](https://mireya.moritzreis.dev/#/)

## What is implemented

- **Backend and admin UI**: ASP.NET Core, Carter minimal APIs, Blazor Server admin interface, ASP.NET Identity, OpenAPI via NSwag, and SignalR for live screen updates.
- **Assets**: image, video, and website assets
- **Campaigns**: ordered playlists, per-item durations, enable/disable state
- **Screens**: first-run screen registration, admin approval/rejection, screen details, direct campaign assignment, online status
- **Display clients**: shared Avalonia client core with desktop and Android TV heads. Clients store backend configuration, register with the backend, reconnect automatically, sync campaign assets, cache media locally, and play scheduled content.
- **Remote control**: restart, reload, identify, next, and previous commands are pushed to connected screens over SignalR.
- **Monitoring and audit**: proof-of-play reporting, asset sync status, audit log, health endpoints.
- **Development and deployment support**: SQLite for local development, PostgreSQL for production-like runs, EF Core migrations per provider, Docker Compose, .NET Aspire AppHost, xUnit application tests, and generated API client code.

## Quickstart

### Requirements

- .NET 10 SDK
- SQLite for the default local setup
- Docker, if using the Docker/PostgreSQL stack
- Android workload and Android SDK only when building the Android TV client

Restore local tools:

```bash
dotnet tool restore
```

Run the API/admin app with local SQLite:

```bash
dotnet run --project src/Mireya.Api/Mireya.Api.csproj --launch-profile https
```

Open the admin UI at `https://localhost:5001/login`.

Development credentials are seeded from `src/Mireya.Api/appsettings.Development.json`:

- Email: `admin@mireya.local`
- Password: `Admin123!`

Run the desktop display client:

```bash
dotnet run --project src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

Run the API with PostgreSQL through Docker Compose:

```bash
cp .env.example .env
# Edit .env and replace both example passwords.
docker compose up -d
```

Compose pulls `moritzreis/mireya-digital-signage:latest`, creates the PostgreSQL
database, applies migrations automatically, and persists the database, uploaded
media, and data-protection keys in named volumes. The admin UI is published on
`http://localhost:8080/login`.

For reproducible deployments, set `MIREYA_VERSION` in `.env` to an exact release
such as `1.0.0` instead of `latest`. See the
[operations guide](https://mireya.moritzreis.dev/#/operations) for all settings,
health checks, image tags, upgrades, and initial-admin behavior.

## Common commands

```bash
# Build the backend API
dotnet build src/Mireya.Api/Mireya.Api.csproj -c Release

# Build the generated API client project
dotnet build src/Mireya.ApiClient/Mireya.ApiClient.csproj -c Release

# Build the desktop client
dotnet build src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj -c Release

# Run application tests
dotnet test src/Mireya.Application.Tests/Mireya.Application.Tests.csproj -c Release
```

## Contributing

1. Fork the repository.
2. Create a branch: `git checkout -b feature/your-feature`.
3. Make changes and run the relevant build/test commands.
4. Submit a pull request with a clear description.

Please keep changes aligned with the existing .NET, Blazor, Avalonia, and EF Core patterns.
