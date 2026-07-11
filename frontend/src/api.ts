import { Release, ReleaseSummary, ReleaseAnalytics, TrackedEvent } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5050';

let _accessToken: string | undefined;

export function setAccessToken(token: string | undefined) {
  _accessToken = token;
}

const getHeaders = (): Record<string, string> => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${_accessToken}`,
});

// No Content-Type here: the browser sets the multipart boundary itself when the body is FormData.
const getAuthHeaders = (): Record<string, string> => ({
  'Authorization': `Bearer ${_accessToken}`,
});

const toReleaseFormData = (release: {
  artistName?: string;
  title?: string;
  headline?: string;
  description?: string;
  coverImage?: File | null;
  ctaText?: string;
  facebookPixelId?: string;
  links?: { platform: string; url: string }[];
}): FormData => {
  const formData = new FormData();
  if (release.artistName !== undefined) formData.append('artistName', release.artistName);
  if (release.title !== undefined) formData.append('title', release.title);
  if (release.headline !== undefined) formData.append('headline', release.headline);
  if (release.description !== undefined) formData.append('description', release.description);
  if (release.ctaText !== undefined) formData.append('ctaText', release.ctaText);
  if (release.facebookPixelId !== undefined) formData.append('facebookPixelId', release.facebookPixelId);
  if (release.links !== undefined) formData.append('linksJson', JSON.stringify(release.links));
  // Omitting the field entirely (not sending an empty file) is what keeps the existing cover image.
  if (release.coverImage) formData.append('coverImage', release.coverImage);
  return formData;
};

const handleResponse = async (res: Response) => {
  if (!res.ok) {
    const error = await res.json().catch(() => ({ message: 'Unknown error' }));
    throw new Error(error.error || error.message || `API Error: ${res.status}`);
  }
  if (res.status === 204) return undefined;
  return res.json();
};

export const api = {
  releases: {
    getAll: async (): Promise<ReleaseSummary[]> =>
      fetch(`${API_BASE_URL}/api/releases`, { headers: getHeaders() }).then(handleResponse).then(data => data.map((r: any) => new ReleaseSummary(r))),
    get: async (id: string): Promise<Release> =>
      fetch(`${API_BASE_URL}/api/releases/${id}`, { headers: getHeaders() }).then(handleResponse).then(data => new Release(data)),
    create: async (release: {
      artistName: string;
      title: string;
      headline: string;
      description: string;
      coverImage: File | null;
      ctaText: string;
      facebookPixelId: string;
      links: { platform: string; url: string }[];
    }): Promise<Release> =>
      fetch(`${API_BASE_URL}/api/releases`, {
        method: 'POST',
        headers: getAuthHeaders(),
        body: toReleaseFormData(release),
      }).then(handleResponse).then(data => new Release(data)),
    update: async (id: string, release: Partial<{
      artistName: string;
      title: string;
      headline: string;
      description: string;
      coverImage: File | null;
      ctaText: string;
      facebookPixelId: string;
      links: { platform: string; url: string }[];
    }>): Promise<Release> =>
      fetch(`${API_BASE_URL}/api/releases/${id}`, {
        method: 'PUT',
        headers: getAuthHeaders(),
        body: toReleaseFormData(release),
      }).then(handleResponse).then(data => new Release(data)),
    delete: async (id: string): Promise<void> =>
      fetch(`${API_BASE_URL}/api/releases/${id}`, {
        method: 'DELETE',
        headers: getHeaders(),
      }).then(handleResponse),
    getAnalytics: async (id: string, filter: 'all' | 'human'): Promise<ReleaseAnalytics> =>
      fetch(`${API_BASE_URL}/api/releases/${id}/analytics?filter=${filter}`, { headers: getHeaders() })
        .then(handleResponse).then(data => new ReleaseAnalytics(data)),
    getEvents: async (id: string): Promise<TrackedEvent[]> =>
      fetch(`${API_BASE_URL}/api/releases/${id}/events`, { headers: getHeaders() })
        .then(handleResponse).then(data => data.map((e: any) => new TrackedEvent(e))),
  },
};
