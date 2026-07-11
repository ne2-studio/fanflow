# CLAUDE.md

FanFlow: a smart-link/landing-page platform for musicians and marketers.
Ads → landing page (tracked) → streaming destination (tracked), with bot filtering.
Full product vision: `docs/PRD.md`.
Use cases: `docs/CONTRACT.md`.
HTTP API contract: `docs/API.md`.
**`docs/ARCHITECTURE.md` is the authoritative architecture/conventions doc for this repo — read it before making structural changes**

Three independently deployable services, no shared code between them: `backend/` (ASP.NET Core .NET 10), `frontend/` (React 19 admin app), `site/` (nginx serving published static landing pages from MinIO + proxying tracking beacons to the backend).

## Commands

### Backend (`backend/`)
```bash
dotnet restore
dotnet run --project FanFlow.Api        # http://localhost:5050, migrations run automatically at startup
dotnet test                              # all tests
dotnet test --filter FullyQualifiedName~ReleaseManagerTests   # single test class
```
Needs Postgres + MinIO locally: `docker compose up postgres minio` (from repo root).

### Frontend (`frontend/`)
```bash
npm install
npm run dev      # http://localhost:3000
npm run lint      # tsc --noEmit — this repo's only lint/typecheck step, treat as required
npm run build
```
Copy `.env.example` to `.env` and set `VITE_API_URL`/`VITE_OIDC_*` first.

### Everything together
```bash
docker compose up --build
```
Frontend Dockerfile builds the Vite app itself (multi-stage: node build -> nginx runtime); `VITE_API_URL`/`VITE_OIDC_*` are passed as build args in `docker-compose.yaml`, defaulted to match the local backend/fake-oidc setup.
Backend :5050 · Frontend :3000 · Site :8081 · Postgres :5432 · MinIO console :9001.
