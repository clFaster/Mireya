# Development

This page contains information for developers who want to contribute to Mireya or run it locally for development purposes.

## Prerequisites

Before you begin, ensure you have the following installed:

- **.NET 9.0 SDK** - Download from [Microsoft's .NET website](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Node.js 20+** - Download from [nodejs.org](https://nodejs.org/)
- **PostgreSQL** (optional, for production-like setup) - Download from [postgresql.org](https://www.postgresql.org/download/)
- **Git** - For cloning the repository

## Project Structure

Mireya consists of several components:

- **Mireya.Api** - ASP.NET Core Web API and Admin interface
- **Mireya.Database** - Entity Framework Core database models
- **Mireya.Database.Sqlite** - SQLite database provider (development)
- **Mireya.Database.Postgres** - PostgreSQL database provider (production)
- **Mireya.Client.Avalonia** - PoC Desktop client application (Windows/macOS/Linux)
- **Mireya.Tv** - (deprecate) React Native TV application (Android TV, Apple TV)

## Running Mireya Locally

### 1. Clone and Setup

```bash
git clone https://github.com/clFaster/Mireya.git
cd Mireya
```

### 2. Database Setup

Mireya supports two database providers:

#### SQLite (Recommended for Development)

SQLite is used by default for local development and requires no additional setup.

#### PostgreSQL (Production-like Setup)

1. Install PostgreSQL and create a database:

```bash
# Create database (adjust connection string as needed)
createdb mireya_dev
```

2. Configure the connection string in `src/Mireya.Api/appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=mireya_dev;Username=your_username;Password=your_password"
  }
}
```

### 3. Run Database Migrations

```bash
# For SQLite (development)
dotnet ef database update --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api

# For PostgreSQL (production)
dotnet ef database update --project src/Mireya.Database.Postgres --startup-project src/Mireya.Api
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
https://localhost:5001/Admin/Login
```

Default admin credentials:

- **Email:** `admin@mireya.local`
- **Password:** Check your environment variables or user secrets configuration

### 6. Run the Desktop Client (Optional)

```bash
cd src/Mireya.Client.Avalonia
dotnet run
```

### 7. Run the TV Client (Optional)

For Android TV development:

```bash
cd src/Mireya.Tv
npm install
npm run android
```

For iOS/tvOS development (macOS only):

```bash
cd src/Mireya.Tv
npm install
npm run ios
```

## Development Workflow

### Making API Changes

1. Modify controllers, models, or services in `src/Mireya.Api/`
2. Update database models in `src/Mireya.Database/`
3. Create and run migrations if database schema changes:

```bash
# Add migration
dotnet ef migrations add YourMigrationName --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api

# Update database
dotnet ef database update --project src/Mireya.Database.Sqlite --startup-project src/Mireya.Api
```

### Admin Interface Development

The admin interface uses ASP.NET Core Razor Pages with Tailwind CSS. Files are located in:

- `src/Mireya.Api/Areas/Admin/Pages/` - Razor pages
- `src/Mireya.Api/wwwroot/css/` - Custom styles
- `src/Mireya.Api/wwwroot/js/` - JavaScript files

### Client Development

#### Avalonia Desktop Client

- ViewModels: `src/Mireya.Client.Avalonia/ViewModels/`
- Views: `src/Mireya.Client.Avalonia/Views/`
- Services: `src/Mireya.Client.Avalonia/Services/`

#### React Native TV Client

- Main app: `src/Mireya.Tv/App.tsx`
- Components: `src/Mireya.Tv/src/components/`
- Screens: `src/Mireya.Tv/src/screens/`

### API Client Generation

If you modify API endpoints, regenerate the TypeScript client:

```bash
cd src/Mireya.Tv
npm run generate:api
```

This uses NSwag to generate `src/lib/api/generated/client.ts` from the API's OpenAPI specification.

## Building for Production

### API

```bash
cd src/Mireya.Api
dotnet publish -c Release -o ./publish
```

### Desktop Client

```bash
cd src/Mireya.Client.Avalonia
dotnet publish -c Release -r win-x64 -o ./publish
```

### React Native

```bash
cd src/Mireya.Tv
npm run android -- --mode=release
# or
npm run ios -- --mode=release
```

## Troubleshooting

### Common Issues

1. **Database connection errors**: Ensure your connection string is correct and the database exists
2. **Migration errors**: Make sure you're using the correct database provider project
3. **Admin login fails**: Check that the database is seeded with the default admin user
4. **React Native build fails**: Ensure Android SDK/iOS development environment is properly configured

### Getting Help

- Check existing issues on [GitHub](https://github.com/clFaster/Mireya/issues)
- Join the discussion in GitHub Discussions

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/your-feature-name`
3. Make your changes and test thoroughly
4. Submit a pull request with a clear description of your changes

Please ensure all tests pass and follow the existing code style and patterns.
