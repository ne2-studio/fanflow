# FanFlow — Frontend

Admin app for authoring and monitoring FanFlow releases, following the conventions in
[`docs/ARCHITECTURE.md`](../docs/ARCHITECTURE.md). Lets a musician or marketer create a release
landing page (title, artwork, CTA, destination links), publish it, and see its traffic analytics
(views, qualified views, clicks, CTR, sources, countries, devices). The release domain
(`types.ts` → `api.ts` → `store/useReleaseStore.ts` → `components/Releases.tsx` /
`ReleaseAnalyticsView.tsx`) talks to the backend's release management and analytics endpoints; see
[`docs/API.md`](../docs/API.md) for the contract.

## Run locally

**Prerequisites:** Node.js

1. Copy `.env.example` to `.env` and set `VITE_API_URL`.
2. Set the real OIDC `authority`/`client_id` in `src/main.tsx`.
3. Install dependencies: `npm install`
4. Run the app: `npm run dev`
