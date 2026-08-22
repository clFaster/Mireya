# syntax=docker/dockerfile:1

ARG VERSION=0.0.0-local
ARG REVISION=unknown

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG VERSION
ARG REVISION
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
RUN dotnet publish src/Mireya.Api/Mireya.Api.csproj \
    --configuration Release \
    --output /app/publish \
    --no-restore \
    -p:Version="${VERSION}" \
    -p:SourceRevisionId="${REVISION}"

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
ARG VERSION
ARG REVISION

LABEL org.opencontainers.image.title="Mireya Digital Signage" \
      org.opencontainers.image.description="Mireya digital-signage backend and administration UI" \
      org.opencontainers.image.source="https://github.com/clFaster/Mireya" \
      org.opencontainers.image.url="https://github.com/clFaster/Mireya" \
      org.opencontainers.image.documentation="https://mireya.moritzreis.dev" \
      org.opencontainers.image.licenses="GPL-3.0-only" \
      org.opencontainers.image.version="${VERSION}" \
      org.opencontainers.image.revision="${REVISION}"

WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_EnableDiagnostics=0

EXPOSE 8080

# curl is used by the image health check. FFmpeg/ffprobe inspect uploaded videos,
# normalize rotation metadata into pixels and generate thumbnails. Uploaded media
# must be writable by the non-root user and mounted as persistent storage.
RUN apt-get update \
    && apt-get install --yes --no-install-recommends curl ffmpeg \
    && rm -rf /var/lib/apt/lists/* \
    && mkdir -p /app/uploads /home/app/.aspnet/DataProtection-Keys \
    && chown -R "$APP_UID:$APP_UID" /app/uploads /home/app/.aspnet

VOLUME ["/app/uploads", "/home/app/.aspnet/DataProtection-Keys"]

COPY --from=build /app/publish .

USER $APP_UID

HEALTHCHECK --interval=30s --timeout=5s --start-period=30s --retries=3 \
    CMD ["curl", "--fail", "--silent", "--show-error", "http://localhost:8080/health"]

ENTRYPOINT ["dotnet", "Mireya.Api.dll"]
