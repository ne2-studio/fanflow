import { create } from 'zustand';
import { Release, ReleaseSummary, ReleaseAnalytics, TrackedEvent } from '../types';
import { api } from '../api';

interface ReleaseInput {
  artistName: string;
  title: string;
  headline: string;
  description: string;
  coverImage: File | null;
  ctaText: string;
  facebookPixelId: string;
  links: { platform: string; url: string }[];
}

interface ReleaseStore {
  releases: ReleaseSummary[];
  isLoading: boolean;
  error: string | null;

  analytics: ReleaseAnalytics | null;
  analyticsLoading: boolean;

  events: TrackedEvent[];
  eventsLoading: boolean;

  fetchData: () => Promise<void>;

  addRelease: (release: ReleaseInput) => Promise<Release>;
  updateRelease: (id: string, release: Partial<ReleaseInput>) => Promise<Release>;
  deleteRelease: (id: string) => Promise<void>;

  loadAnalytics: (id: string, filter: 'all' | 'human') => Promise<void>;
  loadEvents: (id: string) => Promise<void>;
}

const toSummary = (release: Release): ReleaseSummary =>
  new ReleaseSummary({ id: release.id, title: release.title, slug: release.slug, url: release.url, status: release.status, createdAt: release.createdAt });

export const useReleaseStore = create<ReleaseStore>((set) => ({
  releases: [],
  isLoading: false,
  error: null,

  analytics: null,
  analyticsLoading: false,

  events: [],
  eventsLoading: false,

  fetchData: async () => {
    set({ isLoading: true, error: null });
    try {
      const [releases] = await Promise.all([
        api.releases.getAll(),
      ]);

      set({ releases, isLoading: false });
    } catch (error) {
      set({ error: (error as Error).message, isLoading: false });
    }
  },

  addRelease: async (release) => {
    const created = await api.releases.create(release);
    set((state) => ({ releases: [toSummary(created), ...state.releases] }));
    return created;
  },

  updateRelease: async (id, release) => {
    const updated = await api.releases.update(id, release);
    set((state) => ({ releases: state.releases.map(r => (r.id === id ? toSummary(updated) : r)) }));
    return updated;
  },

  deleteRelease: async (id) => {
    await api.releases.delete(id);
    set((state) => ({ releases: state.releases.filter((r) => r.id !== id) }));
  },

  loadAnalytics: async (id, filter) => {
    set({ analyticsLoading: true });
    try {
      const analytics = await api.releases.getAnalytics(id, filter);
      set({ analytics, analyticsLoading: false });
    } catch (error) {
      set({ error: (error as Error).message, analyticsLoading: false });
    }
  },

  loadEvents: async (id) => {
    set({ eventsLoading: true });
    try {
      const events = await api.releases.getEvents(id);
      set({ events, eventsLoading: false });
    } catch (error) {
      set({ error: (error as Error).message, eventsLoading: false });
    }
  },
}));
