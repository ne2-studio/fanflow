import { useEffect, useState } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useReleaseStore } from '../store/useReleaseStore';

function StatTile({ label, value }: { label: string; value: string }) {
  return (
    <div className="card flex flex-col gap-1">
      <span className="text-[10px] font-mono uppercase tracking-widest text-text-secondary">{label}</span>
      <span className="text-2xl font-medium text-text-primary">{value}</span>
    </div>
  );
}

function BreakdownTable({ title, items }: { title: string; items: { label: string; count: number }[] }) {
  return (
    <div className="bg-surface border border-border rounded-sm overflow-hidden">
      <div className="p-4 border-b border-border">
        <h3 className="text-xs font-mono uppercase tracking-widest text-text-secondary">{title}</h3>
      </div>
      <table className="w-full text-left">
        <tbody className="divide-y divide-border/30">
          {items.map((item) => (
            <tr key={item.label}>
              <td className="p-3 text-sm">{item.label}</td>
              <td className="p-3 text-sm text-right font-mono text-text-secondary">{item.count}</td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr>
              <td colSpan={2} className="p-6 text-center text-text-secondary italic text-xs">No data yet.</td>
            </tr>
          )}
        </tbody>
      </table>
    </div>
  );
}

export function ReleaseAnalyticsView() {
  const { id } = useParams<{ id: string }>();
  const { analytics, analyticsLoading, loadAnalytics } = useReleaseStore();
  const [filter, setFilter] = useState<'all' | 'human'>('all');

  useEffect(() => {
    if (id) loadAnalytics(id, filter);
  }, [id, filter]);

  const pct = (value: number) => new Intl.NumberFormat('en-US', { style: 'percent', maximumFractionDigits: 1 }).format(value);

  return (
    <div className="flex flex-col gap-8">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <Link to="/" className="p-1.5 text-text-secondary hover:text-text-primary transition-all">
            <ArrowLeft className="w-4 h-4" />
          </Link>
          <div className="flex flex-col gap-1">
            <h2 className="text-lg font-medium tracking-tight text-text-primary">Analytics</h2>
            <p className="text-xs text-text-secondary">Traffic quality for this release.</p>
          </div>
        </div>

        <div className="flex gap-1 bg-surface border border-border rounded-sm p-1">
          {(['all', 'human'] as const).map((f) => (
            <button
              key={f}
              onClick={() => setFilter(f)}
              className={`px-3 py-1.5 text-xs font-bold uppercase tracking-widest rounded-sm transition-all ${
                filter === f ? 'bg-primary text-white' : 'text-text-secondary hover:text-text-primary'
              }`}
            >
              {f === 'all' ? 'All traffic' : 'Human only'}
            </button>
          ))}
        </div>
      </div>

      {analyticsLoading && !analytics && <p className="text-xs text-text-secondary">Loading...</p>}

      {analytics && (
        <>
          <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
            <StatTile label="Views" value={analytics.views.toString()} />
            <StatTile label="Qualified views" value={analytics.qualifiedViews.toString()} />
            <StatTile label="Clicks" value={analytics.clicks.toString()} />
            <StatTile label="CTR" value={pct(analytics.ctr)} />
          </div>

          <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
            <BreakdownTable title="Traffic sources" items={analytics.trafficSources} />
            <BreakdownTable title="Countries" items={analytics.countries} />
            <BreakdownTable title="Devices" items={analytics.devices} />
          </div>
        </>
      )}
    </div>
  );
}
