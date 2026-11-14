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

export const EVENT_NAME_MIN_LENGTH = 3;
export const EVENT_NAME_MAX_LENGTH = 100;

/** Thrown when the server rejects a request with a 400 ValidationProblem. */
export class ValidationError extends Error {
  readonly errors: Record<string, string[]>;

  constructor(errors: Record<string, string[]>) {
    super(Object.values(errors).flat()[0] ?? 'Validation failed');
    this.name = 'ValidationError';
    this.errors = errors;
  }
}

export async function createEvent(name: string, signal?: AbortSignal): Promise<EventSummary> {
  const response = await fetch('/api/events', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
    body: JSON.stringify({ name }),
    signal,
  });

  if (response.status === 400) {
    const problem = (await response.json().catch(() => null)) as
      | { errors?: Record<string, string[]> }
      | null;
    throw new ValidationError(problem?.errors ?? {});
  }

  if (!response.ok) {
    throw new Error(`Failed to create event (HTTP ${response.status})`);
  }

  return (await response.json()) as EventSummary;
}
