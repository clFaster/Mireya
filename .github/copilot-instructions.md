# Mireya Digital Signage - Copilot Instructions

## Architecture Overview

Mireya is a digital signage platform with two main components:

1. **Backend (`Mireya.Api`)** - ASP.NET Core Web API + Razor Pages admin interface
2. **Client (`Mireya.Client.Avalonia`)** - Avalonia desktop app that displays content on screens

**Data flows:**
- Screens self-register via REST API → Admin approves → Screen receives JWT
- Content sync happens via SignalR hub (`ScreenHub`) using strongly-typed client interface `IScreenClient`
- Assets are uploaded to `/uploads/` folder and served as static files

## Project Structure

| Project | Purpose |
|---------|---------|
| `Mireya.Api` | Backend API, admin Razor Pages, SignalR hub |
| `Mireya.Database` | EF Core DbContext and entity models |
| `Mireya.Database.Postgres` | PostgreSQL migrations (production) |
| `Mireya.Database.Sqlite` | SQLite migrations (development) |
| `Mireya.ApiClient` | NSwag-generated API client for consumers |
| `Mireya.Client.Avalonia` | Desktop display client with local SQLite cache |

## Key Patterns

### Service Layer Pattern
All business logic lives in `Services/` with interface + implementation pairs:
```
Services/
  Asset/AssetService.cs       → IAssetService
  Campaign/CampaignService.cs → ICampaignService
  ScreenManagement/           → IScreenManagementService
```
Register services in `Program.cs` using `builder.Services.AddScoped<IService, Service>()`.

### Database Provider Switching
The app supports SQLite (dev) and PostgreSQL (prod) via `Provider.cs`:
- Set `"provider": "Sqlite"` or `"provider": "Postgres"` in appsettings
- Each provider has its own migrations assembly (see `DbContextServiceCollectionExtension.cs`)

### Role-Based Authorization
Two roles exist in `Constants/Roles.cs`:
- `Roles.Admin` - For admin users accessing Razor Pages
- `Roles.Screen` - For display devices after approval

Use `[Authorize(Roles = Roles.Admin)]` on controllers/pages.

### SignalR Real-Time Updates
Screens connect to `/hubs/screen` via `ScreenHub`. The `IScreenClient` interface defines client methods:
- `ReceiveConfigurationUpdate(ScreenConfiguration)` - Push new campaigns
- `StartAssetSync(List<CampaignSyncInfo>)` - Trigger asset downloads

Use `IScreenSynchronizationService.SyncScreenAsync(displayId)` to push updates.

## Development Commands

```powershell
# Run API server (auto-applies migrations)
cd src/Mireya.Api
dotnet run

# Add EF migration (SQLite)
dotnet ef migrations add MigrationName --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api

# Add EF migration (PostgreSQL)
dotnet ef migrations add MigrationName --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api

# Regenerate API client from OpenAPI spec
cd src/Mireya.ApiClient
dotnet nswag run nswag.json

# Format code
csharpier format .
```

## Admin Interface

Razor Pages live in `Areas/Admin/Pages/`. Key pages:
- `/Admin/Login` - Cookie-based auth (AllowAnonymous)
- `/Admin/Screens/` - Manage and approve displays
- `/Admin/Campaigns/` - Create content playlists
- `/Admin/Assets/` - Upload media files

Layout uses Tailwind CSS. Static files in `wwwroot/`.

## Key Models & Relationships

```
Display ←→ CampaignAssignment ←→ Campaign ←→ CampaignAsset ←→ Asset
   │                                              │
   └── User (ASP.NET Identity)                    └── AssetType: Image|Video|Website
   └── ApprovalStatus: Pending|Approved|Rejected
```

- `Display.ScreenIdentifier` - Unique 10-char device code (shown to admin for approval)
- `CampaignAsset.Position` - Ordering within campaign playlist
- `Asset.Source` - URL path like `/uploads/guid.jpg` or external URL for websites

## Code Style

- Use primary constructors for dependency injection
- Async methods return `Task<T>` and end with `Async` suffix
- DTOs live alongside their service (e.g., `AssetSummary.cs` in `Services/Asset/`)
- Run `csharpier format .` before commits
