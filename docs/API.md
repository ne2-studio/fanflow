# API Contract — FanFlow

Base URL: `VITE_API_URL` on the frontend (dev default `http://localhost:5050`).

All `/api/*` endpoints require a valid OIDC access token: `Authorization: Bearer <token>`.
Unauthenticated requests get `401 Unauthorized`.

The `/pv/*`, `/out/*`, `/trap/*` tracking routes are public, unauthenticated, and rate-limited
(see the `PublicLimiter` policy) — they're hit directly by the static landing pages.

## Releases (`IReleaseManager`, `IReleaseAnalytics`)

### `POST /api/releases`

Create a release and its landing page (CreateRelease). Only Spotify links are supported in MVP.
Request body is `multipart/form-data` (not JSON), since the cover image is an uploaded file:

| Field            | Type                        | Notes                                              |
|------------------|-----------------------------|-----------------------------------------------------|
| `artistName`     | string                      |                                                     |
| `title`          | string                      |                                                     |
| `headline`       | string                      |                                                     |
| `description`    | string                      |                                                     |
| `coverImage`     | file (binary)               | required; JPEG/PNG/WebP; max 10 MB                 |
| `ctaText`        | string                      |                                                     |
| `facebookPixelId`| string                      | numeric, required                                  |
| `linksJson`      | string (JSON-encoded array) | e.g. `[{"platform":"Spotify","url":"https://open.spotify.com/track/..."}]` |

`facebookPixelId` is required — FanFlow always forwards conversions to Meta CAPI and embeds the
Meta Pixel on the landing page, so every release must carry one (numeric string).

`coverImage` is required and is processed server-side into a 420x420 square (center-cropped, not
stretched) WebP image before storage; there is no separate background image field — the landing
page background is a blurred/expanded rendering of the cover image.

Response `200 OK`: the created release (id, slug, url, authored fields including `coverImageUrl`
and `facebookPixelId`, status, createdAt).

`400 Bad Request` — `{ "error": "invalid_destination" }` (non-Spotify link), `{ "error":
"invalid_facebook_pixel_id" }` (missing or non-numeric), `{ "error": "cover_image_required" }`,
`{ "error": "invalid_cover_image_type" }`, `{ "error": "cover_image_too_large" }`, or
`{ "error": "slug_taken" }`.

### `PUT /api/releases/{id}`

Update any subset of a release's authored fields, including `facebookPixelId` (UpdateRelease).
Also `multipart/form-data`; every field is optional. Re-triggers landing page regeneration and
republish. Omitting `facebookPixelId` keeps the existing value — it can never be cleared, only
replaced with another valid numeric id. Omitting `coverImage` keeps the existing cover image;
including it uploads and overwrites the stored cover image.

`404 Not Found` — `{ "error": "release_not_found" }`.
`400 Bad Request` — `{ "error": "invalid_destination" }`, `{ "error": "invalid_facebook_pixel_id" }`,
`{ "error": "invalid_cover_image_type" }`, `{ "error": "cover_image_too_large" }`, or
`{ "error": "slug_taken" }`.

### `GET /api/releases`

List releases owned by the current tenant/user (ListReleases). Response `200 OK`: array of
`{ id, title, slug, url, status, createdAt }`.

### `GET /api/releases/{id}`

Get the full release record (GetRelease). `404 Not Found` — `{ "error": "release_not_found" }`.

### `DELETE /api/releases/{id}`

Soft-delete a release: unpublishes the landing page, keeps tracking data (DeleteRelease).
Response `204 No Content`. `404 Not Found` — `{ "error": "release_not_found" }`.

### `GET /api/releases/{id}/analytics?filter=all|human`

Traffic analytics for a release (GetReleaseAnalytics). `filter` defaults to `all`; `human`
excludes Bot-classified events from Views/Clicks/breakdowns (Qualified Views always reflects the
Human-classified subset regardless of `filter`).

Response `200 OK`:

```json
{
  "views": 120,
  "qualifiedViews": 98,
  "clicks": 40,
  "ctr": 0.333,
  "trafficSources": [{ "label": "instagram.com", "count": 80 }],
  "countries": [{ "label": "US", "count": 70 }],
  "devices": [{ "label": "Mobile", "count": 90 }]
}
```

`404 Not Found` — `{ "error": "release_not_found" }`.

### `GET /api/releases/{id}/events`

Raw, unaggregated list of every tracked event (PageView, DestinationClick, HoneypotHit) recorded
for a release, newest first — for audit/investigation purposes (ListReleaseEvents).

Response `200 OK`: array of
`{ id, type, ipAddress, userAgent, referrer, destinationId, dwellTimeMs, country, botScore, classification, createdAt, fbp, fbc, metaEventId }`.
`botScore`/`classification` are `null` until async spam analysis has run (except `HoneypotHit`,
which is classified `Bot` synchronously at record time).

`404 Not Found` — `{ "error": "release_not_found" }`.

## Tracking (`ITrafficTracker`) — public, unauthenticated, rate-limited

### `GET /pv/{slug}?fbp=<_fbp>&fbc=<_fbc>&eid=<event-id>`

Records a landing-page view (TrackPageView), unclassified. Response `204 No Content`.
`404 Not Found` if the release doesn't exist.

`fbp`/`fbc` are optional: the landing page's own JS reads the browser's `_fbp`/`_fbc` cookies
(deriving `_fbc` from a `?fbclid=` ad-click param when the cookie isn't set yet) and forwards
them as query params. If omitted, the backend falls back to the `_fbp`/`_fbc` cookies on the
request itself. Either way, the values are carried through to the Meta Conversions API call for
this event once classified Human — they only improve event matching/attribution, they play no
role in bot classification.

`eid` is a client-generated id (one per page load, via `crypto.randomUUID()` with a fallback for
older browsers) shared between the browser's fbevents.js `PageView` call (as `eventID`) and this
beacon. It's persisted as `MetaEventId` and sent as `event_id` on the server-side Meta CAPI
`PageView` call, so Meta dedupes the browser Pixel event against the server-side one instead of
counting both as separate conversions.

### `GET /out/{slug}/{destinationId}?dwell=<ms>&fbp=<_fbp>&fbc=<_fbc>&eid=<event-id>`

Records a destination click (TrackDestinationClick). Response `204 No Content` — it does not
redirect; the landing page performs the redirect itself, client-side (native app deep-link
attempt with a web fallback), firing this request in parallel rather than waiting on it.
Recording happens regardless of spam classification, which runs asynchronously afterward.
`destinationId` is the link's platform (e.g. `spotify`). `dwell` is the time in ms since the
page view, measured client-side. `fbp`/`fbc` are optional, same semantics as `/pv` above — read
fresh at click time rather than reused from the page view, to give the Pixel more time to have
set `_fbp`. `eid` is a separate client-generated id for this click (`SpotifyClick` has no browser
Pixel event, so there's nothing to dedupe against — it's still forwarded as `event_id` on the
Meta CAPI call for traceability/idempotency on Meta's side). `404 Not Found` if the release or
destination doesn't exist.

### `GET /trap/{slug}`

Records a hit on the invisible honeypot link (RecordHoneypotHit); classified Bot immediately,
synchronously. Response `204 No Content`. `404 Not Found` if the release doesn't exist.

## `GET /health`

Public, unauthenticated, rate-limited liveness check. Response `200 OK`: `{ "status": "healthy" }`.
