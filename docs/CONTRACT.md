# CONTRACT

List of application use cases. Each one is a mini-specification of observable
behavior — not a technical design.

## 1) CreateRelease

- **Input**: title, headline, description, cover image, background image, CTA text, Facebook Pixel ID (required), destination links (Spotify only, MVP)
- **Output (OK)**: release id, generated slug/URL (e.g. `fanflow.app/<slug>`)
- **Errors**: `invalid_destination` — non-Spotify link (not idempotent); `invalid_facebook_pixel_id` — missing or non-numeric Pixel ID (not idempotent); `slug_taken` (not idempotent); 
- **Rules**: only Spotify is a valid destination in MVP; a Facebook Pixel ID is mandatory on every release — FanFlow is attribution-first, so there is no way to publish a release without one; creating a release triggers landing page generation and publish (Database → Static Generator → HTML → MinIO → Nginx); one landing page per release, auto-managed — there is no separate landing-page entity or publish action

## 2) UpdateRelease

- **Input**: release id, any subset of the authored fields (title, headline, description, cover image, background image, CTA text, Facebook Pixel ID, links)
- **Output (OK)**: updated release representation
- **Errors**: `release_not_found` (idempotent); `invalid_destination` (not idempotent); `invalid_facebook_pixel_id` — non-numeric Pixel ID (not idempotent; omitting the field keeps the existing value, since it can never be cleared); slug/URL can change after publish, previously shared links are ignored, but a warning is shown that they will break
- **Rules**: every update re-triggers landing page regeneration and republish

## 3) ListReleases

- **Input**: tenant id
- **Output (OK)**: list of releases with title, slug, URL, status, created date
- **Errors**: none expected; an empty list is a valid result (idempotent)
- **Rules**: release administration is scoped by tenant/user

## 4) GetRelease

- **Input**: release id
- **Output (OK)**: full release record (authored fields + slug/URL)
- **Errors**: `release_not_found` (idempotent)
- **Rules**: none beyond existence check

## 5) DeleteRelease

- **Input**: release id
- **Output (OK)**: confirmation of deletion
- **Errors**: `release_not_found` (idempotent);
- **Rules**: delete unpublishes the landing page, but keeps all tracking data (i.e. soft delete)

## 6) TrackPageView

- **Input**: release slug (from `/pv/*` request), IP address, user agent, referrer, timestamp
- **Output (OK)**: view recorded, unclassified; no content returned to the visitor
- **Errors**: `release_not_found` (idempotent); TODO: confirm with business — duplicate-view suppression window
- **Rules**: purely records the event with the data needed for later spam analysis; no bot scoring happens synchronously — classification is done by AnalyzePageView

## 7) AnalyzePageView (async)

- **Input**: a recorded, unclassified PageView event (IP, user agent, referrer, timestamp, request-frequency history)
- **Output (OK)**: event updated with bot score (0-100) and classification (Human | Bot)
- **Errors**: TODO: confirm with business — behavior if analysis fails or times out (event left unclassified?)
- **Rules**: score derived from known-crawler user agent lists, request frequency/rate limits, and Cloudflare signals if available; if classified Human, triggers ForwardConversionToMeta

## 8) TrackDestinationClick

- **Input**: release slug, destination identifier (from `/out/*` request), IP address, user agent, referrer, timestamp, elapsed time since the page view (dwell time)
- **Output (OK)**: HTTP redirect to the destination URL (e.g. Spotify)
- **Errors**: `release_not_found` (idempotent); `destination_not_found` (idempotent)
- **Rules**: event is recorded, unclassified, before the redirect is issued; the redirect always happens regardless of classification, since classification runs asynchronously afterward — no bot scoring is done synchronously

## 9) AnalyzeDestinationClick (async)

- **Input**: a recorded, unclassified click event (IP, user agent, dwell time, request-frequency history)
- **Output (OK)**: event updated with bot score (0-100) and classification (Human | Bot)
- **Errors**: TODO: confirm with business — behavior if analysis fails or times out (event left unclassified?)
- **Rules**: score derived from dwell time, known-crawler user agent lists, request frequency/rate limits, and Cloudflare signals if available; if classified Human, triggers ForwardConversionToMeta

## 10) RecordHoneypotHit

- **Input**: release slug (from `/trap/*` request), request metadata
- **Output (OK)**: hit recorded; no visible content returned (invisible link)
- **Errors**: `release_not_found` (idempotent)
- **Rules**: any hit on a honeypot link immediately classifies the visitor/event as Bot, overriding other signals — no async analysis needed, this is a synchronous, definitive signal

## 11) ForwardConversionToMeta

- **Input**: a PageView or SpotifyClick event classified Human by AnalyzePageView / AnalyzeDestinationClick
- **Output (OK)**: event forwarded to Meta Conversions API, server-side
- **Errors**: `meta_api_error` (TODO: confirm with business — retry policy and idempotency/dedupe-by-event-id semantics)
- **Rules**: only Human-classified events are forwarded; forwarding never happens before async classification completes; the Meta Pixel ID used is always the one configured on the event's release (every release has one — see CreateRelease), not a global/app-wide value

## 12) GetReleaseAnalytics

- **Input**: release id, traffic filter (all traffic | human only), TODO: confirm with business — date range
- **Output (OK)**: views, qualified views, clicks, CTR, traffic sources, countries, devices
- **Errors**: `release_not_found` (idempotent)
- **Rules**: "qualified" figures exclude Bot-classified events; filter toggles between all traffic and human-only traffic
