export interface EventSummary {
  id: string;
  name: string;
}

export async function fetchEvents(signal?: AbortSignal): Promise<EventSummary[]> {
  const response = await fetch('/api/events', {
    headers: { Accept: 'application/json' },
    signal,
  });

  if (!response.ok) {
    throw new Error(`Failed to load events (HTTP ${response.status})`);
  }

  return (await response.json()) as EventSummary[];
}
