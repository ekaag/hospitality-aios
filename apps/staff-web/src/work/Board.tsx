import type { ApiClient, Problem, Schema } from "@hospitalityos/api-client";
import { useRealtime } from "@hospitalityos/realtime";
import { useCallback, useEffect, useState } from "react";
import { WorkCard, type StaffMember, type Step, type WorkItem } from "./WorkCard";

type View = Schema<"WorkView">;

const views: readonly View[] = ["Assigned", "Unassigned", "Completed", "Exceptions"];

export interface BoardProps {
  client: ApiClient;
  token: string;
  propertyId: string;
  /** Whether the caller oversees the property (a manager or administrator). */
  oversees: boolean;
}

/**
 * The staff surface (spec §32): My Actions (for managers, everyone's assigned work), Unassigned, Completed, and
 * Exceptions, each read from the API with the steps the caller may take, and read again whenever work changes.
 */
export function Board({ client, token, propertyId, oversees }: BoardProps) {
  const [view, setView] = useState<View>("Assigned");
  const [work, setWork] = useState<Partial<Record<View, WorkItem[]>>>({});
  const [roster, setRoster] = useState<StaffMember[]>([]);
  const [problem, setProblem] = useState<string | undefined>();

  const load = useCallback(async () => {
    const read = await Promise.all(views.map((each) => client.GET("/api/v1/work", { params: { query: { propertyId, view: each } } })));

    if (read.some((result) => !result.data)) {
      setProblem("The work could not be read.");

      return;
    }

    setWork(Object.fromEntries(views.map((each, index) => [each, read[index]?.data ?? []])));
    setProblem(undefined);
  }, [client, propertyId]);

  useEffect(() => {
    // Loading sets state once the API answers.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void load();
  }, [load]);

  useEffect(() => {
    if (!oversees) {
      return;
    }

    void client.GET("/api/v1/staff", { params: { query: { propertyId } } }).then(({ data }) => setRoster(data ?? []));
  }, [client, propertyId, oversees]);

  useRealtime(token, "WorkChanged", () => void load());

  async function step(item: WorkItem, taken: Step) {
    const result = await send(client, item.id, taken);

    setProblem(result.error ? describe(result.error as Problem) : undefined);
    await load();
  }

  return (
    <section aria-label="Work" className="board">
      <nav aria-label="Views" className="tabs">
        {views.map((each) => (
          <button key={each} type="button" aria-pressed={view === each} onClick={() => setView(each)}>
            {title(each, oversees)} <span className="count">({work[each]?.length ?? 0})</span>
          </button>
        ))}
      </nav>
      {problem && <p role="alert">{problem}</p>}
      {work[view]?.length === 0 && <p className="empty">{empty(view)}</p>}
      <div className="cards">
        {work[view]?.map((item) => (
          <WorkCard key={item.id} item={item} roster={roster} onStep={step} />
        ))}
      </div>
    </section>
  );
}

/**
 * Takes a step through the API, which answers with the work as it now stands or the problem.
 */
function send(client: ApiClient, id: string, taken: Step): Promise<{ error?: unknown }> {
  const path = { params: { path: { id } } };

  switch (taken.kind) {
    case "accept":
      return client.POST("/api/v1/work/{id}/accept", path);
    case "start":
      return client.POST("/api/v1/work/{id}/start", path);
    case "claim":
      return client.POST("/api/v1/work/{id}/claim", path);
    case "request-again":
      return client.POST("/api/v1/work/{id}/request-again", path);
    case "complete":
      return client.POST("/api/v1/work/{id}/complete", { ...path, body: taken.note ? { note: taken.note } : {} });
    case "fail":
      return client.POST("/api/v1/work/{id}/fail", { ...path, body: { reason: taken.reason } });
    case "assign":
      return client.POST("/api/v1/work/{id}/assign", { ...path, body: { staffMemberId: taken.staffMemberId } });
  }
}

function title(view: View, oversees: boolean): string {
  switch (view) {
    case "Assigned":
      return oversees ? "Assigned" : "My Actions";
    case "Unassigned":
      return "Unassigned";
    case "Completed":
      return "Completed";
    case "Exceptions":
      return "Exceptions";
  }
}

function empty(view: View): string {
  switch (view) {
    case "Assigned":
      return "Nothing to do right now.";
    case "Unassigned":
      return "Nothing is waiting for someone.";
    case "Completed":
      return "Nothing completed today yet.";
    case "Exceptions":
      return "No exceptions.";
  }
}

function describe(problem: Problem | undefined): string {
  return problem?.title ?? "That could not be done.";
}
