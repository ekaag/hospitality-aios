import type { ApiClient } from "@hospitalityos/api-client";
import { Avatar } from "@hospitalityos/theme";
import { useState } from "react";
import { issueSession, useDevPersonas, worksAt } from "./personas";
import type { DevPersona, DevSession } from "./session";

export interface DevSignInProps {
  client: ApiClient;
  /** The roles this app is for, e.g. `["Guest"]`; other personas are not offered. */
  roles: readonly string[];
  /** The hotel chosen: only its people, and those who work across hotels, are offered. */
  hotel?: string | undefined;
  onSignedIn: (session: DevSession) => void;
}

/**
 * Signs in as one of the seeded personas through the API's dev tokens (Development only). When the API is not running,
 * or dev tokens are off, it says so instead of offering anyone.
 */
export function DevSignIn({ client, roles, hotel, onSignedIn }: DevSignInProps) {
  const { personas, problem: unavailable } = useDevPersonas(client, roles);
  const [problem, setProblem] = useState<string | undefined>();

  async function signIn(persona: DevPersona) {
    const session = await issueSession(client, persona);

    if (session) {
      onSignedIn(session);
    } else {
      setProblem(`Could not sign in as ${persona.displayName}.`);
    }
  }

  return (
    <section aria-label="Sign in">
      <h2>Sign in as</h2>
      {(problem ?? unavailable) && <p role="alert">{problem ?? unavailable}</p>}
      {personas && (
        <ul>
          {personas
            .filter((persona) => worksAt(persona, hotel))
            .map((persona) => (
              <li key={persona.id} className="persona">
                <Avatar name={persona.displayName} size="large" />
                <button type="button" onClick={() => void signIn(persona)}>
                  {persona.displayName}
                </button>
                <span>
                  {persona.role}: {persona.description}
                </span>
              </li>
            ))}
        </ul>
      )}
    </section>
  );
}
