import { useEffect, useState } from 'react';
import { fetchEvents, type EventSummary } from './api';
import './EventsList.css';

export function EventsList() {
  const [events, setEvents] = useState<EventSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  // Bumping this re-runs the fetch effect (used by the Refresh button).
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    const controller = new AbortController();

    fetchEvents(controller.signal)
      .then((data) => setEvents(data))
      .catch((err: unknown) => {
        if (controller.signal.aborted) return;
        setError(err instanceof Error ? err.message : 'Failed to load events');
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });

    return () => controller.abort();
  }, [reloadToken]);

  const refresh = () => {
    setLoading(true);
    setError(null);
    setReloadToken((token) => token + 1);
  };

  return (
    <section className="card events-section" aria-labelledby="events-heading">
      <div className="section-header">
        <h2 id="events-heading" className="section-title">
          Events
          {!loading && !error && <span className="events-count">{events.length}</span>}
        </h2>
        <button
          className="refresh-button"
          onClick={refresh}
          disabled={loading}
          type="button"
        >
          <svg
            className={`refresh-icon ${loading ? 'spinning' : ''}`}
            width="18"
            height="18"
            viewBox="0 0 24 24"
            fill="none"
            stroke="currentColor"
            strokeWidth="2"
            aria-hidden="true"
            focusable="false"
          >
            <path d="M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3M22 12.5a10 10 0 0 1-18.8 4.2" />
          </svg>
          <span>{loading ? 'Loading…' : 'Refresh'}</span>
        </button>
      </div>

      {error && (
        <div className="error-message" role="alert">
          <span>{error}</span>
        </div>
      )}

      {loading && events.length === 0 && !error && (
        <div className="loading-skeleton" role="status" aria-live="polite">
          {[0, 1, 2].map((i) => (
            <div key={i} className="skeleton-row" aria-hidden="true" />
          ))}
          <span className="visually-hidden">Loading events…</span>
        </div>
      )}

      {!loading && !error && events.length === 0 && (
        <p className="events-empty">No events yet.</p>
      )}

      {events.length > 0 && (
        <ul className="events-list">
          {events.map((event) => (
            <li key={event.id} className="event-item">
              <span className="event-name">{event.name}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
