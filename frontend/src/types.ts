export class DestinationLink {
  platform: string;
  url: string;

  constructor(data: { platform: string; url: string }) {
    this.platform = data.platform;
    this.url = data.url;
  }
}

// List-view shape returned by GET /api/releases (ListReleases).
export class ReleaseSummary {
  id: string;
  title: string;
  slug: string;
  url: string;
  status: string;
  createdAt: string;

  constructor(data: { id: string; title: string; slug: string; url: string; status: string; createdAt: string }) {
    this.id = data.id;
    this.title = data.title;
    this.slug = data.slug;
    this.url = data.url;
    this.status = data.status;
    this.createdAt = data.createdAt;
  }
}

// Full record shape returned by GET/POST/PUT /api/releases/{id} (CreateRelease/UpdateRelease/GetRelease).
export class Release {
  id: string;
  slug: string;
  url: string;
  artistName: string;
  title: string;
  headline: string;
  description: string;
  coverImageUrl: string;
  ctaText: string;
  facebookPixelId: string;
  links: DestinationLink[];
  status: string;
  createdAt: string;

  constructor(data: {
    id: string;
    slug: string;
    url: string;
    artistName: string;
    title: string;
    headline: string;
    description: string;
    coverImageUrl: string;
    ctaText: string;
    facebookPixelId: string;
    links: DestinationLink[];
    status: string;
    createdAt: string;
  }) {
    this.id = data.id;
    this.slug = data.slug;
    this.url = data.url;
    this.artistName = data.artistName;
    this.title = data.title;
    this.headline = data.headline;
    this.description = data.description;
    this.coverImageUrl = data.coverImageUrl;
    this.ctaText = data.ctaText;
    this.facebookPixelId = data.facebookPixelId;
    this.links = data.links.map(l => (l instanceof DestinationLink ? l : new DestinationLink(l)));
    this.status = data.status;
    this.createdAt = data.createdAt;
  }
}

export class BreakdownItem {
  label: string;
  count: number;

  constructor(data: { label: string; count: number }) {
    this.label = data.label;
    this.count = data.count;
  }
}

// Shape returned by GET /api/releases/{id}/analytics (GetReleaseAnalytics).
export class ReleaseAnalytics {
  views: number;
  qualifiedViews: number;
  clicks: number;
  ctr: number;
  trafficSources: BreakdownItem[];
  countries: BreakdownItem[];
  devices: BreakdownItem[];

  constructor(data: {
    views: number;
    qualifiedViews: number;
    clicks: number;
    ctr: number;
    trafficSources: BreakdownItem[];
    countries: BreakdownItem[];
    devices: BreakdownItem[];
  }) {
    this.views = data.views;
    this.qualifiedViews = data.qualifiedViews;
    this.clicks = data.clicks;
    this.ctr = data.ctr;
    this.trafficSources = data.trafficSources.map(b => (b instanceof BreakdownItem ? b : new BreakdownItem(b)));
    this.countries = data.countries.map(b => (b instanceof BreakdownItem ? b : new BreakdownItem(b)));
    this.devices = data.devices.map(b => (b instanceof BreakdownItem ? b : new BreakdownItem(b)));
  }
}
