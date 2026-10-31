import type { ApiClient, Schema } from "@hospitalityos/api-client";
import { useEffect, useState } from "react";

type Trace = Schema<"AITraceResponse">;
type Part = Schema<"TracePart">;

const source = (reference: Schema<"SourceReference">) => `${reference.type} ${reference.id.slice(0, 8)} v${String(reference.version)}`;

function part(each: Part): string {
  switch (each.type) {
    case "Text":
      return each.text ?? "";
    case "ToolCall":
      return `calls ${each.name ?? "?"} ${JSON.stringify(each.arguments ?? {})}`;
    case "ToolResult":
      return `${each.isError ? "error from" : "result of"} ${each.callId ?? "?"}: ${each.text ?? ""}`;
    default:
      return each.text ?? String(each.type);
  }
}

/**
 * One trace in full (spec §26): what started it, what the agent read and called, each model call with its prompt and
 * attempts, whether its replies were grounded, what it answered and cited, and the flow's events. A role that may not see
 * a trace whole sees it redacted, and is told so.
 */
export function TraceDetail({ client, id }: { client: ApiClient; id: string }) {
  const [trace, setTrace] = useState<Trace | undefined>();
  const [problem, setProblem] = useState<string | undefined>();

  useEffect(() => {
    let current = true;

    void client.GET("/api/v1/ai/traces/{id}", { params: { path: { id } } }).then(({ data }) => {
      if (current) {
        setTrace(data);
        setProblem(data ? undefined : "The trace could not be read.");
      }
    });

    return () => {
      current = false;
    };
  }, [client, id]);

  if (problem) {
    return <p role="alert">{problem}</p>;
  }

  if (!trace) {
    return <p>Loading…</p>;
  }

  const { body } = trace;

  return (
    <article aria-label="Trace" className="trace">
      <h3>
        {trace.agent ?? "Agent"}: {trace.outcome}
      </h3>
      {trace.redacted && <p role="note">Some of this trace is withheld for your role.</p>}
      <dl>
        <div>
          <dt>Trigger</dt>
          <dd>
            {trace.triggerType}
            {body.triggerText ? `: “${body.triggerText}”` : ""}
          </dd>
        </div>
        <div>
          <dt>Intent</dt>
          <dd>{trace.intent ?? "—"}</dd>
        </div>
        <div>
          <dt>Correlation ID</dt>
          <dd>
            <code>{trace.correlationId}</code>
          </dd>
        </div>
        <div>
          <dt>Took</dt>
          <dd>{String(new Date(trace.completedAt).getTime() - new Date(trace.startedAt).getTime())} ms</dd>
        </div>
        <div>
          <dt>Tokens</dt>
          <dd>
            {String(body.usage.inputTokens)} in, {String(body.usage.outputTokens)} out
          </dd>
        </div>
      </dl>

      <h4>Response</h4>
      <p>{body.response.text ?? "—"}</p>
      <p>Cites: {body.response.citations.map(source).join(", ") || "nothing"}</p>
      <p>Read for context: {body.retrieved.map(source).join(", ") || "nothing"}</p>

      <h4>Model calls</h4>
      <ol>
        {body.modelCalls.map((call, index) => (
          <li key={index}>
            <p>
              <strong>{call.kind}</strong> for <code>{call.task}</code> on {call.model ?? "?"}
              {call.stopReason ? `, stopped: ${call.stopReason}` : ""}
              {call.usage ? ` (${String(call.usage.inputTokens)} in, ${String(call.usage.outputTokens)} out)` : ""}
            </p>
            <ul>
              {call.attempts.map((attempt, number) => (
                <li key={number}>
                  {attempt.provider}/{attempt.model}
                  {attempt.prompt ? `, prompt ${attempt.prompt.name} v${String(attempt.prompt.number)}` : ""}, {String(Math.round(attempt.durationMilliseconds))} ms
                  {attempt.failure ? `, failed: ${attempt.failure}` : ""}
                </li>
              ))}
            </ul>
            {call.tools.length > 0 && <p>Tools: {call.tools.join(", ")}</p>}
            <details>
              <summary>Messages</summary>
              {call.systemPrompt && <pre>{call.systemPrompt}</pre>}
              {[...call.input, ...(call.output ? [call.output] : [])].map((message, number) => (
                <p key={number}>
                  <strong>{message.role}:</strong> {message.parts.map(part).join(" ")}
                </p>
              ))}
            </details>
          </li>
        ))}
      </ol>

      <h4>Capability calls</h4>
      {body.capabilityCalls.length === 0 ? (
        <p>None.</p>
      ) : (
        <ol>
          {body.capabilityCalls.map((call, index) => (
            <li key={index}>
              <p>
                <strong>{call.purpose}</strong> <code>{call.capability}</code>: {call.authorization}
                {call.errorCode ? `, failed: ${call.errorCode}` : ""}
              </p>
              {call.withheld ? (
                <p>Input and output withheld.</p>
              ) : (
                <details>
                  <summary>Input and output</summary>
                  <pre>{JSON.stringify({ input: call.input, output: call.output }, null, 2)}</pre>
                </details>
              )}
            </li>
          ))}
        </ol>
      )}

      {body.grounding && (
        <>
          <h4>Grounding</h4>
          <ol>
            {body.grounding.replies.map((reply, index) => (
              <li key={index}>{reply.accepted ? "Accepted" : `Refused: ${reply.rejection ?? ""}`}</li>
            ))}
          </ol>
        </>
      )}

      <h4>Flow</h4>
      <ol>
        {trace.flowEvents.map((event) => (
          <li key={event.eventId}>
            {new Date(event.recordedAt).toLocaleTimeString()} {event.eventType} ({event.aggregateType})
          </li>
        ))}
      </ol>
    </article>
  );
}
