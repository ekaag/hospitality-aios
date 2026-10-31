import type { ApiClient, Schema } from "@hospitalityos/api-client";
import { useCallback, useEffect, useState, type FormEvent } from "react";
import { TraceDetail } from "./TraceDetail";

type Outcome = Schema<"AITraceOutcome">;
type Summary = Schema<"AITraceSummary">;

const outcomes: readonly Outcome[] = ["Answered", "Uncertain", "NoAuthoritativeSource", "Routed", "Failed"];

/**
 * The AI trace viewer (spec §26, §34): what the property's AI agents did and why, newest first, filtered by outcome or
 * by one guest's flow (its correlation ID), a page at a time; one trace open in full, as the caller's role may see it.
 */
export function Traces({ client, propertyId }: { client: ApiClient; propertyId: string }) {
  const [outcome, setOutcome] = useState<Outcome | "">("");
  const [flow, setFlow] = useState("");
  const [filter, setFilter] = useState<{ outcome?: Outcome; correlationId?: string }>({});
  const [traces, setTraces] = useState<Summary[]>([]);
  const [next, setNext] = useState<string | null>(null);
  const [open, setOpen] = useState<string | undefined>();
  const [problem, setProblem] = useState<string | undefined>();

  const page = useCallback(
    async (after?: string) => {
      const { data } = await client.GET("/api/v1/ai/traces", { params: { query: { propertyId, limit: 20, ...filter, ...(after ? { after } : {}) } } });

      if (!data) {
        setProblem("The traces could not be read.");

        return;
      }

      setProblem(undefined);
      setTraces((now) => (after ? [...now, ...data.traces] : data.traces));
      setNext(data.next);
    },
    [client, propertyId, filter],
  );

  useEffect(() => {
    // Loading sets state once the API answers.
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void page();
  }, [page]);

  function apply(event: FormEvent) {
    event.preventDefault();
    setOpen(undefined);
    setFilter({ ...(outcome ? { outcome } : {}), ...(flow.trim() ? { correlationId: flow.trim() } : {}) });
  }

  return (
    <section aria-labelledby="section" className="traces">
      <h2 id="section">AI traces</h2>
      <form onSubmit={apply} className="filters">
        <label htmlFor="outcome">Outcome</label>
        <select id="outcome" value={outcome} onChange={(event) => setOutcome(event.target.value as Outcome | "")}>
          <option value="">Any</option>
          {outcomes.map((each) => (
            <option key={each} value={each}>
              {each}
            </option>
          ))}
        </select>
        <label htmlFor="flow">Correlation ID</label>
        <input id="flow" type="text" value={flow} onChange={(event) => setFlow(event.target.value)} placeholder="One guest's flow" />
        <button type="submit">Filter</button>
      </form>
      {problem && <p role="alert">{problem}</p>}
      {traces.length === 0 && !problem && <p>No traces.</p>}
      <table>
        <thead>
          <tr>
            <th scope="col">When</th>
            <th scope="col">Agent</th>
            <th scope="col">Intent</th>
            <th scope="col">Outcome</th>
            <th scope="col">Flow</th>
          </tr>
        </thead>
        <tbody>
          {traces.map((trace) => (
            <tr key={trace.id} aria-selected={trace.id === open}>
              <td>
                <button type="button" onClick={() => setOpen(trace.id === open ? undefined : trace.id)}>
                  {new Date(trace.startedAt).toLocaleString()}
                </button>
              </td>
              <td>{trace.agent ?? "—"}</td>
              <td>{trace.intent ?? "—"}</td>
              <td>{trace.outcome}</td>
              <td>
                <button type="button" title="Show this flow's traces" onClick={() => setFlow(trace.correlationId)}>
                  {trace.correlationId.slice(0, 8)}
                </button>
              </td>
            </tr>
          ))}
        </tbody>
      </table>
      {next && (
        <button type="button" onClick={() => void page(next)}>
          More
        </button>
      )}
      {open && <TraceDetail key={open} client={client} id={open} />}
    </section>
  );
}
