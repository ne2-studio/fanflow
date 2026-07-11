---
name: verify
description: How to run FanFlow's backend end-to-end locally (real Postgres/MinIO, real HTTP, real OIDC via the fake-oidc container) to verify a change — no external OIDC provider or auth bypass needed.
---

# Verifying FanFlow backend changes locally

## Bring up real infra

```bash
docker compose up -d postgres minio fake-oidc
# wait for postgres/minio healthy:
docker inspect --format='{{.State.Health.Status}}' fanflow-postgres-1 fanflow-minio-1
```

`fake-oidc` (added in the `feat: use fake-oidc in local environment` change) replaces the real
hosted provider (`auth.ne2.studio`) for local dev — it's a real OIDC server (real PKCE
authorization-code flow, real RS256-signed JWTs, real `/.well-known/openid-configuration` +
JWKS), just pre-seeded with fixed clients/users so there's no external dependency. **There is no
dev auth bypass in `Program.cs` anymore — don't add one.** Get a real token instead (below).

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
Auth__Authority="http://localhost:5000" \
Auth__Audience="frontend" \
dotnet run --project FanFlow.Api
```

Migrations run automatically at startup — watch the log for the expected `[Migration]` entries.

Note: `Auth__Authority=http://localhost:5000` (not `http://fake-oidc:5000`) works fine even though
the token's `iss` claim is `http://fake-oidc:5000` — `JwtBearer` fetches
`{Authority}/.well-known/openid-configuration` and validates against the `issuer` value *in that
document*, not the URL used to reach it, so no `/etc/hosts` entry is needed for this curl-driven
flow (that entry is only needed for the full docker-compose stack, where the browser and the
`backend` container both resolve the hostname `fake-oidc` independently — see the comment in
`docker-compose.yaml`). If you instead run everything via `docker compose up --build`, the backend
container already resolves `fake-oidc` over the compose network and needs no extra setup.

## Auth: mint a real token from fake-oidc

`fake-oidc` only supports the authorization-code + PKCE grant (`grant_types_supported` in its
discovery doc), and it auto-approves every `/authorize` request as whichever user
`OIDC_DEFAULT_USER` in `docker-compose.yaml` names (`admin`, with `roles: ["admin"]`) — there's no
login screen and no per-request way to pick a different seeded user (query params like `user=` or
`userKey=` are silently ignored; the only way to test as a different identity is to run a second
container with a different `OIDC_DEFAULT_USER`). That's sufficient today since the backend has no
role-based `[Authorize(Roles=...)]` checks yet, only plain `[Authorize]`.

Get a token with a plain PKCE dance against the container already started above:

```bash
VERIFIER=$(python3 -c "import secrets; print(secrets.token_urlsafe(64)[:64])")
CHALLENGE=$(python3 -c "
import base64, hashlib, sys
v = '$VERIFIER'.encode()
print(base64.urlsafe_b64encode(hashlib.sha256(v).digest()).decode().rstrip('='))
")

CODE=$(curl -s -G "http://localhost:5000/authorize" \
  --data-urlencode "response_type=code" \
  --data-urlencode "client_id=frontend" \
  --data-urlencode "redirect_uri=http://localhost:3000/callback" \
  --data-urlencode "scope=openid profile email" \
  --data-urlencode "state=verify" \
  --data-urlencode "code_challenge=${CHALLENGE}" \
  --data-urlencode "code_challenge_method=S256" \
  -D - -o /dev/null | sed -n 's/.*[Ll]ocation: .*code=\([^&]*\)&.*/\1/p' | tr -d '\r')

TOKEN=$(curl -s -X POST http://localhost:5000/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=authorization_code&code=${CODE}&redirect_uri=http://localhost:3000/callback&client_id=frontend&code_verifier=${VERIFIER}" \
  | python3 -c "import json,sys; print(json.load(sys.stdin)['access_token'])")

echo "$TOKEN"   # real RS256 JWT, sub=admin-user, aud=frontend, exp in 1h (OIDC_TOKEN_LIFETIME_SECONDS)
```

`$TOKEN` is a real signed JWT the backend's `JwtBearer` middleware genuinely validates (signature,
issuer, audience, expiry) — confirm the round trip with:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5050/api/releases                                    # 401, no token
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:5050/api/releases -H "Authorization: Bearer $TOKEN"   # 200
```

## Exercising the releases API (multipart, since cover image is a file upload)

```bash
curl -X POST http://localhost:5050/api/releases \
  -H "Authorization: Bearer $TOKEN" \
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
