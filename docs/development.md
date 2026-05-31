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

Both projects intentionally keep the historical `Mireya.Client.Avalonia` root namespace
(only the assembly names differ) to avoid churn across the moved XAML and code.

```bash
dotnet build src/Mireya.Client.Desktop/Mireya.Client.Desktop.csproj
```

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
> the project analysis / roadmap for the planned phased rollout.

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
