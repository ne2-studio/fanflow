# FanFlow — Product Requirements Document (PRD)

## 1. Vision

FanFlow is a smart-link and landing platform designed for independent musicians, bands, labels, and music marketers who run paid campaigns and need reliable attribution between advertising platforms and music streaming destinations.

Unlike generic smart-link tools, FanFlow focuses on one problem:

> Drive real fans from advertising platforms to music platforms while filtering bots and measuring traffic quality.

The product sits between Meta Ads, TikTok Ads, Google Ads, Instagram, YouTube and destinations such as Spotify, Apple Music, YouTube Music and Bandcamp.

---

# 2. Problem

Today musicians face several issues:

### Spotify attribution is broken

Spotify does not expose conversion tracking APIs.

Advertisers can see:

```txt
Meta Clicks
Landing Visits
```

But not:

```txt
Streams
Listeners
Saves
Followers
```

This creates uncertainty when evaluating campaigns.

---

### Bot traffic

Many clicks generated through advertising campaigns never become listeners.

Users have no visibility into:

```txt
- Bots
- Crawlers
- Accidental clicks
- Low-quality traffic
```

---

### Existing solutions are expensive

Tools such as:

* Feature.fm
* Linkfire
* ToneDen
* SubmitHub

focus on larger artists, labels, or enterprise customers.

Independent artists often need:

```txt
A landing page.
A few buttons.
Reliable tracking.
Nothing else.
```

---

# 3. Target Customer

## Primary

Independent musicians

Examples:

```txt
Band with 10–10,000 monthly listeners.
Running Meta Ads.
Trying to grow Spotify audience.
```

---

## Secondary

Music marketing agencies

Examples:

```txt
Agency managing 5–100 artists.
Needs campaign reporting.
Needs reusable landing templates.
```

---

## Future

Record labels.

---

# 4. Core Value Proposition

FanFlow helps musicians answer:

> Which campaigns generate real fans instead of just clicks?

---

# 5. Product Principles

## Fast

Landing should load in under:

```txt
1 second
```

under normal conditions.

---

## Static-first

Landing pages are pregenerated HTML.

No React.

No SPA.

No runtime rendering.

---

## Privacy-friendly

Minimal tracking.

No invasive analytics.

---

## Attribution-focused

Track only events that matter.

---

## Bot-resistant

Traffic quality matters more than traffic volume.

---

# 6. MVP Scope

## Landing Pages

Users can create:

```txt
Release
```

Landing page.

---

Fields:

```txt
Title
Headline
Description
Cover image
Background image
CTA text
Links
```

Supported destinations:

```txt
Spotify
```

More destinations will be supported in the future (not now).

---

Generated URL:

```txt
go.artist.com/run-to-me
```

or

```txt
fanflow.app/run-to-me
```

---

## Smart Redirects

Buttons point to Spotify url (or specific destination).

FanFlow calls an internal API to records event, then redirects.

---

## Analytics

Track:

### Landing View

```txt
PageView
```

---

### Destination Click

```txt
SpotifyClick
...
```

---

## Meta Conversions API

Send:

```txt
PageView
SpotifyClick
```

Server-side, but only after bot / spam check has been performed.

---

# 7. Bot Detection

## MVP

Bot score:

```txt
0–100
```

based on:

### User Agent

Known crawler lists.

---

### Honeypot Links

Invisible links.

---

### Dwell Time

LandingView → Click delta.

---

### Request Frequency

Rate limits.

---

### Cloudflare Signals

If available.

---

Events classified:

```txt
Human
Bot
```

---

Analytics should allow:

```txt
Show all traffic
Show only human traffic
```

---

# 8. Analytics Dashboard

Per landing:

```txt
Views
Qualified Views
Clicks
CTR
Traffic Sources
Countries
Devices
```

---

Per destination:

```txt
Spotify Clicks
... Clicks
```

---

Per campaign:

```txt
Cost
Clicks
Qualified Clicks
```

(optional MVP+)

---

# 9. Landing Infrastructure

## Authoring

React Admin.

---

## Backend

ASP.NET.

---

## Storage

Postgres.

---

## Publishing

Landing configuration:

```txt
Database
   ↓
Static Generator
   ↓
HTML
   ↓
Shared Volume
   ↓
Nginx
```

---

## Serving

```txt
Cloudflare
   ↓
Nginx
   ↓
Static HTML
```

---

Tracking routes:

```txt
/out/*
/pv/*
/trap/*
```

proxied to backend.

---

# 10. Domain Model (proposal)


## Release

```txt
Id
Title
Artwork
SpotifyUrl
CreatedAt
```

---

## Event

```txt
Id
ReleaseId
Timestamp
Type
Ip
Country
UserAgent
BotScore
```

---

# 11. Success Metrics

## Product

Landing generation:

```txt
< 1 second
```

---

Landing load:

```txt
< 1 second
```

---

Availability:

```txt
99.9%
```

---

## User

Ability to answer:

```txt
Which campaign performs best?
Which creatives perform best?
How much traffic is suspicious?
Which countries generate engagement?
```

---
# Non-Goals

FanFlow is not:

```txt
A music distributor.
A CRM.
An email marketing platform.
A social media scheduler.
A website builder.
A Spotify analytics replacement.
```

Its purpose is much narrower:

> Get real fans from ads into music platforms and measure the quality of that traffic.
