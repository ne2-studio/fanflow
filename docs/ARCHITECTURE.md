# {ProjectName} Architecture

This document is the architecture standard for new projects: project structure, layering, and conventions for both services. It describes what *should be built*, distilled from patterns proven across prior projects; when starting or extending a project, follow these patterns unless there's a specific, documented reason to deviate.

## System overview

Monorepo with two independently deployable services, no shared code between them:

| Directory | Stack |
|---|---|
| `frontend/` | React 19, TypeScript, Vite, Tailwind CSS v4, Zustand, react-oidc-context |
| `backend/` | ASP.NET Core (.NET 10), PostgreSQL, Serilog |

Auth is OIDC/JWT Bearer end-to-end: the frontend authenticates against an external OIDC provider (e.g. Zitadel, Auth0, Okta), attaches the access token to every API call, and the backend validates it via `JwtBearer` middleware. There is no session state on the backend — every request is authenticated independently and scoped to the caller's identity.

---

## Backend (`backend/{ProjectName}.Api`)

### Architecture: ports & adapters

The backend is split across three projects, with dependencies pointing inward toward the core:

```
{ProjectName}.Api  ──┐
                      ├──→  {ProjectName}  (core: Application + Ports)
{ProjectName}.Infra ──┘
```

- **`{ProjectName}` (core)** — the domain/use-case project. Contains:
  - `Application/` — one class per use case (e.g. a "handler" implementing an input port), holding all business logic.
  - `Ports/Input/` — use-case interfaces and their request/response DTOs (plain `record` types, no behavior, no framework attributes). This is the contract the API layer calls into.
  - `Ports/Output/` — interfaces for everything the core needs from the outside world: repositories, clock, id/slug/token generation, external services, configuration values.
  - This project references only minimal, framework-agnostic libraries (a `Result` type library such as CSharpFunctionalExtensions, logging abstractions) — **no ASP.NET Core, no DB driver, no ORM.** It must be fully unit-testable in isolation, with no infrastructure dependencies to fake.
- **`{ProjectName}.Infra`** — implements every output port (repositories, external service clients, clock, generators, config readers) and exposes a single composition-root extension method (e.g. `ServiceRegistration.AddInfrastructure()`) called once from `Program.cs`. This is the only place infra wiring happens.
- **`{ProjectName}.Api`** — thin ASP.NET Core host: controllers, `Program.cs`, auth/rate-limiting/API-docs setup. Controllers depend only on `Ports/Input` interfaces, never on `Infra` or `Application` concrete types directly.

### Controller conventions

- Thin: extract the caller's identity from the JWT claim (if applicable), delegate to a use-case method, map the result to an HTTP response.
- Use `Result<T>`/`Result` (e.g. CSharpFunctionalExtensions) for expected failure paths instead of throwing. Controllers branch on `result.IsSuccess` and map explicitly to the right status code (`BadRequest` for validation, `NotFound` for missing resources, `Ok`/`NoContent` for success) — no blanket `try/catch` returning 500 for everything, and no swallowed stack traces.
- One controller per bounded resource area, routed under a shared prefix, grouped by resource.
- Every endpoint is `[Authorize]` by default except explicitly public endpoints (health checks, public redirects, etc.).

### Core conventions

- **Every external effect sits behind an interface the core owns**: clock, id/slug generation, configuration reads, external services. No inline `DateTime.UtcNow`, `new Random()`, or raw `IConfiguration` reads inside use-case code — this is what makes fake-based unit testing possible without a mocking framework for core logic.
- **Decorator pattern for cross-cutting infra concerns, composed in the DI root**, rather than baked into a single adapter class — e.g. a caching decorator wrapping a repository, or a buffering/batching decorator wrapping a tracking client that flushes on a timer instead of writing synchronously per request. Decorators are wired by wrapping the concrete instance inside the infra composition-root method, so the decorated chain is visible in one place.
- **Null Object pattern for feature-flagged behavior.** A config flag switches, at the composition root, between a real adapter and a no-op implementation — use-case code never checks the flag itself, it just depends on the interface.
- **DI lifetimes are chosen by statefulness, not a blanket rule.** Default to `Scoped`. Register `Singleton` only for services that must hold state across requests (e.g. an in-memory buffer with a flush timer), and treat that as a deliberate, documented exception.

### Data access

Choose the approach per project based on domain complexity:

- **Dapper + FluentMigrator** — for simple/CRUD-shaped domains. Raw parameterized SQL per repository method (no LINQ-to-SQL translation layer); migrations are plain C# `Migration` classes independent of any ORM.
- **Entity Framework Core** — for complex domains with many relationships or business rules where LINQ and change tracking pay off. Table/column/index mapping via Fluent API in `OnModelCreating` (avoid data annotations on entities); EF Core migrations.
- **Regardless of choice, the project must provide a mechanism to run migrations automatically** — applied at startup (`dbContext.Database.Migrate()` for EF Core, `IMigrationRunner.MigrateUp()` for FluentMigrator) or via an explicit, documented deploy step. Never ship a project where schema changes require manual, undocumented DBA intervention.
- **DB-level constraints (uniqueness, foreign keys) back up application-level checks as defense-in-depth** against races (e.g. catch the database's unique-violation error and translate it to a domain-meaningful error) — they don't replace an application-level check-then-act, they cover its gap.

### Other conventions

- **IDs**: prefer `Guid` primary keys by default; use `text` primary keys with human-readable codes only where the domain calls for it (e.g. well-known system rows). DTOs always expose ids as `string`; parse to the underlying type at the adapter boundary.
- **Multi-tenancy / per-user data isolation** (if the domain scopes data to the caller): every tenant/user-scoped table has an explicit user id column; every repository method that reads or writes such a table takes the user id explicitly and filters/stores by it — don't rely on a global query filter or row-level security unless that's a deliberate, documented choice, so scoping stays visible at each call site.
  - The logged-in user id is resolved behind a secondary (output) port, `ICurrentUserProvider` (`Ports/Output`), so `Application/` use cases depend only on the interface and stay unit-testable with a fake — they never read `HttpContext`/claims directly.
  - Its adapter lives in `{ProjectName}.Infra` (kept consistent with "Infra implements every output port") and reads the OIDC `sub` claim off the current request's validated JWT via `IHttpContextAccessor`; this requires `Infra` to add a `<FrameworkReference Include="Microsoft.AspNetCore.App" />` since it's otherwise a plain class library.
  - The user id is treated as an opaque `string`, not a `Guid` — OIDC providers (e.g. Zitadel) don't guarantee subject ids are GUID-shaped.
  - Use cases call `ICurrentUserProvider.GetUserId()` themselves and pass the result into repository calls; input-port method signatures (e.g. `ITaskManager.ListAsync`) stay unchanged — the caller's identity is never a parameter the API layer has to thread through.
- **Timestamps**: every entity has `CreatedAt`/`UpdatedAt` (`timestamp with time zone`), defaulted at the DB level to UTC. Dates from the client are parsed via a shared helper that assumes/adjusts to UTC.
- **Money** (if the domain handles it): `decimal` with an explicit, consistent precision everywhere it's stored.
- **Logging**: Serilog, configured in two layers — a bootstrap console logger created before `WebApplication.CreateBuilder`, then reconfigured via `UseSerilog` reading from config. Add environment-specific sinks (e.g. Seq in production); request logging via `UseSerilogRequestLogging()`.
- **API docs**: Swagger/Swashbuckle, enabled only in `Development`.
- **CORS**: an explicit policy; `AllowAnyOrigin` is acceptable when auth is enforced via bearer token rather than cookies/origin.
- **Rate limiting**: public, unauthenticated endpoints must be protected by a rate limiter (e.g. ASP.NET Core's built-in fixed-window limiter), keyed by client IP, with limits from config and structured logging on rejection.
- **Privacy**: don't persist raw PII (e.g. IP addresses) for data that doesn't need it — hash/fingerprint it instead (e.g. for unique-visitor analytics).
- **Config**: connection strings and auth `Authority`/`Audience` live in `appsettings.json` (dev defaults committed) and are overridden per environment (`appsettings.Production.json`, environment variables in the container). Never commit production secrets.

### Testing

Two-tier strategy, matched to the layer:

- **Core/application logic** is tested against hand-written fakes for each output port (in-memory repository, static clock, static generators, etc.) — no mocking framework, fast, behavior-focused.
- **Infra-layer adapters/decorators** are tested with a mocking framework (e.g. NSubstitute) against the interface they decorate or call — mocking is reserved for verifying interaction with a wrapped collaborator, not for core business logic.
- Every backend project ships with test project(s) from day one; don't defer test infrastructure to "later."

---

## Frontend (`frontend/src`)

### Layers

```
components/  →  store/use{Domain}Store (Zustand)  →  api.ts (fetch client)  →  types.ts
```

- **`types.ts`** — one class per domain entity. Classes (not plain interfaces) so that raw JSON from the API can be re-hydrated into typed instances via `new Entity(data)` — constructors accept a plain data object and assign fields 1:1. Nested entities are recursively wrapped in their own class if they aren't already instances.
- **`api.ts`** — a single `api` object, namespaced per resource (`api.<resource>`), each exposing `getAll/create/update/delete`. Conventions:
  - Plain `fetch` wrapped in a shared `handleResponse` that throws on non-OK responses.
  - Auth token is module-level state (`_accessToken`) set via `setAccessToken()` from `App.tsx` whenever the OIDC user changes — not read from a context/hook inside `api.ts`.
  - Every response is mapped back into the corresponding `types.ts` class before being returned, so nothing above this layer touches raw JSON.
  - Base URL comes from `VITE_API_URL` (build-time env var via Vite's `define`).
- **`store/use{Domain}Store.ts`** — single global Zustand store holding all domain state plus `isLoading`/`error`. No slices/multiple stores unless the domain genuinely has independent state machines. Conventions:
  - One `fetchData()` that loads all collections in parallel via `Promise.all`, called once on auth.
  - Every mutating action (`addX`/`updateX`/`deleteX`) calls the `api` client first, then updates local state from the server response/merged patch only after the await resolves — not optimistically before.
  - Components read state and call actions directly via the store hook — no selectors, no memoized selector hooks, unless a specific performance problem justifies it.
- **`components/`** — flat directory, one file per screen/feature plus modals. No further subdivision (no per-feature subfolders, no shared `ui/` primitives directory) unless the component count genuinely warrants it — small inline components used only by one screen are defined locally inside that screen rather than extracted.
- **`App.tsx`** — owns routing (`react-router-dom`, `BrowserRouter`), the auth gate, and any state that's derived across multiple domain collections (computed with `useMemo`). Route components receive derived data as props rather than recomputing it themselves.
- **`main.tsx`** — app entry point; wraps `<App />` in `<AuthProvider>` (react-oidc-context) with OIDC config inlined here (not in a separate config file).

### Conventions

- **Auth**: `react-oidc-context`. `App.tsx` redirects to `signinRedirect()` whenever the user isn't authenticated and isn't on `/callback`; the access token is pushed into `api.ts` via `setAccessToken` on every auth state change.
- **Routing**: `react-router-dom`, all routes declared flat in `App.tsx`'s `<Routes>`, wrapped in a single `<Layout>` that renders shared chrome. Unknown routes redirect to `/`.
- **State management**: Zustand only; no React Context for domain data, no server-state library (React Query, SWR, etc.) — the store itself is the cache, refreshed via explicit `fetchData()`/mutation calls.
- **Styling**: Tailwind CSS v4, configured via `@theme` in `index.css` (no `tailwind.config.js` — v4-style CSS-first config). All colors are theme tokens — never raw hex/Tailwind palette classes in components. Shared visual primitives are CSS utility classes in `index.css` rather than React components.
- **Formatting**: locale-appropriate formatting (currency, percentages, dates) done inline with `Intl.NumberFormat`/`Intl.DateTimeFormat` per component, using the target market's locale — no shared formatting util unless the same format is needed in 3+ places.
- **Component structure**: function components, props typed via a local `interface XProps`, no default exports except screen-level components matching their route.
- **TypeScript**: enable `strict` mode in `tsconfig.json` for new projects; type-checking (`tsc --noEmit`) should be enforced, not best-effort. `@/*` path alias maps to the frontend root.
- **Config**: environment-driven via Vite `loadEnv`/`define`, primarily `VITE_API_URL`. OIDC client config is hardcoded in `main.tsx` (not environment-driven) unless multiple environments require different OIDC clients.

---

## Cross-cutting / deployment

- **CI/CD**: three independent GitHub Actions workflows (`backend-deploy.yml`, `frontend-deploy.yml`, `site-deploy.yml`), path-filtered so each only runs when its own directory changes. All: build → (backend also runs `dotnet test`) → build a Docker image → push to a container registry (e.g. GHCR) → trigger a deploy webhook.
- **Containers**: backend is a multi-stage .NET SDK→ASP.NET runtime image exposing port 8080. Frontend is built by Vite in CI, then the static `dist/` is copied into an `nginx:alpine` image (port 80) with a minimal SPA-fallback `nginx.conf` (`try_files $uri $uri/ /index.html`). A third, independently deployable `site/` container is a thin `nginx:alpine` (config templated from env vars via nginx's built-in `envsubst` templating, no build step) that proxies public release-page requests to a MinIO bucket and the tracking routes (`/pv`, `/out`, `/trap`) to the backend — this is the internet-facing container the backend's `Publishing:PublicHostname` config points at when building release URLs. The backend never writes published pages to a local disk/volume; it publishes them as objects directly to MinIO over the S3 API.
- **No shared package/types** between frontend and backend — DTO shapes are duplicated by hand (backend DTOs vs. `types.ts` classes) and must be kept in sync manually, unless a project specifically justifies a shared-types package.
