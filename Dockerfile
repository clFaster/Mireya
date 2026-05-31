# syntax=docker/dockerfile:1

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files first to leverage layer caching for restore.
COPY src/Mireya.Api/Mireya.Api.csproj src/Mireya.Api/
COPY src/Mireya.Application/Mireya.Application.csproj src/Mireya.Application/
COPY src/Mireya.Database/Mireya.Database.csproj src/Mireya.Database/
COPY src/Mireya.Database.Postgres/Mireya.Database.Postgres.csproj src/Mireya.Database.Postgres/
COPY src/Mireya.Database.Sqlite/Mireya.Database.Sqlite.csproj src/Mireya.Database.Sqlite/
COPY src/MireyaDigitalSignage.ServiceDefaults/MireyaDigitalSignage.ServiceDefaults.csproj src/MireyaDigitalSignage.ServiceDefaults/
COPY src/Mireya.ApiClient/Mireya.ApiClient.csproj src/Mireya.ApiClient/

RUN dotnet restore src/Mireya.Api/Mireya.Api.csproj

# Copy the remaining source and publish.
COPY . .
RUN dotnet publish src/Mireya.Api/Mireya.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production

EXPOSE 8080

# Persisted uploaded media lives here; mount a volume to keep it across restarts.
RUN mkdir -p /app/uploads
VOLUME ["/app/uploads"]

COPY --from=build /app/publish .

ENTRYPOINT ["dotnet", "Mireya.Api.dll"]
