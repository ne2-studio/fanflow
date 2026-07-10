# FanFlow — Backend

ASP.NET Core (.NET 10) API for FanFlow, scaffolded to match the conventions in
[`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md). Ports & adapters layout, PostgreSQL via Dapper +
FluentMigrator, JWT bearer auth, and Serilog.

It owns the **release** domain — creating and publishing smart-link landing pages, tracking traffic
against them, and scoring/classifying that traffic for bots — via a few key pieces:

- `Result<T>` for expected failures instead of exceptions/try-catch-500
- an output port per external effect (`IClock`, `IIdGenerator`, `IReleaseRepository`,
  `IEventRepository`, `IReleasePublisher`, `IConversionsApiClient`, `ISlugGenerator`)
- static landing page publishing straight to a MinIO bucket via the S3 API — the backend never
  writes to a local publish folder (see `StaticSiteReleasePublisher`)
- a background worker (`SpamClassificationWorker`) that polls tracked events and scores them
  (`BotScoring`) into `Human`/`Bot`, decoupling classification from the hot tracking path
- the **Null Object pattern** for feature-flagged behavior (`IConversionsApiClient` swaps between
  `MetaConversionsApiClient` and `NullConversionsApiClient` based on
  `Features:MetaConversions:Enabled`, decided once in `ServiceRegistration`)
- public, unauthenticated, rate-limited tracking endpoints (`/pv`, `/out`, `/trap`, `/health`)
  alongside JWT-protected release management endpoints
- two-tier testing: hand-written fakes for core/application logic, NSubstitute mocks for the infra
  publisher

## Architecture

```
FanFlow        — domain core (use cases, ports)
FanFlow.Infra  — adapters (PostgreSQL, MinIO publishing, Meta Conversions API, bot scoring)
FanFlow.Api    — HTTP entry point (controllers, background worker, JWT validation)
```

## API endpoints

Release management endpoints require a valid JWT (`Authorization: Bearer <token>`). Tracking
endpoints (`/pv`, `/out`, `/trap`) are public and rate-limited. Full request/response shapes are
documented in [`docs/API.md`](../docs/API.md).

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `POST` | `/api/releases` | Required | Create a release and its landing page |
| `PUT` | `/api/releases/{id}` | Required | Update a release, republish its landing page |
| `GET` | `/api/releases` | Required | List releases for the current tenant |
| `GET` | `/api/releases/{id}` | Required | Get a release |
| `DELETE` | `/api/releases/{id}` | Required | Soft-delete a release, unpublish its landing page |
| `GET` | `/api/releases/{id}/analytics` | Required | Traffic analytics for a release |
| `GET` | `/pv/{slug}` | Public (rate-limited) | Record a landing page view |
| `GET` | `/out/{slug}/{destinationId}` | Public (rate-limited) | Record a destination click, redirect |
| `GET` | `/trap/{slug}` | Public (rate-limited) | Record a honeypot hit (immediate bot classification) |
| `GET` | `/health` | Public (rate-limited) | Liveness check |

## Getting started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/en-us/download)
- [Docker](https://www.docker.com/products/docker-desktop)

### Run PostgreSQL and MinIO locally

Easiest via the root `docker-compose.yaml` (`docker compose up postgres minio`), or standalone:

```bash
docker run --name pg-fanflow \
  -e POSTGRES_USER=devuser \
  -e POSTGRES_PASSWORD=devpass \
  -e POSTGRES_DB=fanflow \
  -p 5432:5432 \
  -d postgres:16

docker run --name minio-fanflow \
  -e MINIO_ROOT_USER=minioadmin \
  -e MINIO_ROOT_PASSWORD=minioadmin \
  -p 9000:9000 -p 9001:9001 \
  -d minio/minio server /data --console-address ":9001"
```

To stop and remove the containers:

```bash
docker stop pg-fanflow minio-fanflow && docker rm pg-fanflow minio-fanflow
```

### Run the API

```bash
dotnet run --project FanFlow.Api
```

The API will be available at http://localhost:5050. Migrations run automatically at startup.

### Run tests

```bash
dotnet test
```

## Docker

Build the image:

```bash
docker build . -t fanflow-api
```

Run the container:

```bash
docker run --name fanflow-api \
  -e "ConnectionStrings__DefaultConnection=Host=host.docker.internal;Port=5432;Database=fanflow;Username=devuser;Password=devpass" \
  -p 5050:8080 \
  fanflow-api
```

## License

MIT © [Exeal](https://www.exeal.com)
