import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Plus, Trash2, Pencil, BarChart3, ExternalLink } from 'lucide-react';
import { useReleaseStore } from '../store/useReleaseStore';
import { api } from '../api';
import { Release } from '../types';

interface ReleaseFormState {
  title: string;
  headline: string;
  description: string;
  coverImageUrl: string;
  backgroundImageUrl: string;
  ctaText: string;
  spotifyUrl: string;
  facebookPixelId: string;
}

const emptyForm: ReleaseFormState = {
  title: '',
  headline: '',
  description: '',
  coverImageUrl: '',
  backgroundImageUrl: '',
  ctaText: 'Listen Now',
  spotifyUrl: '',
  facebookPixelId: '',
};

function ReleaseFormModal({
  initial,
  onClose,
  onSubmit,
}: {
  initial: ReleaseFormState;
  onClose: () => void;
  onSubmit: (form: ReleaseFormState) => Promise<void>;
}) {
  const [form, setForm] = useState<ReleaseFormState>(initial);
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const set = (field: keyof ReleaseFormState) => (e: React.ChangeEvent<HTMLInputElement | HTMLTextAreaElement>) =>
    setForm((f) => ({ ...f, [field]: e.target.value }));

  const handleSubmit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(form);
      onClose();
    } catch (err) {
      setError((err as Error).message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center p-6 z-50">
      <div className="bg-surface border border-border rounded-sm p-6 w-full max-w-lg flex flex-col gap-4 max-h-[90vh] overflow-y-auto">
        <h3 className="text-sm font-mono uppercase tracking-widest text-text-secondary">Release</h3>

        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">Title</label>
          <input className="p-2 text-sm rounded-sm" value={form.title} onChange={set('title')} />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">Headline</label>
          <input className="p-2 text-sm rounded-sm" value={form.headline} onChange={set('headline')} />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">Description</label>
          <textarea className="p-2 text-sm rounded-sm" rows={3} value={form.description} onChange={set('description')} />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">Cover image URL</label>
          <input className="p-2 text-sm rounded-sm" value={form.coverImageUrl} onChange={set('coverImageUrl')} />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">Background image URL</label>
          <input className="p-2 text-sm rounded-sm" value={form.backgroundImageUrl} onChange={set('backgroundImageUrl')} />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">CTA text</label>
          <input className="p-2 text-sm rounded-sm" value={form.ctaText} onChange={set('ctaText')} />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">Spotify URL</label>
          <input className="p-2 text-sm rounded-sm" value={form.spotifyUrl} onChange={set('spotifyUrl')} placeholder="https://open.spotify.com/track/..." />
        </div>
        <div className="flex flex-col gap-1">
          <label className="text-xs text-text-secondary">Facebook Pixel ID</label>
          <input className="p-2 text-sm rounded-sm" value={form.facebookPixelId} onChange={set('facebookPixelId')} placeholder="123456789012345" />
        </div>

        {error && <p className="text-xs text-error">{error}</p>}

        <div className="flex justify-end gap-3 pt-2">
          <button onClick={onClose} className="px-4 py-2 text-xs font-bold uppercase tracking-widest text-text-secondary hover:text-text-primary transition-all">
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting || !form.title || !form.spotifyUrl || !form.facebookPixelId}
            className="bg-primary text-white px-4 py-2 text-xs font-bold uppercase tracking-widest rounded-sm hover:bg-primary/90 transition-all disabled:opacity-50"
          >
            {submitting ? 'Saving...' : 'Save'}
          </button>
        </div>
      </div>
    </div>
  );
}

export function Releases() {
  const { releases, addRelease, updateRelease, deleteRelease } = useReleaseStore();
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<{ id: string; form: ReleaseFormState } | null>(null);

  const formatDate = (val: string) => new Intl.DateTimeFormat('en-US', { dateStyle: 'medium' }).format(new Date(val));

  const toRequest = (form: ReleaseFormState) => ({
    title: form.title,
    headline: form.headline,
    description: form.description,
    coverImageUrl: form.coverImageUrl,
    backgroundImageUrl: form.backgroundImageUrl,
    ctaText: form.ctaText,
    facebookPixelId: form.facebookPixelId.trim(),
    links: [{ platform: 'Spotify', url: form.spotifyUrl }],
  });

  const openEdit = async (id: string) => {
    const release: Release = await api.releases.get(id);
    setEditing({
      id,
      form: {
        title: release.title,
        headline: release.headline,
        description: release.description,
        coverImageUrl: release.coverImageUrl,
        backgroundImageUrl: release.backgroundImageUrl,
        ctaText: release.ctaText,
        spotifyUrl: release.links.find(l => l.platform.toLowerCase() === 'spotify')?.url ?? '',
        facebookPixelId: release.facebookPixelId,
      },
    });
  };

  return (
    <div className="flex flex-col gap-8">
      <div className="flex items-center justify-between">
        <div className="flex flex-col gap-1">
          <h2 className="text-lg font-medium tracking-tight text-text-primary">Releases</h2>
          <p className="text-xs text-text-secondary">Landing pages that drive fans from ads to Spotify.</p>
        </div>
        <button
          onClick={() => setCreating(true)}
          className="bg-primary text-white px-4 py-1.5 text-xs font-bold uppercase tracking-widest rounded-sm hover:bg-primary/90 transition-all flex items-center gap-2"
        >
          <Plus className="w-3.5 h-3.5" /> New release
        </button>
      </div>

      <div className="bg-surface border border-border rounded-sm overflow-hidden">
        <table className="w-full text-left">
          <thead>
            <tr className="bg-surface-elevated/50 border-b border-border">
              <th className="p-4">Title</th>
              <th className="p-4">URL</th>
              <th className="p-4">Status</th>
              <th className="p-4">Created</th>
              <th className="p-4 text-right">Actions</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/30">
            {releases.map((release) => (
              <tr key={release.id} className="hover:bg-surface-elevated/20 transition-colors">
                <td className="p-4 text-sm font-medium">{release.title}</td>
                <td className="p-4 text-xs font-mono text-text-secondary">
                  <a href={release.url} target="_blank" rel="noreferrer" className="inline-flex items-center gap-1 hover:text-primary">
                    {release.url} <ExternalLink className="w-3 h-3" />
                  </a>
                </td>
                <td className="p-4 text-xs uppercase tracking-wider text-text-secondary">{release.status}</td>
                <td className="p-4 text-xs font-mono text-text-secondary">{formatDate(release.createdAt)}</td>
                <td className="p-4 text-right">
                  <div className="flex justify-end gap-1">
                    <Link to={`/releases/${release.id}/analytics`} className="p-1.5 text-text-secondary hover:text-primary transition-all" title="Analytics">
                      <BarChart3 className="w-3.5 h-3.5" />
                    </Link>
                    <button onClick={() => openEdit(release.id)} className="p-1.5 text-text-secondary hover:text-primary transition-all" title="Edit">
                      <Pencil className="w-3.5 h-3.5" />
                    </button>
                    <button onClick={() => deleteRelease(release.id)} className="p-1.5 text-text-secondary hover:text-error transition-all" title="Delete">
                      <Trash2 className="w-3.5 h-3.5" />
                    </button>
                  </div>
                </td>
              </tr>
            ))}
            {releases.length === 0 && (
              <tr>
                <td colSpan={5} className="p-12 text-center text-text-secondary italic text-xs">
                  No releases yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>

      {creating && (
        <ReleaseFormModal
          initial={emptyForm}
          onClose={() => setCreating(false)}
          onSubmit={async (form) => {
            await addRelease(toRequest(form));
          }}
        />
      )}

      {editing && (
        <ReleaseFormModal
          initial={editing.form}
          onClose={() => setEditing(null)}
          onSubmit={async (form) => {
            await updateRelease(editing.id, toRequest(form));
          }}
        />
      )}
    </div>
  );
}
