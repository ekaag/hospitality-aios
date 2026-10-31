import type { ApiClient, Schema } from "@hospitalityos/api-client";
import { useEffect, useState } from "react";

const statuses: Record<Schema<"ConnectorStatus">, string> = {
  NotConfigured: "Not configured",
  Active: "Active",
  Disabled: "Disabled",
};

/**
 * What the property and the platform are connected to (spec §34 Integrations), read-only: providers, bindings, and
 * models change through configuration.
 */
export function Integrations({ client, propertyId }: { client: ApiClient; propertyId: string }) {
  const [integrations, setIntegrations] = useState<Schema<"IntegrationsResponse"> | undefined>();
  const [problem, setProblem] = useState<string | undefined>();

  useEffect(() => {
    let current = true;

    void client.GET("/api/v1/integrations", { params: { query: { propertyId } } }).then(({ data }) => {
      if (current) {
        setIntegrations(data);
        setProblem(data ? undefined : "The integrations could not be read.");
      }
    });

    return () => {
      current = false;
    };
  }, [client, propertyId]);

  return (
    <section aria-labelledby="section" className="integrations">
      <h2 id="section">Integrations</h2>
      <p>What this property and the platform are connected to. Changing them is done in configuration.</p>
      {problem && <p role="alert">{problem}</p>}
      {integrations && (
        <>
          <table>
            <caption>Connectors</caption>
            <thead>
              <tr>
                <th scope="col">System</th>
                <th scope="col">Provider</th>
                <th scope="col">Status</th>
                <th scope="col">Health</th>
                <th scope="col">Available</th>
              </tr>
            </thead>
            <tbody>
              {integrations.connectors.map((connector) => (
                <tr key={connector.kind}>
                  <th scope="row">{connector.kind}</th>
                  <td>{connector.provider ?? "—"}</td>
                  <td>{statuses[connector.status]}</td>
                  <td>{connector.healthy === null ? "—" : connector.healthy ? "Healthy" : `Unhealthy: ${connector.problem ?? ""}`}</td>
                  <td>{connector.availableProviders.join(", ") || "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <table>
            <caption>AI tasks</caption>
            <thead>
              <tr>
                <th scope="col">Task</th>
                <th scope="col">Kind</th>
                <th scope="col">Provider</th>
                <th scope="col">Model</th>
                <th scope="col">Falls back to</th>
              </tr>
            </thead>
            <tbody>
              {integrations.inferenceTasks.map((task) => (
                <tr key={task.task}>
                  <th scope="row">{task.task}</th>
                  <td>{task.capability}</td>
                  <td>{task.provider ? `${task.provider} (${task.adapter})` : "Not routed"}</td>
                  <td>{task.model || "—"}</td>
                  <td>{task.fallbacks.join(", ") || "—"}</td>
                </tr>
              ))}
            </tbody>
          </table>
          <table>
            <caption>Platform bindings</caption>
            <thead>
              <tr>
                <th scope="col">Port</th>
                <th scope="col">Applies to</th>
                <th scope="col">Adapter</th>
              </tr>
            </thead>
            <tbody>
              {integrations.ports.map((port) => (
                <tr key={`${port.port}:${port.scope}`}>
                  <th scope="row">{port.port}</th>
                  <td>{port.scope}</td>
                  <td>{port.adapter}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </section>
  );
}
