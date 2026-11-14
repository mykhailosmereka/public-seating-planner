import { useEffect, useRef, useState, type FormEvent } from 'react';
import {
  createEvent,
  EVENT_NAME_MAX_LENGTH,
  EVENT_NAME_MIN_LENGTH,
  ValidationError,
  type EventSummary,
} from './api';
import './CreateEventForm.css';

interface CreateEventFormProps {
  onCreated: (event: EventSummary) => void;
}

function validateName(name: string): string | null {
  const length = name.trim().length;
  if (length < EVENT_NAME_MIN_LENGTH || length > EVENT_NAME_MAX_LENGTH) {
    return `Event name must be between ${EVENT_NAME_MIN_LENGTH} and ${EVENT_NAME_MAX_LENGTH} characters.`;
  }
  return null;
}

export function CreateEventForm({ onCreated }: CreateEventFormProps) {
  const [name, setName] = useState('');
  const [touched, setTouched] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [serverError, setServerError] = useState<string | null>(null);
  const controllerRef = useRef<AbortController | null>(null);

  // Abort an in-flight create if the form unmounts.
  useEffect(() => () => controllerRef.current?.abort(), []);

  const clientError = touched ? validateName(name) : null;
  const fieldError = clientError ?? serverError;

  const handleSubmit = async (e: FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    setTouched(true);
    if (validateName(name)) return;

    const controller = new AbortController();
    controllerRef.current = controller;
    setSubmitting(true);
    setServerError(null);

    try {
      const created = await createEvent(name.trim(), controller.signal);
      setName('');
      setTouched(false);
      onCreated(created);
    } catch (err: unknown) {
      if (controller.signal.aborted) return;
      if (err instanceof ValidationError) {
        setServerError(err.errors.name?.[0] ?? err.message);
      } else {
        setServerError(err instanceof Error ? err.message : 'Failed to create event');
      }
    } finally {
      if (!controller.signal.aborted) setSubmitting(false);
    }
  };

  return (
    <form className="create-event-form" onSubmit={handleSubmit} noValidate>
      <label htmlFor="create-event-name" className="visually-hidden">
        Event name
      </label>
      <div className="create-event-row">
        <input
          id="create-event-name"
          className="create-event-input"
          type="text"
          placeholder="New event name"
          value={name}
          disabled={submitting}
          aria-invalid={fieldError ? true : undefined}
          aria-describedby={fieldError ? 'create-event-error' : undefined}
          onChange={(e) => {
            setName(e.target.value);
            setServerError(null);
          }}
          onBlur={() => setTouched(name.length > 0)}
        />
        <button className="refresh-button" type="submit" disabled={submitting}>
          {submitting ? 'Creating…' : 'Create'}
        </button>
      </div>
      {fieldError && (
        <p id="create-event-error" className="create-event-error" role="alert">
          {fieldError}
        </p>
      )}
    </form>
  );
}
