---
name: verify
description: How to run FanFlow's backend end-to-end locally (real Postgres/MinIO, real HTTP) to verify a change, given the API requires a real external OIDC provider that isn't reachable from a sandbox.
---

# Verifying FanFlow backend changes locally

## Bring up real infra

```bash
docker compose up -d postgres minio
# wait for both healthy:
docker inspect --format='{{.State.Health.Status}}' fanflow-postgres-1 fanflow-minio-1
```

## Run the API against them (not docker — faster iteration)

```bash
cd backend
ASPNETCORE_ENVIRONMENT=Development \
ASPNETCORE_URLS=http://localhost:5050 \
ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=fanflow;Username=devuser;Password=devpass" \
Publishing__PublicHostname="http://localhost:8081" \
Publishing__Minio__Endpoint="http://localhost:9000" \
Publishing__Minio__AccessKey="minioadmin" \
Publishing__Minio__SecretKey="minioadmin" \
Publishing__Minio__Bucket="releases" \
dotnet run --project FanFlow.Api
```

Migrations run automatically at startup — watch the log for the expected `[Migration]` entries.

## Auth is a real external OIDC provider (auth.ne2.studio) — no dev bypass exists

`[Authorize]` + JwtBearer validates against a real hosted authority; there's no way to mint a
valid token in a sandboxed environment. To exercise authenticated endpoints locally:

1. In `Program.cs`, temporarily replace the `AddJwtBearer(...)` registration with an
   always-authenticate scheme (see git history around 2026-07-11 for a worked example: a small
   `AuthenticationHandler<AuthenticationSchemeOptions>` that returns
   `AuthenticateResult.Success(...)` with a fixed `sub` claim, wired via
   `AddScheme<AuthenticationSchemeOptions, YourHandler>("TempSmokeTest", _ => {})` and
   `DefaultAuthenticateScheme`/`DefaultChallengeScheme` set to that name).
2. Run the smoke test with `curl` (any `Authorization: Bearer <anything>` header works, since the
   handler ignores it).
3. **Revert `Program.cs` and delete the temp handler file before finishing** — `git checkout --
   backend/FanFlow.Api/Program.cs` — never leave the auth bypass in the tree.

## Exercising the releases API (multipart, since cover image is a file upload)

```bash
curl -X POST http://localhost:5050/api/releases \
  -H "Authorization: Bearer fake" \
  -F "artistName=The Artist" -F "title=Run To Me" -F "headline=h" -F "description=d" \
  -F "coverImage=@/path/to/image.jpg;type=image/jpeg" \
  -F "ctaText=Listen Now" -F "facebookPixelId=123456789012345" \
  -F 'linksJson=[{"platform":"Spotify","url":"https://open.spotify.com/track/123"}]'
```

Fetch stored objects directly from MinIO (bypasses the `site` nginx container, which compose
doesn't start by default): `curl http://localhost:9000/releases/<key>` — e.g.
`{releaseId}/cover.webp` for uploaded covers, `{slug}.html` for the generated landing page.

To inspect an image's actual pixels/dimensions (no ImageMagick/PIL available in this sandbox),
write a throwaway `dotnet run` script referencing `SixLabors.ImageSharp` (already a project
dependency) — `Image.Load<Rgba32>(path)` then index `image[x,y]`.

## Gotcha found via this flow

`multipart/form-data` field values are always strings — a `linksJson` field with lowercase JSON
keys (`{"platform":...}`) will silently fail to bind into PascalCase C# properties unless
`JsonSerializerOptions.PropertyNameCaseInsensitive = true` is passed to
`JsonSerializer.Deserialize`. Symptom looked like a validation bug (`invalid_destination`) rather
than a deserialization one — worth checking first if a multipart form field with JSON content
mysteriously fails validation.

## Cleanup

```bash
docker compose down
```
