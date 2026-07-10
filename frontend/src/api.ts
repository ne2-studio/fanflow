import { Release, ReleaseSummary, ReleaseAnalytics } from './types';

const API_BASE_URL = import.meta.env.VITE_API_URL || 'http://localhost:5050';

let _accessToken: string | undefined;

export function setAccessToken(token: string | undefined) {
  _accessToken = token;
}

const getHeaders = (): Record<string, string> => ({
  'Content-Type': 'application/json',
  'Authorization': `Bearer ${_accessToken}`,
});

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
      coverImageUrl: string;
      backgroundImageUrl: string;
      ctaText: string;
      facebookPixelId: string;
      links: { platform: string; url: string }[];
    }): Promise<Release> =>
      fetch(`${API_BASE_URL}/api/releases`, {
        method: 'POST',
        headers: getHeaders(),
        body: JSON.stringify(release),
      }).then(handleResponse).then(data => new Release(data)),
    update: async (id: string, release: Partial<{
      artistName: string;
      title: string;
      headline: string;
      description: string;
      coverImageUrl: string;
      backgroundImageUrl: string;
      ctaText: string;
      facebookPixelId: string;
      links: { platform: string; url: string }[];
    }>): Promise<Release> =>
      fetch(`${API_BASE_URL}/api/releases/${id}`, {
        method: 'PUT',
        headers: getHeaders(),
        body: JSON.stringify(release),
      }).then(handleResponse).then(data => new Release(data)),
    delete: async (id: string): Promise<void> =>
      fetch(`${API_BASE_URL}/api/releases/${id}`, {
        method: 'DELETE',
        headers: getHeaders(),
      }).then(handleResponse),
    getAnalytics: async (id: string, filter: 'all' | 'human'): Promise<ReleaseAnalytics> =>
      fetch(`${API_BASE_URL}/api/releases/${id}/analytics?filter=${filter}`, { headers: getHeaders() })
        .then(handleResponse).then(data => new ReleaseAnalytics(data)),
  },
};
