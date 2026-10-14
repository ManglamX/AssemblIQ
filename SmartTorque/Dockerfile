# syntax=docker/dockerfile:1
# =============================================================================
# SmartTorque — Guidance & Traceability System
# Multi-stage build for the ASP.NET Core MVC web app (net9.0).
#
# Build:   docker build -t smarttorque .
#
# Run (Linux containers; SQL Server reached on the host):
#   docker run -d --name smarttorque -p 8080:8080 ^
#     -e "ConnectionStrings__Default=Server=host.docker.internal,1433;Database=SmartTorqueDb;User Id=sa;Password=<YourStrong!Pass123>;TrustServerCertificate=True" ^
#     -e "Seed__AdminEmail=admin@smarttorque.local" ^
#     -e "Seed__AdminPassword=<admin password>" ^
#     -v smarttorque-uploads:/app/wwwroot/uploads ^
#     -v smarttorque-logs:/app/logs ^
#     smarttorque
#   (on Linux add: --add-host=host.docker.internal:host-gateway)
#
# Notes:
#   * The app runs EF migrations + seeding on startup (Program.cs), so SQL must
#     be reachable before it boots — otherwise it logs the error and exits.
#   * Integrated Security cannot work from inside a container: use SQL
#     authentication in the connection string (dev SQL: enable SQL auth / sa).
#   * /health includes the EF DbContext check, so the HEALTHCHECK below goes
#     unhealthy when the database is unreachable.
#   * ASPNETCORE_ENVIRONMENT defaults to Production (appsettings.json only);
#     pass -e ASPNETCORE_ENVIRONMENT=Development to also load
#     appsettings.Development.json.
#   * Base images are Linux (Docker Desktop default). For Windows containers,
#     swap the tags to -nanoserver ltsc2022 and use a Windows SQL image.
# =============================================================================

# ---------- build -------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Project files first, so the NuGet restore layer stays cached until they
# change. Directory.Build.props and .editorconfig sit at the repo root and
# affect the compile (warnings-as-errors list, LangVersion, style analyzers).
COPY Directory.Build.props ./
COPY .editorconfig ./
COPY src/SmartTorque.Core/SmartTorque.Core.csproj src/SmartTorque.Core/
COPY src/SmartTorque.Data/SmartTorque.Data.csproj src/SmartTorque.Data/
COPY src/SmartTorque.Services/SmartTorque.Services.csproj src/SmartTorque.Services/
COPY src/SmartTorque.Web/SmartTorque.Web.csproj src/SmartTorque.Web/
# NuGet packages are kept in a BuildKit cache mount rather than an image layer:
# partial downloads survive a failed attempt, so retrying on a flaky network
# makes progress instead of starting over. --disable-parallel limits the number
# of simultaneous connections, which is what kept getting reset.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/SmartTorque.Web/SmartTorque.Web.csproj --disable-parallel

# Full sources, then publish the web project (tests/ and tools/ are never
# copied - .dockerignore keeps bin/obj and runtime uploads out of the context).
COPY src/ src/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/SmartTorque.Web/SmartTorque.Web.csproj \
    -c Release -o /app/publish --no-restore /p:UseAppHost=false

# ---------- runtime -----------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# curl is used only by the HEALTHCHECK below.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Runtime-written folders: wwwroot/uploads/** (template images, product images,
# BM workbooks) and logs/**. The upload services create their own subfolders
# (Directory.CreateDirectory), so the two roots are enough — chowned to the
# non-root app user the image runs as ($APP_UID, provided by the base image).
RUN mkdir -p logs wwwroot/uploads \
    && chown -R $APP_UID:$APP_UID logs wwwroot

USER $APP_UID

EXPOSE 8080

# /health runs the EF DbContext check, so the container reports unhealthy when
# SQL Server is down or migrations could not be applied.
HEALTHCHECK --interval=30s --timeout=5s --start-period=45s --retries=3 \
    CMD curl -fsS http://127.0.0.1:8080/health || exit 1

ENTRYPOINT ["dotnet", "SmartTorque.Web.dll"]
