# Mireya

> Active development: Mireya is usable for local evaluation and development, but is not production ready.

Mireya is an open digital-signage platform with a web admin backend and lightweight display clients. It manages screens, uploads and organizes content assets, schedules campaigns, pushes configuration to approved screens, caches media locally, and records proof-of-play activity.

[Hosted documentation](https://mireya.moritzreis.dev/#/) | [Development guide](https://mireya.moritzreis.dev/#/development) | [Packaging notes](https://mireya.moritzreis.dev/#/packaging)

## What is implemented

- **Backend and admin UI**: ASP.NET Core, Carter minimal APIs, Blazor Server admin interface, ASP.NET Identity, OpenAPI via NSwag, and SignalR for live screen updates.
- **Assets**: image, video, and website assets; drag-and-drop uploads; website asset creation; thumbnails and poster frames; tags; search/filtering; image fit modes.
- **Campaigns**: ordered playlists, per-item durations, enable/disable state, priority, default fallback campaign, start/end dates, weekly recurrence, daily time windows, and time-zone-aware scheduling.
- **Screens and zones**: first-run screen registration, admin approval/rejection, screen details, direct campaign assignment, zone membership, zone-level campaign assignment, online status, and shuffle playback.
- **Display clients**: shared Avalonia client core with desktop and Android TV heads. Clients store backend configuration, register with the backend, reconnect automatically, sync campaign assets, cache media locally, and play scheduled content.
- **Remote control**: restart, reload, identify, next, and previous commands are pushed to connected screens over SignalR.
- **Monitoring and audit**: proof-of-play reporting, asset sync status, audit log, health endpoints, and optional webhook alerting for screens that remain offline beyond a configured threshold.
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
docker compose up --build
```

The API is published on `http://localhost:8080`.

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

## Documentation

- [Feature guide](https://mireya.moritzreis.dev/#/features)
- [Development guide](https://mireya.moritzreis.dev/#/development)
- [Operations guide](https://mireya.moritzreis.dev/#/operations)
- [API guide](https://mireya.moritzreis.dev/#/api)
- [Client packaging](https://mireya.moritzreis.dev/#/packaging)

## Roadmap

- Completed: core API/admin workflow, screen registration and approval, campaign assignment, local client sync/cache, scheduling, zones, audit, proof of play, desktop client, and Android TV client head.
- In progress / planned: production hardening, installer packaging, richer Linux/Raspberry Pi kiosk packaging, broader operational docs, and additional client targets.

## Contributing

1. Fork the repository.
2. Create a branch: `git checkout -b feature/your-feature`.
3. Make changes and run the relevant build/test commands.
4. Submit a pull request with a clear description.

Please keep changes aligned with the existing .NET, Blazor, Avalonia, and EF Core patterns.
