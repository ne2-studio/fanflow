import { useEffect } from 'react';
import { useParams, Link } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useReleaseStore } from '../store/useReleaseStore';

export function ReleaseEventsView() {
  const { id } = useParams<{ id: string }>();
  const { events, eventsLoading, loadEvents } = useReleaseStore();

  useEffect(() => {
    if (id) loadEvents(id);
  }, [id]);

  const formatDate = (val: string) => new Intl.DateTimeFormat('en-US', { dateStyle: 'medium', timeStyle: 'medium' }).format(new Date(val));

  return (
    <div className="flex flex-col gap-8">
      <div className="flex items-center gap-3">
        <Link to="/" className="p-1.5 text-text-secondary hover:text-text-primary transition-all">
          <ArrowLeft className="w-4 h-4" />
        </Link>
        <div className="flex flex-col gap-1">
          <h2 className="text-lg font-medium tracking-tight text-text-primary">Events</h2>
          <p className="text-xs text-text-secondary">Raw tracked events for this release, for audit purposes.</p>
        </div>
      </div>

      {eventsLoading && events.length === 0 && <p className="text-xs text-text-secondary">Loading...</p>}

      <div className="bg-surface border border-border rounded-sm overflow-x-auto">
        <table className="w-full text-left">
          <thead>
            <tr className="bg-surface-elevated/50 border-b border-border">
              <th className="p-4 whitespace-nowrap">Type</th>
              <th className="p-4 whitespace-nowrap">Classification</th>
              <th className="p-4 whitespace-nowrap">Bot score</th>
              <th className="p-4 whitespace-nowrap">IP address</th>
              <th className="p-4 whitespace-nowrap">Country</th>
              <th className="p-4">User agent</th>
              <th className="p-4">Referrer</th>
              <th className="p-4 whitespace-nowrap">fbp</th>
              <th className="p-4 whitespace-nowrap">fbc</th>
              <th className="p-4 whitespace-nowrap">Dwell</th>
              <th className="p-4 whitespace-nowrap">Recorded at</th>
            </tr>
          </thead>
          <tbody className="divide-y divide-border/30">
            {events.map((event) => (
              <tr key={event.id} className="hover:bg-surface-elevated/20 transition-colors">
                <td className="p-4 text-xs uppercase tracking-wider text-text-secondary whitespace-nowrap">
                  {event.type}{event.destinationId ? ` (${event.destinationId})` : ''}
                </td>
                <td className={`p-4 text-xs uppercase tracking-wider whitespace-nowrap ${
                  event.classification === 'Bot' ? 'text-error' : event.classification === 'Human' ? 'text-primary' : 'text-text-secondary'
                }`}>
                  {event.classification ?? 'Pending'}
                </td>
                <td className="p-4 text-xs font-mono text-text-secondary">{event.botScore ?? '-'}</td>
                <td className="p-4 text-xs font-mono text-text-secondary whitespace-nowrap">{event.ipAddress}</td>
                <td className="p-4 text-xs text-text-secondary whitespace-nowrap">{event.country ?? 'Unknown'}</td>
                <td className="p-4 text-xs text-text-secondary max-w-xs truncate" title={event.userAgent}>{event.userAgent}</td>
                <td className="p-4 text-xs text-text-secondary max-w-xs truncate" title={event.referrer ?? undefined}>{event.referrer ?? 'Direct'}</td>
                <td className="p-4 text-xs font-mono text-text-secondary max-w-xs truncate" title={event.fbp ?? undefined}>{event.fbp ?? '-'}</td>
                <td className="p-4 text-xs font-mono text-text-secondary max-w-xs truncate" title={event.fbc ?? undefined}>{event.fbc ?? '-'}</td>
                <td className="p-4 text-xs font-mono text-text-secondary whitespace-nowrap">{event.dwellTimeMs != null ? `${event.dwellTimeMs}ms` : '-'}</td>
                <td className="p-4 text-xs font-mono text-text-secondary whitespace-nowrap">{formatDate(event.createdAt)}</td>
              </tr>
            ))}
            {!eventsLoading && events.length === 0 && (
              <tr>
                <td colSpan={11} className="p-12 text-center text-text-secondary italic text-xs">
                  No events recorded yet.
                </td>
              </tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
