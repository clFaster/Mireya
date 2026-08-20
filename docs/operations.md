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

| Value      | Provider project           | Connection string            |
| ---------- | -------------------------- | ---------------------------- |
| `Sqlite`   | `Mireya.Database.Sqlite`   | `ConnectionStrings:Sqlite`   |
| `Postgres` | `Mireya.Database.Postgres` | `ConnectionStrings:Postgres` |

Development uses SQLite by default. Docker Compose and Aspire use PostgreSQL.

Migrations are applied automatically on API startup. For schema changes, keep migrations in both provider projects.

## Default admin user

The initializer creates the default admin user during startup when configuration is present. If the user already exists, startup only ensures that it has the `Admin` role; it does not reset the existing password.

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

- `db`: PostgreSQL 18 Alpine with a named volume and health check.
- `api`: `moritzreis/mireya-digital-signage`, configured for PostgreSQL with a readiness health check.

Create the local configuration file, replace both example passwords, and start the stack:

```bash
cp .env.example .env
# Edit .env before continuing.
docker compose up -d
```

The `.env` file is ignored by Git and is the right place for host-specific Compose values. Do not commit it. On a production host, restrict its filesystem permissions and prefer the host or orchestrator's secret store when one is available.

The admin UI listens on:

```text
http://localhost:8080/login
```

Check service state and readiness:

```bash
docker compose ps
curl --fail http://localhost:8080/health
```

Compose configuration:

| Variable                           | Required | Default                             | Purpose                                                                                |
| ---------------------------------- | -------- | ----------------------------------- | -------------------------------------------------------------------------------------- |
| `MIREYA_IMAGE`                     | No       | `moritzreis/mireya-digital-signage` | Image repository. Override with `mireya` for a local image.                            |
| `MIREYA_VERSION`                   | No       | `latest`                            | Image tag. Pin an exact version in production.                                         |
| `MIREYA_HTTP_PORT`                 | No       | `8080`                              | Port exposed on the host.                                                              |
| `MIREYA_FORWARDED_HEADERS_ENABLED` | No       | `true`                              | Process proxy-provided client IP and original HTTPS scheme.                            |
| `POSTGRES_DB`                      | No       | `mireya`                            | PostgreSQL database name.                                                              |
| `POSTGRES_USER`                    | No       | `mireya`                            | PostgreSQL user.                                                                       |
| `POSTGRES_PASSWORD`                | Yes      | None                                | PostgreSQL password. Avoid `;` because the value is inserted into a connection string. |
| `MIREYA_ADMIN_EMAIL`               | No       | `admin@mireya.local`                | Initial administrator email.                                                           |
| `MIREYA_ADMIN_PASSWORD`            | Yes      | None                                | Initial administrator password; minimum nine characters and at least one digit.        |

Validate the Compose configuration:

```bash
docker compose --env-file .env config
```

The API applies Entity Framework migrations and creates the initial admin account during startup. The password is used only when creating that account. Changing `MIREYA_ADMIN_PASSWORD` later does not change an existing password.

Three named volumes keep runtime state across container replacement:

| Volume           | Container path                          | Contents                          |
| ---------------- | --------------------------------------- | --------------------------------- |
| `mireya-db`      | `/var/lib/postgresql`                   | PostgreSQL data                   |
| `mireya-uploads` | `/app/uploads`                          | Uploaded assets and thumbnails    |
| `mireya-keys`    | `/home/app/.aspnet/DataProtection-Keys` | ASP.NET Core data-protection keys |

PostgreSQL 18 uses a version-specific data directory below `/var/lib/postgresql`. An existing PostgreSQL 17 volume cannot be upgraded by changing the image tag alone. Back up and restore the database or use `pg_upgrade` before starting PostgreSQL 18 with existing data.

Update a pinned installation by changing `MIREYA_VERSION` and running:

```bash
docker compose pull
docker compose up -d
```

`docker compose down` removes containers and the network but keeps the named volumes. `docker compose down --volumes` permanently deletes the database, uploaded media, and keys; use it only when intentionally resetting the installation.

## Reverse proxies, Coolify, and Cloudflare

The Compose deployment enables ASP.NET Core forwarded-header processing so the application sees the original HTTPS scheme when TLS terminates at Coolify, Traefik, Cloudflare Tunnel, or another reverse proxy. Only expose the application port to a trusted proxy when this setting is enabled. Set `MIREYA_FORWARDED_HEADERS_ENABLED=false` when the application receives client traffic directly and doesn't use forwarded headers.

The proxy must support WebSocket upgrades for the Blazor endpoint at `/_blazor`. Coolify/Traefik and Cloudflare Tunnel normally support WebSockets without a special Mireya setting.

Do not apply a Cloudflare “Cache Everything” rule to Mireya's HTML, authentication, API, health, or SignalR paths. Bypass edge caching for at least:

- `/_blazor*`
- `/api/*`
- `/auth/*`
- `/health`
- `/alive`

Static assets such as `/_framework/*`, CSS, icons, and uploaded media can be cached according to their origin response headers. After replacing a deployment that returned a cached 404 for a framework asset, purge the Cloudflare cache so the corrected asset is fetched from the new container.

## Publishing Docker images

Enable **Release the Docker image** when running the **Release Mireya** workflow. It
publishes a multi-platform `linux/amd64` and `linux/arm64` image. The workflow is the
source of truth for validation, smoke tests, tags, and image metadata. Production
deployments should pin an exact version or commit-specific tag rather than a moving
channel.

Configure these GitHub repository settings before the first publish:

- Actions variable `DOCKERHUB_USERNAME`: the Docker Hub account name (`moritzreis`).
- Actions secret `DOCKERHUB_TOKEN`: a Docker Hub access token with permission to push to `moritzreis/mireya-digital-signage`.

## Aspire

The Aspire AppHost in `src/MireyaDigitalSignage.AppHost` pins PostgreSQL 18,
stores its version-specific data in the `mireya-db-18` volume, adds a
`Postgres` database, and starts the API with `provider=Postgres`. The older
`mireya-db` volume is not upgraded automatically; migrate its data with
`pg_dump`/`pg_restore` before switching an existing development environment.

Run it with:

```bash
dotnet run --project src/MireyaDigitalSignage.AppHost/MireyaDigitalSignage.AppHost.csproj
```

Use Aspire for a local production-like topology with service defaults, health checks, telemetry wiring, and PostgreSQL orchestration.

## Health and discovery endpoints

The service defaults and API expose operational endpoints:

| Endpoint        | Purpose                                                                 |
| --------------- | ----------------------------------------------------------------------- |
| `GET /api/info` | Public Mireya server identity. Clients use it to confirm a backend URL. |
| `GET /alive`    | Liveness check.                                                         |
| `GET /health`   | Readiness check, including database connectivity.                       |

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
