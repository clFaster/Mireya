# Mireya
A versatile digital signage solution for creating and managing visual content across multiple screens in real time.

## Overview

```mermaid
flowchart LR
    A[Web] -- REST API and SignalR --> B[Server]
    C[ClientApp] -- WebSocket --> B
    B -- Content Updates --> C
    B -- Live Updates --> A
```

## Architecture

- **Server (.NET Core):**  
	The backend is developed with .NET Core, responsible for managing screens, user authentication, and content distribution. It exposes a REST API for standard operations and uses SignalR for real-time updates and communication.

- **Web Frontend (Next.js):**  
	The web interface, built with Next.js, allows users to configure screens, design templates, and schedule content. It interacts with the .NET backend via REST APIs and receives live updates through SignalR.

- **Client Application (Android TV):**  
	The client app runs on Android TV devices, connecting to the server using WebSocket for efficient, real-time content delivery and screen updates.

This architecture ensures seamless management and instant synchronization of visual content across multiple screens.

# Development

## Database Providers

- **SQLite**: Used for quick local development
- **PostgreSQL**: Used for production and testing environments

## Migrations

```bash
# Add Migration for SQLite (Development)
dotnet ef migrations add <MigrationName> --project .\src\Mireya.Database.Sqlite --startup-project .\src\Mireya.Api -- --provider Sqlite

# Add Migration for PostgreSQL (Production/Testing)
dotnet ef migrations add <MigrationName> --project .\src\Mireya.Database.Postgres --startup-project .\src\Mireya.Api -- --provider Postgres

# Apply migrations
dotnet ef database update --project .\src\Mireya.Database.Sqlite --startup-project .\src\Mireya.Api -- --provider Sqlite
dotnet ef database update --project .\src\Mireya.Database.Postgres --startup-project .\src\Mireya.Api -- --provider Postgres
```