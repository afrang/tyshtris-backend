# TishtryaCMS Backend

Modular monolith API (`.NET 10`). Schema setup and seed data run automatically when the host starts — there are no EF Core migration folders to apply by hand.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server (local or Docker), reachable with the connection string below
- Node.js 20+ (only if you also run Control Center)
- Docker + Docker Compose (optional full stack)

## Docker (API + SQL Server + Control Center)

From the **repo root** (`TyshtriaCMS/`):

```bash
cp .env.example .env
docker compose up --build -d
```

| Service | URL |
|---|---|
| Control Center | http://localhost:8080 |
| API | http://localhost:5068 |
| Swagger (when `ASPNETCORE_ENVIRONMENT=Development`) | http://localhost:5068/swagger |
| SQL Server | `localhost:1433` |

Schema seeders run automatically when the API container starts (after SQL is healthy).

Useful commands:

```bash
docker compose logs -f api
docker compose down
docker compose down -v   # also wipe SQL + uploads volumes
```

On push to `main`, `.github/workflows/deploy.yml` builds the API image and publishes it to GitHub Container Registry (GHCR), tagged `latest` and with the commit SHA. It does not deploy to a server.

For server deployment, configure the variables and secrets required by `docker-compose.deploy.yml` on the target host, then run Docker Compose there. The API and SQL data are persisted in Docker volumes; do not run `docker compose down -v` unless you intend to delete them.

## Connection string

Edit `src/Host/TishtryaCMS.Api/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost,1433;Database=TishtryaCMS;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;Encrypt=False;"
}
```

Create the database first if SQL Server does not auto-create it for your login:

```sql
CREATE DATABASE TishtryaCMS;
```

## Run the API

From the `backend` folder:

```bash
dotnet restore TishtryaCMS.slnx
dotnet run --project src/Host/TishtryaCMS.Api/TishtryaCMS.Api.csproj --launch-profile http
```

| Endpoint | URL |
|---|---|
| API | http://localhost:5068 |
| Swagger | http://localhost:5068/swagger |
| Health | http://localhost:5068/health |
| DB health | http://localhost:5068/health/db |

HTTPS profile (optional):

```bash
dotnet run --project src/Host/TishtryaCMS.Api/TishtryaCMS.Api.csproj --launch-profile https
```

## Schema “migration” and seed

Full table/column reference: [DATABASE.md](DATABASE.md).

On every startup, `Program.cs` runs module seeders in this order:

1. `IdentitySeeder`
2. `ContentModulesSeeder`
3. `FileManagerSeeder`
4. `EditorTryaSeeder`
5. `SettingsSeeder`

### What they do

| Module | Schema | Behavior |
|---|---|---|
| Identity | `identity` (via EF) | `EnsureCreatedAsync` + seeds SuperAdmin if missing |
| ContentModules | `content` | Idempotent SQL: creates tables / additive columns if missing |
| FileManager | `filemanager` | Idempotent SQL: creates tables if missing |
| EditorTrya | `editortrya` | Idempotent SQL: creates tables if missing |
| Settings | `settings` | Idempotent SQL + default `SiteSettings` row if empty |

After you pull schema changes (new tables/columns in a seeder), **restart the API**. Seeders are idempotent for create-if-missing paths; they do not drop existing user data.

### Seeded SuperAdmin (Identity)

| Field | Value |
|---|---|
| Email | `info@tishtrya.cp` |
| Password | `essi36865` |
| Role | SuperAdmin |

Change these in `IdentitySeeder` before any non-local use.

## Apply a new schema change

1. Update the relevant `*Seeder.cs` under `src/Modules/.../Infrastructure/` with `IF OBJECT_ID ... IS NULL` / `COL_LENGTH ... IS NULL` SQL (same pattern as existing seeders).
2. Rebuild and run the API.
3. Confirm logs mention schema ensured (e.g. `Content module schema ensured...`) and `/health/db` returns healthy.

Optional EF tooling is referenced on the host project, but this repo currently uses startup SQL seeders instead of `dotnet ef migrations add`.

## Run Control Center (admin UI)

In another terminal:

```bash
cd ../control-center
npm install
npm run dev
```

Default UI: http://localhost:5173  
Default API base: `http://localhost:5068` (`VITE_API_URL` overrides).

## Solution layout

```
backend/
  TishtryaCMS.slnx
  src/Host/TishtryaCMS.Api/          # host + Program.cs seed orchestration
  src/Modules/Identity/
  src/Modules/ContentModules/
  src/Modules/FileManager/
  src/Modules/EditorTrya/
  src/Modules/Settings/
  src/BuildingBlocks/TishtryaCMS.SharedKernel/
```
