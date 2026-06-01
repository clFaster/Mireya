# Operations

This guide covers runtime configuration, database providers, Docker, Aspire, health checks, and operational behavior.

## Configuration sources

The API builds configuration from:

1. `appsettings.json`
2. `appsettings.Development.json`
3. user secrets
4. environment variables

Environment variables use ASP.NET Core's double-underscore convention. For example:

```bash
DefaultAdminUser__Password=change-me
ConnectionStrings__Postgres="Host=db;Database=mireya;Username=mireya;Password=change-me"
Alerting__Enabled=true
```

## Database provider

The `provider` setting selects the EF Core provider:

| Value | Provider project | Connection string |
| --- | --- | --- |
| `Sqlite` | `Mireya.Database.Sqlite` | `ConnectionStrings:Sqlite` |
| `Postgres` | `Mireya.Database.Postgres` | `ConnectionStrings:Postgres` |

Development uses SQLite by default. Docker Compose and Aspire use PostgreSQL.

Migrations are applied automatically on API startup. For schema changes, keep migrations in both provider projects.

## Default admin user

The initializer creates or updates the default admin user during startup when configuration is present.

Development settings include:

- `DefaultAdminUser:Email`: `admin@mireya.local`
- `DefaultAdminUser:Password`: `Admin123!`

For shared or production-like environments, provide the password via user secrets or environment variables instead of relying on development settings:

```bash
DefaultAdminUser__Password=change-me
```

## Uploaded media

Uploaded media is served from `/uploads`. The API creates the uploads directory under the application content root.

In Docker, uploads are mounted at `/app/uploads` and persisted in the `mireya-uploads` named volume.

## Docker Compose

`docker-compose.yml` runs:

- `db`: PostgreSQL 17 Alpine with a named volume and health check.
- `api`: the published Mireya API image, configured for PostgreSQL.

Start the stack:

```bash
docker compose up --build
```

The API listens on:

```text
http://localhost:8080
```

Useful environment variables:

```bash
POSTGRES_PASSWORD=change-me
MIREYA_ADMIN_PASSWORD=Admin123!
```

The Compose file maps those into:

- `ConnectionStrings__Postgres`
- `DefaultAdminUser__Password`
- `provider=Postgres`

Validate the Compose configuration:

```bash
docker compose config
```

## Aspire

The Aspire AppHost in `src/MireyaDigitalSignage.AppHost` creates a PostgreSQL resource with a data volume, adds a `Postgres` database, and starts the API with `provider=Postgres`.

Run it with:

```bash
dotnet run --project src/MireyaDigitalSignage.AppHost/MireyaDigitalSignage.AppHost.csproj
```

Use Aspire for a local production-like topology with service defaults, health checks, telemetry wiring, and PostgreSQL orchestration.

## Health and discovery endpoints

The service defaults and API expose operational endpoints:

| Endpoint | Purpose |
| --- | --- |
| `GET /api/info` | Public Mireya server identity. Clients use it to confirm a backend URL. |
| `GET /alive` | Liveness check. |
| `GET /health` | Readiness check, including database connectivity. |

OpenAPI and Swagger UI are enabled in the Development environment.

## Offline screen alerting

Configure alerting through the `Alerting` section:

```json
{
  "Alerting": {
    "Enabled": true,
    "OfflineWebhookUrl": "https://hooks.example.com/your-endpoint",
    "OfflineThresholdMinutes": 5,
    "PollIntervalSeconds": 60
  }
}
```

Environment variable equivalents:

```bash
Alerting__Enabled=true
Alerting__OfflineWebhookUrl=https://hooks.example.com/your-endpoint
Alerting__OfflineThresholdMinutes=5
Alerting__PollIntervalSeconds=60
```

When enabled, the background monitor sends JSON webhook payloads for:

- `screen.offline`
- `screen.online`

Each outage triggers one offline alert. The alert state is cleared after the screen is seen online again.

## Authentication and roles

Mireya uses ASP.NET Identity with two roles:

- `Admin`: web admin and administrative API access.
- `Screen`: display-client access to screen and asset-sync endpoints.

The Blazor admin UI uses cookies. Display clients use bearer tokens and pass the access token to the SignalR hub during connection.

## Runtime notes

- The API applies migrations and runs the initializer during startup.
- In Development, `/swagger` and OpenAPI output are available.
- In non-development environments, HTTPS redirection is enabled by the API pipeline.
- Uploaded files should be stored on persistent storage in any long-running deployment.
- PostgreSQL is the intended provider for production-like runs.
