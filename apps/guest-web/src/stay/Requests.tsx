import type { Schema } from "@hospitalityos/api-client";
import { timeOfDay, what } from "./format";

export type GuestRequest = Schema<"GuestRequestResponse">;

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
    <section aria-labelledby="requests" className="requests">
      <h2 id="requests">Your requests</h2>
      {requests.length === 0 ? (
        <p>Nothing pending.</p>
      ) : (
        <ul>
          {requests.map((request) => (
            <li key={request.id}>
              <strong>{what(request)}</strong>
              <span>{request.location}</span>
              <span className={`status ${request.status}`}>{statuses[request.status]}</span>
              <span>Requested {timeOfDay(request.localRequestedAt)}</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
