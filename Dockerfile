# syntax=docker/dockerfile:1
# Multi-stage build for Roofied (.NET 10 Blazor Server, 4 projects:
# Domain + Application + Infrastructure + Web). Mirrors the alibalib platform
# image conventions (Kenman Design Studio / AWBlazor).

ARG DOTNET_VERSION=10.0

# ---- build ---------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore as a cache-friendly layer — copy only the four .csproj first. The
# project references are relative siblings (..\Roofied.X), so we flatten the
# src/ prefix and keep the projects as siblings under /src. Restoring Web
# pulls Application + Infrastructure + Domain transitively.
COPY src/Roofied.Domain/Roofied.Domain.csproj                 Roofied.Domain/
COPY src/Roofied.Application/Roofied.Application.csproj        Roofied.Application/
COPY src/Roofied.Infrastructure/Roofied.Infrastructure.csproj Roofied.Infrastructure/
COPY src/Roofied.Web/Roofied.Web.csproj                       Roofied.Web/
RUN dotnet restore Roofied.Web/Roofied.Web.csproj

# Copy the rest of the source and publish the web app.
COPY src/Roofied.Domain/         Roofied.Domain/
COPY src/Roofied.Application/     Roofied.Application/
COPY src/Roofied.Infrastructure/ Roofied.Infrastructure/
COPY src/Roofied.Web/            Roofied.Web/
WORKDIR /src/Roofied.Web
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# ---- runtime -------------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS final
WORKDIR /app

# ICU for MudBlazor culture formatting; tzdata for correct timestamps.
RUN apt-get update \
 && apt-get install -y --no-install-recommends tzdata libicu-dev \
 && rm -rf /var/lib/apt/lists/*

# Non-root user for the app process.
RUN groupadd -r roofied && useradd -r -g roofied -m -d /home/roofied roofied

COPY --from=build /app/publish .

# App_Data holds DataProtection keys (Blazor antiforgery); logs/ holds the
# Serilog rolling file sink output. Both must be writable + persistent.
RUN mkdir -p /app/App_Data /app/logs && chown -R roofied:roofied /app

USER roofied

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS=http://+:8080 \
    ASPNETCORE_FORWARDEDHEADERS_ENABLED=true \
    DOTNET_RUNNING_IN_CONTAINER=true

EXPOSE 8080

# The app also exposes an HTTP /health endpoint (DbContext check) that nginx /
# external monitoring can probe; no in-container HEALTHCHECK (the slim aspnet
# image ships no curl/wget, matching the other platform apps).
ENTRYPOINT ["dotnet", "Roofied.Web.dll"]
