import type { ApiClient, Schema } from "@hospitalityos/api-client";
import { useCallback, useEffect, useState } from "react";

type Failed = Schema<"FailedEventResponse">;

const local = (value: string) => value.slice(0, 19).replace("T", " ");
const key = (failed: Failed) => `${failed.eventId}/${failed.handler ?? ""}`;

/**
 * Failed events (L1, spec §42): the tenant's events whose processing was given up on, newest first, those that could
 * not be published and those a step kept failing on, each with its error and the flow it belongs to (its correlation ID,
 * which the AI traces filter by). Once the cause is fixed, Retry processes one again; if it fails again, it comes back.
 * Administrators only.
 */
export function FailedEvents({ client }: { client: ApiClient }) {
  const [failed, setFailed] = useState<Failed[] | undefined>();
  const [problem, setProblem] = useState<string | undefined>();
  const [retried, setRetried] = useState<string | undefined>();
  const [changes, setChanges] = useState(0);

  const load = useCallback(async () => {
    const { data } = await client.GET("/api/v1/failed-events");

    setFailed(data);
    setProblem(data ? undefined : "The failed events could not be read.");
  }, [client]);

  useEffect(() => {
    // Loading sets state once the API answers.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load, changes]);

  async function retry(event: Failed) {
    const { data } = await client.POST("/api/v1/failed-events/{eventId}/retry", {
      params: { path: { eventId: event.eventId } },
      body: event.handler ? { handler: event.handler } : {},
    });

    if (!data) {
      setProblem(`${event.eventType} could not be retried; it may have been retried already.`);
      setChanges((count) => count + 1);

      return;
    }

    setProblem(undefined);
    setRetried(`${event.eventType} was sent to be processed again.`);
    setChanges((count) => count + 1);
  }

  return (
    <section aria-labelledby="section" className="failed-events">
      <h2 id="section">Failed events</h2>
      {problem && <p role="alert">{problem}</p>}
      {retried && <p role="status">{retried}</p>}
      {failed && failed.length === 0 && <p>Nothing has failed. Every event was published and handled.</p>}
      {failed && failed.length > 0 && (
        <table>
          <caption>Given up on, newest first</caption>
          <thead>
            <tr>
              <th scope="col">Event</th>
              <th scope="col">Failed while</th>
              <th scope="col">Error</th>
              <th scope="col">Tries</th>
              <th scope="col">When</th>
              <th scope="col">Flow</th>
              <th scope="col">
                <span className="visually-hidden">Retry</span>
              </th>
            </tr>
          </thead>
          <tbody>
            {failed.map((event) => (
              <tr key={key(event)}>
                <th scope="row">
                  {event.eventType}
                  <br />
                  <small>
                    {event.aggregateType} {event.aggregateId}
                  </small>
                </th>
                <td>{event.stage === "Publish" ? "Publishing" : `Handling (${event.handler ?? "unknown"})`}</td>
                <td>
                  <code>{event.error}</code>
                </td>
                <td>{event.attempts}</td>
                <td>{local(event.failedAt ?? event.recordedAt)}</td>
                <td>
                  <code>{event.correlationId}</code>
                </td>
                <td>
                  <button type="button" onClick={() => void retry(event)}>
                    Retry
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
