# FanFlow

FanFlow is a smart-link and landing platform for independent musicians, bands, labels, and music
marketers running paid campaigns. It sits between advertising platforms (Meta Ads, TikTok Ads,
Google Ads, Instagram, YouTube) and streaming destinations (Spotify, Apple Music, YouTube Music,
Bandcamp), solving one problem: driving real fans from ads to music platforms while filtering bots
and measuring traffic quality. See [`docs/PRD.md`](docs/PRD.md) for the full product vision.

## What's here

| Directory | Contents |
|-----------|----------|
| `docs/PRD.md` | Product requirements: vision, problem, target customer, MVP scope |
| `docs/CONTRACT.md` | Application use cases, each as a mini-specification |
| `docs/API.md` | The HTTP API contract for releases, analytics, and tracking |
| `docs/ARCHITECTURE.md` | The architecture standard both services follow |
| `backend/` | ASP.NET Core (.NET 10) ports & adapters backend — releases, tracking, bot scoring, analytics — see [`backend/README.md`](backend/README.md) |
| `frontend/` | React 19 + Vite + Zustand admin app for authoring releases — see [`frontend/README.md`](frontend/README.md) |
| `site/` | Nginx container serving published static landing pages from MinIO and proxying tracking beacons |
| `.github/workflows/` | Path-filtered CI/CD for each service (build → test → Docker image → registry → deploy webhook) |

## Architecture

Independently deployable services:

| Directory | Stack |
|-----------|-------|
| `frontend/` | React 19, TypeScript, Vite, Tailwind CSS v4, Zustand, react-oidc-context |
| `backend/` | ASP.NET Core (.NET 10), PostgreSQL, MinIO (S3), Serilog |
| `site/` | Nginx, serving pregenerated static landing pages straight from MinIO |

Landing pages are static HTML, generated from the database and published directly to a MinIO
bucket — no server-side rendering on the request path. Auth for the admin app is OIDC/JWT Bearer
end-to-end: the frontend authenticates against an external OIDC provider and attaches the access
token to every API call; the backend validates it via `JwtBearer` middleware. Full conventions and
rationale are in [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md).

## Getting started

### Prerequisites

- Node.js 22+
- .NET 10 SDK
- Docker (for PostgreSQL/MinIO locally, and for building images)

### Backend

```bash
cd backend
dotnet restore
dotnet run --project FanFlow.Api
```

See [`backend/README.md`](backend/README.md) for running PostgreSQL/MinIO locally, environment
configuration, tests, and Docker.

### Frontend

```bash
cd frontend
cp .env.example .env
# Set VITE_API_URL to the backend URL, and the OIDC authority/client_id in src/main.tsx

npm install
npm run dev
```

See [`frontend/README.md`](frontend/README.md) for details.

### Everything via Docker Compose

`docker-compose.yaml` at the repo root spins up Postgres, MinIO, the backend, the frontend, the
static site container, and a `fake-oidc` auth provider together, each built from its own
`Dockerfile` (fake-oidc is pulled prebuilt). The frontend image only copies a pre-built `dist/`
(it doesn't run `npm run build` itself), so build the frontend once first:

```bash
cd frontend && cp .env.example .env && npm install && npm run build && cd ..
docker compose up --build
```

`fake-oidc` replaces the real OIDC provider for local dev only — no external credentials needed,
you're logged in automatically as an "admin" user. Add `127.0.0.1 fake-oidc` to your hosts file
first (see the comment at the top of `docker-compose.yaml` for why).

Backend: http://localhost:5050 · Frontend: http://localhost:3000 · Site: http://localhost:8081 ·
Postgres: localhost:5432 · MinIO console: http://localhost:9001 · fake-oidc: http://localhost:5000.

## Deployment

All services are containerized and deploy independently. CI/CD runs on push to `main`
(path-filtered per service), builds a Docker image, pushes it to GitHub Container Registry, and
triggers a Coolify deploy webhook — see `.github/workflows/backend-deploy.yml`,
`frontend-deploy.yml`, and `site-deploy.yml`.

## License

MIT © [Exeal](https://www.exeal.com)
