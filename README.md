# Roofied

A safety-first web application for reporting suspected drink-spiking ("roofied") encounters, viewing
**moderated, anonymized, approximate** reports on a map, and participating in **moderated** community channels.

> Roofied is **not** a substitute for emergency services, medical care, police reporting, or crisis
> support. The UI states this prominently throughout.

## Table of contents
- [Safety & privacy model](#safety--privacy-model)
- [Architecture](#architecture)
- [Tech stack](#tech-stack)
- [Local setup](#local-setup)
- [Database & migrations](#database--migrations)
- [Running tests](#running-tests)
- [Configuration reference](#configuration-reference)
- [Deployment (IIS + SQL Server)](#deployment-iis--sql-server)
- [Rollback](#rollback)
- [Known limitations & risks](#known-limitations--risks)

## Safety & privacy model

These are core product requirements, enforced in code:

- **Reports start in `PendingReview`** and are never shown publicly until a moderator approves them.
- **Restricted data is physically separated** from public data. The `Report` table holds only
  public-safe, moderated fields. Sensitive content lives in separate, access-restricted tables:
  - `ReportRestricted` — raw narrative, symptoms, exact time, private contact details.
  - `ReportLocations` — exact coordinates / address (moderator-only).
  - `ReportPublicLocations` — intentionally **fuzzed** coordinates for the public map.
- **The only path to public report data** is `PublicReportProjections` (in `Roofied.Application`).
  These projections reference *only* public-safe columns, so exact location, identity, private contact,
  raw narrative, and internal notes **cannot** leak through a public query by construction.
  Tests (`PublicProjectionSafetyTests`, `ReportRedactionIntegrationTests`) assert this structurally.
- **Locations are generalized** by `LocationPrecisionService`: exact coordinates are snapped to the
  centroid of a configurable grid cell (default 1500 m, hard floor 500 m). Nearby reports share a cell,
  which also drives map clustering. Exact coordinates never reach the client.
- **No exact coordinates in HTML/API/logs/browser storage.** The map JS only ever receives fuzzed points.
- **Abuse controls:** server-side validation (FluentValidation), HTML sanitization (HtmlSanitizer),
  heuristic PII/accusation detection (auto-flags for moderators), profanity screening, durable
  rate limiting, pluggable CAPTCHA (Cloudflare Turnstile), content flagging, and audit logging.
- **No direct messaging** between users in v1. **Comments are disabled** until a moderation policy is live.
- **Audit logs never contain** sensitive report text or precise locations; IPs are stored only as salted hashes.

## Architecture

Clean, layered solution:

```
Roofied.sln
├─ src/
│  ├─ Roofied.Domain          # Entities, enums, value objects (no infrastructure dependencies)
│  ├─ Roofied.Application     # DTOs, service interfaces, validators, public projections,
│  │                          #   location-precision + PII + workflow logic, policy names
│  ├─ Roofied.Infrastructure  # EF Core DbContext, configs, migrations, seed, service impls, providers
│  └─ Roofied.Web             # Blazor Web App (Interactive Server) + MudBlazor UI + Identity + endpoints
└─ tests/
   └─ Roofied.Tests           # xUnit: redaction, projection safety, precision, workflow,
                              #   validation, rate limiting, channel moderation, authorization
```

Cross-cutting:
- **Dependency injection** throughout; services live behind interfaces.
- **Authorization** is centralized in explicit policies (`PolicyNames` + `AuthorizationPolicies`), not scattered role checks.
- **Mapping** is isolated behind `IMapInterop` (Leaflet/OpenStreetMap) + a small JS interop layer; the provider can be swapped.
- **Structured logging** via Serilog (console + rolling file).
- **Configuration** via `appsettings.json`, environment variables, and user secrets (development).

## Tech stack

- .NET 10, ASP.NET Core Blazor Web App (Interactive Server render mode)
- MudBlazor design system
- Entity Framework Core 10 + SQL Server
- ASP.NET Core Identity (roles: Administrator, Moderator, RegisteredUser; plus anonymous reporters)
- Leaflet + OpenStreetMap (marker clustering), behind a C# abstraction
- FluentValidation, HtmlSanitizer (Ganss.Xss), Serilog

## Local setup

Prerequisites: **.NET 10 SDK**, **SQL Server LocalDB** (or any SQL Server), and the EF tools:

```bash
dotnet tool install --global dotnet-ef --version 10.0.9   # or 'update'
```

Restore & build:

```bash
dotnet restore
dotnet build
```

Development configuration lives in `src/Roofied.Web/appsettings.Development.json` (LocalDB connection,
a dev IP-hash salt, captcha disabled, and a seed admin `admin@roofied.local` / `ChangeMe!2026`).
For real secrets in development, prefer user secrets:

```bash
cd src/Roofied.Web
dotnet user-secrets set "SeedAdmin:Password" "a-strong-password"
```

Run:

```bash
dotnet run --project src/Roofied.Web
```

On startup the app **applies migrations and seeds** roles, the admin account, report/venue categories,
the initial channels, and starter resources. Visit the printed URL; health check at `/health`.

## Database & migrations

The `DefaultConnection` connection string is required. Migrations live in
`src/Roofied.Infrastructure/Persistence/Migrations`.

```bash
# Add a migration (after model changes)
dotnet ef migrations add <Name> \
  --project src/Roofied.Infrastructure \
  --startup-project src/Roofied.Web \
  --output-dir Persistence/Migrations

# Apply migrations to the database
dotnet ef database update \
  --project src/Roofied.Infrastructure \
  --startup-project src/Roofied.Web

# Verify the model matches the latest migration (use in CI)
dotnet ef migrations has-pending-model-changes \
  --project src/Roofied.Infrastructure --startup-project src/Roofied.Web

# Generate an idempotent SQL script for controlled/production deploys
dotnet ef migrations script --idempotent \
  --project src/Roofied.Infrastructure --startup-project src/Roofied.Web \
  --output ./roofied-migrations.sql
```

The app calls `Database.Migrate()` on startup. For tightly controlled environments you may prefer to
disable auto-migrate and apply the generated SQL script during your deployment window.

> Note: soft-delete query filters are applied via the EF metadata API, which can trigger a
> false-positive `PendingModelChangesWarning` at runtime. It is downgraded to a log entry; real drift
> is caught by `has-pending-model-changes` (run it in CI).

## Running tests

```bash
dotnet test
```

Coverage includes the safety-critical areas: report validation, **public projection/redaction**
(restricted fields proven absent from public DTOs and queries), authorization policies, moderation
workflow transitions, **map-location precision**, anonymous submission rate limits, and channel
moderation/flagging.

## Configuration reference

| Key | Purpose | Required |
|-----|---------|----------|
| `ConnectionStrings:DefaultConnection` | SQL Server connection string | Yes |
| `Security:IpHashSalt` | Salt for one-way hashing of client IPs | Yes (production) |
| `Captcha:Enabled` / `SiteKey` / `SecretKey` | Cloudflare Turnstile bot protection | If `Enabled` |
| `LocationPrecision:DefaultGridSizeMeters` | Default generalization grid (m) | No (default 1500) |
| `LocationPrecision:MinGridSizeMeters` | Hard floor on grid precision (m) | No (default 500) |
| `RateLimiting:*` | Per-action limit + window | No (sensible defaults) |
| `SeedAdmin:Email` / `Password` | First-run admin bootstrap | No (recommended once) |

Set production values via environment variables (`Security__IpHashSalt`, `ConnectionStrings__DefaultConnection`,
…) or a non-committed `appsettings.Production.local.json`. **Never commit secrets.** See `.env.example`.

`StartupValidation` fails fast (in Production) if required configuration is missing or weak.

## Deployment (IIS + SQL Server)

1. **Server prerequisites:** install the **.NET 10 Hosting Bundle** (ASP.NET Core Module v2) on the IIS server.
2. **Publish:**
   ```bash
   dotnet publish src/Roofied.Web -c Release -o ./publish
   ```
   (Or use a publish profile in Visual Studio targeting "Folder" / "IIS".)
3. **IIS site/app:** point an IIS application (under your existing site) at the `./publish` folder.
   Use a dedicated Application Pool set to **No Managed Code** (the ASP.NET Core Module hosts the app).
4. **Configuration:** set the environment variables from `.env.example` on the app pool / site
   (or `web.config` `environmentVariables`). At minimum: `ConnectionStrings__DefaultConnection`,
   `Security__IpHashSalt`, and (recommended) Turnstile keys with `Captcha__Enabled=true`.
   Set `ASPNETCORE_ENVIRONMENT=Production`.
5. **Database:** create the database and a least-privilege SQL login for the app. Apply migrations
   either by letting the app migrate on first start, or by running the generated idempotent SQL script.
6. **HTTPS:** terminate TLS at IIS. The app assumes HTTPS (HSTS + HTTPS redirect enabled in production).
7. **Health monitoring:** point your load balancer / monitoring at `GET /health` (returns `Healthy`
   and includes a database check).
8. **First admin:** set `SeedAdmin__Email`/`SeedAdmin__Password` for the first deploy to bootstrap an
   administrator, then remove them and manage further roles from **Admin → Users & roles**.

### Publish profile guidance
- Target framework-dependent deployment (smaller) since the Hosting Bundle is installed, or
  self-contained if you cannot install the runtime on the server.
- Ensure `appsettings.Production.json` is included but contains **placeholders only**; real secrets
  come from environment variables.

## Rollback

- **App rollback:** keep the previous `./publish` output (or build artifact). To roll back, stop the
  IIS app, swap the folder back to the prior version, and restart the app pool.
- **Database rollback:** migrations are additive and designed to be safe. To revert a specific migration:
  ```bash
  dotnet ef database update <PreviousMigrationName> \
    --project src/Roofied.Infrastructure --startup-project src/Roofied.Web
  ```
  Always **back up the database** before applying or reverting migrations in production. Prefer
  generating and reviewing the idempotent SQL script for production changes.
- If a deploy fails on startup config validation, the app logs the specific missing keys (without
  revealing secret values) and does not start — fix configuration and restart.

## Known limitations & risks

- **Comments** are modeled but disabled until a moderation policy is implemented.
- **CAPTCHA** ships with a Cloudflare Turnstile adapter and a dev no-op; enable and configure keys for production.
- **Rate limiting for sign-in** relies on ASP.NET Identity lockout (5 failed attempts / 15 min). Durable,
  per-action rate limiting covers report submission, channel posting, and content flags. Inside interactive
  Blazor Server circuits a real client IP is not always available, so the rate-limit key falls back to a
  per-circuit session id; for strong per-IP limiting, additionally enforce throttling at the IIS/WAF/CDN layer.
- **PII/accusation detection** is heuristic and conservative — it flags content for moderators, it never
  auto-publishes or auto-rejects. Human moderation remains essential.
- The map uses public OpenStreetMap tiles; for higher volume, use a dedicated tile provider and update the CSP.
