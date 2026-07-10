# API Contract — FanFlow

Base URL: `VITE_API_URL` on the frontend (dev default `http://localhost:5050`).

All `/api/*` endpoints require a valid OIDC access token: `Authorization: Bearer <token>`.
Unauthenticated requests get `401 Unauthorized`.

The `/pv/*`, `/out/*`, `/trap/*` tracking routes are public, unauthenticated, and rate-limited
(see the `PublicLimiter` policy) — they're hit directly by the static landing pages.

## Releases (`IReleaseManager`, `IReleaseAnalytics`)

### `POST /api/releases`

Create a release and its landing page (CreateRelease). Only Spotify links are supported in MVP.

Request body:

```json
{
  "title": "Run To Me",
  "headline": "New single out now",
  "description": "A great song.",
  "coverImageUrl": "https://.../cover.jpg",
  "backgroundImageUrl": "https://.../bg.jpg",
  "ctaText": "Listen Now",
  "links": [{ "platform": "Spotify", "url": "https://open.spotify.com/track/..." }]
}
```

Response `200 OK`: the created release (id, slug, url, authored fields, status, createdAt).

`400 Bad Request` — `{ "error": "invalid_destination" }` (non-Spotify link) or `{ "error": "slug_taken" }`.

### `PUT /api/releases/{id}`

Update any subset of a release's authored fields (UpdateRelease). Re-triggers landing page
regeneration and republish.

`404 Not Found` — `{ "error": "release_not_found" }`.
`400 Bad Request` — `{ "error": "invalid_destination" }` or `{ "error": "slug_taken" }`.

### `GET /api/releases`

List releases owned by the current tenant/user (ListReleases). Response `200 OK`: array of
`{ id, title, slug, status, createdAt }`.

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

## Tracking (`ITrafficTracker`) — public, unauthenticated, rate-limited

### `GET /pv/{slug}`

Records a landing-page view (TrackPageView), unclassified. Response `204 No Content`.
`404 Not Found` if the release doesn't exist.

### `GET /out/{slug}/{destinationId}?dwell=<ms>`

Records a destination click (TrackDestinationClick) and redirects to the destination URL
(`302 Found`) regardless of spam classification, which runs asynchronously afterward.
`destinationId` is the link's platform (e.g. `spotify`). `dwell` is the time in ms since the
page view, measured client-side. `404 Not Found` if the release or destination doesn't exist.

### `GET /trap/{slug}`

Records a hit on the invisible honeypot link (RecordHoneypotHit); classified Bot immediately,
synchronously. Response `204 No Content`. `404 Not Found` if the release doesn't exist.

## `GET /health`

Public, unauthenticated, rate-limited liveness check. Response `200 OK`: `{ "status": "healthy" }`.
