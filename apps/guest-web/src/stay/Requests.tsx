import type { Schema } from "@hospitalityos/api-client";
import { timeOfDay, what } from "./format";

export type GuestRequest = Schema<"GuestRequestResponse">;

const tones: Record<Schema<"GuestRequestStatus">, string> = {
  Open: "badge-neutral",
  InProgress: "badge-brand",
  Exception: "badge-warning",
};

const statuses: Record<Schema<"GuestRequestStatus">, string> = {
  Open: "Received",
  InProgress: "In progress",
  Exception: "Our team is looking into it",
};

/**
 * The guest's requests still pending (spec §33 service status), as the platform knows them: what, where, and where each
 * is, never when it will be done. Completed requests leave the list; the conversation says when they are done.
 */
export function Requests({ requests }: { requests: GuestRequest[] }) {
  return (
    <section aria-labelledby="requests" className="requests card">
      <h2 id="requests">Your requests</h2>
      {requests.length === 0 ? (
        <p className="empty">Nothing pending.</p>
      ) : (
        <ul>
          {requests.map((request) => (
            <li key={request.id}>
              <strong>{what(request)}</strong>
              <span className={`status ${request.status} badge ${tones[request.status]}`}>{statuses[request.status]}</span>
              <span className="where">
                {request.location} · Requested {timeOfDay(request.localRequestedAt)}
              </span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
