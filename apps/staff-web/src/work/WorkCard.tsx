import type { Schema } from "@hospitalityos/api-client";
import { useState } from "react";
import { ago, isOverdue, timeOfDay } from "./format";

export type WorkItem = Schema<"WorkItemResponse">;
export type StaffMember = Schema<"StaffMemberResponse">;

/**
 * A step taken on a card; `body` carries the note, reason, or assignee.
 */
export type Step =
  | { kind: "accept" | "start" | "claim" | "request-again" }
  | { kind: "complete"; note?: string }
  | { kind: "fail"; reason: string }
  | { kind: "assign"; staffMemberId: string };

export interface WorkCardProps {
  item: WorkItem;
  /** The property's roster, for whom a manager may assign the work to. */
  roster?: readonly StaffMember[];
  onStep: (item: WorkItem, step: Step) => Promise<void>;
  now?: Date;
}

/**
 * One piece of work (spec §32): where, what, how urgent, when it was asked for and is due, and only the steps the caller
 * may take now, as the API says. Failing asks why; completing takes an optional note.
 */
export function WorkCard({ item, roster = [], onStep, now = new Date() }: WorkCardProps) {
  const [asking, setAsking] = useState<"complete" | "fail" | "assign" | undefined>();
  const [text, setText] = useState("");
  const [busy, setBusy] = useState(false);
  const eligible = roster.filter((member) => member.isOnDuty && item.skillsRequired.every((skill) => member.skills.includes(skill)));

  async function take(step: Step) {
    setBusy(true);

    try {
      await onStep(item, step);
      setAsking(undefined);
      setText("");
    } finally {
      setBusy(false);
    }
  }

  const can = (action: Schema<"WorkAction">) => item.actions.includes(action);

  return (
    <article aria-label={`${item.location}: ${item.instructions}`} className={`card work priority-${item.priority.toLowerCase()}`}>
      <header>
        <h3>{item.location}</h3>
        <span className={`status status-${item.status.toLowerCase()} badge ${tone(item.status)}`}>{label(item.status)}</span>
      </header>
      {item.items.length > 0 ? (
        <>
          <p>Deliver:</p>
          <ul>
            {item.items.map((thing) => (
              <li key={thing.name}>
                {thing.quantity} × {thing.name.toLowerCase()}
              </li>
            ))}
          </ul>
        </>
      ) : (
        <p>{item.instructions}</p>
      )}
      <dl>
        <dt>Priority</dt>
        <dd>{item.priority}</dd>
        <dt>Requested</dt>
        <dd>{ago(item.requestedAt, now)}</dd>
        {item.deadline && (
          <>
            <dt>Target</dt>
            <dd className={isOverdue(item.deadline, now) && !item.endedAt ? "overdue" : undefined}>{timeOfDay(item.deadline)}</dd>
          </>
        )}
        {item.assigneeName && (
          <>
            <dt>With</dt>
            <dd>{item.assigneeName}</dd>
          </>
        )}
        {item.endedAt && (
          <>
            <dt>{item.status === "Completed" ? "Completed" : "Ended"}</dt>
            <dd>{ago(item.endedAt, now)}</dd>
          </>
        )}
      </dl>
      {item.completionNote && <p className="note">“{item.completionNote}”</p>}
      {item.failureReason && <p className="problem">{item.failureReason}</p>}

      {asking === undefined && (
        <div className="actions">
          {can("Accept") && <Button busy={busy} onClick={() => void take({ kind: "accept" })}>Accept</Button>}
          {can("Claim") && <Button busy={busy} onClick={() => void take({ kind: "claim" })}>Claim</Button>}
          {can("Start") && <Button busy={busy} onClick={() => void take({ kind: "start" })}>Start</Button>}
          {can("Complete") && <Button busy={busy} onClick={() => setAsking("complete")}>Complete</Button>}
          {can("Fail") && <Button busy={busy} onClick={() => setAsking("fail")}>Fail</Button>}
          {can("Assign") && <Button busy={busy} onClick={() => setAsking("assign")}>Assign</Button>}
          {can("RequestAgain") && <Button busy={busy} onClick={() => void take({ kind: "request-again" })}>Request again</Button>}
        </div>
      )}

      {asking === "complete" && (
        <form onSubmit={(event) => { event.preventDefault(); void take({ kind: "complete", ...(text.trim() ? { note: text.trim() } : {}) }); }}>
          <label>
            Note (optional)
            <input value={text} onChange={(event) => setText(event.target.value)} placeholder="Left at the door" />
          </label>
          <Button busy={busy} type="submit">Complete</Button>
          <button type="button" onClick={() => setAsking(undefined)}>Cancel</button>
        </form>
      )}

      {asking === "fail" && (
        <form onSubmit={(event) => { event.preventDefault(); void take({ kind: "fail", reason: text.trim() }); }}>
          <label>
            Why can't it be done?
            <input value={text} onChange={(event) => setText(event.target.value)} placeholder="Could not access the room" required />
          </label>
          <Button busy={busy || !text.trim()} type="submit">Fail</Button>
          <button type="button" onClick={() => setAsking(undefined)}>Cancel</button>
        </form>
      )}

      {asking === "assign" && (
        <form onSubmit={(event) => { event.preventDefault(); void take({ kind: "assign", staffMemberId: text }); }}>
          <label>
            Assign to
            <select value={text} onChange={(event) => setText(event.target.value)} required>
              <option value="">Choose someone on duty</option>
              {eligible.map((member) => (
                <option key={member.id} value={member.id}>
                  {member.displayName}
                </option>
              ))}
            </select>
          </label>
          <Button busy={busy || !text} type="submit">Assign</Button>
          <button type="button" onClick={() => setAsking(undefined)}>Cancel</button>
        </form>
      )}
    </article>
  );
}

function Button({ busy, children, onClick, type = "button" }: { busy: boolean; children: string; onClick?: () => void; type?: "button" | "submit" }) {
  return (
    <button type={type} disabled={busy} onClick={onClick}>
      {children.toUpperCase()}
    </button>
  );
}

function tone(status: Schema<"ActionStatus">): string {
  switch (status) {
    case "Completed":
      return "badge-success";
    case "Failed":
    case "Expired":
      return "badge-danger";
    case "Cancelled":
      return "badge-neutral";
    case "InProgress":
    case "Accepted":
      return "badge-brand";
    default:
      return "badge-warning";
  }
}

function label(status: Schema<"ActionStatus">): string {
  return status === "InProgress" ? "In progress" : status;
}
